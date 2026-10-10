using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.Skill;
using Assets.Code.Source;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1's corpse rule in our fights: a DD1 monster leaves a corpse only if its class has one (death_class in its
/// info.darkest; maggots don't), and never after a critical hit or bleed/blight (damage over time). Every death goes
/// through ActorInstance.Kill: a lethal skill hit is killed there directly (SkillCalculation.ApplyActorResults, no
/// ApplyHealthDamage), a damage-over-time or effect kill too. The rule is decided there from the skill's results or the
/// killing damage, and DD2's own SetDeathClassIgnored keeps the corpse away for both of DD2's checks (the death itself
/// and, later, the death presentation). Boss parts ("boss_..._corpse") and other death classes (phases like the
/// Dreaming General's cadaver) are left alone.
/// </summary>
internal static class CorpseRule
{
    [System.ThreadStatic] private static uint _hitActor;
    [System.ThreadStatic] private static bool _hitIsCrit, _hitIsDot;
    [System.ThreadStatic] private static HashSet<uint> _critKills;

    private static Core.Dd1.Dd1Corpse _corpses;

    public static bool NoCorpse(ActorInstance actor, bool crit, bool dot)
    {
        if (!Dd2Combat.InFight || actor == null) return false;
        if (System.Linq.Enumerable.Contains(Dd2Api.Party, actor.ActorGuid)) return false;   // heroes: DD2's own rules
        string death = actor.DeathActorDataId;
        if (death == null || !death.EndsWith("_corpse") || death.StartsWith("boss_")) return false;
        // The DD1 monster drawn over this stand-in decides (its death_class); other enemies get DD1's usual rule.
        string dd1 = Dd1MonsterView.Dd1Of(actor.ActorGuid);
        var install = Runtime.Session.Current?.Dd1;
        if (_corpses == null && install != null) _corpses = new Core.Dd1.Dd1Corpse(install);
        bool leaves = (dd1 != null ? _corpses?.LeavesCorpse(dd1, crit, dot) : null) ?? !(crit || dot);
        if (leaves) return false;
        string why = crit ? "a critical hit" : dot ? "bleed or blight" : "DD1 gives it no corpse";
        Plugin.Log.LogInfo($"[combat] {dd1 ?? actor.ActorDataId} slain ({why}): no corpse");
        return true;
    }

    /// <summary>A skill's lethal hits are killed outright: remember which of them were critical.</summary>
    [HarmonyPatch(typeof(SkillCalculation), "ApplyActorResults")]
    private static class RememberLethalCrits
    {
        private static void Prefix(IReadOnlyList<SkillCalculation.ActorResult> actorResults)
        {
            (_critKills ??= new HashSet<uint>()).Clear();
            if (actorResults == null) return;
            foreach (var result in actorResults)
                if (result != null && result.IsDamageKill && result.IsCrit) _critKills.Add(result.m_TargetActorGuid);
        }

        private static void Postfix() => _critKills?.Clear();
    }

    /// <summary>Damage that kills through ApplyHealthDamage (effects, damage over time): its crit and source.</summary>
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

    [HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.Kill),
        new[] { typeof(DeathType), typeof(SourceType), typeof(IReadOnlyList<string>), typeof(float), typeof(IReadOnlyList<uint>) })]
    private static class DecideAtDeath
    {
        private static void Prefix(ActorInstance __instance, SourceType sourceType)
        {
            if (__instance == null || !__instance.IsLiving) return;
            uint guid = __instance.ActorGuid;
            bool hit = guid == _hitActor;
            bool crit = _critKills != null && _critKills.Contains(guid) || hit && _hitIsCrit;
            bool dot = sourceType == SourceType.DOT || hit && _hitIsDot;
            if (NoCorpse(__instance, crit, dot)) __instance.SetDeathClassIgnored();
        }
    }
}
