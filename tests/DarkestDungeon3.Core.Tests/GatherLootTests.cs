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
}
