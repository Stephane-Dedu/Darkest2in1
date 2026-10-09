using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class CrawlNavigationTests
{
    private static readonly CrawlContent Content = CrawlContent.Load(Dd1Install.Find());

    public static IEnumerable<object[]> PendingLocations =>
        from location in new[] { "room", "hall", "secret" }
        from pending in new[] { "curio", "required-curio", "spoils", "required-spoils", "camp", "encounter" }
        select new object[] { location, pending };

    [Theory]
    [MemberData(nameof(PendingLocations))]
    public void ActiveEventsRejectNavigationWithoutChangingSavedState(string location, string pending)
    {
        var state = At(location);
        var drops = new List<LootDrop> { new() { Type = pending.StartsWith("required-") ? "quest_item" : "gold",
            Id = pending.StartsWith("required-") ? "holy_relic" : "", Amount = 1 } };
        if (pending.EndsWith("curio")) state.PendingCurio = new CurioReport { LeftBehind = drops };
        if (pending.EndsWith("spoils")) state.PendingSpoils = new BattleSpoils { LeftBehind = drops };
        if (pending == "camp") state.Camp = new CampState { RespiteLeft = 12 };
        if (pending == "encounter") state.PendingEncounter = new CrawlEvent
        {
            Type = CrawlEventType.Ambush, RoomId = state.RoomId, CorridorId = state.CorridorId,
            TileIndex = state.TileIndex, HeroesSurprised = true, ContentId = "camp"
        };

        foreach (bool reload in new[] { false, true })
        {
            if (reload) state = SaveFile.FromJson(Json(state)).Expedition;
            var party = new FakeParty("a", "b");
            var crawl = new Crawl(state, new CrawlRules(), party, Content);
            string before = Json(state);
            Assert.False(crawl.IsBlocked); // The event, rather than destination contents, owns traversal.
            Blocked(crawl.Travel(1));
            Blocked(crawl.Step(true));
            Blocked(crawl.Step(false));
            Assert.False(crawl.CanEnterSecretRoom);
            Blocked(crawl.EnterSecretRoom());
            Blocked(crawl.ExitSecretRoom());
            Assert.False(crawl.CanCamp);
            Blocked(crawl.MakeCamp());
            Assert.Equal(before, Json(state));
            Assert.Empty(party.Log);
            Assert.All(party.Hp.Values, hp => Assert.Equal(1, hp));
            Assert.All(party.Stress.Values, stress => Assert.Equal(0, stress));
        }
    }

    [Theory]
    [InlineData("room")]
    [InlineData("hall")]
    [InlineData("secret")]
    public void DismissingTheCurrentCurioReleasesTheSameSavedLocation(string location)
    {
        var state = At(location);
        state.PendingCurio = new CurioReport();
        state.QuestComplete = true; // Completion still allows continued exploration.
        state = SaveFile.FromJson(Json(state)).Expedition;
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a", "b"), Content);
        Assert.False(crawl.DismissCurio(new CurioReport()));
        Assert.True(crawl.DismissCurio(crawl.LastCurio));
        if (location == "room") Assert.Contains(crawl.Travel(1), e => e.Type == CrawlEventType.EnteredTile);
        if (location == "hall") Assert.Contains(crawl.EnterSecretRoom(), e => e.Type == CrawlEventType.EnteredRoom);
        if (location == "secret") Assert.Contains(crawl.ExitSecretRoom(), e => e.Type == CrawlEventType.EnteredTile);
        Assert.False(state.Ended);
    }

    [Fact]
    public void CampAmbushAndItsLootMustFinishBeforeLeavingTheClearedRoom()
    {
        var state = At("room");
        var rules = new CrawlRules { AmbushCampChance = 1 };
        var crawl = new Crawl(state, rules, new FakeParty("a", "b"), Content);
        crawl.MakeCamp();
        Assert.NotNull(state.Camp);
        Blocked(crawl.Travel(1));
        Assert.Contains(crawl.BreakCamp(), e => e.Type == CrawlEventType.Ambush);
        Assert.Null(state.Camp);
        Assert.NotNull(state.PendingEncounter);
        state = SaveFile.FromJson(Json(state)).Expedition;
        crawl = new Crawl(state, rules, new FakeParty("a", "b"), Content);
        Blocked(crawl.Travel(1));
        Blocked(crawl.MakeCamp());
        crawl.ResolveBattle();
        Assert.Null(state.PendingEncounter);
        Assert.NotNull(crawl.LastSpoils);
        Blocked(crawl.Travel(1));
        Assert.True(crawl.DismissSpoils(crawl.LastSpoils));
        Assert.Contains(crawl.Travel(1), e => e.Type == CrawlEventType.EnteredTile);
    }

    [Theory]
    [InlineData(HallContent.Obstacle)]
    [InlineData(HallContent.Trap)]
    public void AnUnopenedObstacleOrSpottedTrapStillAllowsBackingAway(HallContent content)
    {
        var state = At("hall");
        state.Map.Corridors[0].Tiles[1].Content = content;
        state.Map.Corridors[0].Tiles[1].Scouted = true;
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a", "b"), Content);
        Assert.True(crawl.IsBlocked);
        Assert.Contains(crawl.Step(false), e => e.Type == CrawlEventType.EnteredTile);
        Assert.Equal(0, state.TileIndex);
        Assert.False(state.Map.Corridors[0].Tiles[1].Resolved);
    }

    private static ExpeditionState At(string location)
    {
        var state = new ExpeditionState
        {
            Started = true, Seed = 91, RandomCounter = 17, Light = 40, Party = { "a", "b" },
            Quest = new QuestOffer { Dungeon = "dd2_city", Difficulty = 1, Type = "explore" },
            RoomId = location == "room" ? 0 : location == "secret" ? 2 : -1,
            CorridorId = location == "hall" ? 0 : -1, TileIndex = location == "hall" ? 1 : -1,
            HeadingRoomId = location == "hall" ? 1 : -1,
            SecretReturnCorridorId = 0, SecretReturnTileIndex = 1, SecretReturnHeadingRoomId = 1,
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, Cleared = true, CorridorIds = { 0 } },
                    new Room { Id = 1, Cleared = true, CorridorIds = { 0 } },
                    new Room { Id = 2, IsSecret = true, Scouted = true, Cleared = true } },
                Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles =
                    { new HallTile { Index = 0 }, new HallTile { Index = 1, SecretRoomId = 2 }, new HallTile { Index = 2 } } } }
            }
        };
        state.Pack.Add(Supply.Firewood, 2);
        return state;
    }

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static void Blocked(List<CrawlEvent> events) => Assert.Equal(CrawlEventType.Blocked, Assert.Single(events).Type);
}
