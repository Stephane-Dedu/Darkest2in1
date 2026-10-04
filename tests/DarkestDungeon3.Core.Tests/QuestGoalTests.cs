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
        estate.CompletedPlotQuests.Add("plot_tutorial_crypts");   // the Ruins tutorial is played
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

    [Fact]
    public void FullPackLootCanBeTakenAfterDroppingSomething()
    {
        var (crawl, state) = Expedition("explore", "crypts", 3);
        // Fill the 16 slots with single torches' worth of stacks (DD1: 8 torches a stack).
        state.Pack.Items.Clear();
        state.Pack.Add(Supply.Torch, 8 * Inventory.Slots);
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        var gold = new LootDrop { Type = "gold", Id = "", Amount = 250 };
        var left = new List<LootDrop> { gold };
        var taken = new List<LootDrop>();

        Assert.False(crawl.TakeLeftBehind(left, 0, taken));                 // no room yet
        for (int i = 0; i < 8; i++) Assert.True(crawl.Discard(Supply.Torch));   // shift+click: drop one at a time
        Assert.True(crawl.TakeLeftBehind(left, 0, taken));                  // a slot is free now
        Assert.Empty(left);
        Assert.Same(gold, Assert.Single(taken));
        Assert.Equal(250, state.Pack.Count(gold.Key));

        string quest = ItemCatalog.QuestKey("holy_water");
        state.Pack.Add(quest, 1);
        Assert.False(crawl.Discard(quest));                                   // quest items can't be thrown away
        Assert.False(Crawl.CanLeave(new[] { new LootDrop { Type = "quest_item", Id = "holy_water", Amount = 1 } }));
        Assert.True(Crawl.CanLeave(new[] { gold }));
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
    public void DarkestDungeonRetreatCostsAHero()
    {
        var dd1 = Dd1.Goals.Plot.Single(p => p.Id == "plot_darkest_dungeon_1");
        Assert.True(dd1.CanRetreat);
        Assert.Equal(1, dd1.RetreatKillCount);                                        // DD1 retreat_party_kill_count
        Assert.False(Dd1.Goals.Plot.Single(p => p.Id == "plot_darkest_dungeon_4").CanRetreat);
        Assert.Equal(0, Dd1.Goals.Plot.Single(p => p.Id == "plot_kill_necromancer_1").RetreatKillCount);

        var quest = new QuestOffer { Dungeon = "darkestdungeon", Type = "explore", Length = 2, Difficulty = 6, RetreatKillCount = 1 };
        var outcomes = new[] { "a", "b", "c", "d" }.Select(id => new HeroOutcome { HeroId = id }).ToList();
        var after = Homecoming.RetreatSacrifices(quest, retreated: true, outcomes, new Rng(5));
        var dead = Assert.Single(after, o => o.Died);
        Assert.Equal("sacrificed to cover the retreat", dead.CauseOfDeath);
        var kept = Homecoming.RetreatSacrifices(quest, retreated: false, new[] { new HeroOutcome { HeroId = "a" } }, new Rng(5));
        Assert.DoesNotContain(kept, o => o.Died);                                     // only when abandoning
        var normal = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 };
        Assert.DoesNotContain(Homecoming.RetreatSacrifices(normal, true, new[] { new HeroOutcome { HeroId = "a" } }, new Rng(5)), o => o.Died);
    }

    [Fact]
    public void DarkestDungeonHasNoSurpriseOrScoutingAndAWinClearsStress()
    {
        var dd = Dd1.Goals.Plot.Single(p => p.Id == "plot_darkest_dungeon_1");
        Assert.False(dd.SurpriseEnabled);                       // DD1 is_surprise_enabled
        Assert.False(dd.ScoutingEnabled);                       // is_scouting_enabled
        Assert.True(dd.ClearsRosterStress);                     // is_roster_stress_cleared_on_completion
        Assert.True(Dd1.Goals.Plot.Single(p => p.Id == "plot_kill_necromancer_1").SurpriseEnabled);

        var (crawl, state) = Expedition("explore", "crypts", 11);
        state.Quest.SurpriseEnabled = false;
        Assert.Equal((0f, 0f), crawl.SurpriseChances(corridor: true, known: false, ambush: false));

        var estate = new Estate { Seed = 3 };
        var inParty = new HeroRecord { Id = "p", Name = "P", ClassId = "highwayman", Stress = 1 };
        var atHome = new HeroRecord { Id = "h", Name = "H", ClassId = "vestal", Stress = 7 };
        estate.Roster.Add(inParty);
        estate.Roster.Add(atHome);
        var exp = new ExpeditionState
        {
            Quest = new QuestOffer { Id = "q", Dungeon = "darkestdungeon", Type = "explore", Length = 2, Difficulty = 6, ClearsRosterStress = true },
            Party = { "p" },
            QuestComplete = true,
        };
        Homecoming.Report(estate, Dd1, exp, new[] { new HeroOutcome { HeroId = "p", Stress = 6 } });
        Assert.Equal(0, inParty.Stress);
        Assert.Equal(0, atHome.Stress);
    }

    [Fact]
    public void TownBackgroundFollowsDd1DisplayStates()
    {
        var render = Dd1.TownRender;
        Assert.Equal("campaign/town/town_bg.png", render.Background(null, null));
        Assert.Equal("campaign/town/town_bg_post_dd_1.png", render.Background("plot_darkest_dungeon_1", null));
        Assert.Equal("campaign/town/town_bg_activity_stress_heal_buff.png", render.Background(null, "in_activity_buff_stress_heal_buff"));
        Assert.Equal("campaign/town/town_bg.png", render.Background("plot_kill_necromancer_1", "some_event"));

        var estate = new Estate { Seed = 4 };
        var exp = new ExpeditionState { Quest = new QuestOffer { Id = "q", Dungeon = "darkestdungeon", Type = "explore", Length = 2, Difficulty = 6, PlotId = "plot_darkest_dungeon_2" } };
        Homecoming.Report(estate, Dd1, exp, new HeroOutcome[0]);
        Assert.Equal("plot_darkest_dungeon_2", estate.LastReturnPlotId);
    }

    [Fact]
    public void FailingTheDarkestDungeonWithSeasonedHeroesInspiresTheRoster()
    {
        var dd = Dd1.Goals.Plot.Single(p => p.Id == "plot_darkest_dungeon_1");
        Assert.Equal(new[] { "darkest_dungeon_failure_roster_resolve_xp" }, dd.RosterBuffsOnFailure);
        Assert.Equal(5, dd.RosterBuffMinResolve);

        HomecomingReport Fail(int partyResolve, out Estate estate)
        {
            estate = new Estate { Seed = 5 };
            estate.Roster.Add(new HeroRecord { Id = "p", Name = "P", ClassId = "highwayman", ResolveLevel = partyResolve });
            estate.Roster.Add(new HeroRecord { Id = "h", Name = "H", ClassId = "vestal" });
            var exp = new ExpeditionState
            {
                Quest = new QuestOffer { Id = "q", Dungeon = "darkestdungeon", Type = "explore", Length = 2, Difficulty = 6,
                                         RosterBuffsOnFailure = dd.RosterBuffsOnFailure.ToList(), RosterBuffMinResolve = dd.RosterBuffMinResolve },
                Party = { "p" },
                Retreated = true,
            };
            return Homecoming.Report(estate, Dd1, exp, new[] { new HeroOutcome { HeroId = "p", Stress = 2 } });
        }
        Fail(5, out var seasoned);
        Assert.All(seasoned.Roster, h => Assert.Contains("darkest_dungeon_failure_roster_resolve_xp", h.PendingBuffs));   // whole roster
        Assert.Equal(1f, Dd1.Buffs.Get("darkest_dungeon_failure_roster_resolve_xp").Amount);                               // +100% resolve XP
        Fail(4, out var green);
        Assert.All(green.Roster, h => Assert.Empty(h.PendingBuffs));                                                     // below resolve 5
    }

    [Fact]
    public void TrinketWarningOnHarderQuestsWithFewTrinkets()
    {
        var heroes = new[] { "a", "b", "c", "d" }.Select(id => new HeroRecord { Id = id, ClassId = "highwayman" }).ToList();
        var veteran = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 3 };
        var apprentice = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 };
        Assert.True(Embark.TrinketWarning(Dd1, veteran, heroes));          // 0 of 8 slots
        Assert.False(Embark.TrinketWarning(Dd1, apprentice, heroes));      // DD1: only from difficulty 3
        foreach (var h in heroes) h.Trinkets.Add("t_" + h.Id);
        Assert.False(Embark.TrinketWarning(Dd1, veteran, heroes));         // 4 of 8: 50% is enough
        heroes[0].Trinkets.Clear();
        Assert.True(Embark.TrinketWarning(Dd1, veteran, heroes));          // 3 of 8
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
        Assert.Equal(3, state.Pack.Count(ItemCatalog.QuestKey("holy_relic")));
    }

    [Fact]
    public void InventoryActivateNeedsTheQuestItem()
    {
        var (crawl, state) = Expedition("inventory_activate", "crypts", 4);
        Assert.Equal(3, state.Pack.Count(ItemCatalog.QuestKey("holy_water")));   // the quest hands out its items
        Assert.Equal(0, state.Pack.Count(Supply.HolyWater));                      // not the supply of the same name
        crawl.Begin();
        WalkUntilDone(crawl, state, null);
        Assert.True(state.QuestComplete);
        Assert.Equal(0, state.Pack.Count(ItemCatalog.QuestKey("holy_water")));
    }

    /// <summary>Every quest DD1's tables offer in its four zones can be finished: enough quest curios and quest items,
    /// and using each quest curio counts.</summary>
    [Fact]
    public void EveryDd1QuestCanBeFinished()
    {
        var problems = new List<string>();
        foreach (var zone in new[] { "crypts", "weald", "warrens", "cove" })
        {
            var offers = Dd1.QuestTables[zone].SelectMany(t => t).Select(o => (o.Type, o.Length)).Distinct().ToList();
            foreach (var (type, length) in offers)
                for (int seed = 1; seed <= 12; seed++)
                {
                    var quest = new QuestOffer { Dungeon = zone, Type = type, Length = length, Difficulty = 1, MapSeed = seed * 7919 };
                    var heroes = new[] { "a", "b", "c", "d" }.Select(id => new HeroRecord { Id = id, ClassId = "highwayman" }).ToList();
                    var state = Embark.Create(Dd1, quest, heroes, new Inventory(), Provisioner.Load(Install, Content.Items));
                    var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a", "b", "c", "d"), Content);
                    crawl.Begin();
                    string where = $"{zone} {type} {length} #{seed}";
                    var map = state.Map;
                    switch (type)
                    {
                        case "kill_boss":
                            if (map.BossRoomId < 0) problems.Add(where + ": no boss room");
                            continue;
                        case "cleanse":
                            if (!map.Rooms.Any(r => r.HasBattle)) problems.Add(where + ": no battles to cleanse");
                            continue;
                        case "explore":
                            continue;
                    }
                    var goal = state.Goal;
                    if (goal == null || goal.Amount <= 0) { problems.Add(where + ": no goal"); continue; }
                    var rooms = map.Rooms.Where(r => r.IsQuestGoal).ToList();
                    var tiles = map.Corridors.SelectMany(c => c.Tiles.Where(t => t.IsQuestGoal).Select(t => (c, t))).ToList();
                    if (rooms.Count + tiles.Count < goal.Amount) { problems.Add($"{where}: {rooms.Count + tiles.Count} quest curios for {goal.Amount}"); continue; }
                    if (goal.NeedsItem && state.Pack.Count(ItemCatalog.QuestKey(goal.StartingItems[0].Id)) < goal.Amount)
                        problems.Add($"{where}: {state.Pack.Count(ItemCatalog.QuestKey(goal.StartingItems[0].Id))} quest items for {goal.Amount}");
                    foreach (var r in rooms)
                    {
                        state.RoomId = r.Id; state.CorridorId = -1; state.TileIndex = -1;
                        crawl.InteractCurio("a", crawl.QuestItemNeededHere, out _);
                    }
                    foreach (var (c, t) in tiles)
                    {
                        state.CorridorId = c.Id; state.TileIndex = t.Index; state.HeadingRoomId = c.RoomB;
                        crawl.InteractCurio("a", crawl.QuestItemNeededHere, out _);
                    }
                    if (!state.QuestComplete) problems.Add($"{where}: not complete after every quest curio ({state.GoalProgress}/{goal.Amount})");
                }
        }
        Assert.True(problems.Count == 0, string.Join(" | ", problems.Take(20)));
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
                else if (crawl.CurrentTile?.Content == HallContent.Trap) crawl.DisarmTrap();
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
