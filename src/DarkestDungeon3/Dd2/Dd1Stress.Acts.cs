using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Actor.ActorController;
using Assets.Code.Buff;
using Assets.Code.Combat;
using Assets.Code.Dot;
using Assets.Code.Effect;
using Assets.Code.Library;
using Assets.Code.Source;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

internal static partial class Dd1Stress
{
    internal static void Turn(ActorInstance actor,uint actorGuid,BattleTurnType turn,bool valid)
    {
        if (!Owns(actor) || actor.ActorGuid!=actorGuid || !actor.IsLiving) return;
        var binding=Actors[actorGuid];binding.Pass=binding.RandomCommand=false;binding.RandomSkill=null;
        if (!valid || turn.m_IsExtra || binding.Restoring || binding.Applying || binding.State.IsToxic || binding.State.TraitId==null) return;
        var act=Session.Current.ResolveRules.PickAct(binding.State,binding.Hero.ClassId,NextRng());
        if (act == null || act.Id=="nothing") return;
        var allies=Actors.Values.Where(b=>Owns(b.Actor) && b.Actor.IsLiving && b.Actor.TeamIndex==actor.TeamIndex).Select(b=>b.Actor).ToList();
        var others=allies.Where(a=>a!=actor).ToList();
        ActorInstance PickOther() => others.Count>0 ? NextRng().Pick(others) : null;
        switch (act.Id)
        {
            case "ignore_command": binding.Pass=true;break;
            case "random_command": binding.RandomCommand=true;break;
            case "attack_self": Hurt(actor,actor,act.Amount);break;
            case "attack_friendly":
                if (PickOther() is { } target) { Hurt(actor,target,act.Amount); ApplyDd1Effect(actor,target,act.Effect);binding.Pass=true; }
                break;
            case "bark_stress": if (PickOther() is { } stressed) ApplyDd1Effect(actor,stressed,act.Effect);break;
            case "stress_heal_self": ApplyDd1Effect(actor,actor,act.Effect);break;
            case "stress_heal_party": foreach (var ally in allies) ApplyDd1Effect(actor,ally,act.Effect);break;
            case "heal_self": actor.ApplyHealthHeal(actor.CurrentHpMax*act.Amount,false,SourceType.STATUS,false);break;
            case "buff_random_party_member": if (PickOther() is { } buffed) ApplyDd1Effect(actor,buffed,act.Effect);break;
            case "buff_party": foreach (var ally in allies) ApplyDd1Effect(actor,ally,act.Effect);break;
            case "mark_self": NativeEffect(actor,"m_TokenAddTag,combo,\nm_TokenAddAmount,1,\n");break;
            case "change_pos":
                bool back=binding.State.TraitId=="fearful" || (binding.State.TraitId!="masochistic" && NextRng().Chance(.5));
                actor.MoveTeamPosition(back ? -1 : 1,SourceType.STATUS,actorGuid);break;
        }
        Ui.Gui.Announce(binding.Hero.Name+": "+Name(binding.State.TraitId)+" · "+Ui.HamletUi.Pretty(act.Id),2.5f);
        Plugin.Log.LogInfo($"[resolve act] {binding.Hero.Id}: {act.Id}");
    }
    private static void Hurt(ActorInstance source,ActorInstance target,float fraction)
    { if (fraction>0) target.ApplyHealthDamage(target.CurrentHpMax*fraction,false,false,source,DeathType.EFFECT,SourceType.STATUS,"dd1_affliction",false); }

