using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>Everything about an expedition in progress. Saved with the estate, so a crash resumes mid-dungeon.</summary>
public sealed class ExpeditionState
{
    public QuestOffer Quest;
    public QuestGoal Goal;
    public int GoalProgress;
    public DungeonMap Map;
    public List<string> Party = new();           // hero ids, rank order
    public Inventory Pack = new();
    /// <summary>Resolved battle/camping loot awaiting dismissal, saved with its exact taken/remainder amounts.</summary>
    public BattleSpoils PendingSpoils;
    /// <summary>A resolved curio/quest result awaiting dismissal; its effects have already been applied.</summary>
    public CurioReport PendingCurio;
    /// <summary>The already-rolled encounter at the current spot, including surprise and camp-ambush identity.</summary>
    public CrawlEvent PendingEncounter;
    public float Light = 100f;

    /// <summary>Room the party stands in, or -1 while in a corridor.</summary>
    public int RoomId;
    public int CorridorId = -1;
    public int TileIndex = -1;
    /// <summary>Room the party is walking toward while in a corridor.</summary>
    public int HeadingRoomId = -1;
    /// <summary>The corridor square the party last stepped off into the current room (where a retreat leads).</summary>
    public int CameFromCorridorId = -1, CameFromTileIndex = -1;
    /// <summary>Exact corridor position/direction to resume after a secret-room detour.</summary>
    public int SecretReturnCorridorId = -1, SecretReturnTileIndex = -1, SecretReturnHeadingRoomId = -1;
    /// <summary>The DD1 monsters of the fight at <see cref="FightAt"/> (rolled when it starts, kept through a
    /// retreat so the same group waits there; its loot is theirs).</summary>
    public List<string> FightMonsters;
    public string FightAt;

    public int Seed;
    public int RandomCounter;

    /// <summary>Hero id → DD2 class id and known camp skills, filled at embark.</summary>
    public Dictionary<string, string> HeroClasses = new();
    public Dictionary<string, List<string>> CampSkills = new();
    public CampState Camp;
    /// <summary>DD1 buffs (camp, curios) waiting to become DD2 tokens at the next fight, per hero.</summary>
    public Dictionary<string, List<string>> PendingBuffs = new();
    /// <summary>"hero|buff" → battles left, for DD1 buffs that last a number of battles.</summary>
    public Dictionary<string, int> BuffBattlesLeft = new();
    public int CampsMade;
    /// <summary>Gold paid at the provisioner, refunded if the expedition is cancelled before it starts.</summary>
    public int ProvisionCost;
    /// <summary>True once the party stood in the dungeon (Crawl.Begin ran).</summary>
    public bool Started;

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
    Retreated,         // the party fled a fight and fell back
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
