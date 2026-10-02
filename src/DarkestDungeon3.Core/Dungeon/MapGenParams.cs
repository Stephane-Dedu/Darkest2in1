using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>One <c>map:</c> block of DD1's <c>scripts/map_generator.darkest</c>.</summary>
public sealed class MapGenParams
{
    public string Size;          // short | medium | long
    public string QuestType;     // explore | cleanse | gather | activate | inventory_activate | kill_boss
    public string Dungeon;       // crypts | weald | warrens | cove (DD1 zone ids)
    public int BaseRooms;
    public int BaseCorridors;
    public int GridWidth, GridHeight;
    public int Spacing;
    public int GoalRooms;
    public float Connectivity;
    public int MinFinalDistance;
    public IntRange HallBattle, HallTrap, HallObstacle, HallCurio, HallHunger;
    public IntRange TotalRoomBattles, RoomBattle, RoomGuardedCurio, RoomCurio, RoomGuardedTreasure, RoomTreasure;
    public IntRange SecretRooms, NudgeSpan;
    public float NudgeNeighbourBias, NudgeCentreBias;

    public static MapGenParams FromRecord(DarkestRecord r)
    {
        var grid = r.Values("gridsize");
        return new MapGenParams
        {
            Size = r.Str("size"),
            QuestType = r.Str("quest_type"),
            Dungeon = r.Str("dungeon_type"),
            BaseRooms = r.Int("base_room_number"),
            BaseCorridors = r.Int("base_corridor_number"),
            GridWidth = grid.Count > 0 ? r.Int("gridsize", 0) : 4,
            GridHeight = grid.Count > 1 ? r.Int("gridsize", 1) : 4,
            Spacing = r.Int("spacing", fallback: 4),
            GoalRooms = r.Int("goal_room_number"),
            Connectivity = r.Float("connectivity", fallback: 0.9f),
            MinFinalDistance = r.Int("min_final_distance"),
            HallBattle = r.Range("hallway_battle"),
            HallTrap = r.Range("hallway_trap"),
            HallObstacle = r.Range("hallway_obstacle"),
            HallCurio = r.Range("hallway_curio"),
            HallHunger = r.Range("hallway_hunger"),
            TotalRoomBattles = r.Range("total_room_battles"),
            RoomBattle = r.Range("room_battle"),
            RoomGuardedCurio = r.Range("room_guarded_curio"),
            RoomCurio = r.Range("room_curio"),
            RoomGuardedTreasure = r.Range("room_guarded_treasure"),
            RoomTreasure = r.Range("room_treasure"),
            SecretRooms = r.Range("secret_rooms"),
            NudgeSpan = r.Range("nudge_span"),
            NudgeNeighbourBias = r.Float("nudge_neighbour_bias"),
            NudgeCentreBias = r.Float("nudge_map_centre_bias"),
        };
    }

    public MapGenParams With(Action<MapGenParams> edit)
    {
        var copy = (MapGenParams)MemberwiseClone();
        edit(copy);
        return copy;
    }

    public override string ToString() => $"{Dungeon}/{Size}/{QuestType}: {BaseRooms} rooms, {BaseCorridors} corridors, grid {GridWidth}x{GridHeight}";
}

/// <summary>All map generator blocks, with a lookup that degrades gracefully for combinations DD1 never defined.</summary>
public sealed class MapGenTable
{
    private readonly List<MapGenParams> _all;

    public MapGenTable(IEnumerable<MapGenParams> all) => _all = all.ToList();

    public IReadOnlyList<MapGenParams> All => _all;

    public static MapGenTable Load(string mapGeneratorDarkestPath) =>
        new(DarkestFile.Load(mapGeneratorDarkestPath).Where(r => r.Type == "map").Select(MapGenParams.FromRecord));

    /// <summary>
    /// Exact (dungeon, size, quest) match, then the same size and quest from another dungeon (re-labelled),
    /// then the same size with explore rules. DD1 itself only defines a subset (e.g. no short kill_boss).
    /// </summary>
    public MapGenParams Find(string dungeon, string size, string questType)
    {
        var exact = _all.FirstOrDefault(p => p.Dungeon == dungeon && p.Size == size && p.QuestType == questType);
        if (exact != null) return exact;

        var sameShape = _all.FirstOrDefault(p => p.Size == size && p.QuestType == questType)
                        ?? _all.FirstOrDefault(p => p.Dungeon == dungeon && p.Size == size && p.QuestType == "explore")
                        ?? _all.FirstOrDefault(p => p.Size == size && p.QuestType == "explore")
                        ?? _all.First();
        return sameShape.With(p => { p.Dungeon = dungeon; p.QuestType = questType; });
    }
}
