using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class FightCheckpointTests
{
    [Theory]
    [InlineData(FightKind.Room)]
    [InlineData(FightKind.Hall)]
    [InlineData(FightKind.CampAmbush)]
    public void InterruptedFightRestartsPreFightPartyAndExactResolvedPlanWithoutRng(FightKind kind)
    {
        var state = State();
        if (kind == FightKind.Hall) { state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 1; state.HeadingRoomId = 1; }
        var plan = new FightPlan { Kind = kind, Battle = "table:original", NativePresentation = true, Enemies = new() { "enemy_a", "enemy_b" } };
        Assert.True(ExpeditionFight.Record(state, plan, "chosen_native_battle", "chosen_arena", true, false));
        plan.Enemies.Clear(); state.PartyStates["a"].Hp = 1; state.PartyStates["a"].Stress = 10;
        state.PartyStates["a"].Outcome.Died = true; state.PartyStates["a"].Outcome.Quirks.Clear(); state.Light = 0;
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        var saved = ExpeditionFight.Current(loaded);
        Assert.NotNull(saved); Assert.Equal(12, saved.PartyStates["a"].Hp);
        Assert.True(ExpeditionFight.RestoreParty(loaded));
        Assert.Equal(12, loaded.PartyStates["a"].Hp); Assert.Equal(4.5f, loaded.PartyStates["a"].Stress);
        Assert.False(loaded.PartyStates["a"].Outcome.Died); Assert.Equal(new[] { "quirk" }, loaded.PartyStates["a"].Outcome.Quirks);
        Assert.True(loaded.PartyStates["b"].Outcome.Died); // a death from before this fight stays dead
        Assert.Equal(51, loaded.Light); Assert.Equal(27, loaded.RandomCounter);
        var restoredPlan = saved.Plan();
        Assert.False(restoredPlan.IsTable); Assert.Equal("chosen_native_battle", restoredPlan.BattleId);
        Assert.Equal(new[] { "chosen_arena" }, restoredPlan.Arenas);
        Assert.Equal(new[] { "enemy_a", "enemy_b" }, restoredPlan.Enemies); Assert.True(restoredPlan.NativePresentation);
        restoredPlan.Enemies.Clear(); restoredPlan.Arenas.Clear();
        string before = Json(loaded);
        var crawl = new Crawl(loaded, new CrawlRules(), new FakeParty("a"));
        var events = crawl.Resume();
        var fight = Assert.Single(events.Where(e => e.Type is CrawlEventType.Battle or CrawlEventType.Ambush));
        Assert.Equal(kind == FightKind.CampAmbush ? CrawlEventType.Ambush : CrawlEventType.Battle, fight.Type);
        Assert.Equal(kind == FightKind.CampAmbush ? "camp" : null, fight.ContentId);
        Assert.True(fight.HeroesSurprised); Assert.False(fight.MonstersSurprised);
        Assert.Equal(before, Json(loaded));
        loaded.PartyStates["a"].Outcome.Quirks.Clear();
        Assert.Single(saved.PartyStates["a"].Outcome.Quirks);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("missing-party")]
    [InlineData("foreign")]
    [InlineData("invalid-condition")]
    [InlineData("ended")]
    [InlineData("missing-battle")]
    [InlineData("invalid-light")]
    public void InvalidCheckpointsCannotChangeCurrentPartyOrPresentation(string invalid)
    {
        var state = State();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "battle", null, false, true));
        if (invalid == "stale") state.FightCheckpoint.TileIndex++;
        else if (invalid == "missing-party") state.FightCheckpoint.PartyStates.Remove("a");
        else if (invalid == "foreign") state.FightCheckpoint.PartyStates["a"].Outcome.HeroId = "other";
        else if (invalid == "invalid-condition") state.FightCheckpoint.PartyStates["a"].HpMax = 0;
        else if (invalid == "ended") state.Ended = true;
        else if (invalid == "missing-battle") state.FightCheckpoint.BattleId = null;
        else state.FightCheckpoint.Light = 101;
        string before = Json(state);
        Assert.Null(ExpeditionFight.Current(state)); Assert.False(ExpeditionFight.RestoreParty(state));
        Assert.Null(ExpeditionFight.Presentation(state)); Assert.Equal(before, Json(state));
    }

    [Fact]
    public void RejectedRecordLeavesExistingCheckpointAndLegacyStateAlone()
    {
        var state = State();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "first", null, false, false));
        state.PartyStates.Remove("a");
        string before = Json(state);
        Assert.False(ExpeditionFight.Record(state, new FightPlan(), "later", null, true, true));
        Assert.Equal(before, Json(state));
        var legacy = SaveFile.FromJson("{\"Expedition\":{\"Started\":true}}").Expedition;
        Assert.Null(ExpeditionFight.Current(legacy)); Assert.False(ExpeditionFight.RestoreParty(legacy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WinOrSuccessfulFallbackClearsCheckpointButFailedFallbackKeepsIt(bool retreat)
    {
        var state = State(); state.Map.Rooms[0].Content = RoomContent.Battle;
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "first", null, false, false));
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"));
        if (retreat)
        {
            crawl.FleeBattle(); Assert.NotNull(state.FightCheckpoint); // no prior corridor at the entrance
            state.CameFromCorridorId = 0; state.CameFromTileIndex = 0;
            crawl.FleeBattle();
        }
        else crawl.ResolveBattle();
        Assert.Null(state.FightCheckpoint);
    }

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static ExpeditionState State() => new()
    {
        Started = true, Light = 51, RandomCounter = 27, Party = { "a", "b" },
        Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1 },
        PartyStates =
        {
            ["a"] = new ExpeditionHeroState { Hp = 12, HpMax = 30, Stress = 4.5f, WoundPercent = .1f,
                Outcome = new HeroOutcome { HeroId = "a", Quirks = new() { "quirk" }, Trinkets = new() { "ring" } } },
            ["b"] = new ExpeditionHeroState { HpMax = 30, Outcome = new HeroOutcome { HeroId = "b", Died = true } }
        },
        Map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
            Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1 } } } }
        }
    };
}
