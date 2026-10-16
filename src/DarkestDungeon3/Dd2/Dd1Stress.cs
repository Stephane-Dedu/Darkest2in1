using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Effect;
using Assets.Code.Library;
using Assets.Code.Source;
using Assets.Code.Stress;
using Assets.Code.Token;
using Assets.Code.Utils;
using Assets.Code.Utils.Serialization;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Dd2;

/// <summary>DD1 resolve on registered expedition actors only. Native attacks retain their original stress strength.</summary>
internal static partial class Dd1Stress
{
    private sealed class Binding
    {
        internal ActorInstance Actor;
        internal HeroRecord Hero;
        internal ResolveState State;
        internal bool Applying;
        internal bool Restoring = true, Pass, RandomCommand;
        internal Assets.Code.Actor.ActorController.SkillTargetEntry RandomSkill;
        internal string Death;
    }
    private static readonly Dictionary<uint,Binding> Actors = new();
    private const string Source = "dd1_resolve";
    private static int _fixtureCounter;
    internal static bool Owns(ActorInstance actor) => actor != null && Actors.TryGetValue(actor.ActorGuid,out var b) && ReferenceEquals(actor,b.Actor);
    internal static void Bind(ActorInstance actor, HeroRecord hero, ResolveState saved)
    {
        Actors[actor.ActorGuid]=new Binding {Actor=actor,Hero=hero,State=saved?.Copy() ?? new ResolveState()};
    }
    internal static ResolveState Capture(ActorInstance actor)
    {
        if (!Owns(actor)) return null;
        var state=Actors[actor.ActorGuid].State;
        if (state.IsToxic && actor.IsLiving) CaptureToxic(actor,state);
        return state.Copy();
    }
    internal static string DeathCause(ActorInstance actor) => Owns(actor) ? Actors[actor.ActorGuid].Death : null;
    internal static string DeathCause(uint guid) => Actors.TryGetValue(guid,out var binding) ? binding.Death : null;
    internal static string Trait(ActorInstance actor) => Owns(actor) ? Actors[actor.ActorGuid].State.TraitId : null;
    internal static string Name(string id) => id == "depressed" ? "Hopeless" : id == "toxic" ? "Toxic" : Ui.HamletUi.Pretty(id);
    internal static string Description(ActorInstance actor) => $"Stress {ResolveStress.Dd1(actor.Stress):0.#}/200\n"
        +(Trait(actor) is { } id ? Name(id)+"\n" : "Resolve test at 100\n")
        +(Owns(actor) && Actors[actor.ActorGuid].State.Virtuous ? "At 200, virtue is lost and stress returns to zero." : "Heart attack at 200. Afflictions are cured at zero stress.");
    internal static Rng NextRng()
    {
        var expedition=Driver.Instance?.Expedition;
        return expedition != null ? new Rng(unchecked(expedition.Seed*31 + ++expedition.RandomCounter*977)) : new Rng(++_fixtureCounter*977);
    }
    internal static float StressModifier(ActorInstance actor) => !Owns(actor) ? 1 : Math.Max(0,1+TraitBuffs(actor)
        .Where(b=>b.Stat=="stress_dmg_received_percent").Sum(b=>b.Amount));
    internal static IEnumerable<Dd1Buff> TraitBuffs(ActorInstance actor)
    {
        var session=Session.Current;
        var trait=session?.ResolveRules?.Get(Trait(actor));
        return trait?.BuffIds.Select(id=>session.Content.Buffs.Get(id)).Where(b=>b!=null) ?? Enumerable.Empty<Dd1Buff>();
    }
    internal static void Changed(ActorInstance actor)
    {
        if (!Owns(actor) || !actor.IsLiving) return;
        var binding=Actors[actor.ActorGuid];var rules=Session.Current?.ResolveRules;
        if (binding.Applying || binding.Restoring || rules == null) return;
        bool persist=false;
        binding.Applying=true;
        try
        {
            string before=binding.State.TraitId;
            var result=rules.Check(binding.State,actor.Stress,actor.GetIsStatusActive(ActorStatusType.DEATHS_DOOR),
                binding.Hero.ClassId,binding.Hero.ResolveLevel,Driver.Instance?.Expedition?.Quest?.Difficulty ?? binding.Hero.ResolveLevel,
                actor.GetClampedStatValue(ActorStatType.OVERSTRESS_CHANCE_MODIFIER,"resolute"),
                binding.State.TraitId==null && ResolveStress.Reached(actor.Stress,ResolveStress.Break) ? NextRng() : new Rng(0));
            float previous=actor.Stress;
            Raw(actor,result.Stress);
            if (result.Stress < previous) Assets.Code.Combat.Events.EventStressHeal.Trigger(actor.ActorGuid,actor.TeamIndex,previous-result.Stress,SourceType.STATUS);
            if (result.Event == ResolveEvent.Toxic)
            {
                var toxic=SingletonMonoBehaviour<Library<string,OverstressDefinition>>.Instance.GetLibraryElement("toxic");
                var effects=toxic.Effects.Where(e=>e.m_Id!="toxic_stress").ToList();
                EffectApply.Apply(new AppliedEffects.Input<EffectDefinition>(effects,actor),SourceType.OVERSTRESS,"toxic");
                CaptureToxic(actor,binding.State);
            }
            if (before != binding.State.TraitId) RestoreEffects(actor);
            if (result.Event == ResolveEvent.HeartAttackDeath)
            {
                binding.Death="heart attack";
                actor.Kill(DeathType.EFFECT,SourceType.STATUS,"dd1_heart_attack",0,actor.ActorGuid);
            }
            else if (result.Event == ResolveEvent.HeartAttack)
                actor.ApplyHealthDamage(actor.HpRaw,false,false,actor,DeathType.EFFECT,SourceType.STATUS,"dd1_heart_attack",false);
            if (result.Event != ResolveEvent.None)
            {
                persist=true;
                string message=result.Event switch
                {
                    ResolveEvent.HeartAttack or ResolveEvent.HeartAttackDeath => binding.Hero.Name+": Heart attack!",
                    ResolveEvent.Cured => binding.Hero.Name+": Affliction cured",
                    ResolveEvent.LostVirtue => binding.Hero.Name+": Virtue lost",
                    _ => binding.Hero.Name+": "+Name(binding.State.TraitId)
                };
                Ui.Gui.Announce(message,3.5f);
                Driver.Instance?.Say(message);
                Plugin.Log.LogInfo($"[resolve] {binding.Hero.Id}: {result.Event}; trait={binding.State.TraitId}; stress={ResolveStress.Dd1(actor.Stress)}; hp={actor.HpRaw}");
            }
        }
        finally { binding.Applying=false; }
        // Save conditions at the point they change, including a lethal heart attack. Unsaved previews have no path.
        if (persist && !string.IsNullOrEmpty(Session.Current?.SavePath)) Session.Current.Persist();
    }
    internal static void Raw(ActorInstance actor,float units) => JsonSerializationUtils.PerFieldApplyTo(actor,
        new JObject { ["m_Stress"]=Math.Max(0,Math.Min(ResolveStress.Maximum,units)) });
    internal static void CompleteRestore(ActorInstance actor)
    { RestoreEffects(actor); if (Owns(actor)) Actors[actor.ActorGuid].Restoring=false; }

