using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// The pictures a DD1 zone's dungeon is drawn with, as the zone really has them: corridor walls are numbered
/// differently per zone (the Ruins 00-06, the Weald 01-10, the Warrens without 04...) and the Darkest Dungeon keeps
/// one set per quest (dungeons/darkestdungeon/quest_N). Full paths; null where the zone has none.
/// </summary>
public sealed class ZoneArt
{
    public string Dir;
    public readonly List<string> Walls = new();
    public readonly List<string> Rooms = new();
    public readonly List<string> FinalRooms = new();
    public string Door, EndHall, Background, Mid, ForegroundTop, ForegroundBottom, Entrance;

    public static ZoneArt Load(Dd1Install dd1, string zone, int darkestQuest = 1)
    {
        var art = new ZoneArt { Dir = dd1.ZoneDir(zone) };
        if (Directory.Exists(art.Dir) && !Directory.GetFiles(art.Dir, "*.png").Any())
        {
            // The Darkest Dungeon: one folder per quest.
            string quest = Path.Combine(art.Dir, "quest_" + Math.Max(1, darkestQuest));
            art.Dir = Directory.Exists(quest) ? quest : Directory.GetDirectories(art.Dir, "quest_*").OrderBy(d => d).FirstOrDefault() ?? art.Dir;
        }
        if (!Directory.Exists(art.Dir)) return art;
        var files = Directory.GetFiles(art.Dir, "*.png").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToList();
        string Full(string f) => f == null ? null : Path.Combine(art.Dir, f);
        string First(params string[] patterns)
        {
            foreach (var p in patterns)
            {
                var hit = files.FirstOrDefault(f => Matches(f, p));
                if (hit != null) return Full(hit);
            }
            return null;
        }
        art.Walls.AddRange(files.Where(f => Matches(f, "*.corridor_wall.*.png")).Select(Full));
        art.Rooms.AddRange(files.Where(f => Matches(f, "*.room_wall.*.png") && !f.Contains(".entrance.")).Select(Full));
        art.FinalRooms.AddRange(files.Where(f => Matches(f, "*.final_room_wall*.png")).Select(Full));
        art.Door = First("*.corridor_door.basic.png", "*.corridor_door*.png");
        art.EndHall = First("*.endhall.01.png", "*.endhall*.png");
        art.Background = First("*.corridor_bg.png", "*.corridor_bg*.png");
        art.Mid = First("*.corridor_mid.png", "*.corridor_mid*.png");
        art.ForegroundTop = First("*.foreground_top.01.png", "*.foreground_top*.png");
        art.ForegroundBottom = First("*.foreground_bottom.01.png", "*.foreground_bottom*.png");
        art.Entrance = First("*.entrance_room_wall.png", "*.room_wall.entrance.png");
        return art;
    }

    /// <summary>The wall for a hallway square (any index; picked stably from the zone's walls).</summary>
    public string Wall(int index) => Walls.Count == 0 ? null : Walls[((index % Walls.Count) + Walls.Count) % Walls.Count];

    /// <summary>The backdrop of a room (picked stably per room).</summary>
    public string Room(int roomId) => Rooms.Count == 0 ? null : Rooms[((roomId * 7 + 3) % Rooms.Count + Rooms.Count) % Rooms.Count];

    /// <summary>DD1 prefers a plot-specific final wall, then its generic final wall. Some bosses have neither;
    /// Necromancer uses the Ruins library as an explicit mod choice, rather than inheriting an entrance/hall.</summary>
    public string BossRoom(string bossId, string plotId = null, int seed = 0)
    {
        string Pick(string suffix) => FinalRooms.FirstOrDefault(p => Path.GetFileName(p).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        string family = bossId;
        if (family?.Length > 2 && family[family.Length - 2] == '_') family = family.Substring(0, family.Length - 2);
        string wall = string.IsNullOrEmpty(plotId) ? null : Pick(".final_room_wall." + plotId + ".png");
        wall ??= string.IsNullOrEmpty(bossId) ? null : Pick(".final_room_wall." + bossId + ".png");
        wall ??= string.IsNullOrEmpty(family) ? null : Pick(".final_room_wall." + family + ".png");
        wall ??= Pick(".final_room_wall.png");
        if (wall != null) return wall;
        if (family == "necromancer")
        {
            wall = Rooms.FirstOrDefault(p => Path.GetFileName(p) == "crypts.room_wall.library.png");
            if (wall != null) return wall;
        }
        return Room(seed);
    }

    /// <summary>File-name glob with '*' only.</summary>
    private static bool Matches(string name, string pattern)
    {
        var parts = pattern.Split('*');
        int at = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            int found = name.IndexOf(parts[i], at, StringComparison.OrdinalIgnoreCase);
            if (found < 0 || (i == 0 && found != 0)) return false;
            at = found + parts[i].Length;
        }
        return parts[parts.Length - 1].Length == 0 || name.EndsWith(parts[parts.Length - 1], StringComparison.OrdinalIgnoreCase);
    }
}
