using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>Routes that native QA fixtures walk on generated maps: the nearest fight of one kind that the party can
/// reach without first starting another fight.</summary>
public static class GeneratedRoutes
{
    /// <summary>
    /// The nearest corridor with an unresolved hall fight. <paramref name="rooms"/> lists the rooms to cross, in
    /// order, after <paramref name="start"/> (empty when the fight corridor leaves the start room); the party then
    /// walks <paramref name="corridorId"/> from the last of them until the fight starts. The route never enters a
    /// secret room or a room whose fight is still pending, and it crosses no other corridor with a pending fight.
    /// </summary>
    public static bool ToHallBattle(DungeonMap map, int start, out List<int> rooms, out int corridorId)
    {
        rooms = new List<int>();
        corridorId = -1;
        var parents = new Dictionary<int, int> { [start] = -1 };
        var todo = new Queue<int>();
        todo.Enqueue(start);
        while (todo.Count > 0)
        {
            int room = todo.Dequeue();
            foreach (int hall in map.Room(room).CorridorIds.OrderBy(id => id))
            {
                var corridor = map.Corridor(hall);
                if (corridor.Tiles.Any(PendingFight))
                {
                    for (int id = room; id != start; id = parents[id]) rooms.Insert(0, id);
                    corridorId = hall;
                    return true;
                }
                int next = corridor.Other(room);
                var target = map.Room(next);
                if (parents.ContainsKey(next) || target.IsSecret || target.HasBattle && !target.Cleared) continue;
                parents[next] = room;
                todo.Enqueue(next);
            }
        }
        return false;
    }

    /// <summary>The square where walking <paramref name="corridorId"/> away from <paramref name="fromRoom"/> starts its
    /// first pending fight, counted in DD1's tile order (from RoomA), or -1.</summary>
    public static int FirstFightTile(DungeonMap map, int corridorId, int fromRoom)
    {
        var corridor = map.Corridor(corridorId);
        var order = Enumerable.Range(0, corridor.Tiles.Count);
        if (fromRoom == corridor.RoomB) order = order.Reverse();
        foreach (int i in order)
            if (PendingFight(corridor.Tiles[i])) return i;
        return -1;
    }

    private static bool PendingFight(HallTile tile) => tile.Content == HallContent.Battle && !tile.Resolved;
}
