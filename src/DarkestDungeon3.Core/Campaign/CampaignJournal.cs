using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>DD1's persistent week records, independent of runtime messages and gameplay RNG.</summary>
public static class CampaignJournal
{
    public static bool Initialize(Estate estate)
    {
        if (estate.ActivityLog != null) return false;
        estate.ActivityLog = new List<ActivityWeek>
        {
            new() { Week = estate.Week, Town = new List<string>(estate.TownLog ?? new List<string>()) }
        };
        return true;
    }

    private static ActivityWeek Current(Estate estate)
    {
        Initialize(estate);
        var week = estate.ActivityLog.FirstOrDefault(w => w.Week == estate.Week);
        if (week != null) return week;
        week = new ActivityWeek { Week = estate.Week };
        estate.ActivityLog.Add(week);
        return week;
    }

    public static void Town(Estate estate, string message, HeroRecord hero = null, ActivityEntryKind kind = ActivityEntryKind.HeroActivity)
    {
        var week = Current(estate); // import legacy messages before appending this one
        (estate.TownLog ??= new List<string>()).Add(message);
        week.Town.Add(message);
        if (hero != null) week.TownActors.Add(Actor(hero, week.Town.Count - 1, kind));
    }

    /// <summary>Resolve town activities into the newly advanced week, keeping the previous week's entries.</summary>
    public static void TownResults(Estate estate, IEnumerable<string> messages, IEnumerable<ActivityTownActor> actors = null)
    {
        var week = Current(estate);
        estate.TownLog = messages.ToList();
        int start = week.Town.Count;
        week.Town.AddRange(estate.TownLog);
        foreach (var actor in actors ?? Enumerable.Empty<ActivityTownActor>())
            if (actor.MessageIndex >= 0 && actor.MessageIndex < estate.TownLog.Count)
                week.TownActors.Add(CopyActor(actor, start));
    }

    public static ActivityTownActor Actor(HeroRecord hero, int messageIndex, ActivityEntryKind kind = ActivityEntryKind.HeroActivity) => new()
    {
        MessageIndex = messageIndex, HeroId = hero.Id, HeroName = hero.Name, HeroClass = hero.ClassId, Kind = kind
    };

    public static void BuildingUpgrade(Estate estate, string building, string tree, string code, int percent)
    {
        Town(estate, $"{building.Replace('_', ' ')} has been leveled up to {percent}%.");
        var week = Current(estate);
        week.BuildingUpgrades.Add(new ActivityBuildingUpgrade
        {
            MessageIndex = week.Town.Count - 1, Building = building, Tree = tree, Code = code, Percent = percent
        });
    }

    private static ActivityTownActor CopyActor(ActivityTownActor actor, int offset = 0) => new()
    {
        MessageIndex = offset + actor.MessageIndex, HeroId = actor.HeroId,
        HeroName = actor.HeroName, HeroClass = actor.HeroClass, Kind = actor.Kind
    };

    public static void Embark(Estate estate, QuestOffer quest, IEnumerable<HeroRecord> heroes)
    {
        Current(estate).Raids.Add(new ActivityRaid
        {
            Quest = quest.ToString(), Region = quest.Dungeon, Result = "embark",
            Heroes = heroes.Select(h => new ActivityHero
            {
                Id = h.Id, Name = h.Name, ClassId = h.ClassId,
                ResolveBefore = h.ResolveLevel, ResolveAfter = h.ResolveLevel
            }).ToList()
        });
    }

    public static void Return(Estate estate, HomecomingReport report)
    {
        Current(estate).Raids.Add(new ActivityRaid
        {
            Quest = report.Quest.ToString(), Region = report.Quest.Dungeon, Result = report.Result,
            Messages = report.Log.ToList(),
            MessageActors = report.MessageActors.Where(a => a.MessageIndex >= 0 && a.MessageIndex < report.Log.Count)
                .Select(a => CopyActor(a)).ToList(),
            Heroes = report.Heroes.Select(h => new ActivityHero
            {
                Id = h.Id, Name = h.Name, ClassId = h.ClassId, Died = h.Died,
                ResolveBefore = h.ResolveBefore, ResolveAfter = h.ResolveAfter
            }).ToList()
        });
    }
}

public sealed class ActivityWeek
{
    public int Week;
    public List<string> Town = new();
    /// <summary>Actor snapshots keyed to Town's append-only message indices; older records have none.</summary>
    public List<ActivityTownActor> TownActors = new();
    public List<ActivityBuildingUpgrade> BuildingUpgrades = new();
    public List<ActivityRaid> Raids = new();
}

public sealed class ActivityTownActor
{
    public int MessageIndex;
    public string HeroId, HeroName, HeroClass;
    public ActivityEntryKind Kind;
}

public enum ActivityEntryKind { HeroActivity, LevelUp }

public sealed class ActivityBuildingUpgrade
{
    public int MessageIndex, Percent;
    public string Building, Tree, Code;
}

public sealed class ActivityRaid
{
    public string Quest, Region, Result;
    public List<ActivityHero> Heroes = new();
    public List<string> Messages = new();
    public List<ActivityTownActor> MessageActors = new();
}

public sealed class ActivityHero
{
    public string Id, Name, ClassId;
    public bool Died;
    public int ResolveBefore, ResolveAfter;
}
