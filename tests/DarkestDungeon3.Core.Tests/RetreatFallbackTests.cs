using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Where a party that flees a fight in a room ends up, and what is left waiting behind it.</summary>
public class RetreatFallbackTests
{
    [Fact]
    public void FledNightRaidersAreGoneWhenThereIsNoWayBack()
    {
        // A camp in the entrance room: no hall square was ever stepped off into it.
        var state = State(room: 0);
        var crawl = Ambushed(state);

        var events = crawl.FleeBattle();

        Assert.Contains(events, e => e.Type == CrawlEventType.Retreated);
        Assert.True(state.InRoom);
        Assert.Equal(0, state.RoomId);
        Assert.Null(state.PendingEncounter);
        Assert.Null(state.FightCheckpoint);
        Assert.True(crawl.CanNavigate);
        Assert.True(crawl.CanLeaveExpedition);
    }

    [Fact]
    public void ASavedEntrySquareThatDoesNotLeadHereNeverMovesTheParty()
    {
        // Corridor 0 joins rooms 0 and 1; the party camps in room 2.
        var state = State(room: 2);
        state.CameFromCorridorId = 0; state.CameFromTileIndex = 0;
        var crawl = Ambushed(state);

        crawl.FleeBattle();

        Assert.Equal(2, state.RoomId);
        Assert.Equal(-1, state.CorridorId);
        Assert.Null(state.PendingEncounter);
    }

    [Fact]
    public void OutOfRangeEntrySquaresAreRefusedInsteadOfThrowing()
    {
        var state = State(room: 1);
        state.CameFromCorridorId = 0; state.CameFromTileIndex = 9;
        var crawl = Ambushed(state);

        crawl.FleeBattle();

        Assert.Equal(1, state.RoomId);
        Assert.Null(state.PendingEncounter);
    }

    [Fact]
    public void ASecretRoomIsFledThroughItsOwnDoor()
    {
        var state = State(room: 3);
        state.CameFromCorridorId = 1; state.CameFromTileIndex = 0;   // an older door, nowhere near the secret room
        state.SecretReturnCorridorId = 0; state.SecretReturnTileIndex = 1; state.SecretReturnHeadingRoomId = 1;
        var crawl = Ambushed(state);

        var events = crawl.FleeBattle();

        Assert.Contains(events, e => e.Type == CrawlEventType.Retreated);
        Assert.False(state.InRoom);
        Assert.Equal(0, state.CorridorId);
        Assert.Equal(1, state.TileIndex);
        Assert.Equal(1, state.HeadingRoomId);
        Assert.Equal(-1, state.SecretReturnCorridorId);
        Assert.Null(state.PendingEncounter);
    }

    [Fact]
    public void TheWayThePartyCameStillWinsWhenItLeadsHere()
    {
        var state = State(room: 1);
        state.CameFromCorridorId = 1; state.CameFromTileIndex = 0;
        var crawl = Ambushed(state);

        crawl.FleeBattle();

        Assert.False(state.InRoom);
        Assert.Equal(1, state.CorridorId);
        Assert.Equal(0, state.TileIndex);
        Assert.Equal(2, state.HeadingRoomId);   // facing away from the room it fled
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AWonFightAnnouncesAGuardedCurioOnlyWhileItIsUntouched(bool taken, bool announced)
    {
        var state = State(room: 1);
        var room = state.Map.Rooms[1];
        room.Content = RoomContent.GuardedCurio; room.CurioId = "sack"; room.Cleared = true; room.CurioTaken = taken;
        var crawl = Ambushed(state);

        var events = crawl.ResolveBattle();

        Assert.Equal(announced, events.Any(e => e.Type == CrawlEventType.Curio));
    }

    private static Crawl Ambushed(ExpeditionState state)
    {
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = state.RoomId, HeroesSurprised = true, ContentId = "camp" };
        Assert.True(ExpeditionFight.Record(state, new FightPlan { Kind = FightKind.CampAmbush }, "raiders", null, true, false));
        return new Crawl(state, new CrawlRules(), new FakeParty("a"));
    }

    /// <summary>Rooms 0-1-2 in a line (corridors 0 and 1, two squares each) and secret room 3 behind corridor 0's
    /// second square.</summary>
    private static ExpeditionState State(int room) => new()
    {
        Started = true, Party = { "a" }, RoomId = room,
        Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1 },
        PartyStates = { ["a"] = new ExpeditionHeroState { Hp = 20, HpMax = 30, Outcome = new HeroOutcome { HeroId = "a" } } },
        Map = new DungeonMap
        {
            Rooms =
            {
                new Room { Id = 0, Visited = true, CorridorIds = { 0 } },
                new Room { Id = 1, Visited = true, CorridorIds = { 0, 1 } },
                new Room { Id = 2, Visited = true, CorridorIds = { 1 } },
                new Room { Id = 3, Visited = true, IsSecret = true },
            },
            Corridors =
            {
                new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1, SecretRoomId = 3 } } },
                new Corridor { Id = 1, RoomA = 1, RoomB = 2, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1 } } },
            }
        }
    };
}
