using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CaretakerQuestTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    [Fact]
    public void NewCampaignListsAllNativeLairTiersAndSupportedStoryGoals()
    {
        var estate = new Estate();
        var goals = CaretakerGoals.Quests(estate, Dd1);
        Assert.Equal(17, goals.Count);
        foreach (var zone in CampaignRegions.Primary)
        {
            var lairs = goals.Where(g => g.Region == zone && g.BossId != null).ToList();
            Assert.Equal(new[] { 1, 2, 3 }, lairs.Select(g => g.Tier));
            Assert.All(lairs, g => Assert.Equal($"region_{zone}_boss_{g.Tier}", g.Id));
        }
        Assert.Equal("librarian", goals.First(g => g.Region == "dd2_city").BossId);
        Assert.Equal("harvest_child", goals.First(g => g.Region == "dd2_farm").BossId);
        Assert.Equal("dreaming_general", goals.First(g => g.Region == "dd2_forest").BossId);
        Assert.Equal("leviathan", goals.First(g => g.Region == "dd2_coast").BossId);
        Assert.Equal("dd2_city", goals.Single(g => g.Id == "plot_tutorial_crypts").Region);
        Assert.Equal(4, goals.Count(g => g.Region == QuestBoard.DarkestDungeon));
        Assert.All(goals, g => Assert.False(g.Complete));
        Assert.DoesNotContain(goals, g => g.Id == "plot_town_invasion_0" || g.Id.StartsWith("plot_trinket_retention") || g.Id == "plot_crow_trinket");
    }

    [Fact]
    public void OptionalRegionKeepsItsOwnBossChecksAndIntroDestination()
    {
        var estate = new Estate();
        estate.Toggles["zone.crypts"] = true;
        estate.CompletedPlotQuests.Add("plot_kill_necromancer_1");
        estate.CompletedPlotQuests.Add("region_dd2_city_boss_2");
        var goals = CaretakerGoals.Quests(estate, Dd1);
        Assert.Equal(6, goals.Count(g => g.Region == "crypts" && g.Id != "plot_tutorial_crypts"));
        Assert.True(goals.Single(g => g.Id == "plot_kill_necromancer_1").Complete);
        Assert.False(goals.Single(g => g.Id == "plot_kill_prophet_1").Complete);
        Assert.False(goals.Single(g => g.Id == "region_dd2_city_boss_1").Complete);
        Assert.True(goals.Single(g => g.Id == "region_dd2_city_boss_2").Complete);
        Assert.Equal("crypts", goals.Single(g => g.Id == "plot_tutorial_crypts").Region);
    }

    [Fact]
    public void DisabledAreasRetainOnlyCompletedGoalsWithoutErasingProgress()
    {
        var estate = new Estate();
        estate.Toggles["zone.dd2_city"] = false;
        estate.CompletedPlotQuests.UnionWith(new[] { "region_dd2_city_boss_1", "plot_kill_necromancer_2" });
        var goals = CaretakerGoals.Quests(estate, Dd1);
        Assert.Equal("region_dd2_city_boss_1", Assert.Single(goals.Where(g => g.Region == "dd2_city")).Id);
        Assert.Equal("plot_kill_necromancer_2", Assert.Single(goals.Where(g => g.Region == "crypts")).Id);
        Assert.DoesNotContain(goals, g => g.Id == "plot_tutorial_crypts");
        Assert.Equal(2, estate.CompletedPlotQuests.Count);
        estate.Toggles["zone.dd2_city"] = true;
        estate.Toggles["zone.crypts"] = true;
        var restored = CaretakerGoals.Quests(estate, Dd1);
        Assert.Equal(3, restored.Count(g => g.BossId == "librarian"));
        Assert.Equal(2, restored.Count(g => g.Complete));
    }

    [Fact]
    public void SluiceDoesNotInventALairGoal()
    {
        var estate = new Estate();
        estate.Toggles["zone.dd2_cave"] = true;
        Assert.DoesNotContain(CaretakerGoals.Quests(estate, Dd1), g => g.Region == "dd2_cave");
    }

    [Fact]
    public void RepeatedViewsAndSaveReloadNeverChangeCampaignOrRandomState()
    {
        var estate = new Estate { Seed = 9, RandomCounter = 17, QuestsCompleted = 4 };
        estate.Toggles["zone.cove"] = true;
        estate.CompletedPlotQuests.Add("plot_kill_siren_1");
        estate.Quests.Add(new QuestOffer { Id = "unchanged", MapSeed = 33 });
        estate.ZoneXp["dd2_coast"] = 6;
        var save = new SaveFile { Estate = estate };
        string before = save.ToJson();
        for (int i = 0; i < 3; i++) Assert.True(CaretakerGoals.Quests(estate, Dd1).Single(g => g.Id == "plot_kill_siren_1").Complete);
        Assert.Equal(before, save.ToJson());
        var loaded = SaveFile.FromJson(before);
        Assert.True(CaretakerGoals.Quests(loaded.Estate, Dd1).Single(g => g.Id == "plot_kill_siren_1").Complete);
        Assert.Equal(17, loaded.Estate.RandomCounter);
    }

    [Fact]
    public void GeneratedPlotOffersUseTheSameGoalIdentities()
    {
        var estate = new Estate { QuestsCompleted = 20 };
        estate.Toggles["zone.warrens"] = true;
        foreach (var zone in CampaignRegions.Open(estate, Dd1)) estate.ZoneXp[zone] = Dd1.ZoneLevelThresholds[6];
        estate.CompletedPlotQuests.Add("region_dd2_farm_boss_1");
        var goals = CaretakerGoals.Quests(estate, Dd1);
        var offers = QuestBoard.PlotOffers(estate, Dd1, CampaignRegions.Open(estate, Dd1)).ToList();
        Assert.NotEmpty(offers);
        foreach (var offer in offers)
        {
            var goal = Assert.Single(goals.Where(g => g.Id == offer.PlotId));
            Assert.Equal(offer.Dungeon, goal.Region);
            Assert.False(goal.Complete);
            if (goal.BossId != null) Assert.Equal(offer.BossId, goal.BossId);
        }
        Assert.Contains(offers, q => q.PlotId == "region_dd2_farm_boss_2");
    }
}
