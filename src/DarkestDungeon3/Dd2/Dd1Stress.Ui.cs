using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.UI;
using Assets.Code.UI.Tooltips;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestDungeon3.Dd2;

[HarmonyPatch]
internal static class ResolveNativeTooltip
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ActorInfoUiBhv),"PopulateStatusBarTooltips");
        yield return AccessTools.Method(typeof(ActorStatusUiBhv),"PopulateStatusBarTooltips");
    }
    private static bool Prefix(object __instance,object[] __args)
    {
        var actor=__args.Length>0 ? __args[0] as ActorInstance : AccessTools.Field(typeof(ActorStatusUiBhv),"m_actor").GetValue(__instance) as ActorInstance;
        if(!Dd1Stress.Owns(actor))return true;
        var tooltips=AccessTools.Field(__instance.GetType(),"m_statusBarTooltips").GetValue(__instance) as List<TextTooltipBhv>;
        foreach(var tip in tooltips ?? new()) tip.AddPostStartCallback(()=>tip.Populate(
            actor.ActorName+"\nHealth "+actor.DisplayedHp+"/"+actor.DisplayedHpMax+"\n"+Dd1Stress.Description(actor)));
        return false;
    }
}
[HarmonyPatch(typeof(StressBarBhv),nameof(StressBarBhv.SetValue))]
internal static class ResolveNativePips
{
    private static void Postfix(StressBarBhv __instance)
    {
        var actor=AccessTools.Field(typeof(StressBarBhv),"m_actor").GetValue(__instance) as ActorInstance;
        if(!Dd1Stress.Owns(actor))return;
        var pips=(List<Image>)AccessTools.Field(typeof(StressBarBhv),"m_stressPips").GetValue(__instance);
        for(int i=0;i<pips.Count;i++)
        {
            var normal=Color.Lerp(new Color(.23f,.23f,.23f),new Color(.75f,.73f,.67f),Mathf.Clamp01(actor.Stress-i));
            pips[i].color=Color.Lerp(normal,new Color(1f,.85f,.62f),Mathf.Clamp01(actor.Stress-10-i));
        }
    }
}

// Rescale presentation only: native event listeners and stress calculations still receive compatible units.
[HarmonyPatch(typeof(PopTextManager),"AddToQueue")]
internal static class ResolveNativeStressNumbers
{
    private static void Prefix(PopTextQueueElement element)
    {
        var type=element.EffectDisplayType;
        if (Dd1Stress.Owns(Dd2Api.Actor(element.ActorGuid)) && element.NumberData.HasValue
            && (type==EffectDisplayType.STRESS_DAMAGE || type==EffectDisplayType.STRESS_DAMAGE_SMALL
                || type==EffectDisplayType.STRESS_HEAL || type==EffectDisplayType.STRESS_HEAL_SMALL))
            element.NumberData*=Core.Expedition.ResolveStress.Dd1PerUnit;
    }
}