    private static void NativeEffect(ActorInstance target,string csv)
    {
        var effect=new EffectDefinition("dd3_resolve_act",csv+"m_IgnoreResist,True,\n");
        EffectApply.Apply(new AppliedEffects.Input<EffectDefinition>(new[]{effect},target),SourceType.STATUS,Source);
    }
    private static void ApplyDd1Effect(ActorInstance performer,ActorInstance target,string name)
    {
        var e=Session.Current?.Content?.Effects.Get(name);if (e==null || target==null || !target.IsLiving) return;
        if(RefusesEffect(target,name))return;
        float stress=e.Float("stress"),relief=e.Float("healstress");
        if (stress>0) target.ApplyStressDamage(ResolveStress.Units(stress),false,SourceType.STATUS,Source,performer.ActorGuid);
        if (relief>0) target.ApplyStressHeal(ResolveStress.Units(relief),SourceType.STATUS);
        if (e.Float("tag")>0) NativeEffect(target,"m_TokenAddTag,combo,\nm_TokenAddAmount,1,\n");
        foreach (var buffId in e.Values("buff_ids"))
        {
            var dd1=Session.Current.Content.Buffs.Get(buffId);var effect=Dd1TrinketEffect.Adapt(dd1);
            if (effect?.Stats==null) continue;
            string id="dd3_resolve_act_"+buffId;
            var buffs=SingletonMonoBehaviour<Library<string,BuffDefinition>>.Instance;
            if (!buffs.GetHasLibraryKey(id))
            {
                var stats=new ActorDataStats(id,effect.Stats);stats.Init(id);
                SingletonMonoBehaviour<Library<string,ActorDataStats>>.Instance.AddLibraryElement(stats,true);
                int turns=dd1.Duration>0 ? dd1.Duration : 3;
                var definition=new BuffDefinition(id,"m_DurationType,performer_turn_end,\nm_DurationAmount,"+turns.ToString(CultureInfo.InvariantCulture)+",\nm_IsVisible,True,\n");definition.Init();
                buffs.AddLibraryElement(definition,true);
            }
            target.BuffContainer.TryAdd(buffs.GetLibraryElement(id),false,SourceType.STATUS,"dd1_resolve_act",performer.ActorGuid);
        }
        target.BuffContainer.RefreshActiveBuffs();target.RefreshStats();
        var effects=SingletonMonoBehaviour<Library<string,EffectDefinition>>.Instance;
        var mapped=Dd1SkillToDd2.Map(e).Target.Where(id=>id.StartsWith("skill_dot_") || id.StartsWith("dot_horror_") || id=="add_1_stun")
            .Select(id=>effects.GetLibraryElement(id)).Where(definition=>definition!=null).ToList();
        if(mapped.Count>0) EffectApply.Apply(new AppliedEffects.Input<EffectDefinition>(mapped,1,performer,target),SourceType.STATUS,Source);
    }
    internal static bool Refuses(ActorInstance actor,string reaction)
    {
        if (!Owns(actor)) return false;
        var binding=Actors[actor.ActorGuid];if(binding.Restoring || binding.Applying || !actor.IsLiving)return false;
        var trait=Session.Current?.ResolveRules?.Get(binding.State.TraitId);
        if (trait?.Reactions.TryGetValue(reaction,out var act)!=true || act.Chance<=0 || !NextRng().Chance(act.Chance))return false;
        Ui.Gui.Announce(binding.Hero.Name+": "+Name(binding.State.TraitId)+" · Refuses",2.5f);
        Plugin.Log.LogInfo($"[resolve reaction] {binding.Hero.Id}: {reaction}");
        return true;
    }
    internal static bool RefusesRetreat() => Actors.Values.Where(b=>b.Actor.IsLiving && Owns(b.Actor)).Any(b=>Refuses(b.Actor,"block_combat_retreat"));
    internal static bool RefusesEffect(ActorInstance actor,string effect)
    {
        var trait=Session.Current?.ResolveRules?.Get(Trait(actor));
        return trait?.Reactions.TryGetValue("block_effect",out var reaction)==true && reaction.Effect==effect && Refuses(actor,"block_effect");
    }
    internal static void Reaction(ActorInstance actor,string reaction)
    {
        if(!Owns(actor) || !actor.IsLiving)return;
        var binding=Actors[actor.ActorGuid];var trait=Session.Current.ResolveRules.Get(binding.State.TraitId);
        if(binding.Restoring || binding.Applying || trait?.Reactions.TryGetValue(reaction,out var act)!=true || act.Chance<=0)return;
        if(!NextRng().Chance(act.Chance))return;
        var others=Actors.Values.Where(b=>Owns(b.Actor) && b.Actor.IsLiving && b.Actor.TeamIndex==actor.TeamIndex && b.Actor!=actor).Select(b=>b.Actor).ToList();
        if(others.Count>0)ApplyDd1Effect(actor,NextRng().Pick(others),act.Effect);
        Ui.Gui.Announce(binding.Hero.Name+": "+Name(binding.State.TraitId),2.5f);
    }
    internal static bool Pass(ActorInstance actor) => Owns(actor) && Actors[actor.ActorGuid].Pass;
    internal static bool Pass(ActorControllerBase controller,ActorInstance actor)
    {
        if (Automatic(actor) && !controller.GetValidSkillTargetEntries().Any(e=>e.IsValid && e.m_ValidTargetActorGuids.Count>0))
        { Actors[actor.ActorGuid].RandomCommand=false;Actors[actor.ActorGuid].Pass=true; }
        return Pass(actor);
    }
    internal static bool Automatic(ActorInstance actor) => Owns(actor) && Actors[actor.ActorGuid].RandomCommand;
    internal static string ChooseSkill(ActorControllerBase controller,ActorInstance actor)
    {
        var entries=controller.GetValidSkillTargetEntries().Where(e=>e.IsValid && e.m_ValidTargetActorGuids.Count>0).ToList();
        if(entries.Count==0)return null;
        var binding=Actors[actor.ActorGuid];binding.RandomSkill=NextRng().Pick(entries);return binding.RandomSkill.m_SkillId;
    }
    internal static uint ChooseTarget(ActorInstance actor)
    {
        var targets=Actors[actor.ActorGuid].RandomSkill?.m_ValidTargetActorGuids;
        return targets?.Count>0 ? NextRng().Pick(targets) : 0;
    }
}

