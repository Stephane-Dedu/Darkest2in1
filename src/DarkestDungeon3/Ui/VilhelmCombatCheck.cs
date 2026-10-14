using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Actor.Events;
using Assets.Code.Library;
using Assets.Code.Combat;
using Assets.Code.Combat.Events;
using Assets.Code.Combat.Queries;
using Assets.Code.Events;
using Assets.Code.Skill.Events;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Only called by the unsaved opt-in Vilhelm fixture. Executes native selected skills and targets.</summary>
internal static class VilhelmCombatCheck
{
    private static readonly string[] Actions={"advance","blackflame","slash","sweep","heal"};
    private static int _index;
    private static uint _guid,_target;
    private static string _pending,_pendingKey;
    private static bool _listening,_finalized,_applied;
    private static float _next,_health;
    private static readonly HashSet<string> SampledClips=new();
    private static bool _flameSampled,_idleReturned;
    private static bool _registrationChecked;

    internal static void CheckRegistration()
    {
        if (_registrationChecked) return;
        foreach (bool mastered in new[] {false,true})
        {
            var hero=new Core.Campaign.HeroRecord {Id="vilhelm-data-check",Name="Vilhelm data check",ClassId=ExperimentalHeroes.Vilhelm};
            hero.EquippedSkills.AddRange(VilhelmKit.Loadout);
            if (mastered) hero.MasteredSkills.AddRange(VilhelmKit.Loadout);
            uint guid=LibraryActors.LibraryActorsInstance.CreateActor(ExperimentalHeroes.Vilhelm);
            var actor=Dd2Api.Actor(guid);
            Require(actor!=null,"Test class could not create a native actor");
            try
            {
                Dd2Heroes.Apply(hero,actor);
                foreach (var skill in VilhelmKit.Skills)
                {
                    string id=skill.Id+(mastered ? "_u" : "");
                    var instance=actor.GetCombatSkillInstance(id);
                    Require(instance!=null && instance.GetIsUnlocked() && actor.GetCombatBaseSkillId(instance)==skill.Id,"Native base/mastered identity failed: "+id);
                    Require(actor.GetEquippedCombatSkillIds().Contains(id),"Native loadout omitted "+id);
                }
            }
            finally { LibraryActors.LibraryActorsInstance.RemoveLibraryElement(actor); }
        }
        _registrationChecked=true;
        Plugin.Log.LogInfo("[vilhelm check] five base/mastered skills and canonical loadouts PASS; temporary native actors released");
    }