    private static bool ToxicBuff(string id) => id=="flg_toxic_dbr_penalty" || id=="flg_scourge_toxic_blight_bonus";
    private static void CaptureToxic(ActorInstance actor,ResolveState state)
    {
        int tokens=actor.TokenContainer?.GetNumberOfInstancesWithId("virulent_blight") ?? 0;
        // Native battle-end cleanup may already have removed these before persistence.
        if (tokens>0) state.ToxicLevel=Math.Min(3,tokens);
        foreach (var group in actor.BuffContainer.GetInstances().Where(b=>ToxicBuff(b.Definition.Id)).GroupBy(b=>b.Definition.Id))
            state.ToxicBuffs[group.Key]=group.Count();
    }
    internal static void RestoreEffects(ActorInstance actor)
    {
        if (!Owns(actor) || !actor.IsLiving) return;
        var state=Actors[actor.ActorGuid].State;
        actor.BuffContainer.RemoveAllInstances(b=>b.SourceId==Source,SourceType.STATUS,Source,actor.ActorGuid,sendEvent:false);
        if (state.IsToxic)
        {
            Token("virulent_blight",Math.Max(1,state.ToxicLevel));Token("virulent_blight_manager",1);
            foreach (var b in state.ToxicBuffs.Where(b=>ToxicBuff(b.Key))) AddBuff(b.Key,Math.Max(0,Math.Min(3,b.Value)));
        }
        else
        {
            actor.TokenContainer?.RemoveAllInstances(t=>t.Definition.Id=="virulent_blight" || t.Definition.Id=="virulent_blight_manager",SourceType.STATUS,Source,actor.ActorGuid,sendEvent:false);
            actor.BuffContainer.RemoveAllInstances(b=>ToxicBuff(b.Definition.Id),SourceType.STATUS,Source,actor.ActorGuid,sendEvent:false);
        }
        int index=0;
        foreach (var effect in TraitBuffs(actor).Select(Dd1TrinketEffect.Adapt).Where(e=>e?.Stats!=null))
        {
            string id="dd3_resolve_"+state.TraitId+"_"+index++;
            var library=SingletonMonoBehaviour<Library<string,BuffDefinition>>.Instance;
            if (!library.GetHasLibraryKey(id))
            {
                var stats=new ActorDataStats(id,effect.Stats);stats.Init(id);
                SingletonMonoBehaviour<Library<string,ActorDataStats>>.Instance.AddLibraryElement(stats,overrideCSV:true);
                var definition=new BuffDefinition(id,"m_DurationType,infinite,\nm_IsVisible,False,\nm_showPopText,False,\n");definition.Init();
                library.AddLibraryElement(definition,overrideCSV:true);
            }
            AddBuff(id,1);
        }
        actor.BuffContainer.RefreshActiveBuffs();actor.RefreshStats();actor.ClampHealthToMax();
        void AddBuff(string id,int desired)
        {
            var library=SingletonMonoBehaviour<Library<string,BuffDefinition>>.Instance;
            if (!library.TryGetLibraryElement(id,out var definition)) return;
            int current=actor.BuffContainer.GetNumberOfInstancesWithId(id);
            for (int i=current;i<desired;i++) actor.BuffContainer.Add(new BuffInstance(definition,actor,SourceType.STATUS,Source,actor.ActorGuid),false,sendEvent:false);
        }
        void Token(string id,int desired)
        {
            var library=SingletonMonoBehaviour<Library<string,TokenDefinition>>.Instance;
            if (!library.TryGetLibraryElement(id,out var definition)) return;
            int current=actor.TokenContainer.GetNumberOfInstancesWithId(id);
            for (int i=current;i<desired;i++) actor.TokenContainer.Add(new TokenInstance(definition,actor,false,SourceType.OVERSTRESS,"toxic",actor.ActorGuid),false,sendEvent:false);
        }
    }
}

