using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ScoutingTests
{
    private static DungeonMap Line()
    {
        var map = new DungeonMap();
        for (int i = 0; i < 3; i++) map.Rooms.Add(new Room { Id = i });
        AddCorridor(map, 0, 1, 8);
        AddCorridor(map, 1, 2, 4);
        return map;
    }

    private static void AddCorridor(DungeonMap map, int a, int b, int length)
    {
        var c = new Corridor { Id = map.Corridors.Count, RoomA = a, RoomB = b };
        for (int i = 0; i < length; i++) c.Tiles.Add(new HallTile { Index = i });
        map.Corridors.Add(c);
        map.Room(a).CorridorIds.Add(c.Id);
        map.Room(b).CorridorIds.Add(c.Id);
    }

    [Theory]
    [InlineData(6, 6, false, false)]
    [InlineData(8, 8, true, false)]
    [InlineData(11, 11, true, false)]
    [InlineData(12, 12, true, true)]
    public void ScoutingSpendsSquaresBeforeRevealingTheFarRoom(int budget, int tiles, bool room1, bool room2)
    {
        var map = Line();
        int revealed = map.ScoutFrom(0, budget);
        Assert.Equal(tiles, map.AllTiles.Count(t => t.Scouted));
        Assert.Equal(room1, map.Room(1).Scouted);
        Assert.Equal(room2, map.Room(2).Scouted);
        Assert.Equal(tiles + (room1 ? 1 : 0) + (room2 ? 1 : 0), revealed);
        Assert.Equal(0, map.ScoutFrom(0, budget));   // already known: no duplicate reveals
    }

    [Fact]
    public void ScoutingFromTheOtherEndRevealsTheCorrectSquares()
    {
        var map = Line();
        map.ScoutFrom(2, 6);
        Assert.True(map.Room(1).Scouted);
        Assert.False(map.Room(0).Scouted);
        Assert.All(map.Corridor(0).Tiles.Take(6), t => Assert.False(t.Scouted));
        Assert.All(map.Corridor(0).Tiles.Skip(6), t => Assert.True(t.Scouted));
        Assert.All(map.Corridor(1).Tiles, t => Assert.True(t.Scouted));
    }

    [Fact]
    public void CyclesTerminateAndBranchesShareTheRemainingBudget()
    {
        var map = Line();
        AddCorridor(map, 0, 2, 2);
        Assert.Equal(16, map.ScoutFrom(0, 12));   // fourteen squares and two rooms, counted once
        Assert.All(map.Rooms.Skip(1), r => Assert.True(r.Scouted));
        Assert.Equal(0, map.ScoutFrom(0, 12));
    }

    [Theory]
    [InlineData(0f, 6)]
    [InlineData(1f, 8)]
    public void CrawlUsesNormalOrCriticalScoutingDistance(float critical, int expected)
    {
        var dd1 = Dd1Campaign.Load(Dd1Install.Find());
        var rules = CrawlRules.FromDd1(dd1.Rules);
        Assert.Equal(0.5f, rules.ScoutCriticalChance);
        rules.ScoutChanceBase = 1f;
        rules.ScoutCriticalChance = critical;
        rules.Darkness.Clear();
        var map = Line();
        map.EntranceRoomId = 2;
        var state = new ExpeditionState { Map = map, Quest = new QuestOffer { Type = "explore" }, Party = { "a" } };
        var crawl = new Crawl(state, rules, new FakeParty("a"));
        Assert.DoesNotContain(crawl.Begin(), e => e.Type == CrawlEventType.Scouted);
        crawl.Travel(1);
        for (int i = 0; i < 3; i++) crawl.Step(true);
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.Scouted);
        Assert.Equal(expected, state.Map.Corridor(0).Tiles.Count(t => t.Scouted));
        state.Map.AllTiles.ToList().ForEach(t => t.Scouted = false);
        state.Map.Rooms.ForEach(r => { r.Scouted = false; r.Visited = false; });
        state.Quest.ScoutingEnabled = false;
        Assert.DoesNotContain(crawl.Begin(), e => e.Type == CrawlEventType.Scouted);
        Assert.DoesNotContain(state.Map.AllTiles, t => t.Scouted);
    }

    [Fact]
    public void EnteringADungeonUsesItsOwnChanceWithoutTheRadiantLightBonus()
    {
        var dd1 = Dd1Campaign.Load(Dd1Install.Find());
        var rules = CrawlRules.FromDd1(dd1.Rules);
        Assert.Equal(0f, rules.ScoutEntryChance);
        rules.ScoutChanceBase = 1f;   // even a guaranteed room scout must not leak into entry scouting
        for (int seed = 0; seed < 32; seed++)
        {
            var state = new ExpeditionState { Map = Line(), Quest = new QuestOffer { Type = "explore" }, Party = { "a" }, Seed = seed, Light = 100f };
            var crawl = new Crawl(state, rules, new FakeParty("a"));
            Assert.DoesNotContain(crawl.Begin(), e => e.Type == CrawlEventType.Scouted);
            Assert.DoesNotContain(state.Map.AllTiles, t => t.Scouted);
        }
        rules.ScoutEntryChance = 1f;
        var enabled = new ExpeditionState { Map = Line(), Quest = new QuestOffer { Type = "explore" }, Party = { "a" } };
        Assert.Contains(new Crawl(enabled, rules, new FakeParty("a")).Begin(), e => e.Type == CrawlEventType.Scouted);
    }
}
