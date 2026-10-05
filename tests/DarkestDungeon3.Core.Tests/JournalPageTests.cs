using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class JournalPageTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Fact]
    public void ThanksTableReturnsNativePageZeroWithoutMoneyOrAnExtraRandomDraw()
    {
        var rng = new Rng(4);
        var drop = Assert.Single(Content.Loot.Roll("THANKS", 1, 6, "darkestdungeon", rng));
        Assert.Equal(("journal_page", "0", 1, "journal_page+0"), (drop.Type, drop.Id, drop.Amount, drop.Key));
        var expected = new Rng(4); expected.NextDouble();
        Assert.Equal(expected.NextULong(), rng.NextULong());
        Assert.Equal(1, Content.Items.StackLimit(drop.Key));
    }

    [Fact]
    public void RandomPagesUseTheNativeInclusiveRangeAndStableSequence()
    {
        var first = Content.Loot.Roll("JOURNALONLY", 1000, 1, "dd2_city", new Rng(31));
        var second = Content.Loot.Roll("JOURNALONLY", 1000, 1, "dd2_city", new Rng(31));
        Assert.All(first, d => { Assert.Equal("journal_page", d.Type); Assert.InRange(int.Parse(d.Id), 1, 21); });
        Assert.Equal(1000, first.Sum(d => d.Amount));
        Assert.Contains(first, d => d.Id == "1"); Assert.Contains(first, d => d.Id == "21");
        Assert.Equal(first.Select(d => (d.Key, d.Amount)), second.Select(d => (d.Key, d.Amount)));
    }

    [Fact]
    public void ThanksChestRewardRespectsFullPackOverflowAndSavedSingleUse()
    {
        var map = PlotMap.Load(Install, "DD_map4", "darkestdungeon", "kill_boss", 7, Dd1.Props("darkestdungeon"));
        var secret = Assert.Single(map.Rooms.Where(r => r.IsSecret));
        var state = new ExpeditionState { Map = map, RoomId = secret.Id, Quest = new QuestOffer { Dungeon = "darkestdungeon", Difficulty = 6 }, Party = { "a" } };
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a"), Content);
        var report = crawl.InteractCurio("a", null, out var overflow);
        Assert.Equal("THANKS", CurioLibrary.Load(Install).Get("thanks_chest").Outcomes.Single().Results.Single().Name);
        Assert.Equal("journal_page+0", Assert.Single(report.Loot).Key);
        Assert.Equal("journal_page+0", Assert.Single(overflow).Key);
        Assert.Equal(0, state.Pack.Count(JournalPages.Key(0)));
        Assert.False(crawl.TakeLeftBehind(overflow, 0));
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(overflow, 0));
        Assert.Empty(overflow);
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        Assert.Equal(1, loaded.Pack.Count(JournalPages.Key(0)));
        var resumed = new Crawl(loaded, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a"), Content);
        Assert.Null(resumed.InteractCurio("a", null, out var again));
        Assert.Empty(again);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void HomecomingCollectsOnlyCarriedSurvivingPages(bool complete, bool retreat, bool survives)
    {
        var estate = new Estate();
        estate.Roster.Add(new HeroRecord { Id = "a", ClassId = "highwayman" });
        estate.CollectedJournalPages.Add(7);
        var state = new ExpeditionState { Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1, Length = 1 }, QuestComplete = complete, Retreated = retreat };
        state.Pack.Add(JournalPages.Key(0), 2); state.Pack.Add(JournalPages.Key(7), 1);
        state.Pack.Add("0", 1); state.Pack.Add("journal_page+-1", 1); state.Pack.Add("journal_page+bad", 1);
        var report = Homecoming.Report(estate, Dd1, state, new[] { new HeroOutcome { HeroId = "a", Died = !survives } });
        Assert.Equal(survives, estate.CollectedJournalPages.Contains(0));
        Assert.Contains(7, estate.CollectedJournalPages);
        Assert.Equal(survives ? new[] { 0 } : new int[0], report.JournalPages);
        Assert.Equal(0, estate.Get(Currency.Gold));
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.Equal(estate.CollectedJournalPages.OrderBy(p => p), loaded.CollectedJournalPages.OrderBy(p => p));
        Assert.Empty(JournalPages.Collect(loaded, state.Pack, survived: survives));
    }

    [Fact]
    public void NoRecordedSurvivorCannotInventARecoveredPage()
    {
        var estate = new Estate();
        var state = new ExpeditionState { Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore" } };
        state.Pack.Add(JournalPages.Key(0), 1);
        var report = Homecoming.Report(estate, Dd1, state, new HeroOutcome[0]);
        Assert.Empty(report.JournalPages);
        Assert.Empty(estate.CollectedJournalPages);
    }

    [Fact]
    public void MissingDiscardedPagesAndLegacyEstatesInventNoCollection()
    {
        var estate = SaveFile.FromJson("{\"Estate\":{\"RandomCounter\":17}}").Estate;
        Assert.Empty(estate.CollectedJournalPages);
        var pack = new Inventory(); pack.Add(JournalPages.Key(0), 1); pack.TryUse(JournalPages.Key(0));
        Assert.Empty(JournalPages.Collect(estate, pack, survived: true));
        Assert.Equal(17, estate.RandomCounter);
        Assert.False(JournalPages.TryPage("0", out _));
        Assert.False(JournalPages.TryPage("journal_page+-1", out _));
        Assert.True(JournalPages.TryPage("journal_page+0", out int page)); Assert.Equal(0, page);
    }
}
