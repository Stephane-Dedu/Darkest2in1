using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>Campaign region identity, availability and a one-time transition from the old DD1-first board.
/// DD1 remains the rules/data foundation; its region IDs remain available as separate optional destinations.</summary>
public static class CampaignRegions
{
    public const int LayoutVersion = 1;
    public static readonly IReadOnlyList<string> Primary = new[] { "dd2_city", "dd2_farm", "dd2_forest", "dd2_coast" };
    public static readonly IReadOnlyList<string> Legacy = new[] { "crypts", "warrens", "weald", "cove" };
    public static IEnumerable<string> Options => Primary.Concat(Legacy).Concat(ZoneBase.ExtraZones.Except(Primary).OrderBy(z => z, StringComparer.Ordinal));
    public static string MainFor(string dd1Zone) => Primary.FirstOrDefault(z => ZoneBase.Of(z) == dd1Zone);
    public static bool Enabled(Estate estate, string zone) => estate.Toggles.TryGetValue("zone." + zone, out bool on) ? on : Primary.Contains(zone);
    public static int UnlockAfter(Dd1Campaign dd1, string zone) => dd1.ZoneUnlocks.TryGetValue(ZoneBase.Of(zone), out int after) ? after : 1;
    public static bool Unlocked(Estate estate, Dd1Campaign dd1, string zone) => estate.QuestsCompleted >= UnlockAfter(dd1, zone);
    public static IEnumerable<string> Open(Estate estate, Dd1Campaign dd1) => Options.Where(z => Enabled(estate, z) && Unlocked(estate, dd1, z)).Distinct();

    /// <summary>Enabled destinations sharing a map position, native first. Locked areas remain visible so their
    /// unlock requirement can be shown. This does not merge their campaign identities or progress.</summary>
    public static IReadOnlyList<string> AtLocation(Estate estate, string location) =>
        Options.Where(z => ZoneBase.Of(z) == location && Enabled(estate, z)).Distinct().ToList();

    // The intro/crow story survives in a native region when the original area is disabled. DD1 boss chains stay optional.
    public static string StoryRegion(Estate estate, PlotQuest plot) =>
        (plot.Id == "plot_tutorial_crypts" || plot.Id == "plot_crow_trinket") && !Enabled(estate, plot.Dungeon)
            ? MainFor(plot.Dungeon) ?? plot.Dungeon : plot.Dungeon;

    public static bool Migrate(Estate estate, Dd1Campaign dd1)
    {
        if (estate.RegionLayoutVersion >= LayoutVersion) return false;
        foreach (var legacy in Legacy)
        {
            string main = MainFor(legacy);
            if (main != null && estate.ZoneXp.TryGetValue(legacy, out int oldXp))
                estate.ZoneXp[main] = Math.Max(oldXp, estate.ZoneXp.TryGetValue(main, out int nativeXp) ? nativeXp : 0);
        }
        foreach (var quest in estate.Quests)
        {
            if (Enabled(estate, quest.Dungeon)) continue;
            if (!quest.IsPlot && MainFor(quest.Dungeon) is { } main) quest.Dungeon = main;
            else if (dd1.Goals?.Plot.FirstOrDefault(p => p.Id == quest.PlotId) is { } plot) quest.Dungeon = StoryRegion(estate, plot);
        }
        estate.Quests.RemoveAll(q => q.Dungeon != QuestBoard.DarkestDungeon && !Enabled(estate, q.Dungeon));
        if (estate.Quests.Count == 0) estate.Quests = QuestBoard.Generate(estate, dd1);
        else
            foreach (var plot in QuestBoard.PlotOffers(estate, dd1, Open(estate, dd1)))
                if (!estate.Quests.Any(q => q.PlotId == plot.PlotId)) estate.Quests.Add(plot);
        estate.RegionLayoutVersion = LayoutVersion;
        return true;
    }
}
