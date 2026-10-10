using System.Collections.Generic;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    // Walking to a clicked map destination, one square at a time.
    private readonly Queue<int> _route = new();
    private int _routeTargetRoom = -1;
    private (int corridor, int tile) _routeTargetTile = (-1, -1);

    /// <summary>A room click picks the first exit, then leaves corridor movement to held input.</summary>
    public void SelectMapRoom(int target)
    {
        StopWalking();
        if (Crawl == null || !Crawl.CanNavigate || _travelTo >= 0) return;
        var map = Expedition.Map;
        if (target < 0 || target >= map.Rooms.Count || target == Expedition.RoomId) return;
        if (Crawl.CurrentRoom?.IsSecret == true && !ExitSecretRoom()) return;
        var destination = map.Room(target);
        if (destination.IsSecret)
        {
            if (!destination.Scouted && !destination.Visited) return;
            if (Crawl.CanEnterSecretRoom && Crawl.CurrentTile.SecretRoomId == target) EnterSecretRoom();
            else Ui.Gui.Announce("Approach the marked corridor square to enter the secret room.");
            return;
        }
        if (!Expedition.InRoom || Crawl.IsBlocked) return;
        var path = RoomPath(map, Expedition.RoomId, target);
        if (path.Count > 1) BeginTravel(path[1]);
    }

    // Explicit hall-square walking and developer room routing.

    public bool IsWalking => _route.Count > 0 || _routeTargetRoom >= 0 || _routeTargetTile.corridor >= 0;

    public void StopWalking()
    {
        _route.Clear();
        _routeTargetRoom = -1;
        _routeTargetTile = (-1, -1);
    }

    /// <summary>Walk to a room: through the rooms on the shortest path, square by square.</summary>
    public void WalkToRoom(int target)
    {
        StopWalking();
        if (!Crawl.CanNavigate) return;
        var map = Expedition.Map;
        if (target < 0 || target >= map.Rooms.Count || target == Expedition.RoomId) return;
        if (Crawl.CurrentRoom?.IsSecret == true && !ExitSecretRoom()) return;
        var destination = map.Room(target);
        if (destination.IsSecret)
        {
            if (!destination.Scouted && !destination.Visited) return;
            if (Crawl.CanEnterSecretRoom && Crawl.CurrentTile.SecretRoomId == target) { EnterSecretRoom(); return; }
            var entrance = map.SecretEntrance(target);
            if (entrance.Corridor == null) return;
            WalkToTile(entrance.Corridor.Id, entrance.Tile.Index);
            if (!IsWalking) Ui.Gui.Announce("Approach the marked corridor square to enter the secret room.");
            return;
        }
        int from = Expedition.InRoom ? Expedition.RoomId : NearestEnd(target);
        foreach (int r in RoomPath(map, from, target)) _route.Enqueue(r);
        _routeTargetRoom = target;
    }

    /// <summary>Walk to a square of the corridor the party is in, or of a corridor next to its room.</summary>
    public void WalkToTile(int corridorId, int tileIndex)
    {
        StopWalking();
        if (!Crawl.CanNavigate) return;
        if (Crawl.CurrentRoom?.IsSecret == true && !ExitSecretRoom()) return;
        var c = Expedition.Map.Corridor(corridorId);
        if (Expedition.InRoom && c.RoomA != Expedition.RoomId && c.RoomB != Expedition.RoomId) return;
        if (!Expedition.InRoom && Expedition.CorridorId != corridorId) return;
        _routeTargetTile = (corridorId, tileIndex);
    }

    private int NearestEnd(int target)
    {
        var c = Crawl.CurrentCorridor;
        var map = Expedition.Map;
        return map.Distances(c.RoomA)[target] <= map.Distances(c.RoomB)[target] ? c.RoomA : c.RoomB;
    }

    private static List<int> RoomPath(DungeonMap map, int from, int to)
    {
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            if (r == to) break;
            foreach (int n in map.Neighbours(r))
                if (!prev.ContainsKey(n)) { prev[n] = r; queue.Enqueue(n); }
        }
        var path = new List<int>();
        if (!prev.ContainsKey(to)) return path;
        for (int at = to; at != -1; at = prev[at]) path.Add(at);
        path.Reverse();
        return path;   // starts with `from`
    }
}
