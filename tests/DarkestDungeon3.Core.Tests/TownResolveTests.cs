using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TownResolveTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly Buildings Buildings = Buildings.Load(Install);

    private static Hamlet Town(Estate estate) => new(estate, Dd1, Buildings, new FakeCatalog());
    private static Estate EventEstate() => new() { Week = 8, RandomCounter = 5, TownEventId = "idle_resolve_level_highwayman" };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AwardedLevelHasItsCumulativeXpAndSavedActor(int level)
    {
        var estate = EventEstate();
        var hero = new HeroRecord { Id = "idle", Name = "Dismas", ClassId = "highwayman", ResolveLevel = level, ResolveXp = Dd1.HeroResolveThresholds[level] + 1 };
        estate.Roster.Add(hero);
        var rng = new Rng(77);
        Town(estate).StartTownEvent(rng);
        Assert.Equal(level + 1, hero.ResolveLevel);
        Assert.Equal(Dd1.HeroResolveThresholds[level + 1], hero.ResolveXp);
        Assert.Equal(hero.ResolveLevel, Dd1.HeroResolveLevel(hero.ResolveXp));
        var week = Assert.Single(estate.ActivityLog);
        Assert.Equal(8, week.Week);
        Assert.Equal($"Dismas reached resolve level {level + 1}.", Assert.Single(week.Town));
        var actor = Assert.Single(week.TownActors);
        Assert.Equal(0, actor.MessageIndex);
        Assert.Equal("idle", actor.HeroId);
        Assert.Equal("Dismas", actor.HeroName);
        Assert.Equal("highwayman", actor.HeroClass);
        Assert.Equal(ActivityEntryKind.LevelUp, actor.Kind);
        Assert.Equal(level == 5, estate.CompletedResolveGoals.Contains("highwayman"));
        Assert.Equal(5, estate.RandomCounter);
        Assert.Equal(new Rng(77).Next(1000000), rng.Next(1000000));
    }

    [Fact]
    public void OtherClassesAndMissingHeroesKeepTheirExistingProgress()
    {
        var estate = EventEstate();
        estate.Roster.Add(new HeroRecord { ClassId = "highwayman", MissingWeeks = 1, ResolveLevel = 5, ResolveXp = 47 });
        estate.Roster.Add(new HeroRecord { ClassId = "plague_doctor", ResolveLevel = 5, ResolveXp = 47 });
        Town(estate).StartTownEvent(new Rng(2));
        Assert.All(estate.Roster, h => { Assert.Equal(5, h.ResolveLevel); Assert.Equal(47, h.ResolveXp); });
        Assert.Empty(estate.CompletedResolveGoals);
        Assert.Null(estate.ActivityLog);
    }

    [Fact]
    public void MaxedHeroRecordsAchievementWithoutInventingAnotherLevelUp()
    {
        var estate = EventEstate();
        estate.Roster.Add(new HeroRecord { ClassId = "highwayman", ResolveLevel = 6, ResolveXp = 53 });
        var town = Town(estate);
        town.StartTownEvent(new Rng(2));
        town.StartTownEvent(new Rng(2));
        Assert.Equal(6, estate.Roster[0].ResolveLevel);
        Assert.Equal(53, estate.Roster[0].ResolveXp);
        Assert.Equal("highwayman", Assert.Single(estate.CompletedResolveGoals));
        Assert.Null(estate.ActivityLog);
        Assert.Empty(estate.TownLog);
    }

    [Fact]
    public void EventAchievementAndPortraitSurviveReloadAndDismissal()
    {
        var estate = EventEstate();
        estate.Roster.Add(new HeroRecord { Id = "idle", Name = "Dismas", ClassId = "highwayman", ResolveLevel = 5, ResolveXp = 47 });
        Town(estate).StartTownEvent(new Rng(2));
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.True(Town(loaded).Dismiss("idle"));
        Assert.Equal("highwayman", Assert.Single(loaded.CompletedResolveGoals));
        var actor = Assert.Single(Assert.Single(loaded.ActivityLog).TownActors);
        Assert.Equal("Dismas", actor.HeroName);
        Assert.Equal(ActivityEntryKind.LevelUp, actor.Kind);
        Assert.Equal(5, loaded.RandomCounter);
    }

    [Fact]
    public void QuestXpContinuesFromTheAwardedLevelBoundary()
    {
        var estate = EventEstate();
        estate.Roster.Add(new HeroRecord { Id = "idle", ClassId = "highwayman", ResolveLevel = 1, ResolveXp = 3 });
        Town(estate).StartTownEvent(new Rng(2));
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        var quest = new QuestOffer { Id = "q", Dungeon = "dd2_city", Type = "explore", Length = 1, Difficulty = 1 };
        var report = Homecoming.Report(loaded, Dd1, new ExpeditionState { Quest = quest, QuestComplete = true }, new[] { new HeroOutcome { HeroId = "idle" } });
        Assert.Equal(8 + report.Heroes.Single().XpGained, loaded.Roster[0].ResolveXp);
        Assert.Equal(Dd1.HeroResolveLevel(loaded.Roster[0].ResolveXp), loaded.Roster[0].ResolveLevel);
    }
}
