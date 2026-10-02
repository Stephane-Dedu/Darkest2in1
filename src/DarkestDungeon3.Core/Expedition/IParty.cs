using System.Collections.Generic;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>
/// The four heroes on the expedition, as the crawl rules see them. In the game this wraps DD2's real actors
/// (so DD2 owns HP, death's door and meltdowns); in tests it's a plain fake.
/// </summary>
public interface IParty
{
    /// <summary>Living heroes, front rank first.</summary>
    IReadOnlyList<string> Alive { get; }

    float HpFraction(string heroId);

    /// <summary>Lose a fraction of max HP. DD2's rules decide death's door and death.</summary>
    void Damage(string heroId, float maxHpFraction, string cause);

    void Heal(string heroId, float maxHpFraction);

    /// <summary>Add DD2 stress points (negative relieves).</summary>
    void AddStress(string heroId, int points, string cause);

    /// <summary>Give a quirk or disease named by its DD1 id; the game maps it to DD2. Returns the DD2 id or null.</summary>
    string AddDd1Quirk(string heroId, string dd1QuirkId);

    /// <summary>DD1 "purge": remove one negative quirk or disease. Returns what was removed, or null.</summary>
    string PurgeNegative(string heroId);
}
