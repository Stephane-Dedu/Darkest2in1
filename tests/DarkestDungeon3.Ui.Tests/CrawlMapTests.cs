using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CrawlMapTests : IDisposable
{
    private readonly Session _prior = Session.Current;
    private readonly CrawlMapUi _mapUi = new();
    private readonly List<(int Corridor, int Tile)> _tileMoves = new();
    private readonly List<int> _roomMoves = new();

    public CrawlMapTests()
    {
        Session.Current = new Session { Dd1 = Dd1Install.Find() };
        GUI.enabled = true;
        GUIUtility.clipOffset = default;
    }

    public void Dispose()
    {
        Session.Current = _prior;
        Gui.Tips.Clear();
        GUI.enabled = true;
        GUIUtility.clipOffset = default;
    }

    private static ExpeditionState Raid(RoomContent content, bool quest = false)
    {
        var entrance = new Room { Id = 0, Content = RoomContent.Entrance, Visited = true, CorridorIds = { 0 } };
        var room = new Room { Id = 1, X = 3, Content = content, Scouted = true, CurioId = "test_curio", IsQuestGoal = quest, CorridorIds = { 0 } };
        var hall = new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0, X = 1, Scouted = true } } };
        return new ExpeditionState { RoomId = 0, Map = new DungeonMap { Size = "plot", Rooms = { entrance, room }, Corridors = { hall } } };
    }

    private void Draw(ExpeditionState raid, float x, float y, EventType type = EventType.Repaint, Vector2 delta = default)
    {
        Gui.Tips.Clear();
        Event.current = new Event { type = type, rawType = type, mousePosition = new Vector2(x, y), delta = delta };
        _mapUi.Draw(raid, (c, t) => _tileMoves.Add((c, t)), r => _roomMoves.Add(r));
        Assert.Equal(x, Event.current.mousePosition.x);
        Assert.Equal(y, Event.current.mousePosition.y);
        Assert.Equal(0, GUIUtility.clipOffset.x);
        Assert.Equal(0, GUIUtility.clipOffset.y);
    }

    [Theory]
    [InlineData(RoomContent.Battle, false, "Battle")]
    [InlineData(RoomContent.Curio, false, "Curio")]
    [InlineData(RoomContent.Curio, true, "Quest Location")]
    public void VisibleRoomHoverRequestsItsInstalledDd1Label(RoomContent content, bool quest, string label)
    {
        Draw(Raid(content, quest), 1420, 900);
        Assert.Equal(label, Assert.Single(Gui.Tips));
        Assert.Empty(_roomMoves);
        Assert.Empty(_tileMoves);
    }

    [Theory]
    [InlineData(HallContent.Battle, false, "Battle")]
    [InlineData(HallContent.Curio, false, "Curio")]
    [InlineData(HallContent.Curio, true, "Quest Location")]
    [InlineData(HallContent.Trap, false, "Trap")]
    [InlineData(HallContent.Obstacle, false, "Obstacle")]
    public void HallMarkersRequestTheirLabelsAndResolvedOrUnknownContentStaysHidden(HallContent content, bool quest, string label)
    {
        var raid = Raid(RoomContent.Empty);
        var tile = raid.Map.Corridor(0).Tiles[0];
        tile.Content = content;
        tile.IsQuestGoal = quest;
        Draw(raid, 1340, 900);
        Assert.Equal(label, Assert.Single(Gui.Tips));
        tile.Resolved = true;
        Draw(raid, 1340, 900);
        Assert.Empty(Gui.Tips);
        tile.Resolved = false;
        tile.Scouted = false;
        Draw(raid, 1340, 900);
        Assert.Empty(Gui.Tips); // corridor is visible beside the entrance; its hidden content is not
        tile.Visited = true;
        Draw(raid, 1340, 900);
        Assert.Equal(label, Assert.Single(Gui.Tips));
    }

    [Theory]
    [InlineData(RoomContent.GuardedCurio, "Room Battle with Curio", "Curio")]
    [InlineData(RoomContent.GuardedTreasure, "Room Battle with Treasure", "Treasure")]
    public void GuardedRoomsKeepTheirRemainingCurioLabelAfterBattleAndHideTakenObjects(RoomContent content, string guarded, string remaining)
    {
        var raid = Raid(content);
        var room = raid.Map.Room(1);
        Draw(raid, 1420, 900);
        Assert.Equal(guarded, Assert.Single(Gui.Tips));
        room.Cleared = true;
        Draw(raid, 1420, 900);
        Assert.Equal(remaining, Assert.Single(Gui.Tips));
        room.CurioTaken = true;
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void HiddenAdjacentRoomsAndHungerSquaresDoNotExposeTheirContent()
    {
        var raid = Raid(RoomContent.GuardedCurio, quest: true);
        raid.Map.Room(1).Scouted = false;
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
        var tile = raid.Map.Corridor(0).Tiles[0];
        tile.Content = HallContent.Hunger;
        Draw(raid, 1340, 900);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void GuardedQuestAndBossLabelsRetainTheBattleAndObjectiveWithoutRepeatingCompletedObjectives()
    {
        var raid = Raid(RoomContent.GuardedCurio, quest: true);
        Draw(raid, 1420, 900);
        Assert.Equal("Room Battle with Curio\nQuest Location", Assert.Single(Gui.Tips));
        var room = raid.Map.Room(1);
        room.Cleared = true;
        Draw(raid, 1420, 900);
        Assert.Equal("Quest Location", Assert.Single(Gui.Tips));
        room.CurioTaken = true;
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
        room.Content = RoomContent.Boss;
        room.CurioId = null;
        room.Cleared = false;
        Draw(raid, 1420, 900);
        Assert.Equal("Boss", Assert.Single(Gui.Tips));
        room.Cleared = true;
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void SecretDoorTooltipRequiresKnownSecretRoomAndSurvivesOtherResolvedContent()
    {
        var raid = Raid(RoomContent.Empty);
        var secret = new Room { Id = 2, X = 5, IsSecret = true };
        raid.Map.Rooms.Add(secret);
        var tile = raid.Map.Corridor(0).Tiles[0];
        tile.SecretRoomId = secret.Id;
        tile.Content = HallContent.Curio;
        Draw(raid, 1340, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        secret.Scouted = true;
        Draw(raid, 1340, 900);
        Assert.Equal("Curio\nSecret Door", Assert.Single(Gui.Tips));
        tile.Resolved = true;
        Draw(raid, 1340, 900);
        Assert.Equal("Secret Door", Assert.Single(Gui.Tips));
    }

    [Fact]
    public void PointerOutsideClipOrOnDisabledPageDoesNotRequestATooltip()
    {
        var raid = Raid(RoomContent.Curio);
        GUI.enabled = false;
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
        GUI.enabled = true;
        raid.Map.Room(1).X = 9; // icon would be at 1660, beyond the panel's 1625 right edge
        Draw(raid, 1660, 900);
        Assert.Empty(Gui.Tips);
        raid.Map.Room(1).X = 3;
        Draw(raid, 1420, 900, EventType.Layout);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void MapPanIsNotTravelAndHoverFollowsPanThenRecentersOnPartyMovement()
    {
        var raid = Raid(RoomContent.Curio);
        Draw(raid, 1420, 900, EventType.MouseDown);
        Draw(raid, 1520, 900, EventType.MouseDrag, new Vector2(100, 0));
        Draw(raid, 1520, 900);
        Assert.Empty(Gui.Tips); // suppress tips while panning
        Draw(raid, 1520, 900, EventType.MouseUp);
        Assert.Empty(_roomMoves);
        Draw(raid, 1520, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, 1420, 900);
        Assert.Empty(Gui.Tips);
        raid.RoomId = 1;
        Draw(raid, 1300, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Assert.Empty(_tileMoves);
    }

    [Fact]
    public void OrdinaryClicksRequestRoomSelectionAndHallTravelWithoutHoverChangingTheRaid()
    {
        var raid = Raid(RoomContent.Curio);
        Draw(raid, 1420, 900, EventType.MouseDown);
        Draw(raid, 1420, 900, EventType.MouseUp);
        Assert.Equal(1, Assert.Single(_roomMoves));
        Draw(raid, 1340, 900, EventType.MouseDown);
        Draw(raid, 1340, 900, EventType.MouseUp);
        Assert.Equal((0, 0), Assert.Single(_tileMoves));
        Draw(raid, 1420, 900);
        Assert.Equal(0, raid.RoomId);
        Assert.False(raid.Map.Room(1).Visited);
        Assert.False(raid.Map.Room(1).CurioTaken);
    }

    [Fact]
    public void GeneratedCorridorHoverUsesItsInterpolatedPositionRatherThanPlotCoordinates()
    {
        var raid = Raid(RoomContent.Empty);
        raid.Map.Size = "short";
        raid.Map.Corridor(0).Tiles[0].Content = HallContent.Battle;
        Draw(raid, 1360, 900); // midpoint between the room edges
        Assert.Equal("Battle", Assert.Single(Gui.Tips));
        Draw(raid, 1340, 900); // plot X=1 is not the generated hall's location
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void WheelZoomAnchorsToPointerAndScalesRoomAndHallClickTargets()
    {
        var raid = Raid(RoomContent.Curio);
        raid.Map.Corridor(0).Tiles[0].Content = HallContent.Battle;
        Draw(raid, 1420.5f, 899.5f, EventType.ScrollWheel, new Vector2(0, -100));
        Assert.Equal(EventType.Used, Event.current.type);
        Draw(raid, 1475, 900); // room stays under the pointer, now with a 128px icon
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, 1475, 900, EventType.MouseDown);
        Draw(raid, 1475, 900, EventType.MouseUp);
        Assert.Equal(1, Assert.Single(_roomMoves));
        Draw(raid, 1260.5f, 900); // hallway follows the same anchored scale
        Assert.Equal("Battle", Assert.Single(Gui.Tips));
        Draw(raid, 1260.5f, 900, EventType.MouseDown);
        Draw(raid, 1260.5f, 900, EventType.MouseUp);
        Assert.Equal((0, 0), Assert.Single(_tileMoves));
        Assert.Equal(0, raid.RoomId);
    }

    [Theory]
    [InlineData(-100, 1600, 1610)] // maximum: 200%, room center 1540.5, half width 64
    [InlineData(100, 1375, 1380)] // minimum: 50%, room center 1360.5, half width 16
    public void ZoomIsBoundedAndWheelDoesNotRequestTravel(float scroll, float inside, float outside)
    {
        var raid = Raid(RoomContent.Curio);
        for (int i = 0; i < 3; i++) Draw(raid, 1300.5f, 899.5f, EventType.ScrollWheel, new Vector2(0, scroll));
        Draw(raid, inside, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, outside, 900);
        Assert.Empty(Gui.Tips);
        Assert.Empty(_roomMoves);
        Assert.Empty(_tileMoves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WheelOutsideMapOrOnDisabledPageDoesNotZoomOrConsumeInput(bool disabled)
    {
        var raid = Raid(RoomContent.Curio);
        GUI.enabled = !disabled;
        Draw(raid, disabled ? 1300.5f : 1700, 900, EventType.ScrollWheel, new Vector2(0, -100));
        Assert.Equal(EventType.ScrollWheel, Event.current.type);
        GUI.enabled = true;
        Draw(raid, 1420, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, 1475, 900);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void ZoomedMapPansInScreenPixelsAndKeepsZoomWhenRecenteringOnParty()
    {
        var raid = Raid(RoomContent.Curio);
        Draw(raid, 1300.5f, 899.5f, EventType.ScrollWheel, new Vector2(0, -100));
        Draw(raid, 1540.5f, 900, EventType.MouseDown);
        Draw(raid, 1440.5f, 900, EventType.MouseDrag, new Vector2(-100, 0));
        Draw(raid, 1440.5f, 900, EventType.MouseUp);
        Draw(raid, 1440.5f, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Assert.Empty(_roomMoves);
        raid.RoomId = 1;
        Draw(raid, 1360, 900); // recentered icon retains its 64px half width
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, 1370, 900);
        Assert.Empty(Gui.Tips);
        // A different expedition starts at normal zoom, even if its room ID matches.
        raid = Raid(RoomContent.Curio);
        Draw(raid, 1420, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
        Draw(raid, 1475, 900);
        Assert.Empty(Gui.Tips);
    }

    [Fact]
    public void ZoomDuringAPressCancelsThatClickOnRelease()
    {
        var raid = Raid(RoomContent.Curio);
        Draw(raid, 1420, 900, EventType.MouseDown);
        Draw(raid, 1420, 900, EventType.ScrollWheel, new Vector2(0, -2));
        Draw(raid, 1420, 900, EventType.MouseUp);
        Assert.Equal(EventType.Used, Event.current.type);
        Assert.Empty(_roomMoves);
        Assert.Empty(_tileMoves);
        Draw(raid, 1420, 900);
        Assert.Equal("Curio", Assert.Single(Gui.Tips));
    }
}
