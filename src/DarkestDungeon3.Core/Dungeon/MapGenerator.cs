using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>
/// Builds a DD1-style dungeon from a <see cref="MapGenParams"/> block:
/// rooms grown on a grid, joined by corridors of hall tiles, then filled with battles, curios, traps,
/// obstacles and hunger checks in the amounts DD1's map_generator.darkest asks for.
/// DD1's exact algorithm lives in its exe; this reproduces its inputs and the shape of its output.
/// </summary>
public static class MapGenerator
{
    private static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public static DungeonMap Generate(MapGenParams p, int seed, ZoneProps props = null, Campaign.QuestGoal goal = null)
    {
        props ??= ZoneProps.Empty;
        var rng = new Rng(seed);
        var map = new DungeonMap { Seed = seed, Dungeon = p.Dungeon, Size = p.Size, QuestType = p.QuestType };

        int width = Math.Max(1, p.GridWidth), height = Math.Max(1, p.GridHeight);
        int roomCount = Math.Max(2, Math.Min(p.BaseRooms, width * height));

        // 1. Grow a connected set of grid cells from a random start cell. Every growth step is a tree edge,
        //    so the result is connected by construction.
        var cellToRoom = new Dictionary<(int, int), int>();
        var treeEdges = new List<(int a, int b)>();
        var start = (rng.Next(width), rng.Next(height));
        AddRoom(map, cellToRoom, start);
        while (map.Rooms.Count < roomCount)
        {
            var growable = map.Rooms
                .SelectMany(r => Dirs.Select(d => (from: r.Id, cell: (r.GridX + d.dx, r.GridY + d.dy))))
                .Where(c => c.cell.Item1 >= 0 && c.cell.Item1 < width && c.cell.Item2 >= 0 && c.cell.Item2 < height
                            && !cellToRoom.ContainsKey(c.cell))
                .ToList();
            if (growable.Count == 0) break;
            var pick = rng.Pick(growable);
            int id = AddRoom(map, cellToRoom, pick.cell);
            treeEdges.Add((pick.from, id));
        }

        // 2. Nudge whole columns and rows (DD1's nudge_span) so corridors vary in length. Shifting lines rather
        //    than single rooms keeps every corridor straight, so corridors can never cross each other.
        int spacing = Math.Max(2, p.Spacing);
        int[] colX = NudgedLines(width, spacing, p, rng);
        int[] rowY = NudgedLines(height, spacing, p, rng);
        foreach (var room in map.Rooms)
        {
            room.X = colX[room.GridX];
            room.Y = rowY[room.GridY];
        }

        // 3. Corridors: the spanning tree, plus extra links between grid neighbours up to DD1's corridor count.
        //    Connectivity scales how eagerly extra links are taken.
        var edges = new List<(int a, int b)>(treeEdges);
        var extra = new List<(int a, int b)>();
        foreach (var room in map.Rooms)
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1) }) // right and down: each neighbour pair once
            {
                if (cellToRoom.TryGetValue((room.GridX + dx, room.GridY + dy), out int other)
                    && !edges.Any(e => (e.a == room.Id && e.b == other) || (e.a == other && e.b == room.Id)))
                    extra.Add((room.Id, other));
            }
        rng.Shuffle(extra);
        int targetCorridors = Math.Max(edges.Count, p.BaseCorridors);
        foreach (var e in extra)
        {
            if (edges.Count >= targetCorridors) break;
            if (rng.Chance(Math.Max(0.05, p.Connectivity))) edges.Add(e);
        }
        foreach (var (a, b) in edges) AddCorridor(map, a, b);

        // 4. Entrance, then the boss room (if any) as far from it as possible.
        map.EntranceRoomId = 0;
        map.Room(0).Content = RoomContent.Entrance;
        var dist = map.Distances(map.EntranceRoomId);
        if (p.QuestType == "kill_boss")
        {
            // The farthest rooms; DD1's min_final_distance is met whenever the map is deep enough to allow it.
            int far = dist.Max();
            var boss = rng.Pick(map.Rooms.Where(r => r.Id != map.EntranceRoomId && dist[r.Id] == far).ToList());
            boss.Content = RoomContent.Boss;
            boss.IsQuestGoal = true;
            map.BossRoomId = boss.Id;
        }

        FillRooms(map, p, rng, props);
        FillHalls(map, p, rng, props);
        PlaceQuestCurios(map, goal, rng);
        return map;
    }

    /// <summary>Gather and activate quests need their curios (reliquaries, altars...): rooms first, then halls.</summary>
    private static void PlaceQuestCurios(DungeonMap map, Campaign.QuestGoal goal, Rng rng)
    {
        if (goal == null || string.IsNullOrEmpty(goal.CurioName) || goal.Amount <= 0) return;
        var rooms = map.Rooms.Where(r => r.Id != map.EntranceRoomId && r.Id != map.BossRoomId && !r.IsQuestGoal)
                             .OrderBy(r => r.Content == RoomContent.Empty ? 0 : r.HasBattle ? 1 : 2)
                             .ThenBy(_ => rng.Next(1000))
                             .ToList();
        int placed = 0;
        foreach (var r in rooms)
        {
            if (placed == goal.Amount) return;
            if (r.Content == RoomContent.Empty) r.Content = RoomContent.Curio;
            else if (r.Content == RoomContent.Battle) r.Content = RoomContent.GuardedCurio;
            else continue;   // keep treasures as they are
            r.CurioId = goal.CurioName;
            r.IsQuestGoal = true;
            placed++;
        }
        foreach (var t in map.AllTiles.Where(t => t.Content == HallContent.Empty || t.Content == HallContent.Curio).OrderBy(_ => rng.Next(1000)))
        {
            if (placed == goal.Amount) return;
            t.Content = HallContent.Curio;
            t.ContentId = goal.CurioName;
            t.IsQuestGoal = true;
            placed++;
        }
    }

    private static int AddRoom(DungeonMap map, Dictionary<(int, int), int> cellToRoom, (int x, int y) cell)
    {
        var room = new Room { Id = map.Rooms.Count, GridX = cell.x, GridY = cell.y };
        map.Rooms.Add(room);
        cellToRoom[cell] = room.Id;
        return room.Id;
    }

    /// <summary>
    /// Positions of grid lines: line i sits at i*spacing plus a nudge, and neighbouring lines stay at least
    /// two squares apart so every corridor keeps one hall tile or more.
    /// </summary>
    private static int[] NudgedLines(int count, int spacing, MapGenParams p, Rng rng)
    {
        var pos = new int[count];
        for (int i = 0; i < count; i++)
        {
            int span = rng.Roll(p.NudgeSpan);
            pos[i] = i * spacing + rng.Range(-span, span);
            if (i > 0 && pos[i] < pos[i - 1] + 2) pos[i] = pos[i - 1] + 2;
        }
        return pos;
    }

    /// <summary>Lay hall tiles on the straight line between two grid-adjacent rooms, rooms excluded.</summary>
    private static void AddCorridor(DungeonMap map, int a, int b)
    {
        var ra = map.Room(a);
        var rb = map.Room(b);
        var corridor = new Corridor { Id = map.Corridors.Count, RoomA = a, RoomB = b };

        int dx = Math.Sign(rb.X - ra.X), dy = Math.Sign(rb.Y - ra.Y);
        int steps = Math.Abs(rb.X - ra.X) + Math.Abs(rb.Y - ra.Y) - 1;
        for (int i = 0; i < steps; i++)
            corridor.Tiles.Add(new HallTile { Index = i, X = ra.X + dx * (i + 1), Y = ra.Y + dy * (i + 1) });

        map.Corridors.Add(corridor);
        ra.CorridorIds.Add(corridor.Id);
        rb.CorridorIds.Add(corridor.Id);
    }

    private static void FillRooms(DungeonMap map, MapGenParams p, Rng rng, ZoneProps props)
    {
        var free = map.Rooms.Where(r => r.Content == RoomContent.Empty).ToList();
        rng.Shuffle(free);

        // Room battles first; some of them guard a treasure or a curio.
        int battles = Math.Min(free.Count, rng.Roll(p.TotalRoomBattles));
        var battleRooms = free.Take(battles).ToList();
        var emptyRooms = free.Skip(battles).ToList();
        foreach (var r in battleRooms) r.Content = RoomContent.Battle;

        int guardedTreasure = Math.Min(battleRooms.Count, rng.Roll(p.RoomGuardedTreasure));
        foreach (var r in battleRooms.Take(guardedTreasure))
        {
            r.Content = RoomContent.GuardedTreasure;
            r.CurioId = props.Pick(ZoneProps.RoomTreasures, rng, "locked_strongbox");
        }
        int guardedCurio = Math.Min(battleRooms.Count - guardedTreasure, rng.Roll(p.RoomGuardedCurio));
        foreach (var r in battleRooms.Skip(guardedTreasure).Take(guardedCurio))
        {
            r.Content = RoomContent.GuardedCurio;
            r.CurioId = props.Pick(ZoneProps.RoomCurios, rng);
        }

        int treasures = Math.Min(emptyRooms.Count, rng.Roll(p.RoomTreasure));
        foreach (var r in emptyRooms.Take(treasures))
        {
            r.Content = RoomContent.Treasure;
            r.CurioId = props.Pick(ZoneProps.RoomTreasures, rng, "unlocked_strongbox");
        }
        int curios = Math.Min(emptyRooms.Count - treasures, rng.Roll(p.RoomCurio));
        foreach (var r in emptyRooms.Skip(treasures).Take(curios))
        {
            r.Content = RoomContent.Curio;
            r.CurioId = props.Pick(ZoneProps.RoomCurios, rng);
        }
    }

    private static void FillHalls(DungeonMap map, MapGenParams p, Rng rng, ZoneProps props)
    {
        var tiles = map.AllTiles.ToList();
        rng.Shuffle(tiles);
        int next = 0;

        void Place(HallContent content, int count, Func<string> id)
        {
            for (int i = 0; i < count && next < tiles.Count; i++, next++)
            {
                tiles[next].Content = content;
                tiles[next].ContentId = id?.Invoke();
            }
        }

        // Most important first, so a cramped map drops curios before it drops fights.
        Place(HallContent.Battle, rng.Roll(p.HallBattle), null);
        Place(HallContent.Trap, rng.Roll(p.HallTrap), () => props.Pick(ZoneProps.Traps, rng, "spikes"));
        Place(HallContent.Obstacle, rng.Roll(p.HallObstacle), () => props.Pick(ZoneProps.Obstacles, rng, "rubble"));
        Place(HallContent.Hunger, rng.Roll(p.HallHunger), null);
        Place(HallContent.Curio, rng.Roll(p.HallCurio), () => props.Pick(ZoneProps.HallCurios, rng, "crate"));
    }
}
