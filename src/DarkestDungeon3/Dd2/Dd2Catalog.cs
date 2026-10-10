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
    private IReadOnlyList<string> _classes;
    private bool _classesWithCrusader;

    public Dd2Catalog(Dd1Lore lore) => _lore = lore;

    /// <summary>The stagecoach's classes (<see cref="RecruitClasses"/>): the Crusader once DD2's owned resources show
    /// his DLC.</summary>
    public IReadOnlyList<string> RecruitableClasses
    {
        get
        {
            bool crusader = ActorResources.Has(RecruitClasses.Crusader) == true;
            if (_classes == null || crusader != _classesWithCrusader)
            {
                _classes = RecruitClasses.For(crusader, Dd1HeroClasses.Available(RecruitClasses.Shieldbreaker));
                _classesWithCrusader = crusader;
                Plugin.Log.LogInfo("[stagecoach] classes: " + string.Join(", ", _classes));
            }
            return _classes;
        }
    }

    public string RandomName(string classId, Rng rng) => _lore.RandomName(rng);

    // DD2's quirk and item libraries only exist inside a run; the Hamlet reads DD2's data tables instead.
    private static Core.Dd2Data.Dd2Tables _tables;
    public static Core.Dd2Data.Dd2Tables Tables => _tables ??= LoadTables();

    private static Core.Dd2Data.Dd2Tables LoadTables()
    {
        var t = Core.Dd2Data.Dd2Tables.Load(System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "Excel"));
        Plugin.Log.LogInfo($"[dd2] data tables: {t.Quirks.Count} quirks, {t.Trinkets.Count} trinkets");
        return t;
    }

    private static IEnumerable<Core.Dd2Data.Dd2Quirk> AllQuirks => Tables.Quirks.Values;

    /// <summary>DD2's QuirkDefinition (inside a run only), for putting a quirk on an actor.</summary>
    public static QuirkDefinition Definition(string id) =>
        id != null && SingletonMonoBehaviour<Library<string, QuirkDefinition>>.Instance is { } lib && lib.TryGetLibraryElement(id, out var q) ? q : null;

    public IReadOnlyList<string> StartingQuirks(string classId, Rng rng, int positives, int negatives)
    {
        // DD2's own starting pools (pos_start / neg_start), sorted so a seed always gives the same quirks.
        var pos = AllQuirks.Where(q => q.IsPositive && !q.IsDisease && q.IsStarting).OrderBy(q => q.Id, StringComparer.Ordinal).ToList();
        var neg = AllQuirks.Where(q => q.IsNegative && !q.IsDisease && q.IsStarting).OrderBy(q => q.Id, StringComparer.Ordinal).ToList();
        var result = new List<string>();
        for (int i = 0; i < positives && pos.Count > 0; i++) result.Add(rng.Pick(pos).Id);
        for (int i = 0; i < negatives && neg.Count > 0; i++) result.Add(rng.Pick(neg).Id);
        return result.Distinct().ToList();
    }

    /// <summary>The DD2 quirk standing in for a DD1 quirk: same id if DD2 has it, else one of the same kind.</summary>
    public string MapDd1Quirk(string dd1QuirkId)
    {
        if (dd1QuirkId == null) return null;
        if (Tables.Quirks.ContainsKey(dd1QuirkId)) return dd1QuirkId;
        foreach (var guess in new[] { "quirk_" + dd1QuirkId, "quirk_" + dd1QuirkId + "_pos", "quirk_" + dd1QuirkId + "_neg" })
            if (Tables.Quirks.ContainsKey(guess)) return guess;
        var (positive, disease) = _lore.Quirks.TryGetValue(dd1QuirkId, out var kind) ? kind : (false, false);
        var pool = AllQuirks.Where(q => disease ? q.IsDisease : positive ? q.IsPositive && !q.IsDisease : q.IsNegative && !q.IsDisease)
                            .OrderBy(q => q.Id, StringComparer.Ordinal).ToList();
        if (pool.Count == 0) return null;
        int h = 17;
        foreach (char c in dd1QuirkId) h = unchecked(h * 31 + c);
        return pool[(int)((uint)h % (uint)pool.Count)].Id;
    }

    public QuirkDefinition QuirkFor(string dd1QuirkId) => Definition(MapDd1Quirk(dd1QuirkId));

    public bool IsDisease(string quirkId) => quirkId != null && Tables.Quirks.TryGetValue(quirkId, out var q) && q.IsDisease;
    public bool IsPositive(string quirkId) => quirkId != null && Tables.Quirks.TryGetValue(quirkId, out var q) && q.IsPositive;

    /// <summary>DD1 rarity → DD2 trinket rarities (DD2's "cultist" trinkets are the Cult's cursed ones: never sold).</summary>
    private static readonly Dictionary<string, string[]> Dd2Rarities = new()
    {
        ["very_common"] = new[] { "common" },
        ["common"] = new[] { "common" },
        ["uncommon"] = new[] { "common", "rare" },
        ["rare"] = new[] { "rare" },
        ["very_rare"] = new[] { "epic" },
        ["ancestral"] = new[] { "ancestral" },
    };

    public string RandomTrinket(string rarity, Rng rng, string forClass = null)
    {
        var all = Tables.Trinkets.Values.Where(t => t.Rarity != "cultist" && (t.HeroClass == null || t.HeroClass == forClass))
                                        .OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (all.Count == 0) return null;
        var wanted = rarity != null && Dd2Rarities.TryGetValue(rarity, out var r) ? r : new[] { "common" };
        var matching = all.Where(t => wanted.Contains(t.Rarity)).ToList();
        return rng.Pick(matching.Count > 0 ? matching : all).Id;
    }

    public bool TrinketFits(string trinketId, string classId) =>
        Dd1TrinketData.Get(trinketId) is { } memory ? memory.HeroClasses.Count == 0
            || memory.HeroClasses.Contains(Runtime.Session.Current.Campaign.HeroUpgrades.Dd1Class(classId))
            : trinketId == null || !Tables.Trinkets.TryGetValue(trinketId, out var t) || t.IsForHero(classId);

    public int TrinketEquipLimit(string trinketId) =>
        Tables.Trinkets.TryGetValue(trinketId, out var t) ? t.EquipLimit : 1;

    /// <summary>DD1 prices by rarity (the Nomad Wagon sells DD2 trinkets at DD1 prices).</summary>
    public int TrinketPrice(string trinketId)
    {
        if (Dd1TrinketData.Get(trinketId) is { } memory) return memory.Price;
        string dd2 = trinketId != null && Tables.Trinkets.TryGetValue(trinketId, out var t) ? t.Rarity : "common";
        string rarity = dd2 switch { "epic" => "very_rare", "ancestral" => "very_rare", "rare" => "rare", _ => "common" };
        return _lore.TrinketPriceByRarity.TryGetValue(rarity, out var p) ? p : 7500;
    }
}
