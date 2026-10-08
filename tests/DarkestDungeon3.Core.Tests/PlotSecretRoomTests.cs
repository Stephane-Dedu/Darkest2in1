using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class PlotSecretRoomTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);

    [Theory]
    [InlineData("DD_map4", "darkestdungeon", 2, 17, 12, 3, 12, 5)]
    [InlineData("town_invasion_0", "town", 7, 2, 8, 1, 5, 1)]
    public void NativeSecretDoorTargetsItsHiddenRoomWithoutRewiringTheMainGraph(string name, string zone, int normalRooms, int tileIndex, int doorX, int doorY, int roomX, int roomY)
    {
        var map = PlotMap.Load(Install, name, zone, "kill_boss", 7, Dd1.Props(zone));
        var secret = Assert.Single(map.Rooms.Where(r => r.IsSecret));
        Assert.Equal(normalRooms, secret.Id);
        Assert.Equal((roomX, roomY), (secret.X, secret.Y));
        var door = Assert.Single(map.AllTiles.Where(t => t.SecretRoomId >= 0));
        Assert.Equal((tileIndex, doorX, doorY), (door.Index, door.X, door.Y));
        Assert.Equal(secret.Id, door.SecretRoomId);
        Assert.True(door.SecretDoorAlwaysAccessible);
        Assert.False(secret.Visited); Assert.False(secret.Scouted);
        Assert.Empty(secret.CorridorIds);
        Assert.True(map.IsConnected());
        Assert.Equal(-1, map.Distances(map.EntranceRoomId)[secret.Id]);
        Assert.Equal(normalRooms, map.QuestRooms.Count());
        Assert.All(map.Corridors, c => { Assert.False(map.Room(c.RoomA).IsSecret); Assert.False(map.Room(c.RoomB).IsSecret); });
        Assert.False(map.Room(map.BossRoomId).IsSecret);
        var state = new ExpeditionState { Map = map };
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition.Map;
        Assert.True(loaded.Room(secret.Id).IsSecret);
        Assert.Equal(secret.Id, Assert.Single(loaded.AllTiles.Where(t => t.SecretRoomId >= 0)).SecretRoomId);
        Assert.Equal((roomX, roomY), (loaded.Room(secret.Id).X, loaded.Room(secret.Id).Y));
    }

    [Fact]
    public void OldMapsDefaultToOrdinaryRoomsAndNoSecretDoor()
    {
        var loaded = SaveFile.FromJson("{\"Expedition\":{\"Map\":{\"Rooms\":[{\"Id\":0}],\"Corridors\":[{\"Tiles\":[{\"Index\":0}]}]}}}").Expedition.Map;
        Assert.False(Assert.Single(loaded.Rooms).IsSecret);
        Assert.Equal(-1, Assert.Single(loaded.AllTiles).SecretRoomId);
        Assert.Single(loaded.QuestRooms);
    }

    [Fact]
    public void ExplorationQuotaDoesNotRequireOrCountSecretVisits()
    {
        var map = new DungeonMap { QuestType = "explore", EntranceRoomId = 0 };
        map.Rooms.Add(new Room { Id = 0, Content = RoomContent.Entrance });
        map.Rooms.Add(new Room { Id = 1 });
        map.Rooms.Add(new Room { Id = 2 });
        map.Rooms.Add(new Room { Id = 3, IsSecret = true, Visited = true });
        map.Corridors.Add(new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile() } });
        map.Rooms[0].CorridorIds.Add(0); map.Rooms[1].CorridorIds.Add(0);
        var state = new ExpeditionState { Map = map, Quest = new QuestOffer { Type = "explore" }, Party = { "a" } };
        var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a"));
        crawl.Begin();
        Assert.False(state.QuestComplete);
        crawl.Travel(1);
        Assert.False(state.InRoom);
        crawl.Step(true);
        Assert.True(state.InRoom);
        Assert.True(state.QuestComplete);
    }
}
