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
        foreach (string file in new[] { "trinkets", "buff", "condition", "effect", "dots", "trinkets_actor_effect_trigger" })
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
        string text = RawText(key, localize);
        return text == null ? null : ExpandSprites(text, name => RawText(name, localize));
    }

    private string RawText(string key, Func<string, string> localize)
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
            if (!AddStats(buff, buffLines, localize)) complete = false;
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

    private void AddTriggered(string id, List<string> lines, ref bool complete, Func<string, string> localize, int depth = 0)
    {
        if (!_blocks.TryGetValue(("ActorDataEffects", id), out var groups)) return;
        foreach (var group in groups)
        {
            if (group[0] == "actor_effect_triggers")
            {
                foreach (string trigger in group.Skip(1)) AddActorTrigger(trigger, lines, ref complete, localize, depth);
                continue;
            }
            const string limitedSuffix = "_apply_limit_effects";
            bool limited = group[0].EndsWith(limitedSuffix, StringComparison.Ordinal);
            // This is metadata for its paired effect group, not a separate event.
            if (group[0].EndsWith("_apply_limit", StringComparison.Ordinal) && groups.Any(row => row[0] == group[0] + "_effects")) continue;
            if (!group[0].EndsWith("_effects", StringComparison.Ordinal)) { complete = false; continue; }
            string eventId = group[0].Substring(0, group[0].Length - (limited ? limitedSuffix.Length : "_effects".Length));
            bool friendly = FriendlyEffectEvents.Contains(eventId);
            string title = Plain(Text("effect_tooltip_skill_effect_" + eventId, localize));
            if (title == null) { complete = false; continue; }
            if (limited && Field("ActorDataEffects", id, eventId + "_apply_limit") != "1") { complete = false; continue; }
            AddEffectGroup(title, friendly, group.Skip(1), limited, lines, ref complete, localize, depth);
        }
    }

    private static readonly HashSet<string> ActorTriggerFields = new()
    {
        "m_ActorEffectType", "m_ActorEffectTriggerSourceType", "m_ActorEffectTriggerTargetType",
        "m_IncludeSourceActor", "m_ActorCount", "m_ApplyLimit", "effects",
        "m_NeighborBackCount", "m_NeighborFrontCount",
    };

    private void AddActorTrigger(string id, List<string> lines, ref bool complete, Func<string, string> localize, int depth)
    {
        if (!_blocks.TryGetValue(("ActorEffectTrigger", id), out var rows) || rows.Any(row => !ActorTriggerFields.Contains(row[0])))
        { complete = false; return; }
        string eventId = Field("ActorEffectTrigger", id, "m_ActorEffectType")?.ToLowerInvariant();
        string source = Field("ActorEffectTrigger", id, "m_ActorEffectTriggerSourceType");
        string target = Field("ActorEffectTrigger", id, "m_ActorEffectTriggerTargetType");
        string count = Field("ActorEffectTrigger", id, "m_ActorCount");
        var effects = Values("ActorEffectTrigger", id, "effects");
        bool selfResist = eventId == "on_resist" && source == "target" && target == "target"
            && count == "1" && Field("ActorEffectTrigger", id, "m_IncludeSourceActor") == "True";
        if (eventId == null || (source != "target" && source != "performer")
            || (target != "friendly_team" && target != "enemy_team" && target != "neighbor" && !selfResist)
            || !int.TryParse(count, out int actorCount) || actorCount < 1 || actorCount > 4 || effects.Count == 0
            || !bool.TryParse(Field("ActorEffectTrigger", id, "m_IncludeSourceActor") ?? "False", out _)
            || !int.TryParse(Field("ActorEffectTrigger", id, "m_ApplyLimit") ?? "0", out int limit) || limit > 1)
        { complete = false; return; }
        if (!int.TryParse(Field("ActorEffectTrigger", id, "m_NeighborBackCount") ?? "0", out int back)
            || !int.TryParse(Field("ActorEffectTrigger", id, "m_NeighborFrontCount") ?? "0", out int front)
            || back < 0 || front < 0 || back > 3 || front > 3
            || (target == "neighbor" ? back + front == 0 : back + front != 0))
        { complete = false; return; }
        string titleKey = "effect_tooltip_skill_effect_" + eventId + "_" + target + "_" + count;
        string title = Plain(Text(titleKey + "_" + effects[0], localize) ?? Text(titleKey, localize));
        if (title == null)
        {
            string eventTitle = Plain(Text("effect_tooltip_skill_effect_" + eventId, localize));
            string direction = target == "neighbor" ? (back > 0 && front > 0 ? "adjacent_" : back > 0 ? "back_" : "front_") : "";
            string targetTitle = Plain(Text("actor_trigger_target_type_" + target + "_" + direction + count, localize));
            if (eventTitle != null && (targetTitle != null || selfResist)) title = selfResist ? eventTitle : eventTitle + " " + targetTitle;
        }
        if (title == null) { complete = false; return; }
        AddEffectGroup(title, FriendlyEffectEvents.Contains(eventId), effects, limit > 0, lines, ref complete, localize, depth);
    }

    private void AddEffectGroup(string title, bool friendly, IEnumerable<string> effects, bool limited,
        List<string> lines, ref bool complete, Func<string, string> localize, int depth)
    {
        if (limited)
        {
            string separator = Plain(Text("spaced_or_label", localize));
            var choices = effects.Select(effect => SimpleEffect(effect, localize, friendly, depth)).ToList();
            // Native limit one is a choice. Withhold the whole group if any possible outcome is unknown.
            if (separator == null || choices.Count == 0 || choices.Any(choice => choice == null)) complete = false;
            else lines.Add(title + " " + string.Join(" " + separator + " ", choices));
            return;
        }
        foreach (string effect in effects)
        {
            string description = SimpleEffect(effect, localize, friendly, depth);
            if (description == null) complete = false;
            else lines.Add(title + " " + description);
        }
    }

    // ActorDataEffectDescription's friendly context when describing equipment without an active skill/actor.
    private static readonly HashSet<string> FriendlyEffectEvents = new()
    {
        "target_team_member_random", "performer", "performer_after_target", "round_start", "combat_start",
        "turn_start", "turn_end", "round_end", "combat_end",
    };

    private static readonly HashSet<string> SimpleEffectFields = new()
    {
        "m_Chance", "m_ChancePerRoundSuffix", "m_ShowValue", "m_IsVisible", "m_ConditionId", "all_conditions", "any_conditions",
        "m_TokenAddId", "m_TokenAddAmount", "m_TokenAddAmountRange", "m_StressDamage", "m_StressHeal",
        "m_HealthDamageAmount", "m_HealthHealAmount", "m_HealthHealPercent",
        "m_DotAddId", "m_DotAddAmount", "m_DotAddAmountRange",
        "m_Priority", "m_IgnoreResist", "m_TokenAddTag", "m_TokenRemoveId", "m_TokenRemoveTag",
        "m_TokenRemoveAmount", "m_TokenRemoveAmountRange", "m_TokenRemoveRandom",
        "buffs",
        "m_AddTurn", "m_AddTurnRange",
        "m_Move", "m_MoveRange", "m_Shuffle",
        "m_TokenConvertFromTokenIds", "m_TokenConvertToId", "m_TokenConvertAmount", "m_TokenConvertAmountRange",
        "m_CritChance", "m_CritMultiplier",
        "m_TokenStealTags", "m_TokenStealAmount", "m_TokenStealAmountRange",
        "m_DotStealTags", "m_DotStealAmount", "m_DotStealAmountRange",
    };

    private string SimpleEffect(string id, Func<string, string> localize, bool friendly, int depth)
    {
        if (!_blocks.TryGetValue(("Effect", id), out var rows) || Field("Effect", id, "m_IsVisible") == "False") return null;
        // An unhandled field may change a target, quantity, duration or chance. Withhold the whole effect.
        if (rows.Any(row => !SimpleEffectFields.Contains(row[0]))) return null;
        // DD2's formatter includes these effects; resistance bypass changes application rather than its text.
        string ignoreResist = Field("Effect", id, "m_IgnoreResist");
        if (ignoreResist != null && !bool.TryParse(ignoreResist, out _)) return null;
        if (Field("Effect", id, "m_CritChance") != null || Field("Effect", id, "m_CritMultiplier") != null)
        {
            // Native applies these to health effects but describes their base amount normally.
            if (Field("Effect", id, "m_HealthHealAmount") == null && Field("Effect", id, "m_HealthHealPercent") == null
                && Field("Effect", id, "m_HealthDamageAmount") == null) return null;
            if (!float.TryParse(Field("Effect", id, "m_CritChance") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out float crit)
                || float.IsNaN(crit) || float.IsInfinity(crit) || crit < 0 || crit > 1
                || !float.TryParse(Field("Effect", id, "m_CritMultiplier") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier)
                || float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0 || (crit > 0 && multiplier == 0)) return null;
        }
        var parts = new List<string>();
        foreach (string family in new[] { "Token", "Dot" })
        {
            var tags = Values("Effect", id, "m_" + family + "StealTags");
            if (tags.Count == 0)
            {
                if (rows.Any(row => row[0].StartsWith("m_" + family + "Steal", StringComparison.Ordinal))) return null;
                continue;
            }
            if (!int.TryParse(Field("Effect", id, "m_" + family + "StealAmount"), out int amount) || amount <= 0
                || (Field("Effect", id, "m_" + family + "StealAmountRange") ?? "0") != "0") return null;
            bool token = family == "Token";
            string key = token ? amount >= 99 ? "effect_tooltip_token_steal_all_tag"
                : "effect_tooltip_token_steal_tag" + (amount > 1 ? "+plural" : "") : "effect_tooltip_dot_steal_tag";
            foreach (string tag in tags)
            {
                string label = Plain(Text((token ? "token_tag_" : "dot_") + tag, localize));
                if (label == null) return null;
                string transfer = token && amount > 1 && amount < 99 ? Format(Text(key, localize), amount, label)
                    : Format(Text(key, localize), label);
                if (transfer == null) return null;
                parts.Add(transfer);
            }
        }
        if (rows.Any(row => row[0].StartsWith("m_TokenConvert", StringComparison.Ordinal)))
        {
            var fromTokens = Values("Effect", id, "m_TokenConvertFromTokenIds");
            string toToken = Field("Effect", id, "m_TokenConvertToId");
            if (fromTokens.Count == 0 || toToken == null
                || !int.TryParse(Field("Effect", id, "m_TokenConvertAmount"), out int amount) || amount <= 0
                || (Field("Effect", id, "m_TokenConvertAmountRange") ?? "0") != "0") return null;
            string toText = TokenAmount(toToken, amount, 0, localize);
            if (toText == null) return null;
            foreach (string token in fromTokens)
            {
                string fromText = TokenAmount(token, amount, 0, localize);
                string conversion = fromText == null ? null : Format(Text("effect_tooltip_token_convert_amount", localize), fromText, toText);
                if (conversion == null) return null;
                parts.Add(conversion);
            }
        }
        string move = Field("Effect", id, "m_Move");
        if (move != null)
        {
            if (!int.TryParse(move, out int distance) || distance == 0 || distance == int.MinValue
                || (Field("Effect", id, "m_MoveRange") ?? "0") != "0") return null;
            string direction = distance < 0 ? "forward" : "backward";
            string movement = Format(Text("effect_tooltip_" + (friendly ? "move_" : "target_") + direction, localize), Math.Abs(distance));
            if (movement == null) return null;
            parts.Add(movement);
        }
        string shuffle = Field("Effect", id, "m_Shuffle");
        if (shuffle != null)
        {
            if (!bool.TryParse(shuffle, out bool shuffled)) return null;
            if (shuffled)
            {
                string shuffledText = Plain(Text("effect_tooltip_shuffle", localize));
                if (shuffledText == null) return null;
                parts.Add(shuffledText);
            }
        }
        if (Field("Effect", id, "m_AddTurn") != null)
        {
            if (Field("Effect", id, "m_AddTurn") != "1" || (Field("Effect", id, "m_AddTurnRange") ?? "0") != "0") return null;
            string extraAction = Format(Text("effect_tooltip_add_turn", localize), 1);
            if (extraAction == null) return null;
            parts.Add(extraAction);
        }
        foreach (string buff in Values("Effect", id, "buffs"))
        {
            string buffText = BuffEffect(buff, localize, depth);
            if (buffText == null) return null;
            parts.Add(buffText);
        }
        string dot = Field("Effect", id, "m_DotAddId");
        if (dot != null)
        {
            // Multiple/random applications need their own native quantity semantics.
            if (Number("Effect", id, "m_DotAddAmount") != 1 || Number("Effect", id, "m_DotAddAmountRange") != 0) return null;
            string dotText = DotEffect(dot, localize);
            if (dotText == null) return null;
            parts.Add(dotText);
        }
        foreach (string operation in new[] { "Add", "Remove" })
        {
            string token = Field("Effect", id, "m_Token" + operation + "Id");
            string tag = Field("Effect", id, "m_Token" + operation + "Tag");
            if (token == null && tag == null) continue;
            if (!int.TryParse(Field("Effect", id, "m_Token" + operation + "Amount"), out int amount) || amount <= 0) return null;
            string rawRange = Field("Effect", id, "m_Token" + operation + "AmountRange");
            int range = 0;
            if (rawRange != null && (!int.TryParse(rawRange, out range) || range < 0 || amount > int.MaxValue - range)) return null;
            string op = operation.ToLowerInvariant();
            if (token != null)
            {
                // Native hides named-token quantities unless ShowValue is explicitly true.
                string value = TokenAmount(token, Field("Effect", id, "m_ShowValue") == "True" ? amount : 1,
                    Field("Effect", id, "m_ShowValue") == "True" ? range : 0, localize);
                string tokenText = value == null ? null : Format(Text("effect_tooltip_token_" + op + "_amount", localize), value);
                if (tokenText == null) return null;
                parts.Add(tokenText);
            }
            if (tag != null)
            {
                // Category quantities use different native templates; ranges remain withheld until supported.
                if (range != 0) return null;
                string tagKey = (operation == "Add" ? "token_add_tag_" : "token_tag_") + tag;
                if (operation == "Remove" && amount > 1) tagKey += "+plural";
                string label = Plain(Text(tagKey, localize));
                if (label == null) return null;
                string categoryText = operation == "Remove" && amount >= 99
                    ? Format(Text("effect_tooltip_token_remove_all_tag", localize), label)
                    : Format(Text("effect_tooltip_token_" + op + "_tag" + (amount > 1 ? "+plural" : ""), localize), amount, label);
                if (categoryText == null) return null;
                parts.Add(categoryText);
            }
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

    private string TokenAmount(string token, int amount, int range, Func<string, string> localize)
    {
        string label = Plain(Text("token_name_" + token, localize) ?? Text("token_" + token, localize));
        if (label == null) return null;
        string suffix = range != 0 ? "range" : amount == 1 || amount >= 99 ? "singular" : "plural";
        return Format(Text("token_amount_format_" + suffix, localize), label, amount, amount + range);
    }

    private static readonly HashSet<string> BuffEffectFields = new()
    {
        "m_DurationType", "m_DurationAmount", "m_Tags", "m_ConditionId", "m_IsVisible",
    };

    private string BuffEffect(string id, Func<string, string> localize, int depth)
    {
        if (depth >= 4) return null;
        string authored = Text("buff_desc_" + id + "_override", localize);
        if (!string.IsNullOrEmpty(authored)) return Plain(authored);
        if (!_blocks.TryGetValue(("Buff", id), out var rows) || rows.Any(row => !BuffEffectFields.Contains(row[0]))
            || Field("Buff", id, "m_IsVisible") == "False"
            || _blocks.ContainsKey(("RunDataStats", id))) return null;
        var stats = new List<string>();
        bool complete = AddStats(id, stats, localize);
        AddTriggered(id, stats, ref complete, localize, depth + 1);
        if (!complete || stats.Count == 0) return null;
        string text = string.Join("\n", stats);
        string condition = Field("Buff", id, "m_ConditionId") ?? id;
        if (_blocks.ContainsKey(("Condition", condition))) text = WithCondition(condition, text, localize);
        else if (Field("Buff", id, "m_ConditionId") != null) return null;
        if (text == null || !int.TryParse(Field("Buff", id, "m_DurationAmount"), out int duration) || duration < 1) return null;
        string durationType = Field("Buff", id, "m_DurationType");
        string key;
        switch (durationType)
        {
            case "combat_end":
                if (duration == 1)
                {
                    string suffix = Format(Text("buff_combat_end_single_duration_label", localize));
                    return suffix == null ? null : Format(Text("effect_tooltip_buff", localize), text + suffix);
                }
                key = "duration_display_type_battle"; break;
            case "inn_start":
                string innSuffix = Format(Text(duration == 1 ? "inn_next_biome_append_label" : "inn_next_biome_append_plural_label", localize), duration);
                return innSuffix == null ? null : Format(Text("effect_tooltip_buff", localize), text + innSuffix);
            case "performer_turn_start": case "performer_turn_end": case "every_turn_start": case "every_turn_end": case "round_start":
                key = "duration_display_type_turn"; break;
            case "round_end": key = "duration_display_type_round"; break;
            default: return null;
        }
        if (duration < 99)
        {
            string durationText = Format(Text(key + (duration == 1 ? "" : "+plural"), localize), duration);
            if (durationText == null) return null;
            text = Format(Text("skill_effect_duration_label", localize), text, durationText);
        }
        return text == null ? null : Format(Text("effect_tooltip_buff", localize), text);
    }

    private bool AddStats(string id, List<string> lines, Func<string, string> localize)
    {
        if (!_blocks.TryGetValue(("ActorDataStats", id), out var stats)) return true;
        bool supported = true;
        string[] keys = Array.Empty<string>();
        foreach (var row in stats)
        {
            if (row[0] == "key_map") keys = row.Skip(1).ToArray();
            else if (row[0] == "add_stats" || row[0] == "multiply_stats")
                for (int i = 0; i < keys.Length && i + 1 < row.Length; i++)
                    supported &= AddStat(lines, keys[i], null, row[i + 1], row[0] == "multiply_stats", localize);
            else if ((row[0] == "sub_stat" || row[0] == "multiply_sub_stat") && row.Length >= 4)
                supported &= AddStat(lines, row[1], row[2], row[3], row[0] == "multiply_sub_stat", localize);
            else supported = false;
        }
        return supported;
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
            case "resist":
            case "resist_tag":
                if (numberType != "BOOL") return null;
                string resistName = Text((type == "resist" ? "actor_stat_type_resistance_" : "resist_tag_") + value, localize);
                if (resistName == null) return null;
                args = new object[] { resistName, effect };
                break;
            case "first_initiative":
            case "last_initiative":
                args = new object[] { effect };
                break;
            case "skill":
                string skillOverride = Text(key + "_" + value, localize);
                if (skillOverride != null)
                {
                    string skillText = Format(skillOverride, effect);
                    return skillText != null && skillText.Contains(effect) ? Plain(skillText) : null;
                }
                string skillName = RawText("skill_name_" + value, localize);
                if (skillName == null) return null;
                // The mastery decoration in a skill name is not an additional trinket requirement.
                skillName = Plain(Regex.Replace(skillName, @"<sprite[^>]*\bicon_upgraded_skill\b[^>]*>", ""), key => RawText(key, localize));
                if (skillName == null) return null;
                args = new object[] { skillName, effect };
                break;
            case "token_amount":
            case "token_tag_amount":
            case "dot_tag_amount":
                if (type == "token_tag_amount" && (Field("Condition", id, "m_ActorIsNotSource") == "True"
                    || (Field("Condition", id, "m_SourceConditionActorType") ?? "NONE") != "NONE")) return null;
                bool tokenCondition = type != "dot_tag_amount";
                string stateName = Text((tokenCondition ? "token_" : "dot_") + value, localize);
                if (stateName == null || !float.TryParse(Field("Condition", id, "m_ConditionNumber"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float stateAmount) || float.IsNaN(stateAmount) || float.IsInfinity(stateAmount)) return null;
                bool nonzero = (numberType == "GREATER_THAN" && stateAmount == 0) || (numberType == "GREATER_THAN_OR_EQUAL" && stateAmount == 1);
                bool one = tokenCondition && (numberType == "EQUAL" || numberType == "MULTIPLE") && stateAmount == 1;
                if (nonzero || one || (tokenCondition && numberType == "EQUAL" && stateAmount == 0))
                {
                    string actorName = Text("effect_tooltip_actor_type_" + actor?.ToLowerInvariant(), localize);
                    if (actorName == null) return null;
                    if (numberType == "MULTIPLE")
                    { key += "_multiple"; args = new object[] { stateName, effect }; }
                    else
                    {
                        key += stateAmount == 0 && numberType == "EQUAL" ? "_zero" : "_nonzero";
                        // Native DOT presence and token absence can provide actor-specific templates.
                        if ((!tokenCondition || key.EndsWith("_zero", StringComparison.Ordinal))
                            && Text(key + "_" + actor?.ToLowerInvariant(), localize) != null) key += "_" + actor.ToLowerInvariant();
                        args = new object[] { actorName, stateName, effect };
                    }
                }
                else if (!tokenCondition && numberType == "EQUAL" && stateAmount == 0)
                { key += "_zero"; args = new object[] { stateName, effect }; }
                else
                {
                    string stateComparison = Compare(id, false, localize);
                    if (stateComparison == null) return null;
                    args = new object[] { stateName, stateComparison, "", effect };
                }
                break;
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
            case "item_amount":
                string itemName = Text("item_name_" + value, localize);
                string itemComparison = Compare(id, false, localize);
                if (itemName == null || itemComparison == null) return null;
                args = new object[] { itemName, itemComparison, "", effect };
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

    private bool AddStat(List<string> lines, string stat, string sub, string value, bool multiply, Func<string, string> localize)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number)) return false;
        if (number == 0) return true;
        bool percent = multiply || !ValueStats.Contains(stat);
        if (percent) number *= 100;
        string amount = (number > 0 ? "+" : "") + number.ToString("0.##", CultureInfo.InvariantCulture) + (percent ? "%" : "");
        string template = Text("actor_stat_type_formatted_" + stat + (sub == null ? "" : "_" + sub), localize);
        if (template == null) template = "{0} " + (Text("actor_stat_type_" + stat, localize) ?? Pretty(stat)) + (sub == null ? "" : " (" + Pretty(sub) + ")");
        string text = Format(template, amount);
        if (text == null) return false;
        lines.Add(text);
        return true;
    }

    private List<string> Values(string type, string id, string key) => _blocks.TryGetValue((type, id), out var rows)
        ? rows.Where(r => r[0] == key).SelectMany(r => r.Skip(1)).ToList() : new List<string>();
    private string Field(string type, string id, string key) => Values(type, id, key).FirstOrDefault();

    private static readonly Dictionary<string, string> SpriteTokenAliases = new()
    {
        ["token_deflect"] = "block_plus", ["token_daze_gold"] = "daze", ["token_dodge+"] = "dodge_plus",
        ["token_blind-line"] = "blind", ["token_immoblize"] = "immobilize",
    };

    private static string ExpandSprites(string rich, Func<string, string> localize)
    {
        if (rich.IndexOf("<sprite", StringComparison.Ordinal) < 0) return rich;
        return Regex.Replace(rich.Replace("{q}", "\""), "<sprite[^>]*name=[\"']?([A-Za-z0-9_+\\-]+)[\"']?[^>]*>", m =>
        {
            string sprite = m.Groups[1].Value;
            string token = SpriteTokenAliases.TryGetValue(sprite, out string alias) ? alias
                : sprite.StartsWith("token_", StringComparison.Ordinal) ? sprite.Substring(6) : null;
            string name = token == null ? null : localize?.Invoke("token_name_" + token);
            if (string.IsNullOrWhiteSpace(name) || name == "token_name_" + token) name = null;
            string label = name == null ? null : Plain(name);
            return " " + (label ?? Pretty(sprite)) + " ";
        });
    }

    public static string Plain(string rich, Func<string, string> localize = null)
    {
        if (string.IsNullOrWhiteSpace(rich)) return null;
        string s = rich.Replace("{q}", "\"").Replace("\\n", "\n");
        s = ExpandSprites(s, localize);
        s = Regex.Replace(s, "<[^>]*>", "");
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @"[ \t]+([,:;/])", "$1");
        s = Regex.Replace(s, @"/[ \t]+", "/");
        s = Regex.Replace(s, @"\s*\n\s*", "\n").Trim();
        return s.Length == 0 ? null : s;
    }

    private static string Pretty(string id)
    {
        var aliases = new Dictionary<string, string>
        {
            ["icon_health_v2"] = "HP", ["icon_death_outline"] = "Deathblow", ["token_stress"] = "Stress",
            ["icon_healthup"] = "Heal", ["token_deflect"] = "Block+", ["token_daze_gold"] = "Daze",
            ["token_dodge+"] = "Dodge+", ["token_blind-line"] = "Blind", ["token_immoblize"] = "Immobilize", ["token_guard"] = "Guarded",
        };
        if (aliases.TryGetValue(id, out var alias)) return alias;
        string s = id.Replace("token_", "").Replace("icon_", "").Replace('_', ' ').Trim();
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
