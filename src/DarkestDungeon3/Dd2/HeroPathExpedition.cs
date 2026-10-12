using System;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Library;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Dd2;

/// <summary>The two Path buffs which survive native combat-end cleanup. Only expedition snapshots retain them.</summary>
internal static class HeroPathExpedition
{
    internal const string Monarch = "lep_monarch", Oath = "lep_chop_p3_kill_count", Condition = "lep_chop_p3_kill_count_condition";

    public static MonarchOathState Capture(ActorInstance actor) => actor?.ActorDataPath?.Id == Monarch ? new MonarchOathState
    {
        Stacks = actor.BuffContainer.GetNumberOfInstancesWithId(Oath),
        Conditions = actor.BuffContainer.GetNumberOfInstancesWithId(Condition)
    }.Copy() : null;

    public static void Restore(ActorInstance actor, MonarchOathState saved)
    {
        if (actor?.ActorDataPath?.Id != Monarch || saved == null) return;
        var counts = saved.Copy();
        actor.BuffContainer.RemoveAllInstances(b => b.Definition.Id == Oath || b.Definition.Id == Condition,
            SourceType.PATH, Monarch, actor.ActorGuid, sendEvent: false);
        Add(Oath, counts.Stacks); Add(Condition, counts.Conditions);
        actor.BuffContainer.RefreshActiveBuffs(); actor.RefreshStats();
        void Add(string id, int count)
        {
            var library = SingletonMonoBehaviour<Library<string, BuffDefinition>>.Instance;
            if (library == null || !library.TryGetLibraryElement(id, out var definition)) return;
            for (int i = 0; i < Math.Min(count, definition.m_InstanceLimit); i++)
                actor.BuffContainer.Add(new BuffInstance(definition, actor, SourceType.PATH, Monarch, actor.ActorGuid), false, sendEvent: false);
        }
    }

    /// <summary>Invoked only by the opt-in unsaved native acceptance fixture.</summary>
    internal static void CheckNative()
    {
        uint guid = LibraryActors.LibraryActorsInstance.CreateActor("leper");
        var actor = Dd2Api.Actor(guid);
        try
        {
            var hero = new HeroRecord { Id = "oath-native-check", ClassId = "leper", Name = "Oath QA", PathId = Monarch };
            HeroSkills.Apply(hero, actor);
            Restore(actor, new MonarchOathState { Stacks = 6, Conditions = 6 });
            var saved = Capture(actor);
            if (saved?.Stacks != 6 || saved.Conditions != 6) throw new InvalidOperationException("Monarch Oath did not restore native buff counts");
            Restore(actor, saved);
            if (Capture(actor)?.Stacks != 6) throw new InvalidOperationException("Monarch Oath restored twice");
            Restore(actor, new MonarchOathState { Stacks = 1000, Conditions = -1 });
            if (Capture(actor)?.Stacks != 66 || Capture(actor)?.Conditions != 0) throw new InvalidOperationException("Monarch Oath bounds failed");
            hero.PathId = null; HeroSkills.Apply(hero, actor);
            if (Capture(actor) != null) throw new InvalidOperationException("Monarch Oath leaked into default Path capture");
            Plugin.Log.LogInfo("[paths check] Monarch Oath PASS: expedition capture, bounded idempotent restore, default Path guard");
        }
        finally { LibraryActors.LibraryActorsInstance.RemoveLibraryElement(actor); }
    }
}
