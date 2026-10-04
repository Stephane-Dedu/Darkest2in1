using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>
/// A hero as the Hamlet remembers it between expeditions. DD1 rules (resolve level, guild and blacksmith ranks,
/// town activities) wrapped around a DD2 class. At embark it becomes a DD2 actor; afterwards the results are
/// copied back here.
/// </summary>
public sealed class HeroRecord
{
    public string Id;
    /// <summary>DD2 ActorDataClass id, e.g. "highwayman".</summary>
    public string ClassId;
    public string Name;

    // DD1 progression.
    public int ResolveLevel;
    public int ResolveXp;
    public int WeaponRank;      // blacksmith, 0..4
    public int ArmorRank;       // blacksmith, 0..4
    public Dictionary<string, int> SkillRanks = new();   // guild, per DD2 skill id

    // DD2 loadout.
    public string PathId;                                  // null = class default
    public List<string> EquippedSkills = new();            // empty = DD2 default kit
    public List<string> Trinkets = new();                  // DD2 ids in left/right slots; null marks an empty left slot
    [Newtonsoft.Json.JsonIgnore]
    public IEnumerable<string> WornTrinkets => Trinkets.Where(t => !string.IsNullOrEmpty(t));
    public string TrinketAt(int slot) => slot >= 0 && slot < Trinkets.Count ? Trinkets[slot] : null;
    public void SetTrinket(int slot, string id)
    {
        while (Trinkets.Count <= slot) Trinkets.Add(null);
        Trinkets[slot] = id;
        while (Trinkets.Count > 0 && string.IsNullOrEmpty(Trinkets[Trinkets.Count - 1])) Trinkets.RemoveAt(Trinkets.Count - 1);
    }
    public List<string> CampingSkills = new();             // DD1-style camp skills (ours)
    public List<string> LearnedSkills = new();             // DD2 combat skills learned at the Guild (were locked)
    public List<string> MasteredSkills = new();            // DD2 combat skills upgraded at the Guild (base ids)

    // Condition carried between expeditions.
    public int Stress;                                     // DD2 scale (0..stress_max, 10 by default)
    public List<string> Quirks = new();                    // DD2 quirk ids, positive, negative and diseases
    public List<string> LockedQuirks = new();

    // Town.
    public string Activity;                                // e.g. "abbey.prayer"; null = idle
    public string ActivityTarget;                          // sanitarium: the quirk being treated
    public bool ActivityLocked;                            // DD1 side effect: refuses to leave next week
    public List<string> PendingBuffs = new();              // DD1 town buffs (hangover...) for the next expedition
    public int MissingWeeks;                               // "went missing" side effect
    public int WeekRecruited;

    // Death.
    public bool IsDead;
    /// <summary>Offered at the stagecoach by DD1's "From Beyond" event: a fallen hero who can return (only one).</summary>
    public bool FromGraveyard;
    public string CauseOfDeath;
    public int WeekDied;

    public bool IsAvailable => !IsDead && MissingWeeks == 0 && Activity == null;

    public override string ToString() => $"{Name} ({ClassId}, resolve {ResolveLevel}, stress {Stress})";
}
