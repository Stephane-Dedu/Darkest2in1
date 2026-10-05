using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SavedSpoilsTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeBattleAndCampReportsSurviveRepeatedReloadWithoutAnotherRoll(bool camp)
    {
        var state = new ExpeditionState
        {
            Seed = 17, Party = { "a" }, Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1, Type = "explore" },
            Map = new DungeonMap { Rooms = { new Room { Id = 0, Content = RoomContent.Battle } } }
        };
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var crawl = Crawl(state);
        crawl.TrinketOfRarity = (_, _) => "resolved_ring";
        if (camp)
        {
            state.Camp = new CampState { Ate = true, RespiteLeft = 12 };
            state.CampSkills["a"] = new() { "pilfer" };
            Assert.True(crawl.UseCampSkill("a", "pilfer"));
        }
        else
        {
            crawl.FightMonsters("room");
            crawl.ResolveBattle();
        }
        Assert.Same(state.PendingSpoils, crawl.LastSpoils);
        Assert.NotEmpty(crawl.LastSpoils.LeftBehind);
        Assert.Empty(crawl.LastSpoils.Taken);
        var expected = crawl.LastSpoils.LeftBehind.Select(d => (d.Type, d.Id, d.Amount)).ToArray();
        string json = Json(state);
        for (int i = 0; i < 3; i++)
        {
            state = SaveFile.FromJson(json).Expedition;
            crawl = Crawl(state);
            int picks = 0;
            crawl.TrinketOfRarity = (_, _) => { picks++; return "rerolled_ring"; };
            Assert.Equal(camp ? "camp" : "room", crawl.LastSpoils.Kind);
            Assert.Equal(expected, crawl.LastSpoils.LeftBehind.Select(d => (d.Type, d.Id, d.Amount)));
            Assert.Equal(camp, state.Camp != null);
            Assert.Equal(json, Json(state));
            Assert.Equal(0, picks);
        }
    }

    [Fact]
    public void PartialPickupAndDismissalRemainExactAcrossReload()
    {
        var state = new ExpeditionState { Seed = 29, RandomCounter = 17, PendingSpoils = new BattleSpoils { Kind = "camp" } };
        state.Pack.Add("gold", 1700);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        state.PendingSpoils.LeftBehind.Add(new LootDrop { Type = "gold", Id = "", Amount = 200 });
        var crawl = Crawl(state);
        Assert.True(crawl.TakeLeftBehind(crawl.LastSpoils.LeftBehind, 0, crawl.LastSpoils.Taken));
        state = SaveFile.FromJson(Json(state)).Expedition;
        crawl = Crawl(state);
        Assert.Equal(50, Assert.Single(crawl.LastSpoils.Taken).Amount);
        Assert.Equal(150, Assert.Single(crawl.LastSpoils.LeftBehind).Amount);
        Assert.Equal(1750, state.Pack.Count("gold"));
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(crawl.LastSpoils.LeftBehind, 0, crawl.LastSpoils.Taken));
        Assert.Equal(200, crawl.LastSpoils.Taken.Sum(d => d.Amount));
        Assert.Equal(1900, state.Pack.Count("gold"));
        Assert.True(crawl.DismissSpoils(crawl.LastSpoils));
        Assert.Null(crawl.LastSpoils);
        state = SaveFile.FromJson(Json(state)).Expedition;
        Assert.Null(Crawl(state).LastSpoils);
        Assert.Equal(1900, state.Pack.Count("gold"));
        Assert.Equal(17, state.RandomCounter);
    }

    [Fact]
    public void PassingNormalRemaindersNeverAwardsThemOrReappearsAfterReload()
    {
        var state = new ExpeditionState { PendingSpoils = new BattleSpoils { Kind = "room" }, RandomCounter = 21 };
        state.PendingSpoils.LeftBehind.Add(new LootDrop { Type = "trinket", Id = "resolved_ring", Amount = 1 });
        var crawl = Crawl(state);
        var report = crawl.LastSpoils;
        Assert.True(crawl.DismissSpoils(report));
        Assert.False(crawl.DismissSpoils(report));
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        Assert.Null(Crawl(loaded).LastSpoils);
        Assert.Empty(loaded.Pack.Items);
        Assert.Equal(21, loaded.RandomCounter);
    }

    [Fact]
    public void QuestRemainderStaleReportAndEndedCrawlCannotBeDismissed()
    {
        var state = new ExpeditionState { PendingSpoils = new BattleSpoils { Kind = "camp" } };
        state.PendingSpoils.LeftBehind.Add(new LootDrop { Type = "quest_item", Id = "holy_water", Amount = 1 });
        var crawl = Crawl(state);
        var report = crawl.LastSpoils;
        string before = Json(state);
        Assert.False(crawl.DismissSpoils(null));
        Assert.False(crawl.DismissSpoils(new BattleSpoils()));
        Assert.False(crawl.DismissSpoils(report));
        Assert.Equal(before, Json(state));
        Assert.True(crawl.TakeLeftBehind(report.LeftBehind, 0, report.Taken));
        state.Ended = true;
        before = Json(state);
        Assert.False(crawl.DismissSpoils(report));
        Assert.Equal(before, Json(state));
    }

    [Fact]
    public void LegacySavesInventNoLootReport()
    {
        var state = SaveFile.FromJson("{\"Expedition\":{\"RandomCounter\":31}}").Expedition;
        string before = Json(state);
        Assert.Null(Crawl(state).LastSpoils);
        Assert.Equal(before, Json(state));
        Assert.DoesNotContain("PendingSpoils", before);
    }

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static Crawl Crawl(ExpeditionState state) => new(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a"), Content);
}
