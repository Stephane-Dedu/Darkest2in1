using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>
/// DD1's hand-made plot maps (<c>maps/&lt;name&gt;.dm</c>: the Darkest Dungeon's parts, the town invasion, the crow's
/// lair) as a <see cref="DungeonMap"/>. The file fixes the layout, what is on each square and the named battles; the
/// curios, traps and obstacles themselves are drawn from the zone's prop tables, as DD1 does when it populates the map.
/// </summary>
public static class PlotMap
{
    // A square's content code in the map file (cross-checked against DD1's own raid saves, which store the same code
    // next to the prop that was placed).
    public const int Battle = 1, Trap = 3, Obstacle = 4, RoomCurio = 6, HallCurio = 7, Hunger = 8, SecretRoom = 9, RoomTreasure = 10, SecretDoor = 13;

    public static string PathOf(Dd1Install dd1, string name) => dd1.PathOf("maps", name + ".dm");

    public static bool Exists(Dd1Install dd1, string name) => !string.IsNullOrEmpty(name) && File.Exists(PathOf(dd1, name));

    public static DungeonMap Load(Dd1Install dd1, string name, string dungeon, string questType, int seed, ZoneProps props = null) =>
        From(Dd1Binary.Load(PathOf(dd1, name)), dungeon, questType, seed, props);

    public static DungeonMap From(Dd1Binary file, string dungeon, string questType, int seed, ZoneProps props = null)
    {
        props ??= ZoneProps.Empty;
        var rng = new Rng(seed);
        var root = file.Root["map"];
        var areas = root.At("static_dynamic", "areas");
        var layout = root.At("static_dynamic", "static_save").Nested.Root["areas"];
        var nameOf = layout.Children.ToDictionary(a => a["id"].Int, a => a.Name);
        var map = new DungeonMap { Seed = seed, Dungeon = dungeon, Size = "plot", QuestType = questType };

        // Rooms. Secret rooms open off a corridor square, not a corridor end: the map has no way to walk there yet.
        var roomOf = new Dictionary<string, Room>();
        foreach (var area in layout.Children.Where(a => a["kind"].Int == 0))
        {
            var tile = areas[area.Name]?.At("tiles", "tile0");
            int code = tile?["content"]?.Int ?? 0;
            if (code == SecretRoom) continue;
            var (x, y) = area.At("tiles", "tile0", "mappos").Vector;
            var room = new Room { Id = map.Rooms.Count, X = (int)Math.Round(x), Y = (int)Math.Round(y) };
            room.GridX = room.X;
            room.GridY = room.Y;
            room.MashName = Mash(tile);
            bool guarded = room.MashName != null;
            switch (code)
            {
                case Battle: room.Content = RoomContent.Battle; break;
                case RoomCurio:
                case HallCurio:   // the town invasion puts a curio in a room with the hall code
                    room.Content = guarded ? RoomContent.GuardedCurio : RoomContent.Curio;
                    room.CurioId = props.Pick(ZoneProps.RoomCurios, rng);
                    break;
                case RoomTreasure:
                    room.Content = guarded ? RoomContent.GuardedTreasure : RoomContent.Treasure;
                    room.CurioId = props.Pick(ZoneProps.RoomTreasures, rng, "unlocked_strongbox");
                    break;
                default: room.Content = guarded ? RoomContent.Battle : RoomContent.Empty; break;
            }
            roomOf[area.Name] = room;
            map.Rooms.Add(room);
        }

        Room RoomAt(Dd1Binary.Node tile) =>
            tile != null && nameOf.TryGetValue(tile.At("door_to", "area_to").Int, out var n) && roomOf.TryGetValue(n, out var r) ? r : null;

        // Corridors: their end squares are the doors to the two rooms; squares run from the first to the second.
        foreach (var area in layout.Children.Where(a => a["kind"].Int == 1))
        {
            var squares = area["tiles"].Children;
            var a = RoomAt(squares.FirstOrDefault());
            var b = RoomAt(squares.LastOrDefault());
            if (a == null || b == null || a == b) continue;
            var corridor = new Corridor { Id = map.Corridors.Count, RoomA = a.Id, RoomB = b.Id };
            var dynamic = areas[area.Name]?["tiles"];
            for (int i = 0; i < squares.Count; i++)
            {
                var (x, y) = squares[i]["mappos"].Vector;
                var state = dynamic?[squares[i].Name];
                var hall = new HallTile { Index = i, X = (int)Math.Round(x), Y = (int)Math.Round(y), MashName = Mash(state) };
                switch (state?["content"]?.Int ?? 0)
                {
                    case Battle: hall.Content = HallContent.Battle; break;
                    case Trap: hall.Content = HallContent.Trap; hall.ContentId = props.Pick(ZoneProps.Traps, rng, "spikes"); break;
                    case Obstacle: hall.Content = HallContent.Obstacle; hall.ContentId = props.Pick(ZoneProps.Obstacles, rng, "rubble"); break;
                    case HallCurio: hall.Content = HallContent.Curio; hall.ContentId = props.Pick(ZoneProps.HallCurios, rng, "crate"); break;
                    case Hunger: hall.Content = HallContent.Hunger; break;
                    default: hall.Content = hall.MashName != null ? HallContent.Battle : HallContent.Empty; break;
                }
                corridor.Tiles.Add(hall);
            }
            map.Corridors.Add(corridor);
            a.CorridorIds.Add(corridor.Id);
            b.CorridorIds.Add(corridor.Id);
        }

        // Entrance and final room (the boss's lair for a boss quest).
        if (nameOf.TryGetValue(root["entrance_id"].Int, out var entrance) && roomOf.TryGetValue(entrance, out var start))
        {
            map.EntranceRoomId = start.Id;
            if (start.Content == RoomContent.Empty) start.Content = RoomContent.Entrance;
        }
        if (nameOf.TryGetValue(root["final_room_id"].Int, out var final) && roomOf.TryGetValue(final, out var last) && questType == "kill_boss")
        {
            last.Content = RoomContent.Boss;
            last.IsQuestGoal = true;
            map.BossRoomId = last.Id;
        }
        return map;
    }

