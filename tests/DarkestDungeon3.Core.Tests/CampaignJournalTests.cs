using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CampaignJournalTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly Buildings Buildings = Buildings.Load(Install);

    [Fact]
    public void LegacyMessagesImportOnceAtTheirKnownWeekWithoutInventingPastHistory()
    {
        var save = SaveFile.FromJson("{\"Estate\":{\"Week\":12,\"Seed\":7,\"RandomCounter\":9,\"TownLog\":[\"A hero returned.\",\"A hero returned.\"]}}");
        Assert.True(CampaignJournal.Initialize(save.Estate));
        Assert.False(CampaignJournal.Initialize(save.Estate));
        var week = Assert.Single(save.Estate.ActivityLog);
        Assert.Equal(12, week.Week);
        Assert.Equal(save.Estate.TownLog, week.Town);
        Assert.Empty(week.Raids);
        var loaded = SaveFile.FromJson(save.ToJson());
        Assert.False(CampaignJournal.Initialize(loaded.Estate));
        Assert.Equal(2, Assert.Single(loaded.Estate.ActivityLog).Town.Count);
        Assert.Equal(9, loaded.Estate.RandomCounter);
        Assert.Equal(1, loaded.Estate.Version);
    }

    [Fact]
    public void FirstNewMessageKeepsLegacyMessagesAndRepeatedActions()
    {
        var estate = new Estate { Week = 4, TownLog = new List<string> { "Previous result" } };
        CampaignJournal.Town(estate, "New action");
        CampaignJournal.Town(estate, "New action");
        Assert.Equal(new[] { "Previous result", "New action", "New action" }, Assert.Single(estate.ActivityLog).Town);
        Assert.Equal(estate.TownLog, estate.ActivityLog[0].Town);
    }

    [Fact]
    public void WeekAdvancePreservesUpgradeAndTownResultsAcrossReloadWithoutChangingRolls()
    {
        var estate = Hamlet.NewEstate(37, Dd1, Buildings, new FakeCatalog());
        estate.QuestsCompleted = 3;
        estate.Add(Currency.Gold, 10000);
        estate.Upgrades.Add("blacksmith.weapon:a");
        var hero = estate.Roster[0];
        hero.ResolveLevel = 1;
        var hamlet = new Hamlet(estate, Dd1, Buildings, new FakeCatalog());
        Assert.True(hamlet.UpgradeEquipment(hero.Id, Hamlet.Weapon));
        var previous = Assert.Single(estate.ActivityLog).Town.ToList();
        Assert.Contains(previous, m => m.Contains("Blacksmith"));
        hero.Stress = 6;
        var control = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        control.ActivityLog = null;
        var results = hamlet.EndWeek();
        new Hamlet(control, Dd1, Buildings, new FakeCatalog()).EndWeek();
        Assert.Equal(1, estate.Week);
        Assert.Equal(control.RandomCounter, estate.RandomCounter);
        Assert.Equal(control.Roster[0].Stress, hero.Stress);
        Assert.Equal(control.Quests.Select(q => q.MapSeed), estate.Quests.Select(q => q.MapSeed));
        Assert.NotEmpty(results);
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.Equal(previous, loaded.ActivityLog.Single(w => w.Week == 0).Town);
        Assert.Equal(results, loaded.ActivityLog.Single(w => w.Week == 1).Town);
        Assert.Equal(results, loaded.TownLog);
        Assert.Equal(1, loaded.Roster[0].WeaponRank);
        var upgradeActor = Assert.Single(loaded.ActivityLog.Single(w => w.Week == 0).TownActors);
        Assert.Equal(hero.Id, upgradeActor.HeroId);
        Assert.Equal(hero.ClassId, upgradeActor.HeroClass);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("retreat")]
    [InlineData("defeat")]
    public void HomecomingPersistsOutcomeSnapshotsWithExistingRewards(string result)
    {
        var estate = new Estate { Week = 3, Seed = 11 };
        var hero = new HeroRecord { Id = "hero", Name = "Dismas", ClassId = "highwayman" };
        estate.Roster.Add(hero);
        var quest = new QuestOffer { Id = "q", Dungeon = "dd2_sprawl", Type = "explore", Length = 1, Difficulty = 1 };
        quest.Rewards.Add(new Reward(Currency.Gold, 100));
        var exp = new ExpeditionState { Quest = quest, QuestComplete = result == "complete", Retreated = result == "retreat" };
        exp.Pack.Add(Currency.Gold, 25);
        CampaignJournal.Embark(estate, quest, estate.Roster);
        var report = Homecoming.Report(estate, Dd1, exp, new[]
        {
            new HeroOutcome { HeroId = hero.Id, Stress = 4, Died = result == "defeat", CauseOfDeath = "battle" }
        });
        var expectedMessages = report.Log.ToList();
        var expectedQuest = quest.ToString();
        hero.Name = "Changed later";
        quest.Dungeon = "another_region";
        report.Log.Clear();
        report.Heroes[0].Name = "Changed report";
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        var raids = Assert.Single(loaded.ActivityLog).Raids;
        Assert.Equal(2, raids.Count);
        Assert.Equal("embark", raids[0].Result);
        Assert.Equal("Dismas", Assert.Single(raids[0].Heroes).Name);
        Assert.Equal(result, raids[1].Result);
        Assert.Equal(expectedQuest, raids[1].Quest);
        Assert.Equal("dd2_sprawl", raids[1].Region);
        Assert.Equal(expectedMessages, raids[1].Messages);
        Assert.Equal("Dismas", Assert.Single(raids[1].Heroes).Name);
        Assert.Equal(result == "defeat", raids[1].Heroes[0].Died);
        Assert.Equal(result == "complete" ? 125 : 25, loaded.Get(Currency.Gold));
        Assert.Equal(result == "defeat" ? 1 : 0, loaded.Graveyard.Count);
        Assert.Equal(3, loaded.Week);
    }

    [Fact]
    public void OpeningAndDifferentEstatesKeepSeparateSavedHistoriesWithoutConsumingRandomness()
    {
        var first = new Estate { Week = 0, RandomCounter = 8 };
        var second = new Estate { Week = 5, RandomCounter = 2 };
        var quest = new QuestOffer { Dungeon = "dd2_sprawl", Type = "explore" };
        CampaignJournal.Embark(first, quest, new[] { new HeroRecord { Id = "a", Name = "Reynauld", ClassId = "crusader" } });
        CampaignJournal.Town(second, "Another estate's action");
        Assert.Equal(0, Assert.Single(first.ActivityLog).Week);
        Assert.Empty(first.ActivityLog[0].Town);
        Assert.Empty(Assert.Single(second.ActivityLog).Raids);
        Assert.Equal(8, first.RandomCounter);
        Assert.Equal(2, second.RandomCounter);
        Assert.Equal(0, first.Week);
        Assert.Equal(5, second.Week);
    }

    [Fact]
    public void TownOutcomeActorsRemainCorrectAfterRenameDismissalDeathAndReload()
    {
        var estate = Hamlet.NewEstate(37, Dd1, Buildings, new FakeCatalog());
        var idle = estate.Roster[0];
        idle.Stress = 6;
        var praying = estate.Roster[1];
        praying.Stress = 8;
        praying.Activity = "abbey.prayer";
        estate.Roster.Add(new HeroRecord { Id = "patient", Name = "Patient", ClassId = "leper", Activity = "sanitarium.disease_treatment", ActivityTarget = "disease_test", Quirks = { "disease_test" } });
        estate.Roster.Add(new HeroRecord { Id = "missing", Name = "Missing", ClassId = "jester", MissingWeeks = 1 });
        var identities = estate.Roster.ToDictionary(h => h.Id, h => (h.Name, h.ClassId));
        var messages = new Hamlet(estate, Dd1, Buildings, new FakeCatalog()).EndWeek();
        foreach (var hero in estate.Roster) { hero.Name = "Changed"; hero.ClassId = "runaway"; hero.IsDead = true; }
        estate.Roster.Clear();
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        var week = loaded.ActivityLog.Single(w => w.Week == 1);
        Assert.Equal(messages.Count, week.TownActors.Count);
        Assert.Contains(week.TownActors, a => a.HeroId == idle.Id);
        Assert.Contains(week.TownActors, a => a.HeroId == praying.Id);
        Assert.Contains(week.TownActors, a => a.HeroId == "patient");
        Assert.Contains(week.TownActors, a => a.HeroId == "missing");
        Assert.Equal(Enumerable.Range(0, messages.Count), week.TownActors.Select(a => a.MessageIndex));
        foreach (var actor in week.TownActors)
        {
            Assert.Equal(identities[actor.HeroId].Name, actor.HeroName);
            Assert.Equal(identities[actor.HeroId].ClassId, actor.HeroClass);
            Assert.StartsWith(actor.HeroName, week.Town[actor.MessageIndex]);
        }
    }

    [Fact]
    public void ActorIndicesAppendAfterLegacyMessagesAndCopyBatchMetadataWithoutRandomness()
    {
        var estate = new Estate { Week = 9, RandomCounter = 7, TownLog = new List<string> { "Legacy result" } };
        var hero = new HeroRecord { Id = "a", Name = "Dismas", ClassId = "highwayman" };
        CampaignJournal.Town(estate, "Direct action", hero);
        var actor = CampaignJournal.Actor(hero, 0);
        CampaignJournal.TownResults(estate, new[] { "Batch outcome" }, new[] { actor, CampaignJournal.Actor(hero, -1), CampaignJournal.Actor(hero, 1) });
        actor.HeroClass = "Changed";
        hero.Name = "Changed";
        var week = Assert.Single(estate.ActivityLog);
        Assert.Equal(new[] { "Legacy result", "Direct action", "Batch outcome" }, week.Town);
        Assert.Equal(new[] { 1, 2 }, week.TownActors.Select(a => a.MessageIndex));
        Assert.All(week.TownActors, a => { Assert.Equal("Dismas", a.HeroName); Assert.Equal("highwayman", a.HeroClass); });
        Assert.Equal(7, estate.RandomCounter);
        Assert.Equal(9, estate.Week);
    }
}
