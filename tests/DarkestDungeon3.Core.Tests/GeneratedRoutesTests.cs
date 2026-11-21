using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class GeneratedRoutesTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1TestInstall.Find());

    // The native expedition fixtures embark this exact quest (CrawlSceneryCheck): Sprawl, short cleanse, seed 73232.
    private static DungeonMap FixtureMap()
    {
        var heroes = new[] { "man_at_arms", "highwayman", "grave_robber", "plague_doctor" }
            .Select((c, i) => new HeroRecord { Id = "scenery-check-" + i, Name = c, ClassId = c, ResolveLevel = 1 }).ToList();
        var pack = new Inventory();
        pack.Add(Supply.Food, 12); pack.Add(Supply.Torch, 8); pack.Add(Supply.Shovel, 4);
        return Embark.Create(Dd1, new QuestOffer { Id = "scenery-check", Dungeon = "dd2_city", Type = "cleanse", Length = 1,
            Difficulty = 1, MapSeed = 73232 }, heroes, pack).Map;
    }

    [Fact]
    public void FixtureMapReachesAHallFightWithoutStartingAnotherFight()
    {
        var map = FixtureMap();
        Assert.True(GeneratedRoutes.ToHallBattle(map, map.EntranceRoomId, out var rooms, out int hall));
        int from = map.EntranceRoomId;
        foreach (int room in rooms)
        {
            var link = map.Room(from).CorridorIds.Select(map.Corridor).Single(c => c.Other(from) == room);
            Assert.DoesNotContain(link.Tiles, t => t.Content == HallContent.Battle && !t.Resolved);
            Assert.False(map.Room(room).IsSecret);
            Assert.False(map.Room(room).HasBattle && !map.Room(room).Cleared);
            from = room;
        }
        Assert.Contains(hall, map.Room(from).CorridorIds);
        int tile = GeneratedRoutes.FirstFightTile(map, hall, from);
        Assert.Equal(HallContent.Battle, map.Corridor(hall).Tiles[tile].Content);
        // Report the route so the native run can be checked against it.
        Console.WriteLine($"hall fight route: rooms {string.Join("-", rooms)}, hall {hall} from room {from}, tile {tile}");
    }

    [Fact]
    public void FightTileIsCountedFromTheRoomTheCorridorIsEnteredFrom()
    {
        var map = new DungeonMap();
        map.Rooms.Add(new Room { Id = 0, CorridorIds = { 0 } });
        map.Rooms.Add(new Room { Id = 1, CorridorIds = { 0 } });
        var corridor = new Corridor { Id = 0, RoomA = 0, RoomB = 1 };
        for (int i = 0; i < 5; i++) corridor.Tiles.Add(new HallTile { Index = i });
        corridor.Tiles[1].Content = corridor.Tiles[3].Content = HallContent.Battle;
        map.Corridors.Add(corridor);
        Assert.Equal(1, GeneratedRoutes.FirstFightTile(map, 0, 0));
        Assert.Equal(3, GeneratedRoutes.FirstFightTile(map, 0, 1));
        corridor.Tiles[1].Resolved = corridor.Tiles[3].Resolved = true;
        Assert.Equal(-1, GeneratedRoutes.FirstFightTile(map, 0, 0));
        Assert.False(GeneratedRoutes.ToHallBattle(map, 0, out _, out _));
    }
}
