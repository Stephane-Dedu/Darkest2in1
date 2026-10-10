using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>
/// The classes the stagecoach recruits from: DD2's base heroes; the Bounty Hunter as a permanent recruit (DD2 only
/// hires him at the inn, as a mercenary; owner request 2026-10-10); the Crusader from DD2's DLC when the game owns it,
/// so DD1's Crusader (Reynauld included) plays as himself rather than through the Man-at-Arms; and DD1's Shieldbreaker
/// when DD1's DLC holds her (a DD2 class built from her DD1 data, drawn with her DD1 art).
/// </summary>
public static class RecruitClasses
{
    public const string BountyHunter = "bounty_hunter", Crusader = "crusader", Shieldbreaker = "shieldbreaker";

    public static readonly IReadOnlyList<string> Base = new[]
    {
        "flagellant", "grave_robber", "hellion", "highwayman", "jester", "leper", "man_at_arms", "occultist", "plague_doctor", "runaway", "vestal",
    };

    /// <summary>Classes DD2 has no hero for: built from DD1's data and drawn with DD1's art.</summary>
    public static readonly IReadOnlyList<string> Dd1Only = new[] { Shieldbreaker };

    public static bool IsDd1Only(string classId) => classId != null && Dd1Only.Contains(classId);

    public static IReadOnlyList<string> For(bool ownsCrusader, bool hasShieldbreaker = false) =>
        Base.Append(BountyHunter)
            .Concat(ownsCrusader ? new[] { Crusader } : new string[0])
            .Concat(hasShieldbreaker ? new[] { Shieldbreaker } : new string[0])
            .ToList();
}
