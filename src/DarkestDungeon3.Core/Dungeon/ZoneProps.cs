using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>
/// A zone's weighted prop tables from <c>dungeons/&lt;zone&gt;/&lt;zone&gt;.props.darkest</c>:
/// hall_curios, room_curios, room_treasures, traps, obstacles, prison_doors.
/// </summary>
public sealed class ZoneProps
{
    public const string HallCurios = "hall_curios";
    public const string RoomCurios = "room_curios";
    public const string RoomTreasures = "room_treasures";
    public const string Traps = "traps";
    public const string Obstacles = "obstacles";

    private readonly Dictionary<string, List<(float Weight, string Id)>> _tables = new();

    public static ZoneProps Empty { get; } = new();

    public static ZoneProps Load(string propsDarkestPath) => FromRecords(DarkestFile.Load(propsDarkestPath));

    public static ZoneProps FromRecords(IEnumerable<DarkestRecord> records)
    {
        var props = new ZoneProps();
        foreach (var r in records)
        {
            float weight = r.Float("chance", fallback: 1f);
            var types = r.Values("types");
            if (types.Count == 0) continue;
            if (!props._tables.TryGetValue(r.Type, out var list)) props._tables[r.Type] = list = new();
            // ".types a b" in a props table means "one of these"; split the weight between them.
            foreach (var id in types) list.Add((weight / types.Count, id));
        }
        return props;
    }

    public IReadOnlyList<(float Weight, string Id)> Table(string name) =>
        _tables.TryGetValue(name, out var t) ? t : new List<(float, string)>();

    public string Pick(string table, Rng rng, string fallback = null)
    {
        var t = Table(table);
        if (t.Count == 0) return fallback;
        double roll = rng.NextDouble() * t.Sum(e => e.Weight);
        foreach (var (w, id) in t)
        {
            roll -= w;
            if (roll < 0) return id;
        }
        return t[t.Count - 1].Id;
    }
}
