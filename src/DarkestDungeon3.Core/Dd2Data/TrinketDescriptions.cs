using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Core.Dd2Data;

/// <summary>Menu-safe descriptions from the same CSV stats and localization templates DD2 uses.
/// Native ItemDescription remains preferred once its libraries exist. No game library is initialized here.</summary>
public sealed class TrinketDescriptions
{
    private readonly Dictionary<(string, string), List<string[]>> _blocks = new();
    private readonly Dictionary<string, string> _english = new();
    private static readonly HashSet<string> ValueStats = new()
    {
        "speed", "speed_tie_breaker", "speed_number_of_turns", "health_damage", "health_damage_range",
        "health_damage_dealt_mult_percent", "health_max", "stress_max", "dot_extra_duration_dealt",
        "dot_extra_duration_received", "dot_effect_value_dealt_change", "dot_effect_value_received_change",
        "affinity_relationship_tag_extra_duration", "token_limit", "kingdom_actor_travel_distance",
    };

    public static TrinketDescriptions Load(string streamingAssets)
    {
        var data = new TrinketDescriptions();
        string excel = Path.Combine(streamingAssets, "Excel");
        foreach (string file in new[] { "trinkets", "buff", "condition" })
            foreach (var (id, type, _, lines) in Dd2Tables.BlockLines(Path.Combine(excel, file + "_data_export.Group.csv")))
                data._blocks[(type, id)] = lines;
        string sources = Path.Combine(streamingAssets, "Localization", "Sources");
        // Sources are DD2's English fallback. Runtime localization takes precedence in Text.
        foreach (string file in Directory.Exists(sources) ? Directory.GetFiles(sources, "*.txt") : Array.Empty<string>())
            foreach (string line in File.ReadLines(file))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                data._english[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Replace("\\n", "\n");
            }
        return data;
    }

    public string Text(string key, Func<string, string> localize = null)
    {
        string live = localize?.Invoke(key);
        if (!string.IsNullOrWhiteSpace(live) && live != key) return live;
        return _english.TryGetValue(key, out var text) ? text : null;
    }

