using System.Collections.Generic;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>
/// What the Hamlet needs to know about DD2's heroes, quirks and trinkets. The plugin implements this from
/// DD2's own data; tests use a fake.
/// </summary>
public interface IHeroCatalog
{
    /// <summary>DD2 class ids the Stagecoach can offer.</summary>
    IReadOnlyList<string> RecruitableClasses { get; }

    string RandomName(string classId, Rng rng);

    /// <summary>Starting DD2 quirks for a new recruit.</summary>
    IReadOnlyList<string> StartingQuirks(string classId, Rng rng, int positives, int negatives);

    /// <summary>The DD2 quirk that stands in for a DD1 quirk (e.g. DD1 "alcoholism"), or null if none fits.</summary>
    string MapDd1Quirk(string dd1QuirkId);

    bool IsDisease(string quirkId);
    bool IsPositive(string quirkId);

    /// <summary>A random DD2 trinket of a DD1 rarity (very_common .. very_rare), or null. Hero-only trinkets are
    /// left out unless <paramref name="forClass"/> names their class.</summary>
    string RandomTrinket(string rarity, Rng rng, string forClass = null);

    int TrinketPrice(string trinketId);

    /// <summary>Can a hero of this class wear the trinket (DD2's hero-only trinkets fit one class)?</summary>
    bool TrinketFits(string trinketId, string classId);
}
