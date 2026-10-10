using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class PartialLootTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData("gold", "", 1750, 50, 200)]
    [InlineData("heirloom", "portrait", 3, 1, 2)]
    [InlineData("supply", "torch", 8, 1, 3)]
    public void MatchingStackTakesOnlyWhatFitsAndKeepsTheExactRemainder(string type, string id, int limit, int room, int amount)
    {
        var state = new ExpeditionState { Seed = 71, RandomCounter = 19 };
        var drop = new LootDrop { Type = type, Id = id, Amount = amount };
        Assert.Equal(limit, Content.Items.StackLimit(drop.Key));
        state.Pack.Add(drop.Key, limit - room);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var waiting = new List<LootDrop> { drop };
        var taken = new List<LootDrop>();
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty(), Content);
        Assert.True(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.Same(drop, Assert.Single(waiting));
        Assert.Equal(amount - room, drop.Amount);
        Assert.Equal(room, Assert.Single(taken).Amount);
        Assert.NotSame(drop, taken[0]);
        Assert.Equal(limit, state.Pack.Count(drop.Key));
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        Assert.False(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.Equal(amount, taken.Sum(d => d.Amount) + waiting.Sum(d => d.Amount));

        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        crawl = new Crawl(state, new CrawlRules(), new FakeParty(), Content);
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.Empty(waiting);
        Assert.Same(drop, taken[1]);
        Assert.Equal(room, taken[0].Amount);
        Assert.Equal(amount, taken.Sum(d => d.Amount));
        Assert.Equal(limit + amount - room, state.Pack.Count(drop.Key));
        Assert.Equal(19, state.RandomCounter);
    }

    [Fact]
    public void EmptySlotsTakeMultipleStacksWithoutChangingTheSourceDrop()
    {
        var pack = new Inventory();
        for (int i = 0; i < Inventory.Slots - 2; i++) pack.Add("filler" + i, 1);
        var drop = new LootDrop { Type = "gold", Id = "", Amount = 5000 };
        Assert.Equal(3500, pack.TakePartial(drop, Content.Items));
        Assert.Equal(5000, drop.Amount);
        Assert.Equal(3500, pack.Count("gold"));
        Assert.Equal(Inventory.Slots, pack.SlotsUsed(Content.Items));
        Assert.Equal(0, pack.TakePartial(drop, Content.Items));
    }

    [Theory]
    [InlineData("trinket", "concrete_ring")]
    [InlineData("quest_item", "holy_water")]
    public void SingleSlotItemsRetainOneCopyAndQuestRemaindersCannotBePassed(string type, string id)
    {
        var state = new ExpeditionState { RandomCounter = 23 };
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var drop = new LootDrop { Type = type, Id = id, Amount = 2 };
        var waiting = new List<LootDrop> { drop };
        var taken = new List<LootDrop>();
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty(), Content);
        int picks = 0;
        crawl.TrinketOfRarity = (_, _) => { picks++; return "other_ring"; };
        Assert.True(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.Equal(1, Assert.Single(waiting).Amount);
        Assert.Equal(1, Assert.Single(taken).Amount);
        Assert.Equal(id, taken[0].Id);
        Assert.Equal(type != "quest_item", Crawl.CanLeave(waiting));
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.True(Crawl.CanLeave(waiting));
        Assert.Equal(2, state.Pack.Count(drop.Key));
        Assert.Equal(0, picks);
        Assert.Equal(23, state.RandomCounter);
    }

    [Fact]
    public void NoRoomInvalidAmountOrInvalidIndexCannotCreateAReportedPickup()
    {
        var state = new ExpeditionState();
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var waiting = new List<LootDrop> { new() { Type = "gold", Id = "", Amount = 250 } };
        var taken = new List<LootDrop>();
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty(), Content);
        string before = new SaveFile { Expedition = state }.ToJson();
        Assert.False(crawl.TakeLeftBehind(waiting, 0, taken));
        Assert.False(crawl.TakeLeftBehind(waiting, -1, taken));
        Assert.False(crawl.TakeLeftBehind(waiting, 1, taken));
        Assert.False(crawl.TakeLeftBehind(null, 0, taken));
        Assert.Equal(0, state.Pack.TakePartial(null, Content.Items));
        Assert.Equal(0, state.Pack.TakePartial(new LootDrop { Type = "gold", Id = "", Amount = 0 }, Content.Items));
        Assert.Equal(0, state.Pack.TakePartial(new LootDrop { Type = "gold", Id = "", Amount = -3 }, Content.Items));
        Assert.Empty(taken); Assert.Equal(250, Assert.Single(waiting).Amount);
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
    }
}
