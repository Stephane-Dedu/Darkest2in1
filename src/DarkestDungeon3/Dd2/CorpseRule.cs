using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.Source;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1's corpse rule in our fights: a monster killed by a critical hit or by bleed/blight (damage over time) leaves
/// no corpse. DD2 turns a dead actor into its death class (its "_corpse" actor) in Kill, asking
/// GetIsDeathClassValid; we remember how the killing damage was dealt and veto ordinary corpses. Boss parts
/// ("boss_..._corpse") and other death classes (phases like the Dreaming General's cadaver) are left alone.
/// </summary>
internal static class CorpseRule
{
    [System.ThreadStatic] private static uint _hitActor;
    [System.ThreadStatic] private static bool _hitIsCrit, _hitIsDot;

    public static bool NoCorpse(ActorInstance actor)
    {
        if (!Dd2Combat.InFight || actor == null || actor.ActorGuid != _hitActor || !(_hitIsCrit || _hitIsDot)) return false;
        if (System.Linq.Enumerable.Contains(Dd2Api.Party, actor.ActorGuid)) return false;   // heroes: DD2's own rules
        string death = actor.DeathActorDataId;
        if (death == null || !death.EndsWith("_corpse") || death.StartsWith("boss_")) return false;
        Plugin.Log.LogInfo($"[combat] {actor.ActorDataId} slain by {(_hitIsCrit ? "a critical hit" : "bleed or blight")}: no corpse");
        return true;
    }

    [HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.ApplyHealthDamage),
        new[] { typeof(float), typeof(bool), typeof(bool), typeof(IReadOnlyList<uint>), typeof(DeathType), typeof(SourceType),
                typeof(IReadOnlyList<string>), typeof(bool), typeof(ActorHealthDamageCalculation) })]
    private static class RememberTheHit
    {
        private static void Prefix(ActorInstance __instance, bool isCrit, SourceType sourceType)
        {
            _hitActor = __instance.ActorGuid;
            _hitIsCrit = isCrit;
            _hitIsDot = sourceType == SourceType.DOT;
        }

        private static void Postfix() => _hitActor = 0;
    }

    [HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.GetIsDeathClassValid))]
    private static class VetoTheCorpse
    {
        private static void Postfix(ActorInstance __instance, ref bool __result)
        {
            if (__result && NoCorpse(__instance)) __result = false;
        }
    }
}
