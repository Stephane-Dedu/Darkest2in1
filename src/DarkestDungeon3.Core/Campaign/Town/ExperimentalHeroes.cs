using System.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>Opt-in visitors for local model/combat testing; excluded from ordinary coach rolls.</summary>
public static class ExperimentalHeroes
{
    public const string Vilhelm = "sir_vilhelm", VilhelmName = "Sir Vilhelm", VilhelmDonor = "crusader";

    public static bool CanInviteVilhelm(Estate estate) => estate != null && estate.Recruits.Count < 7
        && !estate.Roster.Concat(estate.Recruits).Any(h => h.ClassId == Vilhelm && !h.IsDead);

    public static HeroRecord InviteVilhelm(Hamlet hamlet, Rng rng)
    {
        if (hamlet==null || !CanInviteVilhelm(hamlet.Estate)) return null;
        var hero=hamlet.MakeHero(Vilhelm,rng,0);
        hero.Name=VilhelmName;
        hero.EquippedSkills.AddRange(VilhelmKit.Loadout);
        hamlet.Estate.Recruits.Add(hero);
        return hero;
    }
}
