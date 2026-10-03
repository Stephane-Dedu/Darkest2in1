using System.Collections.Generic;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>
/// Extra zones (DD2's regions, an estate option) borrow a DD1 zone's data: its maps, curios, quest tables, heirlooms,
/// loot and art. Filled from data/zones.json ("dd1_zone", "region_boss"); every DD1 lookup by zone goes through
/// <see cref="Of"/>, so a DD1 zone is its own base.
/// </summary>
public static class ZoneBase
{
    private static readonly Dictionary<string, (string Dd1Zone, string Boss)> Extra = new();

    public static void Register(string zone, string dd1Zone, string boss)
    {
        if (!string.IsNullOrEmpty(zone) && !string.IsNullOrEmpty(dd1Zone)) Extra[zone] = (dd1Zone, boss);
    }

    /// <summary>The DD1 zone whose data this zone uses (itself for DD1's own zones).</summary>
    public static string Of(string zone) => zone != null && Extra.TryGetValue(zone, out var e) ? e.Dd1Zone : zone;

    /// <summary>One of the extra zones (DD2 regions): its fights are DD2's natives, not translated DD1 encounters.</summary>
    public static bool IsExtra(string zone) => zone != null && Extra.ContainsKey(zone);

    /// <summary>The lair boss an extra zone's kill-boss quests lead to (a key of its zones.json "bosses").</summary>
    public static string BossOf(string zone) => zone != null && Extra.TryGetValue(zone, out var e) ? e.Boss : null;

    public static IEnumerable<string> ExtraZones => Extra.Keys;
}
