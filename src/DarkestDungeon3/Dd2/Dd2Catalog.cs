using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Quirk;
using Assets.Code.Utils;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's heroes, quirks and trinkets as the Hamlet sees them. DD1 quirks are matched to DD2 quirks by id when
/// both games have one, otherwise to a DD2 quirk of the same kind (positive / negative / disease), chosen
/// stably from the DD1 id so the same DD1 quirk always becomes the same DD2 quirk.
/// </summary>
internal sealed class Dd2Catalog : IHeroCatalog
{
    /// <summary>Used if DD2's library can't be read yet (e.g. before the game finished loading).</summary>
    private static readonly string[] FallbackClasses =
        { "flagellant", "grave_robber", "hellion", "highwayman", "jester", "leper", "man_at_arms", "occultist", "plague_doctor", "runaway", "vestal" };

    /// <summary>DD1 rarity → DD2 trinket tags that count as that rarity.</summary>
    private static readonly Dictionary<string, string[]> RarityTags = new()
    {
        ["very_common"] = new[] { "common" },
        ["common"] = new[] { "common" },
        ["uncommon"] = new[] { "uncommon" },
        ["rare"] = new[] { "rare" },
        ["very_rare"] = new[] { "very_rare", "rare" },
        ["ancestral"] = new[] { "very_rare", "unique" },
    };

    private readonly Dd1Lore _lore;
    private List<string> _classes;

    public Dd2Catalog(Dd1Lore lore) => _lore = lore;

    public IReadOnlyList<string> RecruitableClasses => _classes ??= ReadClasses();

    private static List<string> ReadClasses()
    {
        try
        {
            var lib = SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance;
            var classes = lib?.GetLibraryElements(c => c.IsPopulateInRoster && c.GetPotentialTags().Contains("hero"))
                              .Select(c => c.Id).Distinct().ToList();
            if (classes != null && classes.Count >= 4) return classes;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"Hero class list unavailable, using defaults: {e.Message}"); }
        return FallbackClasses.ToList();
    }

    public string RandomName(string classId, Rng rng) => _lore.RandomName(rng);

    private static IReadOnlyList<QuirkDefinition> AllQuirks =>
        SingletonMonoBehaviour<Library<string, QuirkDefinition>>.Instance?.GetLibraryElements() ?? new List<QuirkDefinition>();

    private static QuirkDefinition Quirk(string id) =>
        id != null && SingletonMonoBehaviour<Library<string, QuirkDefinition>>.Instance is { } lib && lib.TryGetLibraryElement(id, out var q) ? q : null;

    public IReadOnlyList<string> StartingQuirks(string classId, Rng rng, int positives, int negatives)
    {
        var pos = AllQuirks.Where(q => q.IsPositive && !q.IsDisease).ToList();
        var neg = AllQuirks.Where(q => q.IsNegative && !q.IsDisease).ToList();
        var result = new List<string>();
        for (int i = 0; i < positives && pos.Count > 0; i++) result.Add(rng.Pick(pos).Id);
        for (int i = 0; i < negatives && neg.Count > 0; i++) result.Add(rng.Pick(neg).Id);
        return result.Distinct().ToList();
    }

    public QuirkDefinition QuirkFor(string dd1QuirkId)
    {
        if (dd1QuirkId == null) return null;
        var same = Quirk(dd1QuirkId);
        if (same != null) return same;
        var (positive, disease) = _lore.Quirks.TryGetValue(dd1QuirkId, out var kind) ? kind : (false, false);
        var pool = AllQuirks.Where(q => disease ? q.IsDisease : positive ? q.IsPositive && !q.IsDisease : q.IsNegative && !q.IsDisease)
                            .OrderBy(q => q.Id, StringComparer.Ordinal).ToList();
        if (pool.Count == 0) return null;
        int h = 17;
        foreach (char c in dd1QuirkId) h = unchecked(h * 31 + c);
        return pool[(int)((uint)h % (uint)pool.Count)];
    }

    public string MapDd1Quirk(string dd1QuirkId) => QuirkFor(dd1QuirkId)?.Id;

    public bool IsDisease(string quirkId) => Quirk(quirkId)?.IsDisease ?? false;
    public bool IsPositive(string quirkId) => Quirk(quirkId)?.IsPositive ?? false;

    private static IReadOnlyList<ItemDefinition> Trinkets =>
        SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance?.GetLibraryElements(i => i.m_type == ItemType.TRINKET)
        ?? new List<ItemDefinition>();

    public string RandomTrinket(string rarity, Rng rng)
    {
        var all = Trinkets;
        if (all.Count == 0) return null;
        var tags = rarity != null && RarityTags.TryGetValue(rarity, out var t) ? t : Array.Empty<string>();
        var matching = all.Where(i => i.m_tags.Any(tags.Contains)).ToList();
        return rng.Pick(matching.Count > 0 ? matching : all).m_id;
    }

    /// <summary>DD1 prices by rarity (the Nomad Wagon sells DD2 trinkets at DD1 prices).</summary>
    public int TrinketPrice(string trinketId)
    {
        var item = Trinkets.FirstOrDefault(i => i.m_id == trinketId);
        string rarity = item == null ? "common"
            : item.m_tags.Contains("very_rare") ? "very_rare"
            : item.m_tags.Contains("rare") ? "rare"
            : item.m_tags.Contains("uncommon") ? "uncommon"
            : "common";
        return _lore.TrinketPriceByRarity.TryGetValue(rarity, out var p) ? p : 7500;
    }
}
