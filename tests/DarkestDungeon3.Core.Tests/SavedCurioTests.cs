using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SavedCurioTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData("heirloom_chest", "skeleton_key")]
    [InlineData("thanks_chest", null)]
    public void NativeCurioResultReloadRestoresLootLinksWithoutConsumingOrResolvingAgain(string curio, string item)
    {
        var state = State(curio);
        int fillers = item == null ? Inventory.Slots : Inventory.Slots - 1;
        for (int i = 0; i < fillers; i++) state.Pack.Add("filler" + i, 1);
        if (item != null) state.Pack.Add(item, 2);
        var crawl = Crawl(state, new FakeParty("a"));
        var report = crawl.InteractCurio("a", item, out _);
        Assert.Same(report, state.PendingCurio);
        Assert.NotEmpty(report.LeftBehind);
        string json = Json(state);
        for (int i = 0; i < 3; i++)
        {
            state = SaveFile.FromJson(json).Expedition;
            var party = new FakeParty("a");
            crawl = Crawl(state, party);
            report = crawl.LastCurio;
            Assert.All(report.LeftBehind, d => Assert.Contains(d, report.Loot));
            Assert.Equal(report.LeftBehind.Count, report.LeftBehind.Distinct().Count());
            Assert.Equal(json, Json(state));
            Assert.Null(crawl.InteractCurio("a", item, out var rejected));
            Assert.Empty(rejected);
            Assert.Same(report, crawl.LastCurio);
            Assert.Empty(party.Log); Assert.Empty(party.Quirks);
            Assert.Equal(json, Json(state));
        }
        Assert.True(crawl.Discard("filler0"));
        var drop = report.LeftBehind[0];
        Assert.True(crawl.TakeLeftBehind(report.LeftBehind, 0));
        Assert.Contains(drop, report.Loot);
        Assert.Equal(drop.Amount, state.Pack.Count(drop.Key));
        Assert.True(crawl.DismissCurio(report));
        Assert.Null(Crawl(SaveFile.FromJson(Json(state)).Expedition, new FakeParty("a")).LastCurio);
    }

    [Theory]
    [InlineData("gather")]
    [InlineData("inventory_activate")]
    public void QuestResultReloadDoesNotRepeatProgressOrQuestItems(string type)
    {
        var goal = Dd1.Goals.For(type, "crypts");
        var state = State(goal.CurioName);
        state.Quest.Type = type; state.Goal = goal; state.GoalProgress = goal.Amount - 1;
        state.Map.Rooms[0].IsQuestGoal = true;
        string item = goal.NeedsItem ? ItemCatalog.QuestKey(goal.StartingItems[0].Id) : null;
        if (item != null) state.Pack.Add(item, 1);
        else state.Pack.Add(ItemCatalog.QuestKey(goal.QuestItem), goal.Amount - 1);
        var crawl = Crawl(state, new FakeParty("a"));
        var report = crawl.InteractCurio("a", item, out _);
        Assert.Equal("Quest", report.OutcomeType);
        Assert.True(state.QuestComplete);
        string json = Json(state);
        state = SaveFile.FromJson(json).Expedition;
        crawl = Crawl(state, new FakeParty("a"));
        Assert.Equal("Quest", crawl.LastCurio.OutcomeType);
        Assert.Null(crawl.InteractCurio("a", item, out _));
        Assert.Equal(json, Json(state));
        Assert.Equal(goal.Amount, state.GoalProgress);
        if (item != null) Assert.Equal(0, state.Pack.Count(item));
        else Assert.Equal(goal.Amount, state.Pack.Count(ItemCatalog.QuestKey(goal.QuestItem)));
        Assert.True(crawl.DismissCurio(crawl.LastCurio));
        Assert.Equal(goal.Amount, state.GoalProgress);
    }

    [Fact]
    public void IdenticalAndPartialWaitingDropsKeepDistinctDisplayLinksAfterReload()
    {
        var state = State("heirloom_chest");
        var first = new LootDrop { Type = "gold", Id = "", Amount = 200 };
        var second = new LootDrop { Type = "gold", Id = "", Amount = 200 };
        state.PendingCurio = new CurioReport { CurioId = "heirloom_chest", HeroId = "a", Loot = { first, second }, LeftBehind = { first, second } };
        state.Pack.Add("gold", 1700);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var crawl = Crawl(state, new FakeParty("a"));
        Assert.True(crawl.TakeLeftBehind(crawl.LastCurio.LeftBehind, 0));
        Assert.Equal(150, first.Amount); Assert.Equal(200, second.Amount);
        string json = Json(state);
        state = SaveFile.FromJson(json).Expedition;
        crawl = Crawl(state, new FakeParty("a"));
        var report = crawl.LastCurio;
        Assert.Equal(json, Json(state));
        Assert.Same(report.Loot[0], report.LeftBehind[0]);
        Assert.Same(report.Loot[1], report.LeftBehind[1]);
        Assert.NotSame(report.LeftBehind[0], report.LeftBehind[1]);
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(report.LeftBehind, 0));
        Assert.Equal(200, Assert.Single(report.LeftBehind).Amount);
        Assert.Same(report.Loot[1], report.LeftBehind[0]);
        Assert.Equal(1900, state.Pack.Count("gold"));
        Assert.Equal(11, state.RandomCounter);
    }

    [Fact]
    public void QuestRemainderStaleAndEndedReportsCannotBeDismissed()
    {
        var state = State("reliquary");
        var drop = new LootDrop { Type = "quest_item", Id = "holy_relic", Amount = 1 };
        state.PendingCurio = new CurioReport { Loot = { drop }, LeftBehind = { drop } };
        var crawl = Crawl(state, new FakeParty("a"));
        string before = Json(state);
        Assert.False(crawl.DismissCurio(null));
        Assert.False(crawl.DismissCurio(new CurioReport()));
        Assert.False(crawl.DismissCurio(crawl.LastCurio));
        Assert.Equal(before, Json(state));
        Assert.True(crawl.TakeLeftBehind(crawl.LastCurio.LeftBehind, 0));
        state.Ended = true;
        before = Json(state);
        Assert.False(crawl.DismissCurio(crawl.LastCurio));
        Assert.Equal(before, Json(state));
    }

    [Fact]
    public void LegacySavesInventNoCurioResult()
    {
        var state = SaveFile.FromJson("{\"Expedition\":{\"RandomCounter\":41}}").Expedition;
        string before = Json(state);
        Assert.Null(Crawl(state, new FakeParty()).LastCurio);
        Assert.Equal(before, Json(state));
        Assert.DoesNotContain("PendingCurio", before);
    }

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static Crawl Crawl(ExpeditionState state, FakeParty party) => new(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);
    private static ExpeditionState State(string curio) => new()
    {
        Seed = 101, RandomCounter = 11, Party = { "a" },
        Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1, Type = "gather" },
        Map = new DungeonMap { Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = curio } } }
    };
}
