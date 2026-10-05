using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dungeon;

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

    /// <summary>Future and completed campaign goals; viewing this list never generates quests or consumes a seed.</summary>
    public static IReadOnlyList<CaretakerQuestGoal> Quests(Estate estate, Dd1Campaign dd1)
    {
        var goals = new List<CaretakerQuestGoal>();
        foreach (var zone in CampaignRegions.Options.Where(ZoneBase.IsExtra).Distinct())
        {
            string boss = ZoneBase.BossOf(zone);
            if (boss == null) continue;
            for (int tier = 1; tier <= 3; tier++)
            {
                string id = $"region_{zone}_boss_{tier}";
                bool complete = estate.CompletedPlotQuests.Contains(id);
                if (CampaignRegions.Enabled(estate, zone) || complete)
                    goals.Add(new CaretakerQuestGoal { Id = id, Region = zone, BossId = boss, Tier = tier, Complete = complete });
            }
        }
        foreach (var plot in dd1.Goals?.Plot ?? Enumerable.Empty<PlotQuest>())
        {
            if (!plot.Progression || (plot.Type == "explore" && plot.MapName == null)) continue;
            string region = CampaignRegions.StoryRegion(estate, plot);
            bool complete = estate.CompletedPlotQuests.Contains(plot.Id);
            if (region == QuestBoard.DarkestDungeon || CampaignRegions.Enabled(estate, region) || complete)
                goals.Add(new CaretakerQuestGoal { Id = plot.Id, Region = region, Complete = complete });
        }
        return goals;
    }
}

public sealed class CaretakerQuestGoal
{
    public string Id, Region, BossId;
    public int Tier;
    public bool Complete;
}
