using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// Turns a DD1 monster skill into DD2 skill data, so a DD1 monster's DD2 stand-in fights with the DD1 skill: DD1's
/// launch and target ranks, all-targets ("~"), damage (scaled to the stand-in's own damage level), crit, its own
/// move, and DD1's effects mapped to DD2's ready-made ones (Blight/Bleed dots, Stun, Stress, Push/Pull, debuff and
/// buff tokens, heals). The DD2 skill it stands on lends only its presentation (animation, camera, sounds).
/// The texts are DD2's per-element CSV ("key,value,value," lines, as inside an element_start/element_end block).
/// </summary>
public static class Dd1SkillToDd2
{
    public const string Prefix = "dd3__";

    /// <summary>The generated skill's id: "dd3__&lt;base DD2 skill&gt;__&lt;DD1 monster&gt;_&lt;DD1 skill&gt;".</summary>
    public static string GeneratedId(string baseSkill, string dd1Monster, string dd1Skill) => $"{Prefix}{baseSkill}__{dd1Monster}_{dd1Skill}";

    /// <summary>The DD2 skill a generated id stands on (for its presentation), or null for any other id.</summary>
    public static string BaseOf(string id)
    {
        if (id == null || !id.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        int end = id.IndexOf("__", Prefix.Length, StringComparison.Ordinal);
        return end > Prefix.Length ? id.Substring(Prefix.Length, end - Prefix.Length) : null;
    }

    /// <summary>
    /// One DD2 skill per DD1 skill (best fit first: same kind, then ranks); DD1 skills with no DD2 skill of their kind
    /// left are dropped. Returns DD1 skill id → DD2 skill id.
    /// </summary>
    public static Dictionary<string, string> Pair(IReadOnlyList<SkillShape> dd2, IReadOnlyList<SkillShape> dd1)
    {
        var pairs = new Dictionary<string, string>();
        var candidates = new List<(float Fit, SkillShape A, SkillShape B)>();
        foreach (var a in dd1)
            foreach (var b in dd2)
                if (a?.Id != null && b?.Id != null && a.Friendly == b.Friendly)
                    candidates.Add((Dd1MonsterSkills.Fit(b, a), a, b));
        var used = new HashSet<string>();
        foreach (var (_, a, b) in candidates.OrderByDescending(c => c.Fit))
        {
            if (pairs.ContainsKey(a.Id) || used.Contains(b.Id)) continue;
            pairs[a.Id] = b.Id;
            used.Add(b.Id);
        }
        return pairs;
    }

    /// <summary>DD1 damage → DD2 damage: the stand-in's attacks' average over the DD1 monster's (0.3x to 2x).</summary>
    public static float DamageScale(IEnumerable<(float Min, float Max)> dd2Attacks, IEnumerable<(float Min, float Max)> dd1Attacks)
    {
        var a = dd2Attacks.Where(d => d.Max > 0).Select(d => (d.Min + d.Max) / 2f).ToList();
        var b = dd1Attacks.Where(d => d.Max > 0).Select(d => (d.Min + d.Max) / 2f).ToList();
        if (a.Count == 0 || b.Count == 0) return 1f;
        return Math.Max(0.3f, Math.Min(2f, a.Average() / b.Average()));
    }

    // ---- effects ----

    /// <summary>DD2 effects for a DD1 skill: what lands on the target(s) and what the performer gets.</summary>
    public static (List<string> Target, List<string> Performer) Effects(SkillShape dd1, Func<string, DarkestRecord> dd1Effect, ICollection<string> unmapped = null,
                                                                        Func<string, Dd1Buff> dd1Buff = null)
    {
        var target = new List<string>();
        var performer = new List<string>();
        foreach (var name in dd1.Effects)
        {
            var e = dd1Effect(name);
            if (e == null) { unmapped?.Add(name); continue; }
            var (t, p) = Map(e, dd1Buff);
            if (t.Count + p.Count == 0) unmapped?.Add(name);
            target.AddRange(t);
            performer.AddRange(p);
        }
        if (dd1.HealMax > 0) target.Add(dd1.HealMax <= 3 ? "heal_15pct" : dd1.HealMax <= 6 ? "heal_25pct" : "heal_35pct");
        if (dd1.MoveBack > 0) performer.Add("move_backward_" + Math.Min(3, dd1.MoveBack));
        if (dd1.MoveForward > 0) performer.Add("move_forward_" + Math.Min(3, dd1.MoveForward));
        return (target.Distinct().ToList(), performer.Distinct().ToList());
    }

    /// <summary>How much a DD1 skill darkens the party's torch (DD1's "Darkness" effects: .torch_decrease); DD2 has no
    /// such effect, so the plugin lowers the expedition's light itself when the skill is used.</summary>
    public static int TorchDecrease(SkillShape dd1, Func<string, DarkestRecord> dd1Effect) =>
        dd1.Effects.Select(dd1Effect).Where(e => e != null).Sum(e => e.Int("torch_decrease"));

    /// <summary>One DD1 effect (effects/*.effects.darkest) as DD2 effects on the target and on the performer;
    /// its DD1 buffs (.buff_ids, shared/buffs) are read through <paramref name="dd1Buff"/>.</summary>
    public static (List<string> Target, List<string> Performer) Map(DarkestRecord e, Func<string, Dd1Buff> dd1Buff = null)
    {
        var dd2 = new List<string>();
        // Conditional bonuses ("+25% damage against marked") and long shots have no DD2 counterpart.
        if (e.Has("keyStatus") || (e.Has("chance") && e.Float("chance") < 0.5f)) return (new List<string>(), new List<string>());
        int dotPoison = e.Int("dotPoison"), dotBleed = e.Int("dotBleed");
        if (dotPoison > 0) dd2.Add($"skill_dot_{DotSize(dotPoison)}_blight");
        if (dotBleed > 0) dd2.Add($"skill_dot_{DotSize(dotBleed)}_bleed");
        int dotStress = e.Int("dotStress");   // DD1's horror: stress every turn, DD2's horror dot
        if (dotStress > 0) dd2.Add(dotStress <= 2 ? "dot_horror_small" : dotStress <= 4 ? "dot_horror_medium" : "dot_horror_large");
        if (e.Int("stun") > 0) dd2.Add("add_1_stun");
        if (e.Int("push") > 0) dd2.Add("move_knockback_" + Math.Min(3, e.Int("push")));
        if (e.Int("pull") > 0) dd2.Add("move_pull_" + Math.Min(3, e.Int("pull")));
        float stress = e.Has("stress") ? e.Float("stress") : 0f;
        if (stress > 0) dd2.Add("stress_damage_" + Math.Max(1, Math.Min(10, (int)Math.Round(stress / 10f, MidpointRounding.AwayFromZero))));
        if (stress < 0) dd2.Add("stress_heal_" + Math.Max(1, Math.Min(10, (int)Math.Round(-stress / 10f, MidpointRounding.AwayFromZero))));
        if (e.Has("heal")) dd2.Add(e.Float("heal") <= 3 ? "heal_15pct" : "heal_25pct");
        if (e.Int("riposte") > 0) dd2.Add("add_1_riposte");
        if (e.Has("shuffletarget")) dd2.Add(e.Has("chance") && e.Float("chance") < 0.75f ? "move_shuffle_50pct" : "move_shuffle");
        // DD1 buffs: the combat stat they change, as DD2's token for it.
        foreach (var buff in e.Values("buff_ids").Select(id => dd1Buff?.Invoke(id)).Where(b => b != null))
        {
            bool down = buff.Amount < 0;
            string token = (buff.Stat, buff.Sub) switch
            {
                ("combat_stat_multiply", "damage_low") or ("combat_stat_multiply", "damage_high") => down ? "add_1_weak" : "add_1_strength",
                ("combat_stat_add", "defense_rating") => down ? "add_1_vulnerable" : "add_1_dodge",
                ("combat_stat_add", "protection_rating") => down ? "add_1_vulnerable" : "add_1_block",
                ("combat_stat_add", "attack_rating") => down ? "add_1_blind" : null,
                ("combat_stat_add", "speed_rating") => down ? null : "add_1_speed",
                ("combat_stat_add", "crit_chance") => down ? null : "add_1_crit",
                _ => null,
            };
            if (token != null && !dd2.Contains(token)) dd2.Add(token);
        }
        if (e.Int("combat_stat_buff") > 0)
        {
            float damage = Stat(e, "damage_low_multiply") + Stat(e, "damage_high_multiply");
            float defense = Stat(e, "defense_rating_add"), protection = Stat(e, "protection_rating_add");
            float accuracy = Stat(e, "attack_rating_add"), speed = Stat(e, "speed_rating_add"), crit = Stat(e, "crit_chance_add");
            if (damage < 0) dd2.Add("add_1_weak");
            if (defense < 0 || protection < 0) dd2.Add("add_1_vulnerable");
            if (accuracy < 0) dd2.Add("add_1_blind");
            if (damage > 0) dd2.Add("add_1_strength");
            if (defense > 0) dd2.Add("add_1_dodge");
            if (protection > 0) dd2.Add("add_1_block");
            if (speed > 0) dd2.Add("add_1_speed");
            if (crit > 0) dd2.Add("add_1_crit");
        }
        else if (e.Int("tag") > 0) dd2.Add("add_1_vulnerable");   // DD1's mark: DD2's vulnerable makes the next hit hurt more

        string on = e.Str("target", "target");
        bool self = on.StartsWith("performer", StringComparison.Ordinal);
        return self ? (new List<string>(), dd2) : (dd2, new List<string>());
    }

    private static float Stat(DarkestRecord e, string key) => e.Has(key) ? e.Float(key) : 0f;

    private static string DotSize(int perTurn) => perTurn <= 1 ? "small" : perTurn == 2 ? "medium" : perTurn <= 4 ? "large" : "massive";

    // ---- DD2's per-element CSV ----

    /// <summary>The element's lines as (key, values).</summary>
    public static List<(string Key, List<string> Values)> Lines(string text) =>
        (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Split(','))
            .Where(c => c.Length > 0 && c[0].Length > 0)
            .Select(c => (c[0], c.Skip(1).Where(v => v.Length > 0).ToList())).ToList();

    private static string Text(IEnumerable<(string Key, List<string> Values)> lines)
    {
        var sb = new StringBuilder();
        foreach (var (key, values) in lines) sb.Append(key).Append(',').Append(string.Join(",", values)).Append(",\n");
        return sb.ToString();
    }

    private static readonly HashSet<string> DroppedSkillKeys = new()
    {
        // Ranks, targeting and data links are DD1's now; DD2's conditions and cooldowns don't exist in DD1.
        "launch_ranks", "target_ranks", "target_relative_ranks", "m_IsFriendly", "m_IsMultiHit", "m_ActorDataStatsId",
        "m_ActorDataEffectsId", "m_AllConditionIds", "m_AnyConditionIds", "m_MultiHitAllTargetsConditionIds", "m_Cooldown",
        "m_IsStartCooldownOnValidMode", "m_MultiHitGuaranteedRanks",
    };

    /// <summary>The DD2 skill block for the DD1 skill: the base skill's lines (tags, presentation flags) with DD1's ranks
    /// and targeting; its stats and effects are the elements of the same id.</summary>
    public static string SkillText(string baseSkillText, SkillShape dd1)
    {
        var lines = Lines(baseSkillText).Where(l => !DroppedSkillKeys.Contains(l.Key)).ToList();
        lines.Insert(0, ("m_IsFriendly", new List<string> { dd1.Friendly ? "True" : "False" }));
        lines.Insert(1, ("launch_ranks", dd1.LaunchRanks.Select(r => r.ToString(CultureInfo.InvariantCulture)).ToList()));
        if (dd1.TargetRanks.Count > 0)
            lines.Insert(2, ("target_ranks", dd1.TargetRanks.Select(r => r.ToString(CultureInfo.InvariantCulture)).ToList()));
        lines.Add(("m_IsMultiHit", new List<string> { dd1.AllTargets ? "True" : "False" }));
        return Text(lines);
    }

    /// <summary>The stats block: the base skill's stats with DD1's damage (scaled) and crit.</summary>
    public static string StatsText(string baseStatsText, SkillShape dd1, float scale)
    {
        var stats = Stats(baseStatsText);
        if (dd1.DamageMax > 0)
        {
            int min = Math.Max(1, (int)Math.Round(dd1.DamageMin * scale, MidpointRounding.AwayFromZero));
            int max = Math.Max(min, (int)Math.Round(dd1.DamageMax * scale, MidpointRounding.AwayFromZero));
            stats["health_damage"] = min;
            stats["health_damage_range"] = max - min;
        }
        else
        {
            stats.Remove("health_damage");
            stats.Remove("health_damage_range");
        }
        stats["crit_chance"] = dd1.CritValid ? dd1.Crit : 0f;
        var other = Lines(baseStatsText).Where(l => l.Key != "key_map" && l.Key != "add_stats");
        var lines = new List<(string, List<string>)>
        {
            ("key_map", stats.Keys.ToList()),
            ("add_stats", stats.Values.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)).ToList()),
        };
        lines.AddRange(other);
        return Text(lines);
    }

    /// <summary>A stats block's key_map/add_stats as key → value.</summary>
    public static Dictionary<string, float> Stats(string statsText)
    {
        var result = new Dictionary<string, float>();
        List<string> keys = null;
        foreach (var (key, values) in Lines(statsText))
        {
            if (key == "key_map") keys = values;
            else if (key == "add_stats" && keys != null)
                for (int i = 0; i < keys.Count && i < values.Count; i++)
                    if (float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) result[keys[i]] = f;
        }
        return result;
    }

    /// <summary>The effects block: DD1's effects only.</summary>
    public static string EffectsText(IReadOnlyCollection<string> target, IReadOnlyCollection<string> performer)
    {
        var lines = new List<(string, List<string>)>();
        if (target.Count > 0) lines.Add(("target_effects", target.ToList()));
        if (performer.Count > 0) lines.Add(("performer_effects", performer.ToList()));
        return Text(lines);
    }
}
