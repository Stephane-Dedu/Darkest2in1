using System.Collections.Generic;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>
/// The four heroes on the expedition, as the crawl rules see them. In the game this wraps DD2's real actors
/// (DD2 owns HP/death's door; the expedition owns resolve); in tests it's a plain fake.
/// </summary>
public interface IParty
{
    /// <summary>Living heroes, front rank first.</summary>
    IReadOnlyList<string> Alive { get; }

    float HpFraction(string heroId);

    /// <summary>Lose a fraction of max HP. DD2's rules decide death's door and death.</summary>
    void Damage(string heroId, float maxHpFraction, string cause);

    void Heal(string heroId, float maxHpFraction);

    /// <summary>Add fractional native units, ten DD1 stress per unit. Negative relieves.</summary>
    void AddStress(string heroId, float points, string cause);

    /// <summary>Give a quirk or disease named by its DD1 id; the game maps it to DD2. Returns the DD2 id or null.</summary>
    string AddDd1Quirk(string heroId, string dd1QuirkId);

    /// <summary>DD1 "purge": remove one negative quirk or disease. Returns what was removed, or null.</summary>
    string PurgeNegative(string heroId);

    /// <summary>Cure one disease (camp medicine). Returns what was cured, or null.</summary>
    string CureDisease(string heroId);
}

/// <summary>Optional native resistances for exploration effects, expressed as fractions.</summary>
public interface ITravelResistance
{
    float Resistance(string heroId, string kind);
}

/// <summary>Affliction reactions at the campaign interaction boundary.</summary>
public interface IResolveParty
{
    bool Allows(string heroId, string reaction);
}
