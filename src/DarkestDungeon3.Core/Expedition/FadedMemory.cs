using System.Collections.Generic;
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
