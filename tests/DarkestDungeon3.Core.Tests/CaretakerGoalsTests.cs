using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CaretakerGoalsTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly Buildings Buildings = Buildings.Load(Install);

    [Fact]
    public void OnlyQualifiedKnownClassesCompleteOnceWithoutChangingRandomState()
    {
        var estate = new Estate { RandomCounter = 8, Week = 9 };
        Assert.False(CaretakerGoals.Record(estate, null));
        Assert.False(CaretakerGoals.Record(estate, new HeroRecord { ClassId = "runaway", ResolveLevel = 5 }));
        Assert.False(CaretakerGoals.Record(estate, new HeroRecord { ClassId = " ", ResolveLevel = 6 }));
        Assert.True(CaretakerGoals.Record(estate, new HeroRecord { ClassId = "runaway", ResolveLevel = 6 }));
        Assert.False(CaretakerGoals.Record(estate, new HeroRecord { ClassId = "runaway", ResolveLevel = 6 }));
        Assert.Equal("runaway", Assert.Single(estate.CompletedResolveGoals));
        Assert.Empty(estate.CompletedPlotQuests);
        Assert.Equal(8, estate.RandomCounter);
        Assert.Equal(9, estate.Week);
    }

    [Fact]
    public void OldSavesRecoverOnlyProvableRosterAndGraveyardAchievementsOnce()
    {
        var save = SaveFile.FromJson("{\"Estate\":{\"RandomCounter\":7,\"Roster\":[{\"ClassId\":\"highwayman\",\"ResolveLevel\":6},{\"ClassId\":\"runaway\",\"ResolveLevel\":5}],\"Graveyard\":[{\"ClassId\":\"vestal\",\"ResolveLevel\":6,\"IsDead\":true}]}}");
        Assert.True(CaretakerGoals.Sync(save.Estate));
        Assert.False(CaretakerGoals.Sync(save.Estate));
        var loaded = SaveFile.FromJson(save.ToJson()).Estate;
        Assert.Equal(new[] { "highwayman", "vestal" }, loaded.CompletedResolveGoals.OrderBy(c => c));
        loaded.Roster.Clear(); loaded.Graveyard.Clear();
        Assert.False(CaretakerGoals.Sync(loaded));
        Assert.Equal(2, loaded.CompletedResolveGoals.Count);
        Assert.Equal(7, loaded.RandomCounter);
    }

    [Fact]
    public void DismissalPreservesCompletedClassAndReturnsTrinketsUsingExistingRules()
    {
        var estate = new Estate { RandomCounter = 4 };
        estate.Roster.Add(new HeroRecord { Id = "a", ClassId = "highwayman", ResolveLevel = 6, Trinkets = { "keepsake" } });
        estate.Roster.Add(new HeroRecord { Id = "b", ClassId = "vestal", ResolveLevel = 5 });
        var hamlet = new Hamlet(estate, Dd1, Buildings, new FakeCatalog());
        Assert.True(hamlet.Dismiss("a")); Assert.True(hamlet.Dismiss("b"));
        Assert.False(hamlet.Dismiss("missing"));
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.Equal("highwayman", Assert.Single(loaded.CompletedResolveGoals));
        Assert.Equal("keepsake", Assert.Single(loaded.Trinkets));
        Assert.Empty(loaded.Roster);
        Assert.Equal(4, loaded.RandomCounter);
    }

    [Fact]
    public void VictoryRecordsNewResolveSixAndDeathKeepsPreviouslyReachedClass()
    {
        var estate = new Estate { Seed = 3 };
        estate.Roster.Add(new HeroRecord { Id = "a", Name = "Dismas", ClassId = "highwayman", ResolveLevel = 5, ResolveXp = 46 });
        estate.Roster.Add(new HeroRecord { Id = "b", Name = "Junia", ClassId = "vestal", ResolveLevel = 6, ResolveXp = 48 });
        var quest = new QuestOffer { Id = "q", Dungeon = "dd2_city", Type = "explore", Length = 1, Difficulty = 1 };
        Homecoming.Report(estate, Dd1, new ExpeditionState { Quest = quest, QuestComplete = true }, new[]
        {
            new HeroOutcome { HeroId = "a" }, new HeroOutcome { HeroId = "b", Died = true }
        });
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.Equal(new[] { "highwayman", "vestal" }, loaded.CompletedResolveGoals.OrderBy(c => c));
        Assert.Equal(48, loaded.Roster[0].ResolveXp);
        Assert.Equal(6, loaded.Roster[0].ResolveLevel);
        Assert.Single(loaded.Graveyard);
        Assert.Equal(1, loaded.RandomCounter);
        Assert.Equal(2, loaded.QuestsCompleted);
    }

    [Fact]
    public void RetreatDoesNotCompleteAClassBelowResolveSix()
    {
        var estate = new Estate { Seed = 3 };
        estate.Roster.Add(new HeroRecord { Id = "a", ClassId = "highwayman", ResolveLevel = 5, ResolveXp = 46 });
        var quest = new QuestOffer { Id = "q", Dungeon = "dd2_city", Type = "explore", Length = 1, Difficulty = 1 };
        Homecoming.Report(estate, Dd1, new ExpeditionState { Quest = quest, Retreated = true }, new[] { new HeroOutcome { HeroId = "a" } });
        Assert.Empty(estate.CompletedResolveGoals);
        Assert.Equal(46, estate.Roster[0].ResolveXp);
    }
}
