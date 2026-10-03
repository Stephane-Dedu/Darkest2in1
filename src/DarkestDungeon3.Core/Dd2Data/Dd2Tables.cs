using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd2Data;

/// <summary>A DD2 quirk as its data table describes it.</summary>
public sealed class Dd2Quirk
{
    public string Id;
    public List<string> Tags = new();
    public bool IsPositive => Tags.Contains("positive");
    public bool IsNegative => Tags.Contains("negative");
    public bool IsDisease => Tags.Contains("disease");
    /// <summary>DD2 rolls new heroes' quirks from these (pos_start / neg_start).</summary>
    public bool IsStarting => Tags.Contains("pos_start") || Tags.Contains("neg_start");
}

/// <summary>A DD2 trinket as its data table describes it.</summary>
public sealed class Dd2Trinket
{
    public string Id;
    public string Rarity;              // common, rare, epic, ancestral, cultist ...
    public List<string> Tags = new();
    /// <summary>The hero class it's made for (condition "performer_is_&lt;class&gt;"), or null.</summary>
    public string HeroClass;
    public bool IsForHero(string classId) => HeroClass == null || HeroClass == classId;
}

/// <summary>
/// DD2's own data tables (StreamingAssets/Excel/*.csv: blocks of "element_start,id,Type" ... "element_end").
/// DD2 builds its quirk and item libraries only inside a run; the Hamlet lives at the main menu, so it reads
/// the same tables directly.
/// </summary>
public sealed class Dd2Tables
{
    public Dictionary<string, Dd2Quirk> Quirks { get; } = new();
    public Dictionary<string, Dd2Trinket> Trinkets { get; } = new();

    public static Dd2Tables Load(string excelDir)
    {
        var t = new Dd2Tables();
        foreach (var (id, type, fields) in Blocks(Path.Combine(excelDir, "quirk_data_export.Group.csv")))
            if (type == "Quirk") t.Quirks[id] = new Dd2Quirk { Id = id, Tags = Values(fields, "m_Tags") };
        foreach (var (id, type, fields) in Blocks(Path.Combine(excelDir, "trinkets_data_export.Group.csv")))
        {
            if (type != "Item" || Values(fields, "m_type").FirstOrDefault() != "trinket") continue;
            const string prefix = "performer_is_";
            string cls = Values(fields, "m_conditionIds").FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));
            t.Trinkets[id] = new Dd2Trinket
            {
                Id = id,
                Rarity = Values(fields, "sub_type").FirstOrDefault() ?? "common",
                Tags = Values(fields, "m_tags"),
                HeroClass = cls?.Substring(prefix.Length),
            };
        }
        return t;
    }

    private static List<string> Values(Dictionary<string, List<string>> fields, string key) =>
        fields.TryGetValue(key, out var v) ? v : new List<string>();

    /// <summary>Each element block: its id, its type and its fields (key → values).</summary>
    public static IEnumerable<(string Id, string Type, Dictionary<string, List<string>> Fields)> Blocks(string path)
    {
        if (!File.Exists(path)) yield break;
        string id = null, type = null;
        Dictionary<string, List<string>> fields = null;
        foreach (var raw in File.ReadLines(path))
        {
            var cells = raw.Split(',');
            if (cells.Length == 0) continue;
            if (cells[0] == "element_start" && cells.Length >= 3)
            {
                id = cells[1];
                type = cells[2];
                fields = new Dictionary<string, List<string>>();
            }
            else if (cells[0] == "element_end")
            {
                if (id != null) yield return (id, type, fields);
                id = null;
            }
            else if (id != null && cells[0].Length > 0)
                fields[cells[0]] = cells.Skip(1).Where(c => c.Length > 0).ToList();
        }
    }
}
