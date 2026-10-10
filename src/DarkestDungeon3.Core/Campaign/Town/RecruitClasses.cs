using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>
/// The classes the stagecoach recruits from: DD2's base heroes; the Bounty Hunter as a permanent recruit (DD2 only
/// hires him at the inn, as a mercenary; owner request 2026-10-10); and the Crusader from DD2's DLC when the game owns
/// it, so DD1's Crusader (Reynauld included) plays as himself rather than through the Man-at-Arms.
/// </summary>
public static class RecruitClasses
{
    public const string BountyHunter = "bounty_hunter", Crusader = "crusader";

    public static readonly IReadOnlyList<string> Base = new[]
    {
        "flagellant", "grave_robber", "hellion", "highwayman", "jester", "leper", "man_at_arms", "occultist", "plague_doctor", "runaway", "vestal",
    };

    public static IReadOnlyList<string> For(bool ownsCrusader) =>
        Base.Append(BountyHunter).Concat(ownsCrusader ? new[] { Crusader } : new string[0]).ToList();
}
