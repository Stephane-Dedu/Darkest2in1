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
        foreach (string file in new[] { "trinkets", "buff", "condition", "effect", "dots" })
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
            AddTriggered(buff, buffLines, ref complete, localize);
            if (buffLines.Count > 0)
            {
                string text = string.Join("\n", buffLines);
                if (conditional) text = WithCondition(condition, text, localize);
                if (text == null) complete = false;
                else lines.Add(text);
            }
            else complete = false;
        }
        AddTriggered(id, lines, ref complete, localize);
        if (Values("Item", id, "m_effectIds").Count > 0) complete = false;
        string note = Text("item_description_" + id, localize);
        if (!string.IsNullOrEmpty(note)) lines.Add(Plain(note));
        return lines.Count == 0 ? null : string.Join("\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private void AddTriggered(string id, List<string> lines, ref bool complete, Func<string, string> localize)
    {
        if (!_blocks.TryGetValue(("ActorDataEffects", id), out var groups)) return;
        foreach (var group in groups)
        {
            const string limitedSuffix = "_apply_limit_effects";
            bool limited = group[0].EndsWith(limitedSuffix, StringComparison.Ordinal);
            // This is metadata for its paired effect group, not a separate event.
            if (group[0].EndsWith("_apply_limit", StringComparison.Ordinal) && groups.Any(row => row[0] == group[0] + "_effects")) continue;
            if (!group[0].EndsWith("_effects", StringComparison.Ordinal)) { complete = false; continue; }
            string eventId = group[0].Substring(0, group[0].Length - (limited ? limitedSuffix.Length : "_effects".Length));
            string title = Plain(Text("effect_tooltip_skill_effect_" + eventId, localize));
            if (title == null) { complete = false; continue; }
            if (limited)
            {
                string separator = Plain(Text("spaced_or_label", localize));
                var choices = group.Skip(1).Select(effect => SimpleEffect(effect, localize)).ToList();
                // Native positive apply limits join candidates with 'or'. Only limit one is supported here.
                // A partial choice list can misstate its outcomes, so withhold the whole list if any is unknown.
                if (Field("ActorDataEffects", id, eventId + "_apply_limit") != "1" || separator == null || choices.Count == 0 || choices.Any(choice => choice == null))
                    complete = false;
                else lines.Add(title + " " + string.Join(" " + separator + " ", choices));
                continue;
            }
            foreach (string effect in group.Skip(1))
            {
                string description = SimpleEffect(effect, localize);
                if (description == null) complete = false;
                else lines.Add(title + " " + description);
            }
        }
    }

    private static readonly HashSet<string> SimpleEffectFields = new()
    {
        "m_Chance", "m_ChancePerRoundSuffix", "m_ShowValue", "m_IsVisible", "m_ConditionId", "all_conditions", "any_conditions",
        "m_TokenAddId", "m_TokenAddAmount", "m_TokenAddAmountRange", "m_StressDamage", "m_StressHeal",
        "m_HealthDamageAmount", "m_HealthHealAmount", "m_HealthHealPercent",
        "m_DotAddId", "m_DotAddAmount", "m_DotAddAmountRange",
    };

    private string SimpleEffect(string id, Func<string, string> localize)
    {
        if (!_blocks.TryGetValue(("Effect", id), out var rows) || Field("Effect", id, "m_IsVisible") == "False") return null;
        // An unhandled field may change a target, quantity, duration or chance. Withhold the whole effect.
        if (rows.Any(row => !SimpleEffectFields.Contains(row[0]))) return null;
        var parts = new List<string>();
        string dot = Field("Effect", id, "m_DotAddId");
        if (dot != null)
        {
            // Multiple/random applications need their own native quantity semantics.
            if (Number("Effect", id, "m_DotAddAmount") != 1 || Number("Effect", id, "m_DotAddAmountRange") != 0) return null;
            string dotText = DotEffect(dot, localize);
            if (dotText == null) return null;
            parts.Add(dotText);
        }
        string token = Field("Effect", id, "m_TokenAddId");
        if (token != null)
        {
            float amount = Number("Effect", id, "m_TokenAddAmount");
            float range = Number("Effect", id, "m_TokenAddAmountRange");
            string tokenName = Plain(Text("token_name_" + token, localize) ?? Text("token_" + token, localize));
            if (amount <= 0 || tokenName == null) return null;
            string count = range == 0 ? amount.ToString("0.##", CultureInfo.InvariantCulture)
                : amount.ToString("0.##", CultureInfo.InvariantCulture) + "-" + (amount + range).ToString("0.##", CultureInfo.InvariantCulture);
            string value = amount == 1 && range == 0 ? tokenName : count + " " + tokenName;
            string tokenText = Plain(Format(Text("effect_tooltip_token_add_amount", localize), value));
            if (tokenText == null) return null;
            parts.Add(tokenText);
        }
        foreach (var stat in new[] { ("m_StressDamage", "stress_damage"), ("m_StressHeal", "stress_heal"),
            ("m_HealthDamageAmount", "health_damage_amount"), ("m_HealthHealAmount", "health_heal_amount"), ("m_HealthHealPercent", "health_heal_percent") })
        {
            float value = Number("Effect", id, stat.Item1);
            if (value == 0) continue;
            string statText = Plain(Format(Text("effect_tooltip_" + stat.Item2, localize),
                (value * (stat.Item1.EndsWith("Percent", StringComparison.Ordinal) ? 100 : 1)).ToString("0.##", CultureInfo.InvariantCulture)));
            if (statText == null) return null;
            parts.Add(statText);
        }
        if (parts.Count == 0) return null;
        float chance = Field("Effect", id, "m_Chance") == null ? 1 : Number("Effect", id, "m_Chance");
        if (chance <= 0 || chance > 1) return null;
        if (chance < 1)
        {
            string suffix = Format(Text(Field("Effect", id, "m_ChancePerRoundSuffix") == "True" ? "effect_tooltip_chance_per_round" : "effect_tooltip_pct", localize),
                (chance * 100).ToString("0.##", CultureInfo.InvariantCulture));
            if (suffix == null) return null;
            parts.Add(Plain(suffix));
        }
        string description = string.Join(" ", parts);
        var conditions = Values("Effect", id, "all_conditions");
        var any = Values("Effect", id, "any_conditions");
        if (any.Count > 1) return null;
        conditions.AddRange(any);
        string implicitCondition = Field("Effect", id, "m_ConditionId") ?? id;
        if (_blocks.ContainsKey(("Condition", implicitCondition))) conditions.Add(implicitCondition);
        else if (Field("Effect", id, "m_ConditionId") != null) return null;
        foreach (string condition in conditions.Distinct())
        {
            description = WithCondition(condition, description, localize);
            if (description == null) return null;
        }
        return description;
    }

    private static readonly HashSet<string> DotFields = new()
    {
        "m_Type", "m_Tags", "m_DurationAmount", "m_DurationType", "effects", "m_IgnoreBlockDotApply",
        "m_IgnoreFriendlyDealtModifications", "m_IgnoreFriendlyReceivedModifications",
        "m_IgnoreEnemyDealtModifications", "m_IgnoreEnemyReceivedModifications",
    };
    private static readonly string[] DotMagnitudeFields = { "m_HealthDamageAmount", "m_HealthHealAmount", "m_StressDamage", "m_StressHeal" };
    private static readonly HashSet<string> DotChildFields = new(DotMagnitudeFields.Concat(new[]
    {
        // Native definition descriptions show base magnitude, before critical/modifier rolls.
        "m_Chance", "m_CritChance", "m_CritMultiplier", "m_ShowValue", "m_IsVisible",
    }));

    private string DotEffect(string id, Func<string, string> localize)
    {
        if (!_blocks.TryGetValue(("Dot", id), out var rows) || rows.Any(row => !DotFields.Contains(row[0]))) return null;
        var children = Values("Dot", id, "effects");
        if (children.Count == 0) return null;
        float magnitude = 0;
        foreach (string child in children)
        {
            if (!_blocks.TryGetValue(("Effect", child), out var effects) || effects.Any(row => !DotChildFields.Contains(row[0]))) return null;
            foreach (string field in DotMagnitudeFields)
            {
                string raw = Field("Effect", child, field);
                if (raw == null) continue;
                // All supported installed DOT magnitudes are nonnegative integers. Keep others withheld.
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    || float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value != Math.Truncate(value)) return null;
                magnitude += value;
            }
        }
        if (magnitude <= 0 || magnitude > int.MaxValue) return null;
        string durationType = Field("Dot", id, "m_DurationType");
        string durationKey;
        switch (durationType)
        {
            case "performer_turn_start": case "performer_turn_end": case "every_turn_start": case "every_turn_end": case "round_start":
                durationKey = "duration_display_type_turn"; break;
            case "round_end": durationKey = "duration_display_type_round"; break;
            default: return null;
        }
        if (!int.TryParse(Field("Dot", id, "m_DurationAmount"), out int duration) || duration < 1 || duration >= 99) return null;
        string durationText = Format(Text(durationKey + (duration == 1 ? "" : "+plural"), localize), duration);
        string amountText = Format(Text("dot_" + Field("Dot", id, "m_Type") + "_amount", localize), (int)magnitude);
        if (durationText == null || amountText == null) return null;
        return Format(Text("effect_tooltip_dot_add_amount", localize), amountText + " (" + durationText + ")");
    }

    private float Number(string type, string id, string field) => float.TryParse(Field(type, id, field), NumberStyles.Float,
        CultureInfo.InvariantCulture, out float value) ? value : 0;

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
            case "actor_stat_value":
                if (string.IsNullOrEmpty(value)) return null;
                string statName = Text("actor_stat_type_" + value?.Replace('+', '_'), localize);
                string statComparison = Compare(id, false, localize);
                if (statName == null || statComparison == null) return null;
                // Native ConditionDescription uses raw units here, even for a resistance sub-stat.
                args = new object[] { statName, statComparison, "", effect };
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
        // Rich tags contain non-.NET substitutions such as #{debuff} and {q}. Resolve them before composite
        // formatting, while retaining comparison templates' leading separator space.
        string plain = Plain(template);
        if (plain == null) return null;
        if (template.StartsWith(" ", StringComparison.Ordinal)) plain = " " + plain;
        try { return string.Format(CultureInfo.InvariantCulture, plain, args); }
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
