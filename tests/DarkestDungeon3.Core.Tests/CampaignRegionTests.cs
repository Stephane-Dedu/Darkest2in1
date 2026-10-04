using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CampaignRegionTests
{
    private static readonly Dd1Campaign Data = Dd1Campaign.Load(Dd1Install.Find());
    [Fact]
    public void DefaultBoardUsesNativeRegionsAndLegacyAreasOnlyAppearWhenEnabled()
    {
        var e = new Estate { Seed = 8, QuestsCompleted = 5 };
        var board = QuestBoard.Generate(e, Data);
        Assert.All(CampaignRegions.Primary, z => Assert.Contains(board, q => q.Dungeon == z));
        Assert.DoesNotContain(board, q => CampaignRegions.Legacy.Contains(q.Dungeon));
        e.Toggles["zone.cove"] = true;
        Assert.Contains(QuestBoard.Generate(e, Data), q => q.Dungeon == "cove");
        e.Toggles["zone.dd2_coast"] = false;
        Assert.DoesNotContain(QuestBoard.Generate(e, Data), q => q.Dungeon == "dd2_coast");
    }
    [Fact]
    public void MainRegionsKeepDd1UnlockOrderAndOpeningTutorialRemainsPlayable()
    {
        var e = new Estate { Seed = 9 };
        var board = QuestBoard.Generate(e, Data);
        Assert.All(board, q => Assert.Equal("dd2_city", q.Dungeon));
        var tutorial = Assert.Single(board, q => q.PlotId == "plot_tutorial_crypts");
        Assert.Equal("tutorial_crypts", tutorial.MapName);
        Assert.DoesNotContain(tutorial.PlotId, e.CompletedPlotQuests);
        Assert.Equal(0, e.Get(Currency.Gold));
    }
    [Fact]
    public void MigrationPreservesProgressExplicitChoicesSeedsAndRewardsAndRunsOnce()
    {
        var e = new Estate { Seed = 19, QuestsCompleted = 6 };
        e.ZoneXp["warrens"] = 20; e.ZoneXp["dd2_farm"] = 25;
        e.ZoneXp["cove"] = 14; e.Toggles["zone.cove"] = true;
        e.CompletedPlotQuests.Add("some_old_boss");
        var old = QuestBoard.Build(e, Data, "warrens", "explore", 2, 3, new Rng(4)); e.Quests.Add(old);
        int seed = old.MapSeed; var rewards = old.Rewards.ToArray();
        Assert.True(CampaignRegions.Migrate(e, Data));
        Assert.Equal("dd2_farm", old.Dungeon); Assert.Equal(seed, old.MapSeed); Assert.Equal(rewards, old.Rewards);
        Assert.Equal(25, e.ZoneXp["dd2_farm"]); Assert.Equal(14, e.ZoneXp["dd2_coast"]); Assert.Equal(20, e.ZoneXp["warrens"]);
        Assert.True(CampaignRegions.Enabled(e, "cove")); Assert.Contains("some_old_boss", e.CompletedPlotQuests);
        int count = e.Quests.Count; Assert.False(CampaignRegions.Migrate(e, Data)); Assert.Equal(count, e.Quests.Count);
    }
    [Fact]
    public void TogglingALegacyRegionChangesTheBoardWithoutResettingEitherRegionsXp()
    {
        var e = new Estate { Seed = 3, QuestsCompleted = 6, RegionLayoutVersion = CampaignRegions.LayoutVersion };
        e.ZoneXp["weald"] = 12; e.ZoneXp["dd2_forest"] = 20;
        var hamlet = new Hamlet(e, Data, Buildings.Load(Dd1Install.Find()), new FakeCatalog());
        hamlet.SetZoneToggle("weald", true); Assert.Contains(e.Quests, q => q.Dungeon == "weald");
        hamlet.SetZoneToggle("weald", false); Assert.DoesNotContain(e.Quests, q => q.Dungeon == "weald");
        Assert.Equal(12, e.ZoneXp["weald"]); Assert.Equal(20, e.ZoneXp["dd2_forest"]);
    }
}
