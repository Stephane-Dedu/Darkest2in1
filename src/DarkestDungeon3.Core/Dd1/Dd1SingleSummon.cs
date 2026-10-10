using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// The single, unconditional performer summon used by the Necromancer. More complex summon effects
/// must use another adapter; accepting them here would silently discard their limits/ranks/timing.
/// This contract does not enqueue a DD2 actor or determine which corpses are replaceable.
/// </summary>
public sealed class Dd1SingleSummon
{
    private static readonly HashSet<string> SupportedKeys = new(StringComparer.Ordinal)
    {
        "name", "target", "chance", "summon_count", "summon_can_spawn_loot",
        "summon_monsters", "summon_chances", "on_hit", "on_miss", "apply_once",
    };

    public string Name { get; }
    public IReadOnlyList<string> Monsters { get; }
    public IReadOnlyList<double> Weights { get; }
    public bool CanSpawnLoot { get; }

    /// <summary>DD1's usable summon ranks: eligible corpses occupy no summon space.</summary>
    public static int AvailableRanks(IEnumerable<(int Size, bool CanBeSummonRank)> formation)
    {
        if (formation == null) throw new ArgumentNullException(nameof(formation));
        int occupied = 0;
        foreach (var actor in formation)
        {
            if (actor.Size < 1 || actor.Size > 4) throw new ArgumentOutOfRangeException(nameof(formation));
            if (!actor.CanBeSummonRank) occupied += actor.Size;
        }
        return Math.Max(0, 4 - occupied);
    }

    private Dd1SingleSummon(string name, string[] monsters, double[] weights, bool canSpawnLoot)
    {
        Name = name;
        Monsters = Array.AsReadOnly(monsters);
        Weights = Array.AsReadOnly(weights);
        CanSpawnLoot = canSpawnLoot;
    }

    public static bool TryRead(DarkestRecord effect, out Dd1SingleSummon summon, out string reason)
    {
        summon = null;
        reason = null;
        if (effect == null || effect.Type != "effect" || !effect.Has("summon_monsters"))
            return Reject("Not a summon effect.", out reason);
        var unsupported = effect.Params.Keys.Where(k => !SupportedKeys.Contains(k)).OrderBy(k => k).ToArray();
        if (unsupported.Length > 0)
            return Reject("Unsupported summon fields: " + string.Join(", ", unsupported), out reason);
        if (string.IsNullOrWhiteSpace(effect.Str("name")) || effect.Str("target") != "performer"
            || effect.Values("name").Count != 1 || effect.Values("target").Count != 1)
            return Reject("A named performer effect is required.", out reason);
        if (!Scalar(effect, "summon_count", out var count) || count != 1
            || !Scalar(effect, "chance", out var chance) || chance != 1)
            return Reject("Only one unconditional summon is supported.", out reason);
        if (!Flag(effect, "on_hit", out var onHit) || !onHit
            || !Flag(effect, "on_miss", out var onMiss) || !onMiss
            || !Flag(effect, "apply_once", out var once) || !once
            || !Flag(effect, "summon_can_spawn_loot", out var loot))
            return Reject("Explicit hit, miss, once and loot flags are required.", out reason);

        var monsters = effect.Values("summon_monsters").ToArray();
        var values = effect.Values("summon_chances");
        if (monsters.Length == 0 || monsters.Length != values.Count
            || monsters.Any(string.IsNullOrWhiteSpace)
            || monsters.Distinct(StringComparer.Ordinal).Count() != monsters.Length)
            return Reject("Each distinct monster needs one weight.", out reason);
        var weights = new double[values.Count];
        for (int i = 0; i < weights.Length; i++)
            if (!Number(values[i], out weights[i]) || weights[i] <= 0)
                return Reject("Summon weights must be finite and positive.", out reason);
        if (double.IsInfinity(weights.Sum())) return Reject("Summon weight sum overflowed.", out reason);
        summon = new Dd1SingleSummon(effect.Str("name"), monsters, weights, loot);
        return true;
    }

    /// <summary>
    /// One attempt: roll from the complete pool, then check the selected monster's size. A full
    /// formation still spends the roll; an oversized choice is not rerolled or deferred. The caller
    /// supplies DD1's usable rank count after applying its corpse policy. Missing actor coverage
    /// rejects before RNG consumption so an incomplete adapter cannot start a fight.
    /// </summary>
    public bool TryPlan(int availableRanks, Func<string, int> sizeOf, Func<double> roll,
                        out Dd1SummonChoice choice)
    {
        choice = null;
        if (availableRanks < 0 || availableRanks > 4) throw new ArgumentOutOfRangeException(nameof(availableRanks));
        if (sizeOf == null) throw new ArgumentNullException(nameof(sizeOf));
        if (roll == null) throw new ArgumentNullException(nameof(roll));
        var sizes = Monsters.Select(sizeOf).ToArray();
        if (sizes.Any(s => s < 1 || s > 4)) return false;
        double unit = roll();
        if (double.IsNaN(unit) || unit < 0 || unit >= 1) throw new ArgumentOutOfRangeException(nameof(roll));
        double scaled = unit * Weights.Sum(), cumulative = 0;
        int selected = Weights.Count - 1;
        for (int i = 0; i < Weights.Count; i++)
        {
            cumulative += Weights[i];
            if (scaled < cumulative) { selected = i; break; }
        }
        if (sizes[selected] > availableRanks) return false;
        choice = new Dd1SummonChoice(Monsters[selected], sizes[selected], CanSpawnLoot);
        return true;
    }

    private static bool Scalar(DarkestRecord effect, string key, out double value)
    {
        value = 0;
        return effect.Values(key).Count == 1 && Number(effect.Str(key), out value);
    }

    private static bool Number(string text, out double value)
    {
        bool percent = text.EndsWith("%", StringComparison.Ordinal);
        if (percent) text = text.Substring(0, text.Length - 1);
        bool valid = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                     && !double.IsNaN(value) && !double.IsInfinity(value);
        if (percent) value /= 100;
        return valid;
    }

    private static bool Flag(DarkestRecord effect, string key, out bool value)
    {
        value = false;
        return effect.Values(key).Count == 1 && bool.TryParse(effect.Str(key), out value);
    }

    private static bool Reject(string message, out string reason) { reason = message; return false; }
}

public sealed class Dd1SummonChoice
{
    public string Monster { get; }
    public int Size { get; }
    public int Rank => 1;
    public bool CanSpawnLoot { get; }

    internal Dd1SummonChoice(string monster, int size, bool canSpawnLoot)
    { Monster = monster; Size = size; CanSpawnLoot = canSpawnLoot; }
}
