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

/// <summary>A DD2 hero class's base numbers (its ActorDataStats block).</summary>
public sealed class Dd2ClassStats
{
    public string Id;
    public Dictionary<string, float> Stats = new();         // health_max, speed, stress_max, deaths_door_chance ...
    public Dictionary<string, float> Resistances = new();   // stun, blight, bleed, burn, disease, move, debuff, death
    public float Get(string key) => Stats.TryGetValue(key, out var v) ? v : 0f;
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
    public Dictionary<string, Dd2ClassStats> Classes { get; } = new();
    /// <summary>Every DD2 skill (ActorDataSkill), and each actor class's skills (those defined in its data file).</summary>
    public Dictionary<string, Dd1.SkillShape> Skills { get; } = new();
    public Dictionary<string, List<string>> ActorSkills { get; } = new();
    /// <summary>How many ranks each actor class takes (m_Size).</summary>
    public Dictionary<string, int> ActorSizes { get; } = new();

    /// <summary>The skills of a DD2 actor class.</summary>
    public List<Dd1.SkillShape> SkillsOf(string actorClass) =>
        actorClass != null && ActorSkills.TryGetValue(actorClass, out var ids)
            ? ids.Where(Skills.ContainsKey).Select(i => Skills[i]).ToList()
            : new List<Dd1.SkillShape>();

    public static Dd2Tables Load(string excelDir)
    {
        var t = new Dd2Tables();
        foreach (var file in Directory.Exists(excelDir) ? Directory.GetFiles(excelDir, "hero_*_data_export.Group.csv") : new string[0])
            foreach (var (id, type, _, lines) in BlockLines(file))
            {
                if (type != "ActorDataStats" || id.EndsWith("_corpse")) continue;
                var c = new Dd2ClassStats { Id = id };
                string[] keys = null;
                foreach (var line in lines)
                {
                    if (line[0] == "key_map") keys = line.Skip(1).ToArray();
                    else if (line[0] == "add_stats" && keys != null)
                        for (int i = 0; i < keys.Length && i + 1 < line.Length; i++) c.Stats[keys[i]] = Num(line[i + 1]);
                    else if (line[0] == "sub_stat" && line.Length >= 4 && line[1] == "resistance") c.Resistances[line[2]] = Num(line[3]);
                }
                if (c.Stats.ContainsKey("health_max")) t.Classes[id] = c;
            }
        // Every actor data file: its classes and skills (an enemy's skills live in the same file as the enemy).
        foreach (var file in Directory.Exists(excelDir) ? Directory.GetFiles(excelDir, "*_data_export.Group.csv") : new string[0])
        {
            var classes = new List<string>();
            var skills = new List<string>();
            foreach (var (id, type, fields) in Blocks(file))
            {
                if (type == "ActorDataClass" && !id.EndsWith("_corpse", StringComparison.Ordinal))
                {
                    classes.Add(id);
                    if (int.TryParse(Values(fields, "m_Size").FirstOrDefault(), out int size)) t.ActorSizes[id] = size;
                }
                else if (type == "ActorDataSkill")
                {
                    skills.Add(id);
                    t.Skills[id] = new Dd1.SkillShape
                    {
                        Id = id,
                        Ranged = Values(fields, "m_Tags").Contains("ranged"),
                        Friendly = Values(fields, "m_IsFriendly").FirstOrDefault() == "True",
                        LaunchRanks = Values(fields, "launch_ranks").Select(v => int.TryParse(v, out var r) ? r : 0).Where(r => r > 0).ToList(),
                        TargetRanks = Values(fields, "target_ranks").Select(v => int.TryParse(v, out var r) ? r : 0).Where(r => r > 0).ToList(),
                    };
                }
            }
            // A class can also appear in other files (hero story fights); its own file ("lost_battalion_foot soldier_...") wins.
            string own = Path.GetFileName(file).Replace("_data_export.Group.csv", "").Replace(' ', '_');
            foreach (var c in classes)
                if (c == own || !t.ActorSkills.ContainsKey(c)) t.ActorSkills[c] = skills;
        }
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

    private static float Num(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f;

    /// <summary>Each element block with every line (repeated keys like sub_stat kept), cells without empties.</summary>
    public static IEnumerable<(string Id, string Type, Dictionary<string, List<string>> Fields, List<string[]> Lines)> BlockLines(string path)
    {
        if (!File.Exists(path)) yield break;
        string id = null, type = null;
        List<string[]> lines = null;
        foreach (var raw in File.ReadLines(path))
        {
            var cells = raw.Split(',');
            if (cells[0] == "element_start" && cells.Length >= 3) { id = cells[1]; type = cells[2]; lines = new List<string[]>(); }
            else if (cells[0] == "element_end")
            {
                if (id != null)
                {
                    var fields = new Dictionary<string, List<string>>();
                    foreach (var l in lines) fields[l[0]] = l.Skip(1).ToList();
                    yield return (id, type, fields, lines);
                }
                id = null;
            }
            else if (id != null && cells[0].Length > 0) lines.Add(cells.Where(c => c.Length > 0).ToArray());
        }
    }

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