    /// <summary>Stat and authored override effects. Unsupported triggered effects are not fabricated.
    /// complete distinguishes a full cold-menu description from a partial one for the parity audit.</summary>
    public string Effects(string id, out bool complete, Func<string, string> localize = null)
    {
        complete = true;
        if (!_blocks.ContainsKey(("Item", id))) { complete = false; return null; }
        var lines = new List<string>();
        string external = Field("Item", id, "m_dataExternalBuffsId") ?? id;
        foreach (string buff in Values("ActorDataExternalBuffs", external, "buffs"))
        {
            string authored = Text("buff_desc_" + buff + "_override", localize);
            if (!string.IsNullOrEmpty(authored)) { lines.Add(Plain(authored)); continue; }
            string condition = Field("Buff", buff, "m_ConditionId") ?? buff;
            bool conditional = _blocks.ContainsKey(("Condition", condition));
            var buffLines = new List<string>();
            if (_blocks.TryGetValue(("ActorDataStats", buff), out var stats))
            {
                string[] keys = Array.Empty<string>();
                foreach (var row in stats)
                {
                    if (row[0] == "key_map") keys = row.Skip(1).ToArray();
                    else if (row[0] == "add_stats" || row[0] == "multiply_stats")
                        for (int i = 0; i < keys.Length && i + 1 < row.Length; i++)
                            AddStat(buffLines, keys[i], null, row[i + 1], row[0] == "multiply_stats", localize);
                    else if ((row[0] == "sub_stat" || row[0] == "multiply_sub_stat") && row.Length >= 4)
                        AddStat(buffLines, row[1], row[2], row[3], row[0] == "multiply_sub_stat", localize);
                }
            }
            if (buffLines.Count > 0)
            {
                string text = string.Join("\n", buffLines);
                if (conditional) text = WithCondition(condition, text, localize);
                if (text == null) complete = false;
                else lines.Add(text);
            }
            else complete = false;
            if (_blocks.ContainsKey(("ActorDataEffects", buff))) complete = false;
        }
        if (_blocks.ContainsKey(("ActorDataEffects", id)) || Values("Item", id, "m_effectIds").Count > 0)
            complete = false;
        string note = Text("item_description_" + id, localize);
        if (!string.IsNullOrEmpty(note)) lines.Add(Plain(note));
        return lines.Count == 0 ? null : string.Join("\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private string WithCondition(string id, string effect, Func<string, string> localize)
    {
        // Invisible or unsupported conditions must never appear as unconditional bonuses.
        if (Field("Condition", id, "m_IsVisible") == "False") return null;
        string authored = Text("effect_condition_" + id + "_override", localize);
        if (!string.IsNullOrEmpty(authored))
        {
            string overridden = Format(authored, effect);
            return overridden != null && overridden.Contains(effect) ? Plain(overridden) : null;
        }
        string type = Field("Condition", id, "m_ConditionType");
        string key = "effect_tooltip_condition_" + type;
        if (Field("Condition", id, "m_IsInverse") == "True") key += "_inverse";
        string value = Field("Condition", id, "m_ConditionString");
        string numberType = Field("Condition", id, "m_ConditionNumberType");
        string actor = Field("Condition", id, "m_ConditionActorType");
        object[] args;
        switch (type)
        {
            case "skill_tag":
            case "biome":
            case "biome_sub_type":
            case "status":
            case "trinket_equipped":
            case "combat_item_equipped":
            case "stage_coach_upgrade_equipped":
            case "item_equipped_tag":
            case "trinket_equipped_tag":
            case "combat_item_equipped_tag":
                string prefix = type == "skill_tag" ? "skill_tag_" : type.StartsWith("biome", StringComparison.Ordinal) ? "biome_name_"
                    : type == "status" ? "status_" : type.EndsWith("_tag", StringComparison.Ordinal) ? "item_tag_" : "item_name_";
                string label = Text(prefix + value, localize);
                if (label == null) return null;
                if (numberType == "MULTIPLE") key += "_multiple";
                args = new object[] { label, effect };
                break;
            case "health_percent":
            case "health_percent_wound_included":
            case "wound_percent":
            case "stress":
            case "stress_percent":
            case "rank":
            case "round":
            case "turn":
                if (type.StartsWith("health", StringComparison.Ordinal) && (actor == "TARGET" || actor == "PARTY")) key += "_" + actor.ToLowerInvariant();
                if (numberType == "MULTIPLE" && (type == "stress" || type == "stress_percent" || type == "round" || type == "turn"))
                { key += "_multiple"; args = new object[] { effect }; }
                else
                {
                    string comparison = Compare(id, type.Contains("percent"), localize);
                    if (comparison == null) return null;
                    // CSV ranks are already one-based; native initialization subtracts one and its description adds it back.
                    args = new object[] { comparison, "", effect };
                }
                break;
            case "run_value":
            case "run_value_percent":
                string runName = Text("run_value_" + value, localize);
                string runComparison = Compare(id, type == "run_value_percent", localize);
                if (runName == null || runComparison == null) return null;
                args = new object[] { runName, runComparison, "", effect };
                break;
            default: return null;
        }
        string result = Format(Text(key, localize), args);
        return result != null && result.Contains(effect) ? Plain(result) : null;
    }

    private string Compare(string id, bool percent, Func<string, string> localize)
    {
        string comparison = Field("Condition", id, "m_ConditionNumberType")?.ToLowerInvariant();
        if (comparison == "equal") comparison = "equals";
        if (comparison != "equals" && comparison != "greater_than" && comparison != "greater_than_or_equal"
            && comparison != "less_than" && comparison != "less_than_or_equal") return null;
        if (!float.TryParse(Field("Condition", id, "m_ConditionNumber"), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return null;
        return Format(Text("comparison_" + comparison + "_label" + (percent ? "_percentage" : ""), localize),
            (value * (percent ? 100 : 1)).ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static string Format(string template, params object[] args)
    {
        if (template == null) return null;
        try { return string.Format(CultureInfo.InvariantCulture, template, args); }
        catch (FormatException) { return null; }
    }

    private void AddStat(List<string> lines, string stat, string sub, string value, bool multiply, Func<string, string> localize)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || number == 0) return;
        bool percent = multiply || !ValueStats.Contains(stat);
        if (percent) number *= 100;
        string amount = (number > 0 ? "+" : "") + number.ToString("0.##", CultureInfo.InvariantCulture) + (percent ? "%" : "");
        string template = Text("actor_stat_type_formatted_" + stat + (sub == null ? "" : "_" + sub), localize);
        if (template == null) template = "{0} " + (Text("actor_stat_type_" + stat, localize) ?? Pretty(stat)) + (sub == null ? "" : " (" + Pretty(sub) + ")");
        lines.Add(Plain(template.Replace("{0}", amount)));
    }

    private List<string> Values(string type, string id, string key) => _blocks.TryGetValue((type, id), out var rows)
        ? rows.Where(r => r[0] == key).SelectMany(r => r.Skip(1)).ToList() : new List<string>();
    private string Field(string type, string id, string key) => Values(type, id, key).FirstOrDefault();

    public static string Plain(string rich)
    {
        if (string.IsNullOrWhiteSpace(rich)) return null;
        string s = rich.Replace("{q}", "\"").Replace("\\n", "\n");
        s = Regex.Replace(s, "<sprite[^>]*name=[\"']?([A-Za-z0-9_]+)[\"']?[^>]*>", m => Pretty(m.Groups[1].Value) + " ");
        s = Regex.Replace(s, "<[^>]*>", "");
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @"\s*\n\s*", "\n").Trim();
        return s.Length == 0 ? null : s;
    }

    private static string Pretty(string id)
    {
        var aliases = new Dictionary<string, string> { ["icon_health_v2"] = "HP", ["icon_death_outline"] = "Deathblow", ["token_stress"] = "Stress" };
        if (aliases.TryGetValue(id, out var alias)) return alias;
        string s = id.Replace("token_", "").Replace("icon_", "").Replace('_', ' ').Trim();
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
