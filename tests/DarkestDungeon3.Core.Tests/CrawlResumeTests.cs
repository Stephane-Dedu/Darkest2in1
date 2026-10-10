using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CrawlResumeTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativePlotRoomAndSecretReentryPreservesTheEntireSavedCrawl(bool secret)
    {
        var map = PlotMap.Load(Install, "DD_map4", "darkestdungeon", "kill_boss", 7, Dd1.Props("darkestdungeon"));
        int room = secret ? map.Rooms.Single(r => r.IsSecret).Id : map.Rooms.Last(r => !r.IsSecret).Id;
        var state = new ExpeditionState { Map = map, Started = true, RoomId = room, Seed = 7, RandomCounter = 22, Light = 57, StepsTaken = 19, Party = { "a" } };
        map.Rooms[room].Cleared = true;
        state.SecretReturnCorridorId = 0; state.SecretReturnTileIndex = 17; state.SecretReturnHeadingRoomId = map.Corridors[0].RoomB;
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        var party = new FakeParty("a");
        var crawl = Crawl(loaded, party);
        string before = Json(loaded);
        Assert.True(crawl.CanResume);
        Assert.Equal(room, crawl.Resume()[0].RoomId);
        Assert.Equal(room, crawl.Begin()[0].RoomId);
        Assert.Equal(before, Json(loaded));
        Assert.Empty(party.Log); Assert.Equal(0, party.Stress["a"]);
    }

    [Theory]
    [InlineData(HallContent.Empty, true, null)]
    [InlineData(HallContent.Hunger, true, null)]
    [InlineData(HallContent.Trap, true, null)]
    [InlineData(HallContent.Trap, false, CrawlEventType.Trap)]
    [InlineData(HallContent.Obstacle, false, CrawlEventType.Obstacle)]
    [InlineData(HallContent.Curio, false, CrawlEventType.Curio)]
    [InlineData(HallContent.Battle, false, CrawlEventType.Battle)]
    public void HallReentryPresentsPendingInteractionWithoutMovementOrEffects(HallContent content, bool resolved, CrawlEventType? interaction)
    {
        var state = State();
        state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 0; state.HeadingRoomId = 1;
        var tile = state.Map.Corridors[0].Tiles[0];
        tile.Content = content; tile.ContentId = "heirloom_chest"; tile.Resolved = resolved; tile.Scouted = true;
        var party = new FakeParty("a");
        party.Hp["a"] = 0.5f;
        var crawl = Crawl(state, party);
        string before = Json(state);
        var events = crawl.Resume();
        Assert.Equal(CrawlEventType.EnteredTile, events[0].Type);
        if (interaction is { } type) Assert.Equal(type, events[1].Type);
        else Assert.Single(events);
        Assert.DoesNotContain(events, e => e.Type is CrawlEventType.Ate or CrawlEventType.Starving or CrawlEventType.TrapSprung or CrawlEventType.Scouted or CrawlEventType.Stress);
        Assert.Equal(before, Json(state));
        Assert.Equal(0.5f, party.Hp["a"]); Assert.Equal(0, party.Stress["a"]); Assert.Empty(party.Log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RolledRoomAndHallEncounterSurpriseIsSavedAndReturnedAsACopy(bool hall)
    {
        var state = State();
        state.Started = false;
        if (hall) state.Map.Corridors[0].Tiles[0].Content = HallContent.Battle;
        else state.Map.Rooms[0].Content = RoomContent.Battle;
        var crawl = Crawl(state, new FakeParty("a"));
        var events = crawl.Begin();
        if (hall) events = crawl.Travel(1);
        var original = Assert.Single(events.Where(e => e.Type == CrawlEventType.Battle));
        Assert.NotSame(original, state.PendingEncounter);
        original.HeroesSurprised = !original.HeroesSurprised;
        var saved = state.PendingEncounter;
        var monsters = crawl.FightMonsters(hall ? "hall" : "room").ToArray();
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        string before = Json(loaded);
        crawl = Crawl(loaded, new FakeParty("a"));
        var resumed = Assert.Single(crawl.Resume().Where(e => e.Type == CrawlEventType.Battle));
        Assert.Equal((saved.HeroesSurprised, saved.MonstersSurprised), (resumed.HeroesSurprised, resumed.MonstersSurprised));
        Assert.NotSame(loaded.PendingEncounter, resumed);
        resumed.MonstersSurprised = !resumed.MonstersSurprised;
        Assert.Equal(monsters, crawl.FightMonsters(hall ? "hall" : "room"));
        Assert.Equal(before, Json(loaded));
        crawl.ResolveBattle();
        Assert.Null(loaded.PendingEncounter);
    }

    [Fact]
    public void CampAndCampAmbushResumeWithoutEatingOrRelightingAgain()
    {
        var state = State();
        state.Camp = new CampState { Ate = true, RespiteLeft = 5, AmbushReduction = -1 };
        var party = new FakeParty("a");
        var crawl = Crawl(state, party);
        string before = Json(state);
        Assert.Single(crawl.Resume());
        Assert.Equal(before, Json(state));
        var ambush = Assert.Single(crawl.BreakCamp().Where(e => e.Type == CrawlEventType.Ambush));
        Assert.Equal("camp", ambush.ContentId);
        Assert.Equal(0, state.Light);
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        before = Json(loaded);
        var resumed = Assert.Single(Crawl(loaded, party).Resume().Where(e => e.Type == CrawlEventType.Ambush));
        Assert.Equal(("camp", true, false), (resumed.ContentId, resumed.HeroesSurprised, resumed.MonstersSurprised));
        Assert.Equal(before, Json(loaded));
    }

    [Fact]
    public void SuccessfulBattleFallbackClearsOnlyItsPendingPresentation()
    {
        var state = State();
        state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 1; state.HeadingRoomId = 1;
        state.Map.Corridors[0].Tiles[1].Content = HallContent.Battle;
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Battle, CorridorId = 0, TileIndex = 1 };
        var crawl = Crawl(state, new FakeParty("a"));
        crawl.FleeBattle();
        Assert.Null(state.PendingEncounter);
        Assert.Equal(0, state.TileIndex);
        Assert.False(state.Map.Corridors[0].Tiles[1].Resolved);
        Assert.Single(crawl.Resume());
    }

    [Theory]
    [InlineData("unstarted")]
    [InlineData("ended")]
    [InlineData("no_map")]
    [InlineData("room")]
    [InlineData("corridor")]
    [InlineData("tile")]
    [InlineData("heading")]
    public void InvalidOrTerminalSavedPositionsCannotBeReentered(string invalid)
    {
        var state = State();
        if (invalid == "unstarted") state.Started = false;
        else if (invalid == "ended") state.Ended = true;
        else if (invalid == "no_map") state.Map = null;
        else if (invalid == "room") state.RoomId = 999;
        else
        {
            state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 0; state.HeadingRoomId = 1;
            if (invalid == "corridor") state.CorridorId = 999;
            else if (invalid == "tile") state.TileIndex = 999;
            else state.HeadingRoomId = 999;
        }
        string before = Json(state);
        var crawl = Crawl(state, new FakeParty("a"));
        Assert.False(crawl.CanResume);
        Assert.Equal(CrawlEventType.Blocked, Assert.Single(crawl.Resume()).Type);
        Assert.Equal(before, Json(state));
    }

    private static Crawl Crawl(ExpeditionState state, FakeParty party) => new(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);
    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static ExpeditionState State() => new()
    {
        Seed = 57, RandomCounter = 12, Started = true, Light = 43, StepsTaken = 21, Party = { "a" },
        Quest = new QuestOffer { Dungeon = "crypts", Type = "gather", Difficulty = 1 },
        Map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
            Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1 } } } }
        }
    };
}
