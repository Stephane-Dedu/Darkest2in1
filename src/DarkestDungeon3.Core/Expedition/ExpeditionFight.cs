using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>A fresh native fight must restart both teams from its beginning, not mix mid-fight heroes with fresh enemies.</summary>
public sealed class FightCheckpoint
{
    public int RoomId, CorridorId, TileIndex;
    public FightKind Kind;
    public string BattleId, Arena;
    public List<string> Enemies;
    public bool NativePresentation, HeroesSurprised, MonstersSurprised;
    public float Light;
    public Dictionary<string, ExpeditionHeroState> PartyStates = new();

    public FightPlan Plan() => new()
    {
        Kind = Kind, Battle = "config:" + BattleId, NativePresentation = NativePresentation,
        Arenas = Arena == null ? new List<string>() : new List<string> { Arena },
        Enemies = Enemies?.ToList()
    };
}

public static class ExpeditionFight
{
    public static FightCheckpoint Current(ExpeditionState state)
    {
        var saved = state?.FightCheckpoint;
        if (state == null || !state.Started || state.Ended || saved == null
            || saved.RoomId != state.RoomId || saved.CorridorId != state.CorridorId || saved.TileIndex != state.TileIndex
            || string.IsNullOrEmpty(saved.BattleId) || !Enum.IsDefined(typeof(FightKind), saved.Kind)
            || float.IsNaN(saved.Light) || float.IsInfinity(saved.Light) || saved.Light < 0 || saved.Light > 100
            || saved.PartyStates == null || state.Party.Count == 0
            || state.Party.Any(id => !saved.PartyStates.TryGetValue(id, out var hero)
                || hero?.Outcome?.HeroId != id || !ExpeditionParty.IsValid(hero))) return null;
        return saved;
    }

    public static bool Record(ExpeditionState state, FightPlan plan, string battle, string arena,
        bool heroesSurprised, bool monstersSurprised)
    {
        if (state == null || plan == null || string.IsNullOrEmpty(battle) || !state.Started || state.Ended
            || state.Party.Count == 0 || state.Party.Any(id => !state.PartyStates.TryGetValue(id, out var hero)
                || hero?.Outcome?.HeroId != id || !ExpeditionParty.IsValid(hero))) return false;
        state.FightCheckpoint = new FightCheckpoint
        {
            RoomId = state.RoomId, CorridorId = state.CorridorId, TileIndex = state.TileIndex,
            Kind = plan.Kind, BattleId = battle, Arena = arena, Enemies = plan.Enemies?.ToList(),
            NativePresentation = plan.NativePresentation, Light = state.Light,
            HeroesSurprised = heroesSurprised, MonstersSurprised = monstersSurprised,
            PartyStates = state.Party.ToDictionary(id => id, id => ExpeditionParty.Copy(state.PartyStates[id]))
        };
        return true;
    }

    public static bool RestoreParty(ExpeditionState state)
    {
        var saved = Current(state);
        if (saved == null) return false;
        state.PartyStates = state.Party.ToDictionary(id => id, id => ExpeditionParty.Copy(saved.PartyStates[id]));
        state.Light = saved.Light;
        return true;
    }

    public static CrawlEvent Presentation(ExpeditionState state)
    {
        var saved = Current(state);
        if (saved == null) return null;
        return new CrawlEvent
        {
            Type = saved.Kind == FightKind.CampAmbush ? CrawlEventType.Ambush : CrawlEventType.Battle,
            ContentId = saved.Kind == FightKind.CampAmbush ? "camp" : null,
            RoomId = state.RoomId, CorridorId = state.CorridorId, TileIndex = state.TileIndex,
            HeroesSurprised = saved.HeroesSurprised, MonstersSurprised = saved.MonstersSurprised
        };
    }
}
