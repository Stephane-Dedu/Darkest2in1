using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>Everything about an expedition in progress. Saved with the estate, so a crash resumes mid-dungeon.</summary>
public sealed class ExpeditionState
{
    public QuestOffer Quest;
    public DungeonMap Map;
    public List<string> Party = new();           // hero ids, rank order
    public Inventory Pack = new();
    public float Light = 100f;

    /// <summary>Room the party stands in, or -1 while in a corridor.</summary>
    public int RoomId;
    public int CorridorId = -1;
    public int TileIndex = -1;
    /// <summary>Room the party is walking toward while in a corridor.</summary>
    public int HeadingRoomId = -1;

    public int Seed;
    public int RandomCounter;

    public int StepsTaken;
    public int BattlesWon;
    public bool QuestComplete;
    public bool Ended;
    public bool Retreated;

    public bool InRoom => RoomId >= 0;
}

public enum CrawlEventType
{
    EnteredRoom,
    EnteredTile,
    LightChanged,
    Stress,
    Battle,            // the party must fight before moving on (room or hall battle)
    Ambush,            // a battle that wasn't on the map (backtracking, camping)
    Trap,              // a scouted trap waits: the player picks disarm or walk past
    TrapDisarmed,
    TrapSprung,
    Obstacle,          // blocks forward movement until cleared
    ObstacleCleared,
    Curio,             // optional interaction
    Ate,
    Starving,
    Scouted,
    QuestComplete,
    Blocked,
}

public sealed class CrawlEvent
{
    public CrawlEventType Type;
    public int RoomId = -1, CorridorId = -1, TileIndex = -1;
    public string HeroId;
    public string ContentId;
    public float Amount;
    public bool HeroesSurprised, MonstersSurprised;

    public override string ToString() =>
        $"{Type}{(RoomId >= 0 ? $" room {RoomId}" : "")}{(CorridorId >= 0 ? $" corridor {CorridorId}#{TileIndex}" : "")}" +
        $"{(ContentId != null ? " " + ContentId : "")}{(HeroId != null ? " hero " + HeroId : "")}{(Amount != 0 ? " " + Amount : "")}" +
        $"{(HeroesSurprised ? " (heroes surprised)" : "")}{(MonstersSurprised ? " (monsters surprised)" : "")}";
}
