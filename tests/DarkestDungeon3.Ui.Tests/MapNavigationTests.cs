using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public sealed class MapNavigationTests
{
    private static readonly CrawlRules Rules = CrawlRules.FromDd1(Dd1Campaign.Load(Dd1Install.Find()).Rules);
    private sealed class Party : IParty
    {
        public IReadOnlyList<string> Alive => new[] { "hero" };
        public float HpFraction(string id) => 1;
        public void Damage(string id, float hp, string cause) { }
        public void Heal(string id, float hp) { }
        public void AddStress(string id, int points, string cause) { }
        public string AddDd1Quirk(string id, string quirk) => quirk;
        public string PurgeNegative(string id) => null;
        public string CureDisease(string id) => null;
    }
    private static Driver Ready(bool inHall = false)
    {
        var map = new DungeonMap
        {
            Rooms = {
                new Room { Id = 0, Visited = true, CorridorIds = { 0 } },
                new Room { Id = 1, X = 3, Scouted = true, CorridorIds = { 0, 1 } },
                new Room { Id = 2, X = 6, Scouted = true, CorridorIds = { 1 } }
            },
            Corridors = {
                new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1 } } },
                new Corridor { Id = 1, RoomA = 1, RoomB = 2, Tiles = { new HallTile { Index = 0 } } }
            }
        };
        var state = new ExpeditionState {
            Started = true, Map = map, Party = { "hero" }, Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 },
            RoomId = inHall ? -1 : 0, CorridorId = inHall ? 0 : -1, TileIndex = inHall ? 0 : -1, HeadingRoomId = inHall ? 1 : -1
        };
        return new Driver { Crawl = new Crawl(state, Rules, new Party()) };
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RoomSelectionPicksOnlyTheFirstCorridorWithoutQueuingAWalk(int target)
    {
        var d = Ready();
        d.WalkToRoom(target); // an existing debug/explicit route must also be cancelled
        Assert.True(d.IsWalking);
        d.SelectMapRoom(target);
        Assert.Equal(1, d.PendingTravel);
        Assert.False(d.IsWalking);
        Assert.Equal(0, d.Expedition.RoomId); // the fade still owns the transition
    }

    [Fact]
    public void MapRoomClickUsesManualSelectionAndDoesNotRestartWalkingInACorridor()
    {
        var d = Ready(inHall: true);
        d.WalkToRoom(1);
        Assert.True(d.IsWalking);
        GUI.enabled = true;
        Event.current = new Event { type = EventType.MouseUp, rawType = EventType.MouseUp, mousePosition = new Vector2(1400.5f, 899.5f) };
        new CrawlMapUi().Draw(d.Expedition, d.WalkToTile, d.SelectMapRoom);
        Assert.False(d.IsWalking);
        Assert.Equal(-1, d.PendingTravel);
        Assert.Equal(0, d.Expedition.TileIndex);
        Assert.Equal(1, d.Expedition.HeadingRoomId);
    }

    [Theory]
    [InlineData("battle")]
    [InlineData("loot")]
    [InlineData("camp")]
    [InlineData("ended")]
    [InlineData("current")]
    [InlineData("invalid")]
    [InlineData("unreachable")]
    public void UnavailableRoomSelectionsDoNotBeginTravel(string reason)
    {
        var d = Ready();
        if (reason == "battle") d.Expedition.Map.Room(0).Content = RoomContent.Battle;
        if (reason == "loot") d.Expedition.PendingSpoils = new BattleSpoils();
        if (reason == "camp") d.Expedition.Camp = new CampState();
        if (reason == "ended") d.Expedition.Ended = true;
        if (reason == "unreachable") d.Expedition.Map.Room(0).CorridorIds.Clear();
        d.SelectMapRoom(reason == "current" ? 0 : reason == "invalid" ? 99 : 1);
        Assert.Equal(-1, d.PendingTravel);
        Assert.False(d.IsWalking);
    }

    [Fact]
    public void AdditionalRoomClickCannotReplaceAnActiveFade()
    {
        var d = Ready();
        d.SelectMapRoom(1);
        d.SelectMapRoom(2);
        Assert.Equal(1, d.PendingTravel);
        Assert.False(d.IsWalking);
    }

    [Fact]
    public void SecretRoomSelectionRequiresManualApproachAndStillEntersAtItsDoor()
    {
        var d = Ready(inHall: true);
        var secret = new Room { Id = 3, IsSecret = true, Scouted = true };
        d.Expedition.Map.Rooms.Add(secret);
        d.Expedition.Map.Corridor(0).Tiles[1].SecretRoomId = 3;
        d.SelectMapRoom(3);
        Assert.False(d.IsWalking);
        Assert.False(d.Expedition.InRoom);
        d.Expedition.TileIndex = 1;
        d.SelectMapRoom(3);
        Assert.Equal(3, d.Expedition.RoomId);
        Assert.False(d.IsWalking);
        d.SelectMapRoom(1);
        Assert.False(d.Expedition.InRoom);
        Assert.Equal(1, d.Expedition.TileIndex);
        Assert.Equal(-1, d.PendingTravel);
        Assert.False(d.IsWalking);
    }
}
