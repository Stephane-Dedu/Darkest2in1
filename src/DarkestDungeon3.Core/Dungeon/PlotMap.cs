using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>
/// DD1's hand-made maps as a <see cref="DungeonMap"/>: the plot maps (<c>maps/&lt;name&gt;.dm</c>: the Darkest Dungeon's
/// parts, the town invasion, the crow's lair) and the new game's opening raid (<c>scripts/starting_save/persist.map.json</c>,
/// the same areas and squares written as JSON). The file fixes the layout, what is on each square and the named
/// battles; curios, traps and obstacles it doesn't name are drawn from the zone's prop tables, as DD1 does when it
/// populates the map.
/// </summary>
public static class PlotMap
{
    // A square's content code in the map file (cross-checked against DD1's own raid saves, which store the same code
    // next to the prop that was placed).
    public const int Battle = 1, Trap = 3, Obstacle = 4, RoomCurio = 6, HallCurio = 7, Hunger = 8, SecretRoom = 9, RoomTreasure = 10, SecretDoor = 13;

    /// <summary>The map name standing for the new game's opening raid (DD1's starting save).</summary>
    public const string Opening = "@opening";

    public static string PathOf(Dd1Install dd1, string name) =>
        name == Opening ? dd1.PathOf("scripts", "starting_save", "persist.map.json") : dd1.PathOf("maps", name + ".dm");

    public static bool Exists(Dd1Install dd1, string name) => !string.IsNullOrEmpty(name) && File.Exists(PathOf(dd1, name));

    public static DungeonMap Load(Dd1Install dd1, string name, string dungeon, string questType, int seed, ZoneProps props = null,
                                  string entranceArea = null, string goalArea = null) =>
        name == Opening
            ? FromJson(JToken.Parse(Regex.Replace(File.ReadAllText(PathOf(dd1, name)), @",(\s*[}\]])", "$1"))["data"]?["map"],
                       dd1, dungeon, questType, seed, props, entranceArea, goalArea)
            : From(Dd1Binary.Load(PathOf(dd1, name)), dungeon, questType, seed, props);

    // ---- the areas as read from either file ----

    private sealed class Square
    {
        public int Content;
        public string Mash, Prop, DoorTo;
        public float X, Y;
        public bool AlwaysAccessible;
    }

    private sealed class Area
    {
        public string Name;
        public int Kind;   // 0 room, 1 corridor
        public List<Square> Squares = new();
    }

    public static DungeonMap From(Dd1Binary file, string dungeon, string questType, int seed, ZoneProps props = null)
    {
        var root = file.Root["map"];
        var dynamicAreas = root.At("static_dynamic", "areas");
        var layout = root.At("static_dynamic", "static_save").Nested.Root["areas"];
        var nameOf = layout.Children.ToDictionary(a => a["id"].Int, a => a.Name);
        var areas = layout.Children.Select(a =>
        {
            var area = new Area { Name = a.Name, Kind = a["kind"].Int };
            var states = dynamicAreas[a.Name]?["tiles"];
            foreach (var t in a["tiles"].Children)
            {
                var state = states?[t.Name];
                var (x, y) = t["mappos"].Vector;
                string mash = state?["mash_name"]?.String;
                area.Squares.Add(new Square
                {
                    Content = state?["content"]?.Int ?? 0,
                    Mash = string.IsNullOrEmpty(mash) ? null : mash,
                    X = x, Y = y,
                    DoorTo = nameOf.TryGetValue(t.At("door_to", "area_to")?.Int ?? 0, out var n) ? n : null,
                    AlwaysAccessible = t["hd_always_accessible"]?.Bool ?? false,
                });
            }
            return area;
        }).ToList();
        string entrance = nameOf.TryGetValue(root["entrance_id"].Int, out var e) ? e : null;
        string final = nameOf.TryGetValue(root["final_room_id"].Int, out var f) ? f : null;
        return Build(areas, entrance, final, dungeon, questType, seed, props);
    }

    /// <summary>
    /// The opening raid's map (persist.map.json): area names instead of hashed ids, a square's battle as
    /// <c>mash_index</c> (the zone's <c>tutorial_N</c> named rows, N = index + 1) and its curio as <c>cur</c> (DD1's string
    /// hash of the curio's name). Its entrance and final room come from the raid (in_area, the goal's room).
    /// </summary>
    private static DungeonMap FromJson(JToken map, Dd1Install dd1, string dungeon, string questType, int seed, ZoneProps props,
                                       string entranceArea, string goalArea)
    {
        var curios = CurioNames(dd1);
        var areas = new List<Area>();
        foreach (var p in (map?["areas"] as JObject ?? new JObject()).Properties())
        {
            var area = new Area { Name = p.Name, Kind = (int?)p.Value["kind"] ?? 0 };
            foreach (var t in (p.Value["tiles"] as JObject ?? new JObject()).Properties().OrderBy(t => TileIndex(t.Name)).Select(t => t.Value))
            {
                int mashIndex = (int?)t["mash_index"] ?? -1;
                uint cur = (uint?)(long?)t["cur"] ?? 0u;
                string door = (string)t["door_to"]?["area_to"];
                area.Squares.Add(new Square
                {
                    Content = (int?)t["content"] ?? 0,
                    Mash = mashIndex >= 0 ? "tutorial_" + (mashIndex + 1) : null,
                    Prop = cur != 0 && curios.TryGetValue(cur, out var curio) ? curio : null,
                    X = (float?)t["mappos"]?["x"] ?? 0f, Y = (float?)t["mappos"]?["y"] ?? 0f,
                    DoorTo = door == "none" ? null : door,
                });
            }
            areas.Add(area);
        }
        return Build(areas, entranceArea ?? areas.FirstOrDefault(a => a.Kind == 0)?.Name, goalArea, dungeon, questType, seed, props, goalIsQuest: goalArea != null);
    }

