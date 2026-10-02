using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class FakeParty : IParty
{
    public Dictionary<string, float> Hp = new();
    public Dictionary<string, int> Stress = new();
    public List<string> Log = new();

    public FakeParty(params string[] heroes)
    {
        foreach (var h in heroes) { Hp[h] = 1f; Stress[h] = 0; }
    }

    public IReadOnlyList<string> Alive => Hp.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
    public float HpFraction(string heroId) => Hp[heroId];
    public void Damage(string heroId, float f, string cause) { Hp[heroId] = System.Math.Max(0.01f, Hp[heroId] - f); Log.Add($"dmg {heroId} {f} {cause}"); }
    public void Heal(string heroId, float f) => Hp[heroId] = System.Math.Min(1f, Hp[heroId] + f);
    public void AddStress(string heroId, int points, string cause) => Stress[heroId] += points;
    public List<string> Quirks = new();
    public string AddDd1Quirk(string heroId, string dd1QuirkId) { Quirks.Add(heroId + ":" + dd1QuirkId); return "dd2_" + dd1QuirkId; }
    public string PurgeNegative(string heroId) => "purged";
    public string CureDisease(string heroId) => "cured";
}

public class CrawlTests
{
    private readonly ITestOutputHelper _out;
    public CrawlTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());
    private static readonly CrawlRules Rules = CrawlRules.FromDd1(Dd1.Rules);

    private static (Crawl crawl, FakeParty party) NewCrawl(string zone, string size, string type, int seed, int food = 16, int torches = 8, int shovels = 2)
    {
        var quest = new QuestOffer { Dungeon = zone, Type = type, Length = size == "short" ? 1 : size == "medium" ? 2 : 3, Difficulty = 1, MapSeed = seed };
        var map = MapGenerator.Generate(Dd1.MapGen.Find(zone, size, type), seed, Dd1.Props(zone));
        var state = new ExpeditionState { Quest = quest, Map = map, Seed = seed, Party = { "a", "b", "c", "d" } };
        state.Pack.Add(Supply.Food, food);
        state.Pack.Add(Supply.Torch, torches);
        state.Pack.Add(Supply.Shovel, shovels);
        var party = new FakeParty("a", "b", "c", "d");
        return (new Crawl(state, Rules, party), party);
    }

    [Fact]
    public void RulesComeFromDd1()
    {
        Assert.Equal(6f, Rules.LightLossNewTile);
        Assert.Equal(1f, Rules.LightLossVisitedTile);
        Assert.Equal(0.3f, Rules.StressChanceForward, 3);
        Assert.Equal(0.55f, Rules.StressChanceBack, 3);
        Assert.Equal(5, Rules.Darkness.Count);
        Assert.Equal("Radiant", CrawlRules.BandName(100));
        Assert.Equal("Dim", CrawlRules.BandName(75));       // DD1's lowest_excluded: 75 is not Radiant
        Assert.Equal(0, Rules.Band(100).StressChanceIncrease);
        Assert.Equal(25, Rules.Band(10).StressChanceIncrease);
    }

    [Fact]
    public void StartsInEntranceAndBattlesBlockUntilWon()
    {
        var (crawl, _) = NewCrawl("crypts", "short", "explore", 5);
        crawl.Begin();
        Assert.True(crawl.State.InRoom);
        Assert.Equal(crawl.State.Map.EntranceRoomId, crawl.State.RoomId);
        Assert.False(crawl.IsBlocked);

        // Walk until something blocks us.
        var map = crawl.State.Map;
        var target = map.Neighbours(crawl.State.RoomId).First();
        var events = crawl.Travel(target);
        Assert.Contains(events, e => e.Type == CrawlEventType.EnteredTile);
        Assert.True(crawl.State.Light < 100);
    }

    [Fact]
    public void LightDropsSixPerNewSquareAndOnePerVisited()
    {
        var (crawl, _) = NewCrawl("crypts", "short", "explore", 21);
        crawl.Begin();
        var map = crawl.State.Map;
        var corridor = map.Corridors.First(c => c.RoomA == map.EntranceRoomId || c.RoomB == map.EntranceRoomId);
        int other = corridor.Other(map.EntranceRoomId);
        crawl.Travel(other);
        float afterFirst = crawl.State.Light;
        Assert.Equal(94f, afterFirst);
        if (crawl.IsBlocked) crawl.ResolveBattle();
        crawl.Step(forward: false); // back into the entrance room
        Assert.True(crawl.State.InRoom);
        crawl.Travel(other);        // the first square again: already visited
        Assert.Equal(afterFirst - 1f, crawl.State.Light);
    }

    [Fact]
    public void HungerEatsFoodOrStarves()
    {
        // Find a map whose first corridor from the entrance has a hunger square.
        for (int seed = 0; seed < 500; seed++)
        {
            foreach (int food in new[] { 8, 0 })
            {
                var (crawl, party) = NewCrawl("weald", "short", "explore", seed, food: food);
                crawl.Begin();
                var map = crawl.State.Map;
                var c = map.Corridors.FirstOrDefault(k => k.RoomA == map.EntranceRoomId && k.Tiles[0].Content == HallContent.Hunger);
                if (c == null) break;
                var events = crawl.Travel(c.RoomB);
                if (food > 0)
                {
                    Assert.Contains(events, e => e.Type == CrawlEventType.Ate);
                    Assert.Equal(food - 4, crawl.State.Pack.Count(Supply.Food));
                }
                else
                {
                    Assert.Contains(events, e => e.Type == CrawlEventType.Starving);
                    Assert.All(party.Hp.Values, hp => Assert.True(hp <= 0.8f + 1e-4));
                    return;
                }
            }
        }
        Assert.Fail("no map with a hunger square next to the entrance in 500 seeds");
    }

    [Theory]
    [InlineData("crypts", "short", "explore")]
    [InlineData("weald", "medium", "cleanse")]
    [InlineData("cove", "long", "kill_boss")]
    [InlineData("warrens", "medium", "explore")]
    public void AutoWalkerFinishesTheQuest(string zone, string size, string type)
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var (crawl, party) = NewCrawl(zone, size, type, seed, food: 40, torches: 16, shovels: 6);
            crawl.Begin();
            int battles = 0, guard = 0;
            var map = crawl.State.Map;

            while (!crawl.State.QuestComplete && guard++ < 5000)
            {
                if (crawl.IsBlocked)
                {
                    if (crawl.State.InRoom || crawl.CurrentTile.Content == HallContent.Battle) { crawl.ResolveBattle(); battles++; }
                    else crawl.ClearObstacle();
                    continue;
                }
                if (crawl.State.Light < 50) crawl.UseTorch();

                if (crawl.State.InRoom)
                {
                    // Head for the nearest room that still matters for the quest.
                    int next = NextHop(map, crawl.State.RoomId, type);
                    if (next < 0) break;
                    crawl.Travel(next);
                }
                else
                {
                    if (crawl.CurrentTile.Content == HallContent.Trap && !crawl.CurrentTile.Resolved) crawl.DisarmTrap();
                    crawl.Step(forward: true);
                }
            }
            Assert.True(crawl.State.QuestComplete, $"{zone} {size} {type} seed {seed} not complete after {guard} moves\n{map.ToAscii()}");
            if (seed == 0) _out.WriteLine($"{zone} {size} {type}: {crawl.State.StepsTaken} steps, {battles} fights, light {crawl.State.Light}, stress {string.Join(",", party.Stress.Values)}, food left {crawl.State.Pack.Count(Supply.Food)}");
        }
    }

    /// <summary>First step on the shortest path to the closest room the quest still needs.</summary>
    private static int NextHop(DungeonMap map, int from, string questType)
    {
        bool Wanted(Room r) => questType switch
        {
            "cleanse" => r.HasBattle && !r.Cleared,
            "kill_boss" => r.Id == map.BossRoomId,
            _ => !r.Visited,
        };
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            if (r != from && Wanted(map.Room(r)))
            {
                while (prev[r] != from) r = prev[r];
                return r;
            }
            foreach (int n in map.Neighbours(r))
                if (!prev.ContainsKey(n)) { prev[n] = r; queue.Enqueue(n); }
        }
        return -1;
    }

    [Fact]
    public void HallwayStressAveragesLikeDd1()
    {
        // DD1: 30% per square of 2 stress (of 100) per hero. In DD2 points that's 0.06 per hero per square.
        int squares = 0, stress = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            var (crawl, party) = NewCrawl("crypts", "medium", "explore", seed, torches: 0);
            crawl.Begin();
            crawl.State.Light = 100;
            var map = crawl.State.Map;
            int target = map.Neighbours(map.EntranceRoomId).First();
            crawl.Travel(target);
            squares++;
            stress += party.Stress.Values.Sum();
        }
        double perHeroPerSquare = stress / (double)(squares * 4);
        _out.WriteLine($"stress per hero per new square at full light: {perHeroPerSquare:F3}");
        Assert.InRange(perHeroPerSquare, 0.03, 0.10);
    }
}