    internal static bool Tick()
    {
        var combat=SingletonMonoBehaviour<CombatBhv>.Instance;
        if (combat==null || !combat.IsBattleRunning) return false;
        if (!_listening)
        {
            EventManager.AddListener<EventSkillFinalizeResults>(OnResult);
            EventManager.AddListener<EventBattleStateChanged>(OnState);
            _listening=true;
        }
        if (_pending!=null)
            foreach (var model in UnityEngine.Object.FindObjectsOfType<ExportedHeroModel>().Where(m => m.Ready && m.Actor.GetActorGuid()==_guid))
            {
                if (model.SampledClip!=null) SampledClips.Add(model.SampledClip);
                _flameSampled|=model.FlameVisible && model.FlameParticles>0 && model.FlameMaterialReady;
                _idleReturned|=_applied && model.SampledClip=="a002_000000";
            }
        if (Time.unscaledTime<_next) return false;
        if (_pending!=null)
        {
            if (!_finalized || !_applied || !_idleReturned || combat.CurrentBattleState!=BattleState.IN_TURN_SELECT) return false;
            var actor=Dd2Api.Actor(_guid); var target=Dd2Api.Actor(_target);
            if (_pendingKey=="blackflame")
            {
                Require(actor.BuffContainer.GetInstances().Any(b => b.Definition.Id=="cru_radiance_melee_bonus"),"Blackflame native buff missing");
                Require(SampledClips.Contains("a245_038700") && _flameSampled,"Original Blackflame motion or emitted particles missing");
            }
            if (_pendingKey=="slash") Require(SampledClips.Contains("a025_030000"),"Original slash motion missing");
            if (_pendingKey=="slash") Require(_flameSampled,"Blackflame did not follow the next attack");
            if (_pendingKey=="sweep") Require(SampledClips.Contains("a025_030010"),"Original sweep motion missing");
            if (_pendingKey=="advance") Require(SampledClips.Contains("a025_030100"),"Original advancing motion missing");
            if (_pendingKey=="heal") Require(new[] {"a000_042003","a000_042103","a000_042303"}.All(SampledClips.Contains),"Original heal phase missing");
            if (_pendingKey=="heal") Require(target!=null && target.HpRaw>_health,"Native healing did not restore HP");
            Plugin.Log.LogInfo($"[vilhelm check] applied {_pending}; target {_target}; health {_health:0.0}->{target?.HpRaw:0.0}; native action/turn accepted");
            _pending=null;
            if (_pendingKey==Actions[_index]) _index++;
            if (_index==Actions.Length)
            {
                EventManager.RemoveListener<EventSkillFinalizeResults>(OnResult);
                EventManager.RemoveListener<EventBattleStateChanged>(OnState);
                Require(!actor.BuffContainer.GetInstances().Any(b => b.Definition.Id=="cru_radiance_melee_bonus"),"Expired flame buff remained active");
                Plugin.Log.LogInfo("[vilhelm check] PASS: five native skills, original motion phases and idle returns, emitted flame through attack and native buff expiry; assisted survival setup, no balance claim");
                return true;
            }
        }
        if (combat.CurrentBattleState!=BattleState.IN_TURN_SELECT) return false;
        uint current=combat.GetCurrentActorGuid();
        if (!Driver.Instance.Party.Guids.Contains(current)) return false;
        var performer=Dd2Api.Actor(current);
        if (performer?.Controller?.GetIsInputValid()!=true) return false;
        if (performer.ActorDataId!=ExperimentalHeroes.Vilhelm)
        { EventBattlePass.Trigger(current); _next=Time.unscaledTime+.6f; return false; }
        _guid=current;
        // Keep this rendering/action fixture alive for five full native turns. This is
        // explicit QA setup, not character rules or evidence about combat balance.
        foreach (uint guid in Driver.Instance.Party.Guids)
        {
            var member=Dd2Api.Actor(guid);
            if (member?.IsLiving==true && member.HpRaw<member.CurrentHpMax)
                member.ApplyHealthHeal(member.CurrentHpMax-member.HpRaw,false,SourceType.DEBUG,hasDisplayed:false);
        }
        if (Actions[_index]=="heal") performer.ApplyHealthDamage(performer.HpRaw-performer.CurrentHpMax*.35f,
            false,false,performer,DeathType.DEBUG,SourceType.DEBUG,"vilhelm_unsaved_heal_check",hasDisplayed:false);
        _pendingKey=Actions[_index];
        string id=VilhelmKit.Skills.Single(s => s.Key==_pendingKey).Id;
        var valid=QueryIsValidSkill.Trigger(current,id);
        if (!valid.IsValid)
        {
            // Enemies may push Vilhelm out of his launch ranks. Recover through his
            // actual advancing attack instead of forcing party positions.
            string advance=VilhelmKit.Skills.Single(s => s.Key=="advance").Id;
            if (QueryIsValidSkill.Trigger(current,advance).IsValid)
            { id=advance; _pendingKey="advance"; valid=QueryIsValidSkill.Trigger(current,id); }
        }
        Require(valid.IsValid,"Skill invalid: "+id+" / "+valid.m_ValidityType);
        EventSkillSelectionChanged.Trigger(true,current,id,true,false);
        var targets=QuerySelectedSkillTargets.Trigger(current,id).m_ValidTargetActorGuids;
        _target=_pendingKey=="heal" || _pendingKey=="blackflame" ? current : targets.FirstOrDefault(t => !Driver.Instance.Party.Guids.Contains(t));
        Require(_target!=0 && targets.Contains(_target),"Native target not eligible for "+id);
        _health=Dd2Api.Actor(_target)?.HpRaw ?? 0;
        _pending=id; _finalized=false; _applied=false; SampledClips.Clear(); _flameSampled=false; _idleReturned=false;
        Plugin.Log.LogInfo($"[vilhelm check] selecting {id} -> {_target}; ordinary validated target confirmation");
        EventSelectActor.Trigger(_target,true);
        _next=Time.unscaledTime+.5f;
        return false;
    }
    private static void OnResult(EventSkillFinalizeResults e)
    {
        if (e.PerformerGuid==_guid && e.SkillId==_pending)
        { _finalized=true; Plugin.Log.LogInfo($"[vilhelm check] finalized {e.SkillId}; targets {string.Join(",",e.TargetGuids)}"); }
    }
    private static void OnState(EventBattleStateChanged e)
    {
        if (_pending!=null && e.m_CurrentActorActorGuid==_guid && (e.m_ToBattleState==BattleState.IN_TURN_APPLY || e.m_ToBattleState==BattleState.IN_TURN_AFTER_APPLY)) _applied=true;
    }
    private static void Require(bool valid,string message) { if (!valid) throw new InvalidOperationException(message); }
}
