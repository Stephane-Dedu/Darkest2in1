using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class GatherLootTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    private static (Crawl Crawl, ExpeditionState State) AtGoal(string zone, bool hall = false)
    {
        var goal = Dd1.Goals.For("gather", zone);
        Assert.Equal("gather", goal.Type);
        var map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = goal.CurioName, IsQuestGoal = true } },
            Corridors = { new Corridor { Id = 0, Tiles = { new HallTile { Index = 0, Content = HallContent.Curio, ContentId = goal.CurioName, IsQuestGoal = true } } } },
        };
        var state = new ExpeditionState
        {
            Started = true, Goal = goal, Quest = new QuestOffer { Dungeon = zone, Type = "gather" }, Map = map,
            RoomId = hall ? -1 : 0, CorridorId = hall ? 0 : -1, TileIndex = hall ? 0 : -1, RandomCounter = 19, Party = { "h" },
        };
        return (new Crawl(state, new CrawlRules(), new FakeParty("h"), Content), state);
    }

    [Theory]
    [InlineData("crypts", false)]
    [InlineData("warrens", false)]
    [InlineData("weald", false)]
    [InlineData("cove", false)]
    [InlineData("crypts", true)]
    public void FullPackRetainsNativeGatherItemForPickupAfterReloadWithoutAnotherObjective(string zone, bool hall)
    {
        var (crawl, state) = AtGoal(zone, hall);
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var report = crawl.InteractCurio("h", null, out var overflow);
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        string key = ItemCatalog.QuestKey(state.Goal.QuestItem);
        Assert.Equal(0, state.Pack.Count(key));
        var drop = Assert.Single(report.Loot);
        Assert.Same(drop, Assert.Single(report.LeftBehind));
        Assert.Same(drop, Assert.Single(overflow));
        Assert.Equal(1, drop.Amount);
        Assert.Equal(1, state.GoalProgress);
        Assert.False(Crawl.CanLeave(report.LeftBehind));
        Assert.False(crawl.DismissCurio(report));
        Assert.Null(crawl.InteractCurio("h", null, out var repeated));
        Assert.Empty(repeated);
        Assert.Same(report, crawl.LastCurio);
        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        crawl = new Crawl(state, new CrawlRules(), new FakeParty("h"), Content);
        report = crawl.LastCurio;
        Assert.Same(Assert.Single(report.Loot), Assert.Single(report.LeftBehind));
        Assert.False(crawl.TakeLeftBehind(report.LeftBehind, 0));
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(report.LeftBehind, 0));
        Assert.Empty(report.LeftBehind);
        Assert.Equal(1, state.Pack.Count(key));
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        Assert.Equal(1, state.GoalProgress);
        Assert.Equal(19, state.RandomCounter);
        Assert.True(hall ? state.Map.Corridor(0).Tiles[0].Resolved : state.Map.Room(0).CurioTaken);
        Assert.True(crawl.DismissCurio(report));
        Assert.Null(crawl.LastCurio);
        Assert.False(crawl.TakeLeftBehind(report.LeftBehind, 0));
    }

    [Theory]
    [InlineData("crypts")]
    [InlineData("warrens")]
    [InlineData("weald")]
    [InlineData("cove")]
    public void AvailableSlotCollectsNativeGatherItemOnceAndDoesNotClaimOverflow(string zone)
    {
        var (crawl, state) = AtGoal(zone);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var report = crawl.InteractCurio("h", null, out var overflow);
        var drop = Assert.Single(report.Loot);
        Assert.Empty(report.LeftBehind);
        Assert.Empty(overflow);
        Assert.Equal(1, state.Pack.Count(drop.Key));
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        Assert.Equal(1, Content.Items.StackLimit(drop.Key)); // native quest objects occupy one slot each
        Assert.Equal(1, state.GoalProgress);
        Assert.True(crawl.DismissCurio(report));
        Assert.Null(crawl.InteractCurio("h", null, out _));
        Assert.Equal(1, state.Pack.Count(drop.Key));
        Assert.Equal(19, state.RandomCounter);
    }

    [Fact]
    public void MissingCatalogKeepsGatherItemOnTheScrollWithoutInventingCapacity()
    {
        var (_, state) = AtGoal("crypts");
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("h"));
        var report = crawl.InteractCurio("h", null, out var overflow);
        Assert.Empty(state.Pack.Items);
        Assert.Same(Assert.Single(report.Loot), Assert.Single(report.LeftBehind));
        Assert.Single(overflow);
        Assert.False(crawl.DismissCurio(report));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherInvestigationCannotReplaceRequiredGatherLoot(bool hall)
    {
        var goal = Dd1.Goals.For("gather", "crypts");
        var map = new DungeonMap
        {
            Rooms =
            {
                new Room { Id = 0, Content = RoomContent.Curio, CurioId = goal.CurioName, IsQuestGoal = true },
                new Room { Id = 1, Content = RoomContent.Curio, CurioId = goal.CurioName, IsQuestGoal = true },
            },
            Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles =
            {
                new HallTile { Index = 0, Content = hall ? HallContent.Curio : HallContent.Empty,
                    ContentId = hall ? goal.CurioName : null, IsQuestGoal = hall },
            } } },
        };
        var state = new ExpeditionState
        {
            Started = true, Goal = goal, Quest = new QuestOffer { Dungeon = "crypts", Type = "gather" },
            Map = map, RoomId = 0, CorridorId = -1, TileIndex = -1, Party = { "h" },
        };
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("h"), Content);
        var first = crawl.InteractCurio("h", null, out _);
        Assert.Single(first.LeftBehind);
        crawl.Travel(1);
        if (!hall) crawl.Step(true);
        Assert.Equal(goal.CurioName, crawl.CurioHere);
        string before = new SaveFile { Expedition = state }.ToJson();
        Assert.Null(crawl.InteractCurio("h", null, out var overflow));
        Assert.Empty(overflow);
        Assert.Same(first, crawl.LastCurio);
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
        state = SaveFile.FromJson(before).Expedition;
        crawl = new Crawl(state, new CrawlRules(), new FakeParty("h"), Content);
        first = crawl.LastCurio;
        Assert.Null(crawl.InteractCurio("h", null, out var reloadedOverflow));
        Assert.Empty(reloadedOverflow);
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(first.LeftBehind, 0));
        Assert.True(crawl.DismissCurio(first));
        var second = crawl.InteractCurio("h", null, out var nextOverflow);
        Assert.NotNull(second);
        Assert.Same(second, crawl.LastCurio);
        Assert.Single(nextOverflow);
        Assert.Equal(2, state.GoalProgress);
        Assert.Equal(1, state.Pack.Count(ItemCatalog.QuestKey(goal.QuestItem)));
        Assert.Single(second.LeftBehind);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void PendingRequiredLootGuardsNativeItemConsumptionButOptionalLootKeepsItsExistingBehavior(bool spoils, bool required)
    {
        var drop = new LootDrop { Type = required ? "quest_item" : "gold", Id = required ? "holy_relic" : null, Amount = 1 };
        var state = new ExpeditionState
        {
            Started = true, Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 }, Party = { "h" },
            RoomId = 0, CorridorId = -1, TileIndex = -1, RandomCounter = 11,
            Map = new DungeonMap { Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = "heirloom_chest" } } },
        };
        state.Pack.Add(Supply.Key, 1);
        if (spoils) state.PendingSpoils = new BattleSpoils { LeftBehind = { drop } };
        else state.PendingCurio = new CurioReport { Loot = { drop }, LeftBehind = { drop } };
        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("h"), Content);
        string before = new SaveFile { Expedition = state }.ToJson();
        var report = crawl.InteractCurio("h", Supply.Key, out var overflow);
        if (required)
        {
            Assert.Null(report);
            Assert.Empty(overflow);
            Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
        }
        else
        {
            Assert.NotNull(report);
            Assert.Equal(Supply.Key, report.ItemUsed);
            Assert.Equal(0, state.Pack.Count(Supply.Key));
            Assert.True(state.Map.Room(0).CurioTaken);
        }
    }
}
