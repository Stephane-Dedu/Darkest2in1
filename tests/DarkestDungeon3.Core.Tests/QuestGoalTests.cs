using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class QuestGoalTests
{
    private readonly ITestOutputHelper _out;
    public QuestGoalTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Fact]
    public void GoalsResolvePerZone()
    {
        Assert.Equal("gather_holy_relic", Dd1.Goals.For("gather", "crypts").Id);
        Assert.Equal("reliquary", Dd1.Goals.For("gather", "crypts").CurioName);
        Assert.Equal(3, Dd1.Goals.For("gather", "crypts").Amount);
        Assert.Equal("inventory_activate_corrupted_altar", Dd1.Goals.For("inventory_activate", "crypts").Id);
        Assert.True(Dd1.Goals.For("inventory_activate", "crypts").NeedsItem);
        Assert.Equal("explore_all_rooms", Dd1.Goals.For("explore", "weald").Id);
        Assert.Equal(34, Dd1.Goals.Plot.Count);
    }

    [Fact]
    public void BossQuestsAppearAtZoneLevel()
    {
        var estate = new Estate { Seed = 1, QuestsCompleted = 5 };
        Assert.DoesNotContain(QuestBoard.Generate(estate, Dd1), q => q.IsPlot);

        estate.ZoneXp["crypts"] = 6;  // zone level 2: the Necromancer
        var necro = Assert.Single(QuestBoard.Generate(estate, Dd1).Where(q => q.IsPlot));
        Assert.Equal("plot_kill_necromancer_1", necro.PlotId);
        Assert.Equal("necromancer_A", necro.BossId);
        Assert.Equal(4, necro.ResolveXp);
        Assert.Contains(necro.Rewards, r => r.Type == "trinket" && r.Id == "very_rare");

        estate.CompletedPlotQuests.Add("plot_kill_necromancer_1");
        estate.ZoneXp["crypts"] = 10; // level 3: the Prophet
        Assert.Equal("plot_kill_prophet_1", QuestBoard.Generate(estate, Dd1).Single(q => q.IsPlot).PlotId);
    }

    [Fact]
    public void DarkestDungeonOpensAtZoneLevelSixOnePartAtATime()
    {
        var estate = new Estate { Seed = 2, QuestsCompleted = 9 };
        estate.ZoneXp["weald"] = 32;  // level 6
        var dd = QuestBoard.Generate(estate, Dd1).Where(q => q.Dungeon == QuestBoard.DarkestDungeon).ToList();
        Assert.Equal("plot_darkest_dungeon_1", Assert.Single(dd).PlotId);
        estate.CompletedPlotQuests.Add("plot_darkest_dungeon_1");
        dd = QuestBoard.Generate(estate, Dd1).Where(q => q.Dungeon == QuestBoard.DarkestDungeon).ToList();
        Assert.Equal("plot_darkest_dungeon_2", Assert.Single(dd).PlotId);
        Assert.False(Homecoming.WillEmbark(new HeroRecord { ResolveLevel = 4 }, dd[0]));
        Assert.True(Homecoming.WillEmbark(new HeroRecord { ResolveLevel = 5 }, dd[0]));
    }

    private static (Crawl, ExpeditionState) Expedition(string type, string zone, int seed)
    {
        var quest = new QuestOffer { Dungeon = zone, Type = type, Length = 2, Difficulty = 1, MapSeed = seed, GoalId = Dd1.Goals.For(type, zone).Id };
        var heroes = new List<HeroRecord>
        {
            new() { Id = "a", ClassId = "plague_doctor" }, new() { Id = "b", ClassId = "grave_robber" },
            new() { Id = "c", ClassId = "highwayman" }, new() { Id = "d", ClassId = "vestal" },
        };
        var pack = new Inventory();
        pack.Add(Supply.Food, 30);
        pack.Add(Supply.Torch, 12);
        pack.Add(Supply.Shovel, 4);
        var state = Embark.Create(Dd1, quest, heroes, pack, Provisioner.Load(Install, Content.Items));
        return (new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a", "b", "c", "d"), Content), state);
    }

    [Fact]
    public void GatherQuestPlacesAndCountsRelics()
    {
        var (crawl, state) = Expedition("gather", "crypts", 11);
        int relics = state.Map.Rooms.Count(r => r.IsQuestGoal) + state.Map.AllTiles.Count(t => t.IsQuestGoal);
        Assert.Equal(3, relics);
        Assert.Equal(1, state.Pack.Count(Supply.Firewood));        // medium quest: one free firewood
        Assert.True(state.Pack.Count(Supply.Antivenom) >= 1);      // the plague doctor brings antivenom

        crawl.Begin();
        WalkUntilDone(crawl, state, null);
        Assert.True(state.QuestComplete);
        Assert.Equal(3, state.Pack.Count("holy_relic"));
    }

    [Fact]
    public void InventoryActivateNeedsTheQuestItem()
    {
        var (crawl, state) = Expedition("inventory_activate", "crypts", 4);
        Assert.Equal(3, state.Pack.Count("holy_water"));            // the quest hands out its items
        crawl.Begin();
        WalkUntilDone(crawl, state, null);
        Assert.True(state.QuestComplete);
        Assert.Equal(0, state.Pack.Count("holy_water"));
    }

    private void WalkUntilDone(Crawl crawl, ExpeditionState state, string useItem)
    {
        var map = state.Map;
        int guard = 0;
        while (!state.QuestComplete && guard++ < 4000)
        {
            if (crawl.IsBlocked)
            {
                if (state.InRoom || crawl.CurrentTile.Content == HallContent.Battle) crawl.ResolveBattle();
                else crawl.ClearObstacle();
                continue;
            }
            if (crawl.CurioHere != null)
            {
                var r = crawl.InteractCurio("a", crawl.QuestItemNeededHere ?? useItem, out _);
                if (r?.OutcomeType == "Quest") _out.WriteLine(r.Text);
                if (crawl.CurioHere != null) crawl.SkipCurio();
                continue;
            }
            if (state.InRoom)
            {
                int next = NextHopToUnexplored(map, state.RoomId);
                if (next < 0) break;
                crawl.Travel(next);
            }
            else crawl.Step(forward: true);
        }
    }

    /// <summary>First step toward the nearest unvisited room or unexplored corridor (breadth-first).</summary>
    private static int NextHopToUnexplored(DungeonMap map, int from)
    {
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            foreach (int n in map.Neighbours(r))
            {
                bool interesting = !map.Room(n).Visited || map.FindCorridor(r, n).Tiles.Any(t => !t.Visited);
                if (interesting)
                {
                    int hop = n;
                    int at = r;
                    while (at != from) { hop = at; at = prev[at]; }
                    return hop;
                }
                if (!prev.ContainsKey(n)) { prev[n] = r; queue.Enqueue(n); }
            }
        }
        return -1;
    }
}
