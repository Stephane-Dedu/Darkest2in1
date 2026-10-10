using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>Saved identity and presentation of one memory. Placement and combat activation are separate slices.</summary>
public sealed class FadedMemoryEncounter
{
    public int Version = 1;
    public string Id, Dungeon, BossId;
    public int Difficulty;
    public Dictionary<string, string> HeroSprites = new();
    public MemoryReturnPosition ReturnPosition;
    public int CurioRoomId = -1;
    public string Stage = "ready";
    public List<string> Rewards = new();
    public string HeroId;
}

/// <summary>A separate room prop, so guaranteed placement cannot erase a quest item or ordinary curio.</summary>
public static class FadedMemory
{
    public const string CurioId = "faded_memory";
    public const string TrinketPrefix = "dd3_dd1_";
    public static bool Active(ExpeditionState state) => state?.FadedMemory?.Stage == "fighting";
    public static bool Here(ExpeditionState state) => state is { InRoom: true, Ended: false }
        && state.FadedMemory is { Stage: "ready" } memory && memory.CurioRoomId == state.RoomId;

    public static bool Place(ExpeditionState state, string dungeon, string boss, int difficulty, bool test = false)
    {
        if (state?.Map == null || state.Quest == null || state.FadedMemory != null
            || (!test && state.Quest.Type != "kill_boss") || string.IsNullOrEmpty(dungeon) || string.IsNullOrEmpty(boss)) return false;
        int entrance = state.Map.EntranceRoomId;
        if (entrance < 0 || entrance >= state.Map.Rooms.Count || state.Map.Room(entrance).IsSecret) return false;
        state.FadedMemory = new FadedMemoryEncounter
        {
            Id = state.Quest.Id + "/memory", Dungeon = dungeon, BossId = boss, Difficulty = difficulty,
            CurioRoomId = test && state.InRoom ? state.RoomId : entrance
        };
        return true;
    }

    /// <summary>Roll once before entry. Prefer unowned limited items; exhaustion still honours the two-item reward.</summary>
    public static bool PrepareRewards(FadedMemoryEncounter memory, Dd1Trinkets items, IEnumerable<string> owned, Rng rng)
    {
        if (memory == null || items == null || rng == null) return false;
        if (memory.Rewards.Count != 0) return memory.Rewards.Count == 2;
        var counts = (owned ?? Enumerable.Empty<string>()).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var selected = new List<string>();
        foreach (string rarity in new[] { "very_rare", "ancestral" })
        {
            var pool = items.ForRarity(rarity);
            var available = pool.Where(t => t.Limit == 0 || !counts.TryGetValue(TrinketPrefix + t.Id, out int n) || n < t.Limit).ToList();
            if (available.Count == 0) available = pool.ToList();
            if (available.Count == 0) return false;
            selected.Add(TrinketPrefix + rng.Pick(available).Id);
        }
        memory.Rewards = selected;
        return true;
    }

    public static bool Enter(ExpeditionState state, string hero, string item)
    {
        if (!Here(state) || item != null || !state.Started || !state.Party.Contains(hero)
            || state.PendingCurio != null || state.PendingSpoils != null || state.PendingEncounter != null
            || state.Camp != null || state.FightCheckpoint != null || state.FadedMemory.Rewards.Count != 2) return false;
        var position = MemoryReturnPosition.Capture(state);
        if (position == null) return false;
        state.FadedMemory.ReturnPosition = position;
        state.FadedMemory.HeroId = hero;
        state.FadedMemory.Stage = "fighting";
        return true;
    }

    public static bool Finish(ExpeditionState state, ItemCatalog items, bool victory)
    {
        if (!Active(state) || state.FadedMemory.ReturnPosition?.TryRestore(state) != true
            || state.FadedMemory.Rewards.Count != 2 || state.PendingCurio != null || items == null) return false;
        var memory = state.FadedMemory;
        var report = new CurioReport { CurioId = CurioId, HeroId = memory.HeroId, OutcomeType = victory ? "Victory" : "Retreat",
            Text = victory ? "The vision recedes. Its spoils remain." : "The past releases its hold. This memory is spent." };
        if (victory)
            foreach (string id in memory.Rewards)
            {
                var drop = new LootDrop { Type = "trinket", Id = id, Amount = 1 };
                report.Loot.Add(drop);
                if (!state.Pack.TryTake(drop, items)) report.LeftBehind.Add(drop);
            }
        state.FightCheckpoint = null;
        memory.Stage = victory ? "complete" : "fled";
        state.PendingCurio = report;
        return true;
    }
}

