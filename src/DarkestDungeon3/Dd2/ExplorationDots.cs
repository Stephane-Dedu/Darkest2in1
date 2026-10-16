using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Dot;
using Assets.Code.Effect;
using Assets.Code.Library;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

/// <summary>Transfers individual persisted exploration bleed/blight stacks into DD2 for each fight, then
/// captures surviving stacks before DD2 clears combat containers. Native cures and turn ticks own combat.</summary>
internal static class ExplorationDots
{
    private const string Source = "dd3_exploration";
    private static readonly Dictionary<DotInstance, TravelEffect> Imported = new();
    private static readonly Dictionary<uint, List<TravelEffect>> Pending = new();
    private static readonly HashSet<uint> ActiveActors = new();
    private static readonly HashSet<uint> FinishedActors = new();

    public static void Begin()
    {
        Imported.Clear();
        Pending.Clear();
        ActiveActors.Clear();
        FinishedActors.Clear();
    }

    public static void End() => Begin();

    private static string HeroId(ActorInstance actor)
    {
        var driver = Driver.Instance;
        if (!Dd2Combat.InFight || actor == null || driver?.Party == null || driver.Expedition == null) return null;
        return driver.Expedition.Party.FirstOrDefault(id => driver.Party.Guid(id) == actor.ActorGuid);
    }

    internal static void Import(ActorInstance actor)
    {
        string heroId = HeroId(actor);
        if (heroId == null || !actor.IsLiving) return;
        var state = Driver.Instance.Expedition;
        var library = SingletonMonoBehaviour<Library<string, DotDefinition>>.Instance;
        if (library == null) return;
        ActiveActors.Add(actor.ActorGuid);
        FinishedActors.Remove(actor.ActorGuid);
        Pending[actor.ActorGuid] = new List<TravelEffect>();
        // A native autosave can contain the old proxy stack. The DD1 expedition record is authoritative.
        foreach (var old in actor.DotContainer.GetInstances().Where(d => d.SourceId == Source).ToList())
            actor.DotContainer.Remove(old, SourceType.DRIVING, Source, actor.ActorGuid);
        foreach (var effect in state.TravelEffects.Where(e => e.HeroId == heroId && e.TurnsLeft > 0 && e.DamageFraction > 0).ToList())
        {
            string id = effect.Kind == "bleed" ? "small_bleed_dot" : effect.Kind == "blight" ? "small_blight_dot" : null;
            if (id == null || !library.GetHasLibraryKey(id)) { Pending[actor.ActorGuid].Add(effect); continue; }
            var definition = library.GetLibraryElement(id);
            float baseDamage = definition.m_Effects.Sum(e => e.m_HealthDamageAmount);
            if (baseDamage <= 0) { Pending[actor.ActorGuid].Add(effect); continue; }
            var dot = new DotInstance(definition, actor, SourceType.DRIVING, Source, actor.ActorGuid,
                extraDuration: 0, effectValueChange: 0, effectValueMultiplier: effect.DamageFraction * actor.CurrentHpMax / baseDamage);
            dot.SetDurationAmount(effect.TurnsLeft);
            // Resistance was already rolled by exploration. The instance overload preserves exact duration
            // and tick amount without rolling native application resistance or adding path duration twice.
            actor.DotContainer.Add(dot, isLockedTeamPosition: false);
            if (actor.DotContainer.GetInstances().Contains(dot)) Imported[dot] = effect;
            else Pending[actor.ActorGuid].Add(effect);
        }
    }

    internal static void Capture(ActorInstance actor)
    {
        string heroId = HeroId(actor);
        if (heroId == null || !ActiveActors.Contains(actor.ActorGuid) || FinishedActors.Contains(actor.ActorGuid)) return;
        var state = Driver.Instance.Expedition;
        state.TravelEffects.RemoveAll(e => e.HeroId == heroId);
        if (!actor.IsLiving || Dd2Api.IsDead(actor.ActorGuid)) return;
        if (Pending.TryGetValue(actor.ActorGuid, out var pending)) state.TravelEffects.AddRange(pending);
        foreach (var dot in actor.DotContainer.GetInstances())
        {
            string kind = dot.Definition.m_Type;
            if (kind is not ("bleed" or "blight") || dot.GetDurationAmount() <= 0) continue;
            if (!TryDamage(dot, actor.CurrentHpMax, out float fraction))
            {
                // Current installed bleed/blight definitions all have fixed health damage. Fail visibly
                // if an update introduces a different payload instead of silently changing its rules.
                Plugin.Log.LogWarning("[exploration status] unsupported native damage payload: " + dot.Definition.Id);
                if (Imported.TryGetValue(dot, out var original)) state.TravelEffects.Add(original);
                continue;
            }
            state.TravelEffects.Add(new TravelEffect
            {
                HeroId = heroId, Kind = kind, DamageFraction = fraction, TurnsLeft = dot.GetDurationAmount(),
                StepProgress = Imported.TryGetValue(dot, out var previous) ? previous.StepProgress : 0,
            });
        }
    }

    private static bool TryDamage(DotInstance dot, float hpMax, out float fraction)
    {
        fraction = 0;
        if (hpMax <= 0) return false;
        foreach (EffectDefinition effect in dot.Definition.m_Effects)
        {
            if (!effect.HasHealthDamage || effect.m_HealthDamageAmountRange != 0 || effect.m_HealthDamagePercent != 0
                || effect.m_HealthDamagePercentRange != 0 || effect.m_HealthDamageDownToPercent != 0
                || effect.m_HealthDamageDownToPercentRange != 0 || effect.m_Chance != 1) return false;
            fraction += Math.Max(0, effect.m_HealthDamageAmount + dot.m_EffectValueChange) * dot.m_EffectValueMultiplier / hpMax;
        }
        return fraction > 0;
    }

    internal static void CaptureForSave()
    {
        if (!Dd2Combat.InFight) return;
        foreach (uint guid in ActiveActors.ToArray()) Capture(Dd2Api.Actor(guid));
    }

    internal static void BeforeNativeClear(ActorInstance actor)
    {
        Capture(actor);
        if (actor != null && ActiveActors.Contains(actor.ActorGuid)) FinishedActors.Add(actor.ActorGuid);
    }
}

[HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.OnBattleStart))]
internal static class ImportExplorationStatusesIntoCombat
{
    private static void Postfix(ActorInstance __instance) => ExplorationDots.Import(__instance);
}

[HarmonyPatch(typeof(ActorInstance), "ClearAllCombatSpecificContainers")]
internal static class KeepSurvivingCombatStatusesForExploration
{
    private static void Prefix(ActorInstance __instance) => ExplorationDots.BeforeNativeClear(__instance);
}

[HarmonyPatch(typeof(Session), nameof(Session.Persist))]
internal static class CaptureExplorationStatusesBeforeSave
{
    private static void Prefix() => ExplorationDots.CaptureForSave();
}
