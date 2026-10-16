using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Source;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// The crawl's view of the party, backed by real DD2 actors and persistent DD1 resolve conditions,
/// so out-of-combat effects land with the same rules as in a fight.
/// </summary>
internal sealed class Dd2Party : IParty, ITravelResistance, IResolveParty
{
    private readonly Dictionary<string, uint> _guids;     // hero id → actor guid, rank order
    private readonly Dd2Catalog _catalog;

    public Dd2Party(Dictionary<string, uint> guids, Dd2Catalog catalog)
    {
        _guids = guids;
        _catalog = catalog;
    }

    public uint Guid(string heroId) => _guids.TryGetValue(heroId, out var g) ? g : 0u;
    public bool Allows(string heroId,string reaction) => reaction.StartsWith("effect:")
        ? !Dd1Stress.RefusesEffect(Dd2Api.Actor(Guid(heroId)),reaction.Substring(7))
        : !Dd1Stress.Refuses(Dd2Api.Actor(Guid(heroId)),reaction);

    /// <summary>Every hero's DD2 actor id.</summary>
    public IEnumerable<uint> Guids => _guids.Values;

    public IReadOnlyList<string> Alive =>
        _guids.Where(kv => !Dd2Api.IsDead(kv.Value)).Select(kv => kv.Key).ToList();

    public float HpFraction(string heroId) => Dd2Api.HpFraction(Dd2Api.Actor(Guid(heroId)));

    public float Resistance(string heroId, string kind) => Dd2Api.Actor(Guid(heroId))?.ActorData
        .GetDataContainerSum<ActorDataStats>().StatContainer.GetStatTotal(ActorStatType.RESISTANCE, kind) ?? 0f;

    public void Damage(string heroId, float maxHpFraction, string cause)
    {
        var a = Dd2Api.Actor(Guid(heroId));
        if (a == null || maxHpFraction <= 0) return;
        a.ApplyHealthDamage(maxHpFraction * a.CurrentHpMax, isCrit: false, isRiposte: false, a, DeathType.EFFECT,
                            SourceType.DRIVING, cause, hasDisplayed: false);
    }

    public void Heal(string heroId, float maxHpFraction)
    {
        var a = Dd2Api.Actor(Guid(heroId));
        if (a == null || maxHpFraction <= 0) return;
        a.ApplyHealthHeal(maxHpFraction * a.CurrentHpMax, isCrit: false, SourceType.DRIVING, hasDisplayed: false);
    }

    public void AddStress(string heroId, float points, string cause)
    {
        var a = Dd2Api.Actor(Guid(heroId));
        if (a == null || points == 0) return;
        if (points > 0) a.ApplyStressDamage(points, canResist: true, SourceType.DRIVING, cause, 0u);
        else a.ApplyStressHeal(-points, SourceType.DRIVING);
    }

    public string AddDd1Quirk(string heroId, string dd1QuirkId)
    {
        var a = Dd2Api.Actor(Guid(heroId));
        var quirk = _catalog.QuirkFor(dd1QuirkId, Runtime.Session.Current?.Save.Estate.Hero(heroId)?.ClassId);
        if (a?.QuirkContainer == null || quirk == null) return null;
        // DD1's limits (5 positive, 5 negative, 3 diseases): over the cap the new quirk replaces an unlocked one of
        // its kind, or isn't gained if they're all locked.
        var s = Runtime.Session.Current;
        var instances = a.QuirkContainer.GetInstances().ToList();
        var current = instances.Select(i => i.Definition.m_Id).ToList();
        var locked = s?.Save.Estate.Hero(heroId)?.LockedQuirks ?? new List<string>();
        var limits = s?.Campaign?.QuirkLimits ?? new Core.Campaign.QuirkLimits();
        var (gained, replaced) = limits.Gain(current, locked, quirk.m_Id, _catalog.IsPositive, _catalog.IsDisease, new Core.Rng(System.Environment.TickCount));
        if (!gained) return null;
        if (replaced != null && instances.FirstOrDefault(i => i.Definition.m_Id == replaced) is { } old)
        {
            a.QuirkContainer.Remove(old, SourceType.DRIVING, "dd3", 0u);
            Plugin.Log.LogInfo($"[quirks] {heroId}: {quirk.m_Id} replaces {replaced} (DD1 quirk limit)");
        }
        a.QuirkContainer.Add(quirk, SourceType.DRIVING, dd1QuirkId, 0u);
        return quirk.m_Id;
    }

    public string PurgeNegative(string heroId) => RemoveFirst(heroId, q => q.IsNegative || q.IsDisease);

    public string CureDisease(string heroId) => RemoveFirst(heroId, q => q.IsDisease);

    private string RemoveFirst(string heroId, System.Predicate<Assets.Code.Quirk.QuirkDefinition> which)
    {
        var a = Dd2Api.Actor(Guid(heroId));
        var target = a?.QuirkContainer?.GetInstances().FirstOrDefault(i => which(i.Definition));
        if (target == null) return null;
        a.QuirkContainer.Remove(target, SourceType.DRIVING, "dd3", 0u);
        return target.Definition.m_Id;
    }
}