/// <summary>Only expedition location is restored after a memory; combat consequences belong to the current party.</summary>
public sealed class MemoryReturnPosition
{
    public string QuestId, Dungeon;
    public int Seed;
    public int RoomId = -1, CorridorId = -1, TileIndex = -1, HeadingRoomId = -1;
    public int CameFromCorridorId = -1, CameFromTileIndex = -1;
    public int SecretReturnCorridorId = -1, SecretReturnTileIndex = -1, SecretReturnHeadingRoomId = -1;

    public static MemoryReturnPosition Capture(ExpeditionState state)
    {
        if (!ExpeditionRecovery.PositionIsValid(state) || string.IsNullOrEmpty(state.Quest?.Id)
            || string.IsNullOrEmpty(state.Quest.Dungeon)) return null;
        var position = new MemoryReturnPosition
        {
            QuestId = state.Quest.Id, Dungeon = state.Quest.Dungeon, Seed = state.Seed,
            RoomId = state.RoomId, CorridorId = state.CorridorId, TileIndex = state.TileIndex,
            HeadingRoomId = state.HeadingRoomId, CameFromCorridorId = state.CameFromCorridorId,
            CameFromTileIndex = state.CameFromTileIndex,
            SecretReturnCorridorId = state.SecretReturnCorridorId, SecretReturnTileIndex = state.SecretReturnTileIndex,
            SecretReturnHeadingRoomId = state.SecretReturnHeadingRoomId
        };
        return position.LocationIsValid(state.Map) ? position : null;
    }

    public bool TryRestore(ExpeditionState state)
    {
        if (string.IsNullOrEmpty(QuestId) || state is not { Started: true, Ended: false, Map: not null, Quest: not null }
            || state.Quest.Id != QuestId || state.Quest.Dungeon != Dungeon || state.Seed != Seed
            || !LocationIsValid(state.Map)) return false;
        state.RoomId = RoomId; state.CorridorId = CorridorId; state.TileIndex = TileIndex;
        state.HeadingRoomId = HeadingRoomId; state.CameFromCorridorId = CameFromCorridorId;
        state.CameFromTileIndex = CameFromTileIndex;
        state.SecretReturnCorridorId = SecretReturnCorridorId; state.SecretReturnTileIndex = SecretReturnTileIndex;
        state.SecretReturnHeadingRoomId = SecretReturnHeadingRoomId;
        return true;
    }

    private bool LocationIsValid(DungeonMap map)
    {
        if (RoomId >= 0)
        {
            if (RoomId >= map.Rooms.Count) return false;
        }
        else if (RoomId != -1 || !HallIsValid(map, CorridorId, TileIndex, HeadingRoomId)) return false;
        // This remembers the previous room entrance even while travelling or inside a secret room.
        if (CameFromCorridorId != -1 && !HallCoordinatesValid(map, CameFromCorridorId, CameFromTileIndex)) return false;
        if (RoomId >= 0 && map.Room(RoomId).IsSecret)
        {
            if (!HallIsValid(map, SecretReturnCorridorId, SecretReturnTileIndex, SecretReturnHeadingRoomId)) return false;
            if (map.Corridor(SecretReturnCorridorId).Tiles[SecretReturnTileIndex].SecretRoomId != RoomId) return false;
        }
        return true;
    }

    private static bool HallIsValid(DungeonMap map, int corridor, int tile, int heading) =>
        HallCoordinatesValid(map, corridor, tile)
        && (heading == map.Corridor(corridor).RoomA || heading == map.Corridor(corridor).RoomB);

    private static bool HallCoordinatesValid(DungeonMap map, int corridor, int tile) =>
        corridor >= 0 && corridor < map.Corridors.Count && tile >= 0 && tile < map.Corridor(corridor).Tiles.Count;
}