[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.OnStartTurn))]
internal static class ResolveStartTurn
{
    private static void Postfix(ActorInstance __instance,uint actorGuid,BattleTurnType battleTurnType,bool isActOutsValid)
        => Dd1Stress.Turn(__instance,actorGuid,battleTurnType,isActOutsValid);
}
[HarmonyPatch]
internal static class ResolvePassCommand
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Assets.Code.Actor.ActorController.ActorControllerInput"),"CalculateIsSkip");
    private static void Postfix(ActorControllerBase __instance,ref bool __result)
    { var actor=(ActorInstance)AccessTools.Field(typeof(ActorControllerBase),"m_PerformerActor").GetValue(__instance);__result|=Dd1Stress.Pass(__instance,actor); }
}
[HarmonyPatch]
internal static class ResolveRandomCommand
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Assets.Code.Actor.ActorController.ActorControllerInput"),"SelectSkilIId");
    private static bool Prefix(ActorControllerBase __instance,ref string __result)
    {
        var actor=(ActorInstance)AccessTools.Field(typeof(ActorControllerBase),"m_PerformerActor").GetValue(__instance);
        if(!Dd1Stress.Automatic(actor))return true;__result=Dd1Stress.ChooseSkill(__instance,actor);return false;
    }
}
[HarmonyPatch]
internal static class ResolveRandomTarget
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Assets.Code.Actor.ActorController.ActorControllerInput"),"SelectTargetGuid");
    private static bool Prefix(ActorControllerBase __instance,ref uint __result)
    {
        var actor=(ActorInstance)AccessTools.Field(typeof(ActorControllerBase),"m_PerformerActor").GetValue(__instance);
        if(!Dd1Stress.Automatic(actor))return true;__result=Dd1Stress.ChooseTarget(actor);return false;
    }
}
[HarmonyPatch]
internal static class ResolveAutomaticInput
{
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Assets.Code.Actor.ActorController.ActorControllerInput"),"GetIsInputValid");
    private static void Postfix(ActorControllerBase __instance,ref bool __result)
    { var actor=(ActorInstance)AccessTools.Field(typeof(ActorControllerBase),"m_PerformerActor").GetValue(__instance);if(Dd1Stress.Automatic(actor))__result=false; }
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.ApplyHealthHeal))]
internal static class ResolveRefuseHealing
{
    private static void Prefix(ActorInstance __instance,ref float heal)
    { if(heal>0 && Dd1Stress.Refuses(__instance,"block_heal"))heal=0; }
}

[HarmonyPatch(typeof(Assets.Code.Skill.Events.EventSkillFinalizeResults),nameof(Assets.Code.Skill.Events.EventSkillFinalizeResults.Trigger))]
internal static class ResolveSkillReactions
{
    private static void Postfix(SkillCalculation.SkillResult skillResult)
    {
        foreach(var result in skillResult.ActorResults)
        {
            var target=Dd2Api.Actor(result.m_TargetActorGuid);var performer=Dd2Api.Actor(result.m_PerformerActorGuid);
            if(target==null || performer==null || target.TeamIndex==performer.TeamIndex)continue;
            Dd1Stress.Reaction(target,result.IsHit ? "comment_self_hit" : "comment_self_missed");
            foreach(var guid in Driver.Instance?.Party?.Guids ?? Enumerable.Empty<uint>())
            {
                var observer=Dd2Api.Actor(guid);if(observer==null || observer==target || observer==performer)continue;
                if(observer.TeamIndex==target.TeamIndex) Dd1Stress.Reaction(observer,result.IsHit ? "comment_ally_hit" : "comment_ally_missed");
                if(observer.TeamIndex==performer.TeamIndex) Dd1Stress.Reaction(observer,result.IsHit ? "comment_ally_attack_hit" : "comment_ally_attack_missed");
            }
        }
    }
}
