using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionExitTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetreatCannotDiscardRequiredLootFromEitherReport(bool curio)
    {
        var drop = new LootDrop { Type = "quest_item", Id = "holy_relic", Amount = 1 };
        var state = new ExpeditionState { Started = true, Quest = new QuestOffer() };
        if (curio) state.PendingCurio = new CurioReport { Loot = { drop }, LeftBehind = { drop } };
        else state.PendingSpoils = new BattleSpoils { LeftBehind = { drop } };
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty());
        string before = new SaveFile { Expedition = state }.ToJson();
        crawl.Retreat();
        Assert.False(state.Ended);
        Assert.False(state.Retreated);
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
    }

    [Fact]
    public void NativeFinalDarkestDungeonQuestCannotBeAbandonedThroughCore()
    {
        var native = Assert.Single(Dd1.Goals.Plot, q => q.Id == "plot_darkest_dungeon_4");
        Assert.False(native.CanRetreat);
        var state = new ExpeditionState { Started = true, Quest = new QuestOffer { CanRetreat = native.CanRetreat } };
        new Crawl(state, new CrawlRules(), new FakeParty()).Retreat();
        Assert.False(state.Ended);
        Assert.False(state.Retreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitReturnWaitsForRequiredLootThenEndsOnceWithTheCorrectOutcome(bool complete, bool curio)
    {
        var content = CrawlContent.Load(Dd1Install.Find());
        var drop = new LootDrop { Type = "quest_item", Id = "holy_relic", Amount = 1 };
        var state = new ExpeditionState { Started = true, QuestComplete = complete, Quest = new QuestOffer { CanRetreat = true }, RandomCounter = 47 };
        if (curio) state.PendingCurio = new CurioReport { Loot = { drop }, LeftBehind = { drop } };
        else state.PendingSpoils = new BattleSpoils { LeftBehind = { drop } };
        // Required-loot guards survive a save round trip, not just reference identity.
        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty(), content);
        string before = new SaveFile { Expedition = state }.ToJson();
        Assert.False(crawl.CanLeaveExpedition);
        Assert.False(crawl.TryLeave());
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
        var left = curio ? state.PendingCurio.LeftBehind : state.PendingSpoils.LeftBehind;
        Assert.True(crawl.TakeLeftBehind(left, 0));
        Assert.True(crawl.CanLeaveExpedition);
        Assert.True(crawl.TryLeave());
        Assert.True(state.Ended);
        Assert.Equal(!complete, state.Retreated);
        Assert.Equal(complete, state.QuestComplete);
        Assert.Equal(47, state.RandomCounter);
        Assert.Equal(1, state.Pack.Count(drop.Key));
        string ended = new SaveFile { Expedition = state }.ToJson();
        Assert.False(crawl.TryLeave());
        crawl.Retreat();
        Assert.Equal(ended, new SaveFile { Expedition = state }.ToJson());
    }

    [Fact]
    public void ForbiddenQuestCanReturnAfterCompletionButCannotBeAbandonedOrMarkedRetreated()
    {
        var state = new ExpeditionState { Started = true, Quest = new QuestOffer { CanRetreat = false } };
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty());
        Assert.False(crawl.CanLeaveExpedition);
        Assert.False(crawl.TryLeave());
        state.QuestComplete = true;
        crawl.Retreat();
        Assert.False(state.Ended); // direct retreat is still forbidden
        Assert.True(crawl.CanLeaveExpedition);
        Assert.True(crawl.TryLeave());
        Assert.True(state.Ended);
        Assert.False(state.Retreated);
    }

    [Fact]
    public void OptionalLootCanBeLeftBehindAndMissingLegacyQuestRetainsPermittedRetreat()
    {
        var state = new ExpeditionState
        {
            PendingCurio = new CurioReport { LeftBehind = { new LootDrop { Type = "gold", Amount = 100 } } },
            PendingSpoils = new BattleSpoils { LeftBehind = { new LootDrop { Type = "heirloom", Id = "portrait", Amount = 2 } } },
        };
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty());
        Assert.True(crawl.TryLeave());
        Assert.True(state.Retreated);
        Assert.True(state.Ended);
        Assert.Empty(state.Pack.Items);
    }
}
