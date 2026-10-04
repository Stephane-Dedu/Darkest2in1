using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's hand-made plot maps (maps/*.dm) turned into dungeon maps.</summary>
public class PlotMapTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);

    private static DungeonMap Load(string name, string dungeon, string type = "kill_boss") =>
        PlotMap.Load(Install, name, dungeon, type, 7, Dd1.Props(dungeon));

    private static void Connected(DungeonMap map) =>
        Assert.All(map.Distances(map.EntranceRoomId), d => Assert.True(d >= 0));

    [Fact]
    public void TheDarkestDungeonsFirstPartIsDd1sMap()
    {
        var map = Load("DD_map1", "darkestdungeon");
        Assert.Equal(15, map.Rooms.Count);
        Assert.Equal(18, map.Corridors.Count);
        Assert.Equal(108, map.AllTiles.Count());
        Connected(map);

        // DD1: the party enters in rooE and the Shuffler waits in rooK (entrance_id / final_room_id).
        Assert.Equal(RoomContent.Entrance, map.Room(map.EntranceRoomId).Content);
        var boss = map.Room(map.BossRoomId);
        Assert.Equal(RoomContent.Boss, boss.Content);
        Assert.True(boss.IsQuestGoal);
        Assert.NotEqual(map.EntranceRoomId, map.BossRoomId);

        // Named battles from darkestdungeon.6.mash.darkest, and the corridor content DD1 set.
        Assert.Equal(4, map.Rooms.Count(r => r.Content == RoomContent.Battle));
        Assert.Contains(map.Rooms, r => r.MashName == "dd_quest_1_mash_07" && r.Content == RoomContent.Battle);
        var guarded = Assert.Single(map.Rooms, r => r.Content == RoomContent.GuardedCurio);
        Assert.Equal("dd_quest_1_mash_09", guarded.MashName);
        Assert.Contains(guarded.CurioId, new[] { "stack_of_books", "discarded_pack", "sconce", "crate", "sack" });   // room_curios
        Assert.Equal(8, map.AllTiles.Count(t => t.Content == HallContent.Battle && t.MashName != null));
        Assert.Equal(3, map.AllTiles.Count(t => t.Content == HallContent.Hunger));
        Assert.All(map.Corridors, c => Assert.NotEqual(c.RoomA, c.RoomB));
    }

    [Fact]
    public void TheHeartsCaveIsOneLongCorridorWithItsObstacles()
    {
        var map = Load("DD_map4", "darkestdungeon");
        Assert.Equal(2, map.Rooms.Count);                       // the secret room off the corridor is left out for now
        var corridor = Assert.Single(map.Corridors);
        Assert.Equal(28, corridor.Tiles.Count);
        Assert.Equal(3, corridor.Tiles.Count(t => t.Content == HallContent.Obstacle && t.ContentId == "rubble"));
        Assert.Equal(map.EntranceRoomId, corridor.RoomA);
        Assert.Equal(map.BossRoomId, corridor.RoomB);
        Assert.Equal((20, 2), (map.Room(map.BossRoomId).X, map.Room(map.BossRoomId).Y));
    }

    [Fact]
    public void TheTownInvasionAndTheCrowsLair()
    {
        var town = Load("town_invasion_0", "town");
        Assert.Equal(7, town.Rooms.Count);
        Assert.Equal(7, town.Corridors.Count);
        Connected(town);
        Assert.Equal(6, town.AllTiles.Count(t => t.Content == HallContent.Obstacle));
        Assert.Equal(4, town.AllTiles.Count(t => t.Content == HallContent.Curio));
        Assert.Contains(town.AllTiles, t => t.MashName == "town_incursion_weak_07");
        Assert.Equal(RoomContent.Boss, town.Room(town.BossRoomId).Content);

        var crow = Load("crow_map1", "weald");
        var lair = Assert.Single(crow.Rooms);
        Assert.Empty(crow.Corridors);
        Assert.Equal(lair.Id, crow.EntranceRoomId);
        Assert.Equal("crow_1", lair.MashName);
        Assert.Equal(RoomContent.Battle, lair.Content);
    }

    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Fact]
    public void SetFightsAreDd1sNamedMashRows()
    {
        var b = Content.Battles;
        Assert.Equal(new[] { "cultist_shrouded_D", "cultist_warlord_D", "cultist_harpy_D", "cultist_harpy_D" },
            b.NamedEncounter("darkestdungeon", 6, "dd_quest_1_mash_07", new Rng(1)));
        // The same name at each level of the zone: the crow's lair by quest level (weald.1/3/5.mash.darkest).
        Assert.Equal(new[] { "nest_A", "crow_A" }, b.NamedEncounter("weald", 1, "crow_1", new Rng(1)));
        Assert.Equal(new[] { "nest_B", "crow_B" }, b.NamedEncounter("weald", 3, "crow_1", new Rng(1)));
        Assert.Equal(new[] { "nest_C", "crow_C" }, b.NamedEncounter("weald", 5, "crow_1", new Rng(1)));
        Assert.Equal(new[] { "brigand_cutthroat_B", "brigand_blood_A", "brigand_fusilier_B" },
            b.NamedEncounter("town", 6, "town_incursion_weak_07", new Rng(1)));
        Assert.Null(b.NamedEncounter("crypts", 1, "dd_quest_1_mash_07", new Rng(1)));

        // In the Darkest Dungeon's first part, the room DD1 set with mash_07 fights exactly that group.
        var map = Load("DD_map1", "darkestdungeon");
        var quest = new QuestOffer { Dungeon = "darkestdungeon", Type = "kill_boss", Length = 3, Difficulty = 6, MapSeed = 7 };
        var state = new ExpeditionState { Quest = quest, Map = map, Seed = 7, Party = { "a", "b", "c", "d" } };
        var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a", "b", "c", "d"), Content);
        crawl.Begin();
        state.RoomId = map.Rooms.First(r => r.MashName == "dd_quest_1_mash_07").Id;
        Assert.Equal(new[] { "cultist_shrouded_D", "cultist_warlord_D", "cultist_harpy_D", "cultist_harpy_D" }, crawl.FightMonsters("room"));
    }

    [Fact]
    public void TheDarkestDungeonEmbarksOnDd1sOwnMaps()
    {
        var parts = Dd1.Goals.Plot.Where(p => p.Id.StartsWith("plot_darkest_dungeon_")).OrderBy(p => p.Id).ToList();
        Assert.Equal(new[] { "DD_map1", "DD_map2", "DD_map3", "DD_map4" }, parts.Select(p => p.MapName));
        Assert.Null(Dd1.Goals.Plot.First(p => p.Id == "plot_kill_necromancer_1").MapName);   // boss quests stay generated

        var heroes = new[] { "crusader", "vestal", "highwayman", "plague_doctor" }
            .Select((c, i) => new HeroRecord { Id = "h" + i, Name = c, ClassId = c, ResolveLevel = 5 }).ToList();
        ExpeditionState Embark(PlotQuest p) => Campaign.Embark.Create(Dd1, new QuestOffer
        {
            Id = p.Id, PlotId = p.Id, Dungeon = p.Dungeon, Type = p.Type, Length = p.Length, Difficulty = p.Difficulty,
            MapSeed = 3, GoalId = p.GoalIds.FirstOrDefault(), MapName = p.MapName,
        }, heroes, new Inventory());

        var one = Embark(parts[0]).Map;
        Assert.Equal(15, one.Rooms.Count);                        // DD_map1, not a generated map
        Assert.Equal(RoomContent.Boss, one.Room(one.BossRoomId).Content);

        var two = Embark(parts[1]);                                // three beacons, each behind a miniboss
        var beacons = two.Map.Rooms.Where(r => r.IsQuestGoal).ToList();
        Assert.Equal(3, beacons.Count);
        Assert.All(beacons, r => Assert.Equal("beacon", r.CurioId));
        Assert.All(beacons, r => Assert.StartsWith("dd_quest_2_miniboss_", r.MashName));
        Assert.Equal(3, two.Pack.Count(ItemCatalog.QuestKey("beacon_light")));

        var three = Embark(parts[2]).Map;                          // the teleporter behind its guards
        var teleporter = Assert.Single(three.Rooms, r => r.IsQuestGoal);
        Assert.Equal("teleporter", teleporter.CurioId);
        Assert.Equal("dd_quest_3_teleport", teleporter.MashName);

        var four = Embark(parts[3]).Map;
        Assert.Equal(28, Assert.Single(four.Corridors).Tiles.Count);
    }

    [Theory]
    [InlineData("DD_map2", "darkestdungeon", "inventory_activate", 18, 22)]
    [InlineData("DD_map3", "darkestdungeon", "activate", 31, 43)]
    public void TheMiddlePartsLoadWhole(string name, string dungeon, string type, int rooms, int corridors)
    {
        var map = Load(name, dungeon, type);
        Assert.Equal(rooms, map.Rooms.Count);
        Assert.Equal(corridors, map.Corridors.Count);
        Assert.Equal(-1, map.BossRoomId);                         // no boss: their goals are curios
        Connected(map);
    }
}
