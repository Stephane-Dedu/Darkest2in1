using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SecretBranchTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlRules Rules = CrawlRules.FromDd1(Dd1.Rules);

    private static ExpeditionState AtDoor(bool towardBoss = true)
    {
        var map = PlotMap.Load(Install, "DD_map4", "darkestdungeon", "kill_boss", 7, Dd1.Props("darkestdungeon"));
        var corridor = Assert.Single(map.Corridors);
        var door = Assert.Single(corridor.Tiles.Where(t => t.SecretRoomId >= 0));
        door.Visited = door.Resolved = true;
        return new ExpeditionState
        {
            Map = map, Quest = new QuestOffer { Type = "kill_boss", ScoutingEnabled = false }, RoomId = -1,
            CorridorId = corridor.Id, TileIndex = door.Index, HeadingRoomId = towardBoss ? corridor.RoomB : corridor.RoomA,
            Light = 57, StepsTaken = 10, RandomCounter = 4, Seed = 9, Party = { "a" }
        };
    }

    private static Crawl Crawl(ExpeditionState state) => new(state, Rules, new FakeParty("a"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeAlwaysAccessibleBranchReturnsToTheSavedSquareAndDirection(bool towardBoss)
    {
        var state = AtDoor(towardBoss);
        int corridor = state.CorridorId, tile = state.TileIndex, heading = state.HeadingRoomId;
        var crawl = Crawl(state);
        Assert.True(crawl.CanEnterSecretRoom);
        Assert.Collection(crawl.EnterSecretRoom(),
            e => Assert.Equal(CrawlEventType.EnteredRoom, e.Type),
            e => { Assert.Equal(CrawlEventType.Curio, e.Type); Assert.Equal("thanks_chest", e.ContentId); });
        Assert.True(crawl.CurrentRoom.IsSecret);
        Assert.True(crawl.CurrentRoom.Visited);
        Assert.False(state.QuestComplete);
        Assert.Equal(4, state.RandomCounter);
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        var resumed = Crawl(loaded);
        Assert.Equal(CrawlEventType.EnteredTile, Assert.Single(resumed.ExitSecretRoom()).Type);
        Assert.Equal((-1, corridor, tile, heading), (loaded.RoomId, loaded.CorridorId, loaded.TileIndex, loaded.HeadingRoomId));
        Assert.Equal(57, loaded.Light);
        Assert.Equal(10, loaded.StepsTaken);
        Assert.Equal(4, loaded.RandomCounter);
        Assert.Equal((-1, -1, -1), (loaded.SecretReturnCorridorId, loaded.SecretReturnTileIndex, loaded.SecretReturnHeadingRoomId));
        Assert.True(resumed.CanEnterSecretRoom);
        Assert.Equal(CrawlEventType.Blocked, Assert.Single(resumed.ExitSecretRoom()).Type);
    }

    [Theory]
    [InlineData(false, 17, false)]
    [InlineData(false, 28, false)]
    [InlineData(true, 17, false)]
    [InlineData(true, 18, true)]
    public void OrdinarySecretsRequireCriticalScoutingThatActuallyReachesTheDoor(bool critical, int budget, bool discovered)
    {
        var state = AtDoor();
        var crawl = Crawl(state);
        crawl.CurrentTile.SecretDoorAlwaysAccessible = false;
        Assert.False(crawl.CanEnterSecretRoom);
        state.Map.ScoutFrom(state.Map.EntranceRoomId, budget, revealSecrets: critical);
        Assert.Equal(discovered, state.Map.Room(crawl.CurrentTile.SecretRoomId).Scouted);
        Assert.Equal(discovered, crawl.CanEnterSecretRoom);
        Assert.Equal(4, state.RandomCounter);
    }

    [Fact]
    public void WalkingOntoANativeAlwaysAccessibleDoorRevealsItsBranchWithoutScouting()
    {
        var state = AtDoor();
        state.TileIndex--;
        var crawl = Crawl(state);
        Assert.False(crawl.CanEnterSecretRoom);
        crawl.CurrentTile.Resolved = true;
        crawl.Step(true);
        Assert.Equal(17, state.TileIndex);
        Assert.True(state.Map.Room(crawl.CurrentTile.SecretRoomId).Scouted);
        Assert.True(crawl.CanEnterSecretRoom);
    }

    [Fact]
    public void BlockedOrEndedExpeditionsCannotEnterTheBranch()
    {
        var state = AtDoor();
        var crawl = Crawl(state);
        crawl.CurrentTile.Content = HallContent.Battle;
        crawl.CurrentTile.Resolved = false;
        Assert.False(crawl.CanEnterSecretRoom);
        Assert.Equal(CrawlEventType.Blocked, Assert.Single(crawl.EnterSecretRoom()).Type);
        Assert.False(state.InRoom);
        crawl.CurrentTile.Resolved = true;
        state.Ended = true;
        Assert.False(crawl.CanEnterSecretRoom);
        Assert.Equal(CrawlEventType.Blocked, Assert.Single(crawl.EnterSecretRoom()).Type);
    }

    [Fact]
    public void InvalidSavedReturnDoesNotMoveThePartyOrConsumeEffects()
    {
        var state = AtDoor();
        var crawl = Crawl(state);
        crawl.EnterSecretRoom();
        state.SecretReturnTileIndex = 999;
        int room = state.RoomId;
        Assert.Equal(CrawlEventType.Blocked, Assert.Single(crawl.ExitSecretRoom()).Type);
        Assert.Equal(room, state.RoomId);
        Assert.Equal(57, state.Light);
        Assert.Equal(4, state.RandomCounter);
        Assert.Equal(10, state.StepsTaken);
    }
}
