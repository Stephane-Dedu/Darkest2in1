using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD2's regions as DD1-style zones (an estate option).</summary>
public class RegionTests
{
    private static readonly ZoneEncounters Zones = ZoneEncounters.Load(Path.GetFullPath(Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "zones.json")));
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    // Loading zones.json is what registers the regions; make sure it happened before each test.
    public RegionTests() => Assert.NotNull(Zones);

    [Fact]
    public void RegionsBorrowADd1Zone()
    {
        Assert.Equal("crypts", ZoneBase.Of("dd2_city"));
        Assert.Equal("weald", ZoneBase.Of("dd2_forest"));
        Assert.Equal("crypts", ZoneBase.Of("crypts"));
        Assert.True(ZoneBase.IsExtra("dd2_coast"));
        Assert.False(ZoneBase.IsExtra("cove"));
        Assert.Equal(5, ZoneBase.ExtraZones.Count());
        Assert.Equal("The Sprawl", Zones.ZoneName("dd2_city"));
        Assert.NotEmpty(Zones.Blurb("dd2_city"));
    }

    [Fact]
    public void RegionsAreFoughtByTheirNatives()
    {
        Assert.Equal("table:fanatic_mashes_normal", Zones.Plan("dd2_city", 1, FightKind.Hall, new Rng(1)).Battle);
        Assert.Equal("table:swine_mashes_hard_champions", Zones.Plan("dd2_cave", 5, FightKind.Hall, new Rng(1)).Battle);
        var boss = Zones.Plan("dd2_city", 3, FightKind.Boss, new Rng(1), ZoneBase.BossOf("dd2_city"));
        Assert.Equal("config:city_dungeon_3_a", boss.Battle);
        Assert.Contains("combat_arena_city_dungeon_interior", boss.Arenas);
    }

    [Fact]
    public void ARegionQuestPlaysOnItsDd1ZonesMaps()
    {
        var p = Dd1.MapGen.Find("dd2_forest", "medium", "explore");
        Assert.Equal("weald", p.Dungeon);
        var map = MapGenerator.Generate(p, 7, Dd1.Props("dd2_forest"));
        Assert.Contains(map.Rooms, r => r.CurioId != null);
        Assert.NotNull(Dd1.Goals.For("gather", "dd2_forest") ?? Dd1.Goals.For("explore", "dd2_forest"));
    }

    [Fact]
    public void ToggledRegionsJoinTheBoard()
    {
        var estate = new Estate { Seed = 5, QuestsCompleted = 4 };
        estate.Toggles["zone.dd2_farm"] = true;
        var board = QuestBoard.Generate(estate, Dd1, new[] { "dd2_farm" });
        Assert.Contains(board, q => q.Dungeon == "dd2_farm");
        var added = QuestBoard.OffersFor(estate, Dd1, "dd2_coast");
        Assert.True(added.Count >= 2);
        Assert.All(added, q => Assert.Equal("dd2_coast", q.Dungeon));
        Assert.All(added, q => Assert.NotEmpty(q.Rewards));
    }

    [Fact]
    public void LairBossTiersOpenAtZoneLevelsTwoFourAndSix()
    {
        var estate = new Estate { Seed = 3 };
        Assert.DoesNotContain(QuestBoard.PlotOffers(estate, Dd1, new[] { "dd2_city" }), q => q.Dungeon == "dd2_city" && q.Type == "kill_boss");

        estate.ZoneXp["dd2_city"] = Dd1.ZoneLevelThresholds[2];
        var first = QuestBoard.PlotOffers(estate, Dd1, new[] { "dd2_city" }).Single(q => q.Dungeon == "dd2_city" && q.Type == "kill_boss");
        Assert.Equal("kill_boss", first.Type);
        Assert.Equal("librarian", first.BossId);
        Assert.Equal(1, first.Difficulty);
        Assert.True(first.IsPlot);
        Assert.Contains(first.Rewards, r => r.Type == Currency.Gold);
        Assert.DoesNotContain(first.Rewards, r => r.Id != null && r.Id.StartsWith("boss_"));

        // Beaten: the next tier waits for zone level 4.
        estate.CompletedPlotQuests.Add(first.PlotId);
        Assert.DoesNotContain(QuestBoard.PlotOffers(estate, Dd1, new[] { "dd2_city" }), q => q.Dungeon == "dd2_city" && q.Type == "kill_boss");
        estate.ZoneXp["dd2_city"] = Dd1.ZoneLevelThresholds[4];
        var second = QuestBoard.PlotOffers(estate, Dd1, new[] { "dd2_city" }).Single(q => q.Dungeon == "dd2_city" && q.Type == "kill_boss");
        Assert.Equal(3, second.Difficulty);
    }
}