    /// <summary>
    /// An activate/gather goal's curios on a hand-made map (DD_map2's three beacons, DD_map3's teleporter). The map file
    /// doesn't mark them: they go in the curio rooms (code 6) held by a set fight of their own (a miniboss or the
    /// teleporter's guards, not one of the quest's numbered <c>_mash_NN</c> rows), as the Unity port's copies of these maps
    /// have them; then the curio rooms farthest from the entrance.
    /// </summary>
    public static void PlaceGoal(DungeonMap map, Campaign.QuestGoal goal)
    {
        if (goal == null || string.IsNullOrEmpty(goal.CurioName) || goal.Amount <= 0) return;
        var dist = map.Distances(map.EntranceRoomId);
        var rooms = map.Rooms.Where(r => r.Content is RoomContent.Curio or RoomContent.GuardedCurio && r.Id != map.EntranceRoomId)
                             .OrderBy(r => r.MashName != null && !NumberedMash(r.MashName) ? 0 : 1)
                             .ThenByDescending(r => dist[r.Id])
                             .ThenBy(r => r.Id)
                             .Take(goal.Amount);
        foreach (var r in rooms)
        {
            r.CurioId = goal.CurioName;
            r.IsQuestGoal = true;
        }
    }

    private static bool NumberedMash(string name)
    {
        int i = name.LastIndexOf("_mash_", StringComparison.Ordinal);
        return i >= 0 && name.Substring(i + 6).All(char.IsDigit);
    }

    private static string Mash(Dd1Binary.Node tile)
    {
        var name = tile?["mash_name"]?.String;
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
