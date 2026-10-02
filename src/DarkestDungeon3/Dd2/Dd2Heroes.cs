using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Quirk;
using Assets.Code.Roster;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using HarmonyLib;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// Hamlet heroes ⇄ DD2 actors. At embark each <see cref="HeroRecord"/> becomes a fresh DD2 actor of its class,
/// carrying the record's name, quirks, trinkets and stress; on the way home the actor's condition is read back.
/// </summary>
internal static class Dd2Heroes
{
    private static readonly AccessTools.FieldRef<RosterManager, List<RosterEntry>> Entries =
        AccessTools.FieldRefAccess<RosterManager, List<RosterEntry>>("m_Entries");

    /// <summary>Replace DD2's party with our heroes, front rank first. Returns hero id → actor guid.</summary>
    public static Dictionary<string, uint> BuildParty(IReadOnlyList<HeroRecord> heroes)
    {
        var roster = Dd2Api.Roster;
        var map = new Dictionary<string, uint>();
        if (roster == null) { Plugin.Log.LogError("BuildParty: no RosterManager (is a run active?)"); return map; }

        // Everyone DD2 put in the party goes back to the bench; their actors stay in the library untouched.
        var entries = Entries(roster);
        foreach (var e in entries.Where(e => e.GetRosterStatus() == RosterStatusType.PARTY))
            e.SetRosterStatus(RosterStatusType.IDLE, 0u);

        foreach (var hero in heroes)
        {
            uint guid = LibraryActors.LibraryActorsInstance.CreateActor(hero.ClassId);
            var actor = Dd2Api.Actor(guid);
            if (actor == null) { Plugin.Log.LogError($"Could not create a DD2 {hero.ClassId} for {hero.Name}"); continue; }

            Apply(hero, actor);
            var entry = new RosterEntry(hero.ClassId, guid);
            entries.Add(entry);
            // Like DD2's own roster code: a new entry starts IDLE, then joins the party. Going straight to PARTY
            // crashes ActorInstance.HandleEventRosterEntryStatusChanged (it reads the previous status, null here).
            entry.SetRosterStatus(RosterStatusType.IDLE, 0u);
            entry.SetRosterStatus(RosterStatusType.PARTY, 0u);
            map[hero.Id] = guid;
            Plugin.Log.LogInfo($"[party] {hero.Name} ({hero.ClassId}) → actor {guid}, hp {actor.HpRaw}/{actor.CurrentHpMax}, stress {actor.Stress}/{actor.StressMax}");
        }
        return map;
    }

    /// <summary>Write a record's persistent state onto a fresh DD2 actor.</summary>
    public static void Apply(HeroRecord hero, ActorInstance actor)
    {
        actor.SetActorName(hero.Name);

        // DD2 rolls starting quirks for a new actor; the Hamlet's record is the truth.
        if (actor.QuirkContainer != null)
        {
            actor.QuirkContainer.RemoveAllInstances(_ => true, SourceType.ROSTER, "dd3", 0u, sendEvent: false);
            var quirks = SingletonMonoBehaviour<Library<string, QuirkDefinition>>.Instance;
            foreach (var id in hero.Quirks)
                if (quirks != null && quirks.TryGetLibraryElement(id, out var q))
                    actor.QuirkContainer.Add(q, SourceType.ROSTER, "dd3", 0u);
        }

        var trinkets = actor.GetTrinketInventory();
        var items = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
        if (trinkets != null && items != null)
            foreach (var id in hero.Trinkets)
                if (items.TryGetLibraryElement(id, out var t))
                    trinkets.AddItems(t, 1, false);

        if (hero.Stress > 0) actor.ApplyStressDamage(hero.Stress, canResist: false, SourceType.ROSTER, "dd3", 0u);
    }

    /// <summary>Read an actor's condition back into a homecoming outcome.</summary>
    public static HeroOutcome ReadBack(HeroRecord hero, uint guid)
    {
        var actor = Dd2Api.Actor(guid);
        bool dead = actor == null || Dd2Api.IsDead(guid);
        var outcome = new HeroOutcome { HeroId = hero.Id, Died = dead, CauseOfDeath = dead ? "fell in the dungeon" : null };
        if (dead) return outcome;

        outcome.Stress = (int)System.Math.Round(actor.Stress);
        outcome.Quirks = actor.QuirkContainer?.GetInstances().Select(i => i.Definition.Id).ToList();
        outcome.Trinkets = actor.GetTrinketInventory()?.GetItemIds()?.ToList();
        return outcome;
    }
}
