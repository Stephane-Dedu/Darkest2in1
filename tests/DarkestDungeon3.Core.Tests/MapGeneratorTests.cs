using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class MapGeneratorTests
{
    private readonly ITestOutputHelper _out;
    public MapGeneratorTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Dd1 = Dd1Install.Find();

    [Fact]
    public void Dd1IsInstalled() => Assert.NotNull(Dd1);

    [Fact]
    public void ParsesAll44MapGeneratorBlocks()
    {
        var table = MapGenTable.Load(Dd1.MapGenerator);
        Assert.Equal(44, table.All.Count);
        var shortCrypts = table.Find("crypts", "short", "explore");
        Assert.Equal(9, shortCrypts.BaseRooms);
        Assert.Equal(10, shortCrypts.BaseCorridors);
        Assert.Equal((4, 3), (shortCrypts.GridWidth, shortCrypts.GridHeight));
        Assert.Equal(new IntRange(2, 4).ToString(), shortCrypts.HallBattle.ToString());
    }

    [Fact]
    public void LookupFallsBackForUndefinedCombinations()
    {
        var table = MapGenTable.Load(Dd1.MapGenerator);
        // DD1 has no short kill_boss block; we still get a sensible map labelled correctly.
        var p = table.Find("crypts", "short", "kill_boss");
        Assert.Equal("crypts", p.Dungeon);
        Assert.Equal("kill_boss", p.QuestType);
        Assert.Equal("short", p.Size);
    }

    [Fact]
    public void EveryDd1ConfigProducesValidMapsAcrossSeeds()
    {
        var table = MapGenTable.Load(Dd1.MapGenerator);
        foreach (var p in table.All)
        {
            var props = ZoneProps.Load(Dd1.ZoneProps(p.Dungeon));
            for (int seed = 0; seed < 200; seed++)
            {
                var map = MapGenerator.Generate(p, seed, props);
                string ctx = $"{p} seed {seed}";

                Assert.True(map.IsConnected(), "disconnected: " + ctx);
                Assert.Equal(System.Math.Min(p.BaseRooms, p.GridWidth * p.GridHeight), map.Rooms.Count);
                Assert.True(map.Corridors.Count >= map.Rooms.Count - 1, "too few corridors: " + ctx);
                Assert.Equal(RoomContent.Entrance, map.Room(map.EntranceRoomId).Content);
                Assert.All(map.Corridors, c => Assert.NotEmpty(c.Tiles));

                // No two things share a square.
                var cells = map.Rooms.Select(r => (r.X, r.Y)).Concat(map.AllTiles.Select(t => (t.X, t.Y))).ToList();
                Assert.True(cells.Count == cells.Distinct().Count(), "overlapping squares: " + ctx + "\n" + map.ToAscii());

                int hallBattles = map.AllTiles.Count(t => t.Content == HallContent.Battle);
                Assert.InRange(hallBattles, 0, p.HallBattle.Max);
                int roomBattles = map.Rooms.Count(r => r.HasBattle && r.Content != RoomContent.Boss);
                Assert.InRange(roomBattles, 0, p.TotalRoomBattles.Max);

                if (p.QuestType == "kill_boss")
                {
                    Assert.True(map.BossRoomId > 0, "no boss room: " + ctx);
                    int d = map.Distances(map.EntranceRoomId)[map.BossRoomId];
                    Assert.Equal(map.Distances(map.EntranceRoomId).Max(), d);
                }
                else Assert.Equal(-1, map.BossRoomId);

                Assert.All(map.AllTiles.Where(t => t.Content is HallContent.Curio or HallContent.Trap or HallContent.Obstacle),
                           t => Assert.False(string.IsNullOrEmpty(t.ContentId)));
            }
        }
    }

    [Fact]
    public void SameSeedSameMap()
    {
        var p = MapGenTable.Load(Dd1.MapGenerator).Find("weald", "medium", "explore");
        var props = ZoneProps.Load(Dd1.ZoneProps("weald"));
        Assert.Equal(MapGenerator.Generate(p, 1234, props).ToAscii(), MapGenerator.Generate(p, 1234, props).ToAscii());
        Assert.NotEqual(MapGenerator.Generate(p, 1234, props).ToAscii(), MapGenerator.Generate(p, 1235, props).ToAscii());
    }

    [Fact]
    public void PrintSampleMaps()
    {
        var table = MapGenTable.Load(Dd1.MapGenerator);
        foreach (var (zone, size, quest) in new[] { ("crypts", "short", "explore"), ("weald", "medium", "cleanse"), ("cove", "long", "kill_boss") })
        {
            var map = MapGenerator.Generate(table.Find(zone, size, quest), 7, ZoneProps.Load(Dd1.ZoneProps(zone)));
            _out.WriteLine($"{zone} {size} {quest}: {map.Rooms.Count} rooms, {map.Corridors.Count} corridors, {map.AllTiles.Count()} hall tiles");
            _out.WriteLine(map.ToAscii());
        }
    }
}
