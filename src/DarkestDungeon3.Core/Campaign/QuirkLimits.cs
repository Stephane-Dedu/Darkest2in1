using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>
/// DD1's quirk limits (shared/rules.json quirks_max_positive 5, quirks_max_negative 5, quirks_max_diseases 3): a hero
/// who gains a quirk of a kind they already have the most of loses an unlocked one of that kind in exchange (DD1 marks
/// it with shared/character/quirkreplaced.png); if every one of that kind is locked, the new quirk isn't gained.
/// </summary>
public sealed class QuirkLimits
{
    public int MaxPositive = 5, MaxNegative = 5, MaxDiseases = 3;
    /// <summary>How many quirks of each kind can be locked (quirks_max_locked_positive / _negative).</summary>
    public int MaxLockedPositive = 3, MaxLockedNegative = 3;

    public static QuirkLimits FromDd1(JObject rules)
    {
        var q = new QuirkLimits();
        if (rules == null) return q;
        q.MaxPositive = (int?)rules["quirks_max_positive"] ?? q.MaxPositive;
        q.MaxNegative = (int?)rules["quirks_max_negative"] ?? q.MaxNegative;
        q.MaxDiseases = (int?)rules["quirks_max_diseases"] ?? q.MaxDiseases;
        q.MaxLockedPositive = (int?)rules["quirks_max_locked_positive"] ?? q.MaxLockedPositive;
        q.MaxLockedNegative = (int?)rules["quirks_max_locked_negative"] ?? q.MaxLockedNegative;
        return q;
    }

    /// <summary>
    /// What gaining <paramref name="quirk"/> does to a hero holding <paramref name="current"/>: whether it is gained,
    /// and which quirk it replaces (null if there was room). Nothing changes if they already have it.
    /// </summary>
    public (bool Gained, string Replaced) Gain(IReadOnlyCollection<string> current, ICollection<string> locked, string quirk,
                                               Func<string, bool> isPositive, Func<string, bool> isDisease, Rng rng)
    {
        if (quirk == null || current.Contains(quirk)) return (false, null);
        string Kind(string q) => isDisease(q) ? "disease" : isPositive(q) ? "positive" : "negative";
        string kind = Kind(quirk);
        int max = kind == "disease" ? MaxDiseases : kind == "positive" ? MaxPositive : MaxNegative;
        var same = current.Where(q => Kind(q) == kind).ToList();
        if (same.Count < max) return (true, null);
        var replaceable = same.Where(q => locked == null || !locked.Contains(q)).OrderBy(q => q, StringComparer.Ordinal).ToList();
        if (replaceable.Count == 0) return (false, null);
        return (true, rng.Pick(replaceable));
    }

    /// <summary>Apply <see cref="Gain"/> to a quirk list. Returns the replaced quirk, or null.</summary>
    public string Apply(List<string> quirks, ICollection<string> locked, string quirk, Func<string, bool> isPositive, Func<string, bool> isDisease, Rng rng, out bool gained)
    {
        var (g, replaced) = Gain(quirks, locked, quirk, isPositive, isDisease, rng);
        gained = g;
        if (!g) return null;
        if (replaced != null) quirks.Remove(replaced);
        quirks.Add(quirk);
        return replaced;
    }
}
