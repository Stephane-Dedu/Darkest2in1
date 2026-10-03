using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

public sealed class CampEffect
{
    public string Selection;   // self, individual, party, party_other
    public string Type;        // stress_heal_amount, health_heal_max_health_percent, buff, remove_poison...
    public string SubType;     // buff id for "buff"
    public float Amount;
    public float Chance = 1f;
}

public sealed class CampSkill
{
    public string Id;
    public int Cost;
    public int UseLimit;
    public List<CampEffect> Effects = new();
    public List<string> Classes = new();
    public int TeachCost;      // gold at the Survivalist
    public bool NeedsTarget => Effects.Any(e => e.Selection == "individual");
}

/// <summary>DD1 camping skills (<c>raid/camping/*.camping_skills.json</c>, including DLC classes).</summary>
public sealed class CampingSkills
{
    /// <summary>DD2 classes with no DD1 namesake borrow a DD1 class's camp skills.</summary>
    public static readonly Dictionary<string, string> Dd1ClassFor = new()
    {
        ["runaway"] = "arbalest",
    };

    public Dictionary<string, CampSkill> Skills { get; } = new();

    /// <summary>DD1: a skill listed for this many classes or more counts as shared by everyone.</summary>
    public int SharedThreshold { get; private set; } = 4;

    public static CampingSkills Load(Dd1Install dd1)
    {
        var lib = new CampingSkills();
        var files = new List<string> { dd1.PathOf("raid", "camping", "default.camping_skills.json") };
        var dlc = dd1.PathOf("dlc");
        if (Directory.Exists(dlc)) files.AddRange(Directory.GetFiles(dlc, "*.camping_skills.json", SearchOption.AllDirectories));

        foreach (var file in files.Where(File.Exists))
        {
            var root = JToken.Parse(File.ReadAllText(file));
            if (root["configuration"]?["class_specific_number_of_classes_threshold"] is JValue t) lib.SharedThreshold = (int)t;
            foreach (var s in root["skills"] ?? new JArray())
            {
                string id = (string)s["id"];
                if (!lib.Skills.TryGetValue(id, out var skill))
                {
                    skill = new CampSkill
                    {
                        Id = id,
                        Cost = (int?)s["cost"] ?? 0,
                        UseLimit = (int?)s["use_limit"] ?? 1,
                        TeachCost = (int?)s["upgrade_requirements"]?.First?["currency_cost"]?.First?["amount"] ?? 1750,
                        Effects = s["effects"].Select(e => new CampEffect
                        {
                            Selection = (string)e["selection"],
                            Type = (string)e["type"],
                            SubType = (string)e["sub_type"],
                            Amount = (float?)e["amount"] ?? 0f,
                            Chance = (float?)e["chance"]?["amount"] ?? 1f,
                        }).ToList(),
                    };
                    lib.Skills[id] = skill;
                }
                // DLC files add classes to shared skills (e.g. the Flagellant to "encourage").
                foreach (var c in s["hero_classes"] ?? new JArray())
                    if (!skill.Classes.Contains((string)c)) skill.Classes.Add((string)c);
            }
        }
        lib.LoadBuffs(dd1);
        return lib;
    }

    // ---- what the skills do, in words (DD1's camping buffs live in shared/buffs/*.json) ----

    private readonly Dictionary<string, (string Stat, string Sub, float Amount, int Duration)> _buffs = new();

    private void LoadBuffs(Dd1Install dd1)
    {
        string dir = dd1.PathOf("shared", "buffs");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            foreach (var b in JToken.Parse(File.ReadAllText(file))["buffs"] ?? new JArray())
            {
                string id = (string)b["id"];
                if (id == null || !id.StartsWith("camping", System.StringComparison.OrdinalIgnoreCase)) continue;
                _buffs[id.ToLowerInvariant()] = ((string)b["stat_type"] ?? "", (string)b["stat_sub_type"] ?? "", (float?)b["amount"] ?? 0f, (int?)b["duration"] ?? 0);
            }
    }

