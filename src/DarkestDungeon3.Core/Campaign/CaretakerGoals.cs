namespace DarkestDungeon3.Core.Campaign;

/// <summary>Permanent DD1 Caretaker roster achievements, separate from quest completion and gameplay RNG.</summary>
public static class CaretakerGoals
{
    public const int ResolveTarget = 6;

    public static bool Record(Estate estate, HeroRecord hero) => hero != null && hero.ResolveLevel >= ResolveTarget
        && !string.IsNullOrWhiteSpace(hero.ClassId) && estate.CompletedResolveGoals.Add(hero.ClassId);

    /// <summary>Recover achievements still evidenced by older saves; never guess dismissed heroes' history.</summary>
    public static bool Sync(Estate estate)
    {
        bool changed = false;
        foreach (var hero in estate.Roster) changed |= Record(estate, hero);
        foreach (var hero in estate.Graveyard) changed |= Record(estate, hero);
        return changed;
    }
}
