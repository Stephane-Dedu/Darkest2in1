using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkestDungeon3.Core.Dungeon;

public enum RoomContent
{
    Empty,
    Entrance,
    Battle,
    Curio,
    GuardedCurio,     // battle, then a curio
    Treasure,
    GuardedTreasure,  // battle, then a treasure curio
    Boss,
}

public enum HallContent
{
    Empty,
    Battle,
    Trap,
    Obstacle,
    Curio,
    Hunger,           // a hunger check when the party steps here
}

/// <summary>A DD1 room. Rooms sit on a coarse grid; X/Y are fine coordinates (grid * spacing + nudge).</summary>
public sealed class Room
{
    public int Id;
    public int GridX, GridY;
    public int X, Y;
    public RoomContent Content;
    /// <summary>Curio id for Curio/GuardedCurio/Treasure/GuardedTreasure rooms (from the zone's props table).</summary>
    public string CurioId;
    public bool IsQuestGoal;
    /// <summary>A hand-made map's named battle (DD1 mash "named:" entry), else null (rolled from the zone).</summary>
    public string MashName;
    public List<int> CorridorIds = new();

    // Expedition state.
    public bool Visited, Scouted, Cleared, CurioTaken;

    public bool HasBattle => Content is RoomContent.Battle or RoomContent.GuardedCurio or RoomContent.GuardedTreasure or RoomContent.Boss;
}

/// <summary>One hall square of a corridor.</summary>
public sealed class HallTile
{
    public int Index;
    public int X, Y;
    public HallContent Content;
    public string ContentId;
    public bool IsQuestGoal;
    /// <summary>A hand-made map's named battle (DD1 mash "named:" entry), else null (rolled from the zone).</summary>
    public string MashName;

    // Expedition state.
    public bool Visited, Scouted, Resolved;
}

/// <summary>A corridor between two rooms. Tiles run from RoomA to RoomB.</summary>
public sealed class Corridor
{
    public int Id;
    public int RoomA, RoomB;
    public List<HallTile> Tiles = new();

    public int Other(int roomId) => roomId == RoomA ? RoomB : RoomA;
}

public sealed class DungeonMap
{
    public int Seed;
    public string Dungeon, Size, QuestType;
    public List<Room> Rooms = new();
    public List<Corridor> Corridors = new();
    public int EntranceRoomId;
    public int BossRoomId = -1;

    public Room Room(int id) => Rooms[id];
    public Corridor Corridor(int id) => Corridors[id];

    public IEnumerable<HallTile> AllTiles => Corridors.SelectMany(c => c.Tiles);

    public Corridor FindCorridor(int a, int b) =>
        Corridors.FirstOrDefault(c => (c.RoomA == a && c.RoomB == b) || (c.RoomA == b && c.RoomB == a));

    public IEnumerable<int> Neighbours(int roomId) => Rooms[roomId].CorridorIds.Select(cid => Corridors[cid].Other(roomId));

    /// <summary>Room-graph distance from a room (corridors count as one step).</summary>
    public int[] Distances(int from)
    {
        var dist = Enumerable.Repeat(-1, Rooms.Count).ToArray();
        var queue = new Queue<int>();
        dist[from] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            foreach (int n in Neighbours(r))
                if (dist[n] < 0) { dist[n] = dist[r] + 1; queue.Enqueue(n); }
        }
        return dist;
    }

    public bool IsConnected() => Distances(EntranceRoomId).All(d => d >= 0);

    /// <summary>ASCII picture for logs and tests: rooms as letters, hall tiles as symbols.</summary>
    public string ToAscii()
    {
        var points = Rooms.Select(r => (r.X, r.Y)).Concat(AllTiles.Select(t => (t.X, t.Y))).ToList();
        int minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
        int minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        var grid = new char[maxY - minY + 1, maxX - minX + 1];
        for (int y = 0; y < grid.GetLength(0); y++)
            for (int x = 0; x < grid.GetLength(1); x++)
                grid[y, x] = ' ';

        foreach (var t in AllTiles)
            grid[t.Y - minY, t.X - minX] = t.Content switch
            {
                HallContent.Battle => 'b',
                HallContent.Trap => 't',
                HallContent.Obstacle => 'o',
                HallContent.Curio => 'c',
                HallContent.Hunger => 'h',
                _ => '.',
            };
        foreach (var r in Rooms)
            grid[r.Y - minY, r.X - minX] = r.Content switch
            {
                RoomContent.Entrance => 'E',
                RoomContent.Battle => 'B',
                RoomContent.Curio => 'C',
                RoomContent.GuardedCurio => 'G',
                RoomContent.Treasure => 'T',
                RoomContent.GuardedTreasure => 'X',
                RoomContent.Boss => '!',
                _ => 'o',
            };

        var sb = new StringBuilder();
        for (int y = 0; y < grid.GetLength(0); y++)
        {
            for (int x = 0; x < grid.GetLength(1); x++) sb.Append(grid[y, x]);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
