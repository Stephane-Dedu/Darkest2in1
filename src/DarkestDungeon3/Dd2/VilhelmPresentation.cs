using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Combat;
using Assets.Code.Gfx.Timeline;
using DarkestDungeon3.Core.Campaign.Town;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Playables;

namespace DarkestDungeon3.Dd2;

/// <summary>The hidden Crusader's weapon particles do not match the original Onyx Blade motion.</summary>
[HarmonyPatch(typeof(RedhookParticleSystemPlayableAsset),nameof(RedhookParticleSystemPlayableAsset.CreatePlayable))]
internal static class VilhelmPresentation
{
    private static bool Prefix(RedhookParticleSystemPlayableAsset __instance,PlayableGraph graph,GameObject owner,ref Playable __result)
    {
        var actor=owner?.GetComponentInParent<ActorBhv>();
        if (!SoulsHeroClasses.IsVilhelm(actor)) return true;
        var combat=Object.FindObjectsOfType<CombatActorBhv>().FirstOrDefault(c => c.CachedActorBhv==actor);
        if (VilhelmKit.Find(combat?.CurrentSkillPresentation?.SkillId)==null) return true;
        // Keep native animation, camera, signals, damage and target reactions. Only the
        // performer's borrowed particle clips are replaced by his original flame atlas.
        Plugin.Log.LogDebug("[vilhelm] replaced donor particle clip "+__instance.prefab?.name);
        __result=Playable.Create(graph);
        return false;
    }
}
