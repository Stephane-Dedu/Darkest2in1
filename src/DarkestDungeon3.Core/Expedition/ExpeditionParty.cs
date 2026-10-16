using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>A plain snapshot of DD2 condition. Living zero/negative HP represents death's door, not a fallen hero.</summary>
public sealed class ExpeditionHeroState
{
    public HeroOutcome Outcome = new();
    public float Hp, HpMax, Stress, WoundPercent;
    public ResolveState Resolve;
    /// <summary>Monarch's native Oath accumulates within this expedition, never across Hamlet weeks.</summary>
    public MonarchOathState MonarchOath;
}

public sealed class MonarchOathState
{
    public int Stacks, Conditions;
    public MonarchOathState Copy() => new() { Stacks = Math.Max(0, Math.Min(66, Stacks)), Conditions = Math.Max(0, Math.Min(66, Conditions)) };
}

public static class ExpeditionParty
{
    /// <summary>Copy only valid members. Missing native actors never erase the last recorded state.</summary>
    public static void Capture(ExpeditionState state, IEnumerable<ExpeditionHeroState> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            string id = snapshot?.Outcome?.HeroId;
            if (id == null || !state.Party.Contains(id) || !IsValid(snapshot)) continue;
            state.PartyStates[id] = Copy(snapshot);
        }
    }

    public static bool IsValid(ExpeditionHeroState snapshot) => snapshot?.Outcome?.HeroId != null
        && Finite(snapshot.Hp) && Finite(snapshot.HpMax) && snapshot.HpMax > 0
        && Finite(snapshot.Stress) && snapshot.Stress >= 0 && snapshot.Stress < int.MaxValue
        && Finite(snapshot.WoundPercent) && snapshot.WoundPercent >= 0 && snapshot.WoundPercent <= 1;

    /// <summary>A fresh actor gets a copied persistent loadout plus recorded changes; fallen heroes never respawn.</summary>
    public static HeroRecord HeroForRestore(HeroRecord hero, ExpeditionHeroState snapshot)
    {
        if (hero == null || hero.IsDead) return null;
        if (snapshot?.Outcome?.HeroId != hero.Id) snapshot = null;
        if (snapshot?.Outcome?.Died == true) return null;
        var copy = JsonConvert.DeserializeObject<HeroRecord>(JsonConvert.SerializeObject(hero));
        if (!IsValid(snapshot)) return copy;
        copy.Stress = snapshot.Stress;
        copy.Resolve = snapshot.Resolve?.Copy() ?? copy.Resolve;
        if (snapshot.Outcome.Quirks != null) copy.Quirks = snapshot.Outcome.Quirks.ToList();
        if (snapshot.Outcome.Trinkets != null) copy.Trinkets = TrinketEquipment.RestoreSlots(hero.Trinkets, snapshot.Outcome.Trinkets);
        return copy;
    }

    public static ExpeditionHeroState Copy(ExpeditionHeroState snapshot) => new()
    {
        Hp = snapshot.Hp, HpMax = snapshot.HpMax, Stress = snapshot.Stress, WoundPercent = snapshot.WoundPercent,
        Outcome = CopyOutcome(snapshot.Outcome, snapshot.Stress, snapshot.Resolve), MonarchOath = snapshot.MonarchOath?.Copy(), Resolve = snapshot.Resolve?.Copy()
    };

    /// <summary>Use recorded condition for recovery/results; older saves retain only their estate evidence.</summary>
    public static List<HeroOutcome> Outcomes(ExpeditionState state, Estate estate) => state.Party.Select(id =>
        state.PartyStates.TryGetValue(id, out var saved) ? CopyOutcome(saved.Outcome, saved.Stress, saved.Resolve)
        : new HeroOutcome { HeroId = id, Died = estate.Hero(id)?.IsDead == true, Stress = estate.Hero(id)?.Stress ?? 0,
            CauseOfDeath = estate.Hero(id)?.CauseOfDeath, Resolve = estate.Hero(id)?.Resolve?.Copy() }).ToList();

    private static HeroOutcome CopyOutcome(HeroOutcome outcome, float stress, ResolveState resolve = null) => new()
    {
        HeroId = outcome.HeroId, Died = outcome.Died, CauseOfDeath = outcome.CauseOfDeath,
        Stress = stress, Quirks = outcome.Quirks?.ToList(), Trinkets = outcome.Trinkets?.ToList(), Resolve = (resolve ?? outcome.Resolve)?.Copy()
    };

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
