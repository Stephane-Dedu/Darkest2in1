using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;

namespace DarkestDungeon3.Core.Expedition;

public enum ExpeditionLoadRoute { Town, Refund, Resume, Results, Invalid }

/// <summary>Inspect a loaded save without abandoning it or running a second embark/week.</summary>
public static class ExpeditionRecovery
{
    public static ExpeditionLoadRoute Inspect(SaveFile save)
    {
        var state = save?.Expedition;
        if (state == null) return ExpeditionLoadRoute.Town;
        if (!state.Started && !state.Ended) return ExpeditionLoadRoute.Refund;
        if (state.Quest == null || string.IsNullOrEmpty(state.Quest.Dungeon) || state.Party == null
            || state.Party.Count == 0 || state.Party.Distinct().Count() != state.Party.Count
            || state.Party.Any(id => id == null || save.Estate?.Hero(id) == null)) return ExpeditionLoadRoute.Invalid;
        if (state.Ended) return ExpeditionLoadRoute.Results;
        if (!PositionIsValid(state)) return ExpeditionLoadRoute.Invalid;
        var conditions = Conditions(state);
        return state.Party.Any(id => !ConfirmedDead(save.Estate.Hero(id), conditions, id))
            ? ExpeditionLoadRoute.Resume : ExpeditionLoadRoute.Results;
    }

    public static IReadOnlyDictionary<string, ExpeditionHeroState> Conditions(ExpeditionState state) =>
        ExpeditionFight.Current(state)?.PartyStates ?? state.PartyStates;

    public static bool PositionIsValid(ExpeditionState state) => state is { Started: true, Ended: false, Map: not null }
        && (state.InRoom ? state.RoomId < state.Map.Rooms.Count
            : state.RoomId == -1 && state.CorridorId >= 0 && state.CorridorId < state.Map.Corridors.Count
              && state.TileIndex >= 0 && state.TileIndex < state.Map.Corridor(state.CorridorId).Tiles.Count
              && (state.HeadingRoomId == state.Map.Corridor(state.CorridorId).RoomA
                  || state.HeadingRoomId == state.Map.Corridor(state.CorridorId).RoomB));

    private static bool ConfirmedDead(HeroRecord hero, IReadOnlyDictionary<string, ExpeditionHeroState> conditions, string id) =>
        hero.IsDead || conditions.TryGetValue(id, out var saved) && saved?.Outcome?.HeroId == id && saved.Outcome.Died;
}
