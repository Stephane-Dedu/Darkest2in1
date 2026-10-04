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
        Assert.True(boss.NativePresentation);
        Assert.False(Zones.Plan("crypts", 1, FightKind.Room, new Rng(1)).NativePresentation);
    }

    [Theory]
    [InlineData("dd2_city")]
    [InlineData("dd2_farm")]
    [InlineData("dd2_forest")]
    [InlineData("dd2_coast")]
    [InlineData("dd2_cave")]
    public void NativeCampingUsesTheSameRegionalFactionAsRoomFights(string zone)
    {
        for (int difficulty = 1; difficulty <= 5; difficulty += 2)
        {
            var room = Zones.Plan(zone, difficulty, FightKind.Room, new Rng(2));
            var ambush = Zones.Plan(zone, difficulty, FightKind.CampAmbush, new Rng(2));
            Assert.Equal(room.Battle, ambush.Battle); Assert.Equal(room.Arenas, ambush.Arenas);
            Assert.Equal(FightKind.CampAmbush, ambush.Kind); Assert.True(ambush.NativePresentation);
        }
        Assert.Equal("table:camp_mashes_master", Zones.Plan("weald", 5, FightKind.CampAmbush, new Rng(2)).Battle);
    }

    [Fact]
    public void MigratedCrowStoryKeepsItsSpecialFightAndSluiceHasNoNewLairChain()
    {
        var e = new Estate { Seed = 4, QuestsCompleted = 10 };
        var crowPlot = Dd1.Goals.Plot.Single(p => p.Id == "plot_crow_trinket");
        var crow = QuestBoard.PlotOffer(e, Dd1, crowPlot);
        Assert.Equal("dd2_forest", crow.Dungeon);
        var plan = Zones.Plan(crow.Dungeon, crow.Difficulty, FightKind.Boss, new Rng(3), crow.BossId);
        Assert.Equal("config:carrion_my_wayward_son_c", plan.Battle); Assert.True(plan.NativePresentation);
        e.ZoneXp["dd2_cave"] = Dd1.ZoneLevelThresholds[6];
        Assert.Null(ZoneBase.BossOf("dd2_cave"));
        Assert.DoesNotContain(QuestBoard.PlotOffers(e, Dd1, new[] { "dd2_cave" }), q => q.Dungeon == "dd2_cave" && q.Type == "kill_boss");
        // Existing saved Exemplar offers still have their explicit encounter, but new Sluice boards don't create them.
        Assert.Equal("config:cultist_guardian_biome_3_boss_1", Zones.Plan("dd2_cave", 5, FightKind.Boss, new Rng(3), "exemplar").Battle);
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