[HarmonyPatch(typeof(ActorInstance),"get_StressMax")]
internal static class ExpeditionStressMaximum
{
    private static bool Prefix(ActorInstance __instance,ref float __result)
    { if (!Dd1Stress.Owns(__instance)) return true;__result=ResolveStress.Maximum;return false; }
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.GetStressPercent))]
internal static class ExpeditionStressPercent
{
    private static bool Prefix(ActorInstance __instance,ref float __result)
    { if (!Dd1Stress.Owns(__instance)) return true;__result=__instance.Stress/ResolveStress.Maximum;return false; }
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.GetIsOverstressed))]
internal static class ExpeditionNoMeltdown
{
    private static bool Prefix(ActorInstance __instance,ref bool __result)
    { if (!Dd1Stress.Owns(__instance)) return true;__result=false;return false; }
}
[HarmonyPatch(typeof(StressManager),nameof(StressManager.ProcessOverstressedActor))]
internal static class ExpeditionNoQueuedMeltdown
{
    private static bool Prefix(uint actorGuid,ref OverstressDefinition overstressDefinitionToApply,ref bool __result)
    {
        if (!Dd1Stress.Owns(Dd2Api.Actor(actorGuid))) return true;
        overstressDefinitionToApply=null;__result=false;return false;
    }
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.ApplyStressDamage))]
internal static class ExpeditionResolveDamage
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ActorInstance __instance,ref float damage)
    {
        if(Dd1Stress.Owns(__instance)) damage=(float)Math.Round(damage*Dd1Stress.StressModifier(__instance)*10,MidpointRounding.AwayFromZero)/10;
    }
    private static void Postfix(ActorInstance __instance) => Dd1Stress.Changed(__instance);
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.ApplyStressHeal))]
internal static class ExpeditionResolveHeal
{
    private static void Postfix(ActorInstance __instance) => Dd1Stress.Changed(__instance);
}
[HarmonyPatch(typeof(ActorInstance),nameof(ActorInstance.OnBattleEnd))]
internal static class ExpeditionResolveBattleEnd
{
    private static void Prefix(ActorInstance __instance) { if (Dd1Stress.Owns(__instance)) Dd1Stress.Capture(__instance); }
    private static void Postfix(ActorInstance __instance) => Dd1Stress.RestoreEffects(__instance);
}
