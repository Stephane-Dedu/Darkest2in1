using System;
using System.Reflection;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Actor.Events;
using Assets.Code.Combat;
using Assets.Code.Combat.Summon;
using Assets.Code.Game;
using Assets.Code.Math;
using Assets.Code.Skill;
using Assets.Code.Skill.Events;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

/// <summary>One bounded native summon after a prepared enemy skill, independent of its target count/hits.</summary>
[HarmonyPatch(typeof(EventSkillFinalizeResults), nameof(EventSkillFinalizeResults.Trigger))]
internal static class Dd1EnemySummons
{
    private static readonly FieldInfo TeamField = AccessTools.Field(typeof(ActorInstance), "m_Team");

    private static void Postfix(SkillCalculation.SkillResult skillResult)
    {
        if (!Dd2Combat.InFight || skillResult == null) return;
        var performer = Dd2Api.Actor(skillResult.m_PerformerActorGuid);
        var entry = Dd1EnemyData.Get(performer?.ActorDataId);
        if (entry == null || !entry.Summons.TryGetValue(skillResult.m_SkillId, out var summon)
            || !performer.IsLiving || performer.HpRaw <= 0 || TeamField?.GetValue(performer) is not Team team) return;
        try
        {
            // Never borrow an unregistered actor, or consume a roll with incomplete candidate coverage.
            foreach (string monster in summon.Monsters)
            {
                var (family, tier) = Dd1Bestiary.Split(monster);
                if (!Dd1EnemyData.TryRegister(family, tier, out _)) return;
            }
            int room = Dd1SingleSummon.AvailableRanks(team.Actors.Select(a =>
                (a.Size, Dd1EnemyData.Get(a.ActorDataId)?.Kit is { Corpse: true, CanBeSummonRank: true })));
            bool planned = summon.TryPlan(room,
                id => Dd1EnemyData.Get(Dd1EnemyData.ActorId(id))?.Kit.Size ?? 0,
                // DD2's float RNG includes 1; integer Range is exclusive at its upper bound.
                () => RandomContainer.Range(RandomIdentifier.SUMMON, 0, 16777216) / 16777216d, out var choice);
            if (!planned)
            {
                Plugin.Log.LogInfo($"[memory summon] {entry.Kit.Id}: attempt spent, {room} free ranks");
                return;
            }
            // No pending summon survives a full/changed formation. Zero keeps DD2's normal next-round timing;
            // exact DD1 initiative must be verified before live memory activation.
            EventSummonQueueActor.Trigger(team.m_TeamIndex, Dd1EnemyData.ActorId(choice.Monster),
                SummonLocationType.FRONT, removeAfterProcess: true, isWave: false, addToTurnOrderAfterCurrentTurnIndex: 0);
            Plugin.Log.LogInfo($"[memory summon] {entry.Kit.Id}: queued {choice.Monster} once, front, loot={choice.CanSpawnLoot}");
        }
        catch (Exception ex) { Plugin.Log.LogWarning("[memory summon] " + ex); }
    }
}

/// <summary>The native creation event completes actor identity before its view finishes spawning.</summary>
[HarmonyPatch(typeof(BattleTeams), "HandleEventCreateActor")]
internal static class Dd1EnemySummonPresentation
{
    private static void Postfix(EventCreateActor createActorEvent)
    {
        if (!Dd2Combat.InFight || createActorEvent == null || createActorEvent.m_CreatedActorGuid == 0
            || Dd1EnemyData.Get(createActorEvent.m_NewActorClassId) == null) return;
        Dd1MonsterView.AddPreparedEnemy(createActorEvent.m_CreatedActorGuid, createActorEvent.m_NewActorClassId);
    }
}
