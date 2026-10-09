using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class CrawlInteractionPhaseTests
{
    private static readonly CrawlContent Content = CrawlContent.Load(Dd1Install.Find());

    public static IEnumerable<object[]> Cases =>
        from action in new[] { "curio-room", "curio-hall", "skip-room", "skip-hall", "obstacle", "trap" }
        from pending in new[] { "curio", "empty-curio", "spoils", "camp", "encounter" }
        select new object[] { action, pending };

    [Theory]
    [MemberData(nameof(Cases))]
    public void NewEnvironmentalActionsCannotReplaceOrRunBehindAnActiveEvent(string action, string pending)
    {
        var state = At(action);
        SetPending(state, pending);
        foreach (bool reload in new[] { false, true })
        {
            if (reload) state = SaveFile.FromJson(Json(state)).Expedition;
            var party = new FakeParty("a");
            var crawl = new Crawl(state, new CrawlRules(), party, Content);
            var report = crawl.LastCurio;
            var spoils = crawl.LastSpoils;
            string before = Json(state);
            if (action.StartsWith("curio"))
            {
                Assert.Null(crawl.InteractCurio("a", Supply.Key, out var overflow));
                Assert.Empty(overflow);
            }
            else if (action.StartsWith("skip")) crawl.SkipCurio();
            else if (action == "obstacle") Blocked(crawl.ClearObstacle());
            else Blocked(crawl.DisarmTrap("a"));
            Assert.Equal(before, Json(state));
            Assert.Same(report, crawl.LastCurio);
            Assert.Same(spoils, crawl.LastSpoils);
            Assert.Empty(party.Log);
            Assert.Empty(party.Quirks);
            Assert.Equal(1, party.Hp["a"]);
            Assert.Equal(0, party.Stress["a"]);
        }
    }

    [Theory]
    [InlineData("curio")]
    [InlineData("spoils")]
    public void CollectingAndDismissingOptionalLootAllowsOneNativeChestInteraction(string pending)
    {
        var state = At("curio-room");
        SetPending(state, pending);
        state = SaveFile.FromJson(Json(state)).Expedition;
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"), Content);
        string before = Json(state);
        Assert.Null(crawl.InteractCurio("a", Supply.Key, out _));
        Assert.Equal(before, Json(state));
        var left = crawl.LastCurio?.LeftBehind ?? crawl.LastSpoils.LeftBehind;
        Assert.True(crawl.TakeLeftBehind(left, 0));
        Assert.Equal(50, state.Pack.Count("gold"));
        Assert.Null(crawl.InteractCurio("a", Supply.Key, out _)); // Reading the result still owns input.
        Assert.True(pending == "curio" ? crawl.DismissCurio(crawl.LastCurio) : crawl.DismissSpoils(crawl.LastSpoils));
        var result = crawl.InteractCurio("a", Supply.Key, out _);
        Assert.NotNull(result);
        Assert.Equal(Supply.Key, result.ItemUsed);
        Assert.Equal(0, state.Pack.Count(Supply.Key));
        Assert.True(state.Map.Rooms[0].CurioTaken);
        before = Json(state);
        Assert.Null(crawl.InteractCurio("a", Supply.Key, out _));
        Assert.Equal(before, Json(state));
    }

    [Theory]
    [InlineData("obstacle")]
    [InlineData("trap")]
    public void DismissingAReportRestoresTheEnvironmentalAction(string action)
    {
        var state = At(action);
        SetPending(state, "empty-curio");
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"), Content);
        Assert.True(crawl.DismissCurio(crawl.LastCurio));
        var events = action == "obstacle" ? crawl.ClearObstacle() : crawl.DisarmTrap("a");
        Assert.DoesNotContain(events, e => e.Type == CrawlEventType.Blocked);
        Assert.True(crawl.CurrentTile.Resolved);
    }

    private static void SetPending(ExpeditionState state, string pending)
    {
        var drop = new LootDrop { Type = "gold", Id = "", Amount = 50 };
        if (pending == "curio") state.PendingCurio = new CurioReport { Loot = { drop }, LeftBehind = { drop } };
        if (pending == "empty-curio") state.PendingCurio = new CurioReport();
        if (pending == "spoils") state.PendingSpoils = new BattleSpoils { LeftBehind = { drop } };
        if (pending == "camp") state.Camp = new CampState { RespiteLeft = 12 };
        if (pending == "encounter") state.PendingEncounter = new CrawlEvent
            { Type = CrawlEventType.Ambush, RoomId = state.RoomId, CorridorId = state.CorridorId, TileIndex = state.TileIndex };
    }

    private static ExpeditionState At(string action)
    {
        bool hall = !action.EndsWith("room");
        var state = new ExpeditionState
        {
            Started = true, Seed = 47, RandomCounter = 9, Light = 40, Party = { "a" },
            Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 },
            RoomId = hall ? -1 : 0, CorridorId = hall ? 0 : -1, TileIndex = hall ? 0 : -1,
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = "heirloom_chest" } },
                Corridors = { new Corridor { Id = 0, Tiles = { new HallTile { Index = 0,
                    Content = action == "obstacle" ? HallContent.Obstacle : action == "trap" ? HallContent.Trap : HallContent.Curio,
                    ContentId = action == "trap" || action == "obstacle" ? "crypts" : "heirloom_chest", Scouted = true } } } }
            }
        };
        state.Pack.Add(Supply.Key, 1);
        state.Pack.Add(Supply.Shovel, 1);
        return state;
    }

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static void Blocked(List<CrawlEvent> events) => Assert.Equal(CrawlEventType.Blocked, Assert.Single(events).Type);
}