    private static string Pct(float f) => (f >= 0 ? "+" : "") + (f * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
    private static string Num(float f) => f.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A DD1 camping buff in words ("+20% damage (ranged)"), or null if unknown.</summary>
    public string BuffText(string buffId)
    {
        if (buffId == null || !_buffs.TryGetValue(buffId.ToLowerInvariant(), out var b)) return null;
        string what = (b.Stat, b.Sub) switch
        {
            ("combat_stat_add", "attack_rating") => $"{Pct(b.Amount)} accuracy",
            ("combat_stat_multiply", "damage_low") or ("combat_stat_multiply", "damage_high") => $"{Pct(b.Amount)} damage",
            ("combat_stat_add", "crit_chance") => $"{Pct(b.Amount)} crit",
            ("combat_stat_add", "defense_rating") => $"{Pct(b.Amount)} dodge",
            ("combat_stat_add", "protection_rating") => $"{Pct(b.Amount)} protection",
            ("combat_stat_multiply", "max_hp") => $"{Pct(b.Amount)} max HP",
            ("combat_stat_add", "speed_rating") => $"{(b.Amount >= 0 ? "+" : "")}{Num(b.Amount)} speed",
            ("stress_dmg_received_percent", _) => $"{Pct(b.Amount)} stress taken",
            ("hp_heal_received_percent", _) => $"{Pct(b.Amount)} healing received",
            ("scouting_chance", _) => $"{Pct(b.Amount)} scouting",
            ("party_surprise_chance", _) => $"{Pct(b.Amount)} chance the party is surprised",
            ("monsters_surprise_chance", _) => $"{Pct(b.Amount)} chance to surprise monsters",
            ("resistance", "poison") => $"{Pct(b.Amount)} blight resist",
            ("resistance", _) => $"{Pct(b.Amount)} {b.Sub} resist",
            _ => null,
        };
        if (what == null) return null;
        string id = buffId.ToLowerInvariant();
        if (id.EndsWith("largemonsters")) what += " vs large foes";
        else if (id.EndsWith("notfrontrank")) what += " if not in front";
        else if (id.EndsWith("frontrank")) what += " in the front rank";
        else if (id.EndsWith("ranged")) what += " (ranged)";
        else if (id.EndsWith("melee")) what += " (melee)";
        return what;
    }

    /// <summary>One effect in words: "-1.5 stress (ally)", "+20% damage (self)". DD1 stress counts in tenths here.</summary>
    public string Describe(CampEffect e)
    {
        string what = e.Type switch
        {
            "stress_heal_amount" => $"-{Num(e.Amount / 10f)} stress",
            "stress_damage_amount" => $"+{Num(e.Amount / 10f)} stress",
            "health_heal_max_health_percent" => $"heal {Num(e.Amount * 100f)}% HP",
            "buff" => BuffText(e.SubType) ?? e.SubType,
            "remove_bleeding" or "remove_bleed" => "cure bleeding",
            "remove_poison" => "cure blight",
            "remove_disease" => "cure a disease",
            "remove_deaths_door_recovery_buffs" => "shake off death's door",
            "reduce_ambush_chance" => "no ambush tonight",
            "loot" => "find supplies",
            "reduce_torch" => "the torch burns down",
            _ => e.Type.Replace('_', ' '),
        };
        string who = e.Selection switch { "self" => "self", "individual" => "ally", "party" => "party", "party_other" => "others", _ => e.Selection };
        return (e.Chance < 1f ? $"{Num(e.Chance * 100f)}%: " : "") + what + $" ({who})";
    }

    /// <summary>All of a skill's effects in words, paired damage-low/high buffs merged, plus how long buffs last.</summary>
    public List<string> DescribeAll(CampSkill skill)
    {
        var lines = new List<string>();
        if (skill == null) return lines;
        foreach (var e in skill.Effects)
        {
            string line = Describe(e);
            if (!lines.Contains(line)) lines.Add(line);
        }
        int battles = skill.Effects.Where(e => e.Type == "buff" && e.SubType != null && _buffs.ContainsKey(e.SubType.ToLowerInvariant()))
                                   .Select(e => _buffs[e.SubType.ToLowerInvariant()].Duration).DefaultIfEmpty(0).Max();
        if (battles > 0) lines.Add($"(buffs last {battles} battles)");
        return lines;
    }

    public CampSkill Get(string id) => id != null && Skills.TryGetValue(id, out var s) ? s : null;

    /// <summary>The camp skills a DD2 class can learn, via its DD1 namesake.</summary>
    public List<string> ForClass(string dd2ClassId)
    {
        string dd1Class = Dd1ClassFor.TryGetValue(dd2ClassId, out var mapped) ? mapped : dd2ClassId;
        return Skills.Values
            .Where(s => s.Classes.Contains(dd1Class) || s.Classes.Count >= SharedThreshold)
            .Select(s => s.Id).ToList();
    }

    /// <summary>DD1 heroes start knowing four of their class's camp skills.</summary>
    public List<string> Starting(string dd2ClassId, Rng rng)
    {
        var all = ForClass(dd2ClassId);
        rng.Shuffle(all);
        return all.Take(4).ToList();
    }
}

public enum Meal { None, Half, Full, Feast }

/// <summary>A camp in progress (DD1: firewood in a cleared room, 12 respite points).</summary>
public sealed class CampState
{
    public int RespiteLeft;
    public bool Ate;
    public float AmbushReduction;
    public Dictionary<string, int> Uses = new();      // "heroId:skillId" -> times used
    /// <summary>DD1 camp buffs per hero, applied as DD2 tokens at the next fights.</summary>
    public Dictionary<string, List<string>> Buffs = new();
}
