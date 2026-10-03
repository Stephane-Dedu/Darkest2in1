using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>How one hero came home (read from the DD2 actor at the end of the expedition).</summary>
public sealed class HeroOutcome
{
    public string HeroId;
    public bool Died;
    public string CauseOfDeath;
    public int Stress;
    public List<string> Quirks;     // null = unchanged
    public List<string> Trinkets;   // null = unchanged
}

/// <summary>DD1's return to the Hamlet: rewards, loot, resolve and zone XP, the dead to the graveyard.</summary>
public static class Homecoming
{
    /// <summary>DD1 heroes above a difficulty's resolve band refuse that quest.</summary>
    public static int MaxResolveFor(int difficulty) => difficulty switch { 1 => 2, 3 => 4, _ => 6 };

    /// <summary>The Darkest Dungeon (difficulty 6) only admits seasoned heroes.</summary>
    public static int MinResolveFor(int difficulty) => difficulty >= 6 ? 5 : 0;

    /// <param name="anyResolve">A town event lifted the resolve limits this week.</param>
    public static bool WillEmbark(HeroRecord hero, QuestOffer quest, bool anyResolve = false) =>
        hero.IsAvailable && (anyResolve || (hero.ResolveLevel <= MaxResolveFor(quest.Difficulty) && hero.ResolveLevel >= MinResolveFor(quest.Difficulty)));

    /// <summary>Resolve XP for a finished quest: 2 / 4 / 6 by length (DD1's values; not in its data files).</summary>
    public static int ResolveXp(QuestOffer quest) => quest.ResolveXp > 0 ? quest.ResolveXp : 2 * Math.Max(1, quest.Length);

    public static List<string> Apply(Estate estate, Dd1Campaign dd1, ExpeditionState expedition, IEnumerable<HeroOutcome> outcomes)
    {
        var log = new List<string>();
        var quest = expedition.Quest;
        bool success = expedition.QuestComplete && !expedition.Retreated;

        // Loot carried out of the dungeon always counts, success or not.
        foreach (var kv in expedition.Pack.Items)
            if (kv.Key == Currency.Gold || Currency.Heirlooms.Contains(kv.Key))
            {
                estate.Add(kv.Key, kv.Value);
                log.Add($"Brought home {kv.Value} {kv.Key}");
            }

        if (success)
        {
            foreach (var r in quest.Rewards)
            {
                if (r.Type == "trinket") { if (r.Id != null) estate.Trinkets.Add(r.Id); }
                else estate.Add(r.Type, r.Amount);
                log.Add($"Quest reward: {r}");
            }
            estate.QuestsCompleted++;
            int zoneXp = quest.Difficulty < dd1.ZoneXpPerQuest.Count ? dd1.ZoneXpPerQuest[quest.Difficulty] : 2;
            estate.ZoneXp[quest.Dungeon] = (estate.ZoneXp.TryGetValue(quest.Dungeon, out var z) ? z : 0) + zoneXp;
            if (quest.IsPlot && quest.PlotId != null) estate.CompletedPlotQuests.Add(quest.PlotId);
        }

        foreach (var o in outcomes)
        {
            var hero = estate.Hero(o.HeroId);
            if (hero == null) continue;
            if (o.Died)
            {
                hero.IsDead = true;
                hero.CauseOfDeath = o.CauseOfDeath;
                hero.WeekDied = estate.Week;
                estate.Roster.Remove(hero);
                estate.Graveyard.Add(hero);
                log.Add($"{hero.Name} did not return ({o.CauseOfDeath}).");
                continue;
            }
            hero.Stress = o.Stress;
            if (o.Quirks != null) hero.Quirks = o.Quirks;
            if (o.Trinkets != null) hero.Trinkets = o.Trinkets;
            if (success)
            {
                hero.ResolveXp += ResolveXp(quest);
                int before = hero.ResolveLevel;
                hero.ResolveLevel = Math.Min(6, dd1.ZoneLevel(hero.ResolveXp));
                if (hero.ResolveLevel > before) log.Add($"{hero.Name} reached resolve level {hero.ResolveLevel}.");
            }
        }

        estate.Quests.RemoveAll(q => q.Id == quest.Id);
        log.Add(success ? $"Quest complete: {quest}." : expedition.Retreated ? $"The party retreated from {quest}." : $"Quest failed: {quest}.");
        return log;
    }
}
