using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class FallenConditionTests
{
    [Fact]
    public void A_wipe_after_completing_the_objective_never_grants_quest_rewards()
    {
        var estate = new Estate { QuestsCompleted = 0, Roster = { new HeroRecord { Id = "a", ClassId = "highwayman" } } };
        var quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1, Length = 1,
            Rewards = { new Reward { Type = "gold", Amount = 1000 } } };
        var exp = new ExpeditionState { Party = { "a" }, Ended = true, Quest = quest, QuestComplete = true };
        exp.Pack.Add(Currency.Gold, 500);
        exp.Pack.Add(Currency.Crest, 4);
        exp.Pack.Add("trinket:unclaimed_test_ring", 1);
        var report = Homecoming.Report(estate, Dd1Campaign.Load(Dd1Install.Find()), exp,
            new[] { new HeroOutcome { HeroId = "a", Died = true } });
        Assert.Equal("defeat", report.Result);
        Assert.Equal(0, estate.QuestsCompleted);
        Assert.Equal(0, estate.Get(Currency.Gold));
        Assert.Equal(0, estate.Get(Currency.Crest));
        Assert.Empty(estate.Trinkets);
        Assert.Empty(report.Rewards);
    }

    [Fact]
    public void RecordedQuirksReachTheGraveAndExistingFromBeyondWithoutChangingLoadoutPolicy()
    {
        var install = Dd1Install.Find(); var dd1 = Dd1Campaign.Load(install); var buildings = Buildings.Load(install);
        var estate = new Estate { Seed = 51, Week = 19 };
        var hero = new HeroRecord
        {
            Id = "fallen", ClassId = "highwayman", Name = "Dismas", ResolveLevel = 3, ResolveXp = 18,
            WeaponRank = 2, ArmorRank = 3, Quirks = { "old" }, Trinkets = { null, "original_ring" },
            LockedQuirks = { "locked" }, EquippedSkills = { "pistol_shot" }
        };
        estate.Roster.Add(hero);
        var exp = new ExpeditionState
        {
            Party = { "fallen" }, Ended = true, Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1, Length = 1 }
        };
        var outcome = new HeroOutcome
        {
            HeroId = hero.Id, Died = true, CauseOfDeath = "recorded death",
            Quirks = new() { "changed", "disease_new", "locked" }, Trinkets = new() { "different_ring" }
        };
        Homecoming.Report(estate, dd1, exp, new[] { outcome });
        outcome.Quirks.Clear();
        var fallen = Assert.Single(estate.Graveyard);
        Assert.Equal(new[] { "changed", "disease_new", "locked" }, fallen.Quirks);
        Assert.Equal(new string[] { null, "original_ring" }, fallen.Trinkets); // unchanged existing dead-trinket policy
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        for (int i = 0; i < 2; i++) loaded.Graveyard.Add(new HeroRecord { Id = "other" + i, ClassId = "highwayman", IsDead = true });
        var eventData = dd1.TownEvents.Get("dead_recruit");
        Assert.Equal(3, eventData.DeadHeroes); Assert.Equal(3, Assert.Single(eventData.Data).Num);
        loaded.TownEventId = eventData.Id;
        var hamlet = new Hamlet(loaded, dd1, buildings, new FakeCatalog());
        hamlet.StartTownEvent(new Rng(2));
        Assert.True(hamlet.Recruit("fallen"));
        var returned = loaded.Hero("fallen");
        Assert.False(returned.IsDead); Assert.Equal(new[] { "changed", "disease_new", "locked" }, returned.Quirks);
        Assert.Equal(3, returned.ResolveLevel); Assert.Equal(18, returned.ResolveXp);
        Assert.Equal(2, returned.WeaponRank); Assert.Equal(3, returned.ArmorRank);
        Assert.Equal(new[] { "pistol_shot" }, returned.EquippedSkills); Assert.Equal(new[] { "locked" }, returned.LockedQuirks);
        Assert.Equal(new string[] { null, "original_ring" }, returned.Trinkets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownQuirksKeepTheRecordWhileKnownEmptyQuirksClearIt(bool knownEmpty)
    {
        var estate = new Estate { Roster = { new HeroRecord { Id = "a", ClassId = "highwayman", Quirks = { "old" } } } };
        var exp = new ExpeditionState { Ended = true, Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore" } };
        var outcome = new HeroOutcome { HeroId = "a", Died = true, Quirks = knownEmpty ? new List<string>() : null };
        Homecoming.Report(estate, Dd1Campaign.Load(Dd1Install.Find()), exp, new[] { outcome });
        Assert.Equal(knownEmpty ? Array.Empty<string>() : new[] { "old" }, Assert.Single(estate.Graveyard).Quirks);
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson());
        Assert.Equal(estate.Graveyard[0].Quirks, loaded.Estate.Graveyard[0].Quirks);
    }
}