    private static int TileIndex(string name) => int.TryParse(name.Replace("tile", ""), out var i) ? i : 0;

    /// <summary>DD1's curio names by their string hash (props/shared/curios).</summary>
    private static Dictionary<uint, string> CurioNames(Dd1Install dd1)
    {
        var names = new Dictionary<uint, string>();
        string dir = dd1.PathOf("props", "shared", "curios");
        if (Directory.Exists(dir))
            foreach (var d in Directory.GetDirectories(dir)) names[Dd1Binary.Hash(Path.GetFileName(d))] = Path.GetFileName(d);
        return names;
    }

    private static DungeonMap Build(List<Area> areas, string entrance, string final, string dungeon, string questType, int seed, ZoneProps props,
                                    bool goalIsQuest = false)
    {
        props ??= ZoneProps.Empty;
        var rng = new Rng(seed);
        var map = new DungeonMap { Seed = seed, Dungeon = dungeon, Size = "plot", QuestType = questType };

        // Append hidden branch rooms after normal rooms, preserving existing IDs and normal prop-roll order.
        var roomOf = new Dictionary<string, Room>();
        foreach (var area in areas.Where(a => a.Kind == 0).OrderBy(a => a.Squares.FirstOrDefault()?.Content == SecretRoom))
        {
            var tile = area.Squares.FirstOrDefault();
            int code = tile?.Content ?? 0;
            var room = new Room { Id = map.Rooms.Count, X = (int)Math.Floor(tile?.X ?? 0f), Y = (int)Math.Floor(tile?.Y ?? 0f), IsSecret = code == SecretRoom };
            room.GridX = room.X;
            room.GridY = room.Y;
            room.MashName = tile?.Mash;
            bool guarded = room.MashName != null;
            switch (code)
            {
                case Battle: room.Content = RoomContent.Battle; break;
                case RoomCurio:
                case HallCurio:   // the town invasion puts a curio in a room with the hall code
                    room.Content = guarded ? RoomContent.GuardedCurio : RoomContent.Curio;
                    room.CurioId = tile?.Prop ?? props.Pick(ZoneProps.RoomCurios, rng);
                    break;
                case RoomTreasure:
                    room.Content = guarded ? RoomContent.GuardedTreasure : RoomContent.Treasure;
                    room.CurioId = tile?.Prop ?? props.Pick(ZoneProps.RoomTreasures, rng, "unlocked_strongbox");
                    break;
                default: room.Content = guarded ? RoomContent.Battle : RoomContent.Empty; break;
            }
            roomOf[area.Name] = room;
            map.Rooms.Add(room);
        }

        Room RoomAt(Square s) => s?.DoorTo != null && roomOf.TryGetValue(s.DoorTo, out var r) ? r : null;

        // Corridors: their end squares are the doors to the two rooms; squares run from the first to the second.
        foreach (var area in areas.Where(a => a.Kind == 1))
        {
            var a = RoomAt(area.Squares.FirstOrDefault());
            var b = RoomAt(area.Squares.LastOrDefault());
            if (a == null || b == null || a == b) continue;
            var corridor = new Corridor { Id = map.Corridors.Count, RoomA = a.Id, RoomB = b.Id };
            for (int i = 0; i < area.Squares.Count; i++)
            {
                var s = area.Squares[i];
                var hall = new HallTile { Index = i, X = (int)Math.Floor(s.X), Y = (int)Math.Floor(s.Y), MashName = s.Mash };
                if (s.Content == SecretDoor && RoomAt(s) is { IsSecret: true } secret)
                {
                    hall.SecretRoomId = secret.Id;
                    hall.SecretDoorAlwaysAccessible = s.AlwaysAccessible;
                }
                switch (s.Content)
                {
                    case Battle: hall.Content = HallContent.Battle; break;
                    case Trap: hall.Content = HallContent.Trap; hall.ContentId = props.Pick(ZoneProps.Traps, rng, "spikes"); break;
                    case Obstacle: hall.Content = HallContent.Obstacle; hall.ContentId = props.Pick(ZoneProps.Obstacles, rng, "rubble"); break;
                    case HallCurio: hall.Content = HallContent.Curio; hall.ContentId = s.Prop ?? props.Pick(ZoneProps.HallCurios, rng, "crate"); break;
                    case Hunger: hall.Content = HallContent.Hunger; break;
                    default: hall.Content = hall.MashName != null ? HallContent.Battle : HallContent.Empty; break;
                }
                corridor.Tiles.Add(hall);
            }
            map.Corridors.Add(corridor);
            a.CorridorIds.Add(corridor.Id);
            b.CorridorIds.Add(corridor.Id);
        }

        // Entrance and final room (the boss's lair for a boss quest, the goal room for the opening raid).
        if (entrance != null && roomOf.TryGetValue(entrance, out var start))
        {
            map.EntranceRoomId = start.Id;
            if (start.Content == RoomContent.Empty) start.Content = RoomContent.Entrance;
        }
        // A boss quest on a map naming no final room (the crow's lair: a single room): the room holding a set fight.
        if (final == null && questType == "kill_boss")
            final = roomOf.Where(r => r.Value.MashName != null).Select(r => r.Key).LastOrDefault() ?? (roomOf.Count == 1 ? roomOf.Keys.First() : null);
        if (final != null && roomOf.TryGetValue(final, out var last))
        {
            if (questType == "kill_boss")
            {
                last.Content = RoomContent.Boss;
                last.IsQuestGoal = true;
                map.BossRoomId = last.Id;
            }
            else if (goalIsQuest) last.IsQuestGoal = true;
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
}
