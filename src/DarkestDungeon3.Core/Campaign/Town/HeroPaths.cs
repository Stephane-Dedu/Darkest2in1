using DarkestDungeon3.Core.Dd2Data;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>Campaign specialisation: Resolve unlocks a choice, while native DD2 supplies its effects.</summary>
public static class HeroPaths
{
    public const int RequiredResolve = 2;
    public const int TrainingGold = 1000;

    public static string Refusal(Estate estate, HeroRecord hero, HeroPathCatalog catalog, string pathId,
        bool expeditionActive, bool guildOpen, int cost)
    {
        if (expeditionActive) return "Retrain in the Hamlet";
        if (hero == null || !estate.Roster.Contains(hero)) return "Choose a roster hero";
        if (!guildOpen) return "The Guild is not open";
        if (!hero.IsAvailable) return "Hero is unavailable";
        string target = pathId ?? catalog.DefaultId(hero.ClassId);
        if (!catalog.IsValid(hero.ClassId, target)) return "This class has no such Path";
        if (catalog.ResolveId(hero.ClassId, hero.PathId) == target) return "Already following this Path";
        if (hero.ResolveLevel < RequiredResolve) return "Needs resolve 2";
        if (estate.Get(Currency.Gold) < cost) return "Not enough gold";
        return null;
    }

    public static bool Train(Estate estate, HeroRecord hero, HeroPathCatalog catalog, string pathId,
        bool expeditionActive, bool guildOpen, int cost)
    {
        cost = System.Math.Max(0, cost);
        if (Refusal(estate, hero, catalog, pathId, expeditionActive, guildOpen, cost) != null) return false;
        string target = pathId ?? catalog.DefaultId(hero.ClassId);
        hero.PathId = target == catalog.DefaultId(hero.ClassId) ? null : target;
        estate.Add(Currency.Gold, -cost);
        // Learned/mastered/equipped IDs stay attached to the underlying skill across every Path.
        CampaignJournal.Town(estate, $"{hero.Name} retrained at the Guild.", hero);
        return true;
    }
}
