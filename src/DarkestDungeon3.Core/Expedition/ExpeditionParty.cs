using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>A plain snapshot of DD2 condition. Living zero/negative HP represents death's door, not a fallen hero.</summary>
public sealed class ExpeditionHeroState
{
    public HeroOutcome Outcome = new();
    public float Hp, HpMax, Stress, WoundPercent;
}

public static class ExpeditionParty
{
    /// <summary>Copy only valid members. Missing native actors never erase the last recorded state.</summary>
    public static void Capture(ExpeditionState state, IEnumerable<ExpeditionHeroState> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            string id = snapshot?.Outcome?.HeroId;
            if (id == null || !state.Party.Contains(id) || !Finite(snapshot.Hp) || !Finite(snapshot.HpMax)
                || snapshot.HpMax <= 0 || !Finite(snapshot.Stress) || snapshot.Stress < 0
                || !Finite(snapshot.WoundPercent) || snapshot.WoundPercent < 0 || snapshot.WoundPercent > 1) continue;
            state.PartyStates[id] = Copy(snapshot);
        }
    }

    public static ExpeditionHeroState Copy(ExpeditionHeroState snapshot) => new()
    {
        Hp = snapshot.Hp, HpMax = snapshot.HpMax, Stress = snapshot.Stress, WoundPercent = snapshot.WoundPercent,
        Outcome = CopyOutcome(snapshot.Outcome, snapshot.Stress)
    };

    /// <summary>Use recorded condition for recovery/results; older saves retain only their estate evidence.</summary>
    public static List<HeroOutcome> Outcomes(ExpeditionState state, Estate estate) => state.Party.Select(id =>
        state.PartyStates.TryGetValue(id, out var saved) ? CopyOutcome(saved.Outcome, saved.Stress)
        : new HeroOutcome { HeroId = id, Died = estate.Hero(id)?.IsDead == true, Stress = estate.Hero(id)?.Stress ?? 0,
            CauseOfDeath = estate.Hero(id)?.CauseOfDeath }).ToList();

    private static HeroOutcome CopyOutcome(HeroOutcome outcome, float stress) => new()
    {
        HeroId = outcome.HeroId, Died = outcome.Died, CauseOfDeath = outcome.CauseOfDeath,
        Stress = (int)Math.Round(stress), Quirks = outcome.Quirks?.ToList(), Trinkets = outcome.Trinkets?.ToList()
    };

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
