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

        ApplyEquipment(hero, actor);
        if (hero.Stress > 0) actor.ApplyStressDamage(hero.Stress, canResist: false, SourceType.ROSTER, "dd3", 0u);
    }

    // DD1's Blacksmith ranks as DD2's own permanent buffs, distinct ids stacked per rank (the same id doesn't stack).
    // DD1 weapons gain damage, crit and speed; armour gains HP (DD2 has no dodge stat).
    private static readonly string[][] WeaponBuffs =
    {
        new string[0],
        new[] { "memory_buff_04", "trinket_tiered_minor_heartseeker_01" },                                  // +10% dmg, +3% crit
        new[] { "trinket_tiered_sharpness_charm_01", "trinket_tiered_heartseeker_01", "memory_buff_02" },    // +15%, +5%, +1 speed
        new[] { "trinket_tiered_greater_sharpness_charm_01", "memory_buff_04", "trinket_tiered_heartseeker_01",
                "trinket_tiered_minor_heartseeker_01", "memory_buff_02" },                                   // +30%, +8%, +1
        new[] { "trinket_tiered_greater_sharpness_charm_01", "trinket_tiered_sharpness_charm_01", "memory_buff_04",
                "trinket_tiered_greater_heartseeker_01", "quirk_lightning_reflexes" },                       // +45%, +10%, +2
    };

    private static readonly string[][] ArmourBuffs =
    {
        new string[0],
        new[] { "memory_buff_01" },                                                                         // +10% max HP
        new[] { "trinket_tiered_hale_draught_01", "memory_buff_18_end_buff" },                              // +20%
        new[] { "trinket_tiered_greater_hale_draught_01", "memory_buff_01" },                               // +30%
        new[] { "trinket_tiered_greater_hale_draught_01", "trinket_tiered_hale_draught_01", "memory_buff_01" }, // +45%
    };

    /// <summary>What a Blacksmith rank gives, for the building window (rank 0 = as recruited).</summary>
    public static string EquipmentText(string slot, int rank) => slot == "weapon"
        ? rank switch { 0 => "Standard issue", 1 => "+10% damage, +3% crit", 2 => "+15% damage, +5% crit, +1 speed", 3 => "+30% damage, +8% crit, +1 speed", _ => "+45% damage, +10% crit, +2 speed" }
        : rank switch { 0 => "Standard issue", 1 => "+10% max HP", 2 => "+20% max HP", 3 => "+30% max HP", _ => "+45% max HP" };

    private static void ApplyEquipment(HeroRecord hero, ActorInstance actor)
    {
        var buffs = SingletonMonoBehaviour<Library<string, Assets.Code.Buff.BuffDefinition>>.Instance;
        if (buffs == null || actor.BuffContainer == null) return;
        var ids = WeaponBuffs[System.Math.Min(hero.WeaponRank, 4)].Concat(ArmourBuffs[System.Math.Min(hero.ArmorRank, 4)]).ToList();
        if (ids.Count == 0) return;
        foreach (var id in ids)
        {
            if (buffs.TryGetLibraryElement(id, out var buff))
                actor.BuffContainer.TryAdd(buff, isLockedTeamPosition: false, SourceType.CLASS, "dd3_blacksmith", actor.ActorGuid);
            else Plugin.Log.LogWarning($"[party] buff {id} not in DD2's library");
        }
        actor.BuffContainer.RefreshActiveBuffs();
        // A bigger health pool starts full.
        if (actor.HpRaw < actor.CurrentHpMax)
            actor.ApplyHealthHeal(actor.CurrentHpMax - actor.HpRaw, isCrit: false, SourceType.DRIVING, hasDisplayed: false);
        Plugin.Log.LogInfo($"[party] {hero.Name}: weapon rank {hero.WeaponRank + 1}, armour rank {hero.ArmorRank + 1} → hp {actor.HpRaw}/{actor.CurrentHpMax}");
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
