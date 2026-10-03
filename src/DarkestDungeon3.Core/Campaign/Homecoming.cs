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

    public static List<string> Apply(Estate estate, Dd1Campaign dd1, ExpeditionState expedition, IEnumerable<HeroOutcome> outcomes) =>
        Report(estate, dd1, expedition, outcomes).Log;

    /// <summary>
    /// Bring the party home and say how it went. Gold and heirlooms count success or not; gems are sold for DD1's
    /// value (<paramref name="items"/>); trinkets found go to the estate (rarity-only ones are rolled with
    /// <paramref name="trinketOfRarity"/>).
    /// </summary>
    public static HomecomingReport Report(Estate estate, Dd1Campaign dd1, ExpeditionState expedition, IEnumerable<HeroOutcome> outcomes,
                                          ItemCatalog items = null, Func<string, string> trinketOfRarity = null)
    {
        var report = new HomecomingReport { Quest = expedition.Quest };
        var log = report.Log;
        var quest = expedition.Quest;
        var outcomeList = outcomes.ToList();
        bool success = expedition.QuestComplete && !expedition.Retreated;
        bool wiped = outcomeList.Count > 0 && outcomeList.All(o => o.Died);
        report.Result = success ? "complete" : wiped ? "defeat" : "retreat";

        // Loot carried out of the dungeon always counts, success or not.
        foreach (var kv in expedition.Pack.Items.ToList())
        {
            if (kv.Value <= 0) continue;
            if (kv.Key == Currency.Gold || Currency.Heirlooms.Contains(kv.Key))
            {
                estate.Add(kv.Key, kv.Value);
                report.Loot[kv.Key] = (report.Loot.TryGetValue(kv.Key, out var had) ? had : 0) + kv.Value;
                log.Add($"Brought home {kv.Value} {kv.Key}");
            }
            else if (items?.Get(kv.Key) is { Type: "gem" } gem)
            {
                int value = gem.SellPrice * kv.Value;
                estate.Add(Currency.Gold, value);
                report.GemGold += value;
                report.Gems[kv.Key] = kv.Value;
                log.Add($"Sold {kv.Value} {kv.Key} for {value} gold");
            }
            else if (kv.Key.StartsWith("trinket:"))
            {
                string id = kv.Key.Substring(8);
                for (int i = 0; i < kv.Value; i++)
                {
                    string trinket = LootDrop.IsTrinketRarity(id) ? trinketOfRarity?.Invoke(id) : id;
                    if (trinket == null) continue;
                    estate.Trinkets.Add(trinket);
                    report.Trinkets.Add(trinket);
                    log.Add($"Found a trinket: {trinket}");
                }
            }
        }

        if (success)
        {
            foreach (var r in quest.Rewards)
            {
                if (r.Type == "trinket")
                {
                    string trinket = LootDrop.IsTrinketRarity(r.Id) ? trinketOfRarity?.Invoke(r.Id) : r.Id;
                    if (trinket != null) { estate.Trinkets.Add(trinket); report.Trinkets.Add(trinket); }
                }
                else estate.Add(r.Type, r.Amount);
                report.Rewards.Add(r);
                log.Add($"Quest reward: {r}");
            }
            estate.QuestsCompleted++;
            int zoneXp = quest.Difficulty < dd1.ZoneXpPerQuest.Count ? dd1.ZoneXpPerQuest[quest.Difficulty] : 2;
            estate.ZoneXp[quest.Dungeon] = (estate.ZoneXp.TryGetValue(quest.Dungeon, out var z) ? z : 0) + zoneXp;
            if (quest.IsPlot && quest.PlotId != null) estate.CompletedPlotQuests.Add(quest.PlotId);
        }

        foreach (var o in outcomeList)
        {
            var hero = estate.Hero(o.HeroId);
            if (hero == null) continue;
            var result = new HeroResult { Id = hero.Id, Name = hero.Name, ClassId = hero.ClassId, ResolveBefore = hero.ResolveLevel };
            report.Heroes.Add(result);
            if (o.Died)
            {
                result.Died = true;
                result.Cause = o.CauseOfDeath;
                hero.IsDead = true;
                hero.CauseOfDeath = o.CauseOfDeath;
                hero.WeekDied = estate.Week;
                estate.Roster.Remove(hero);
                estate.Graveyard.Add(hero);
                log.Add($"{hero.Name} did not return ({o.CauseOfDeath}).");
                continue;
            }
            var quirksBefore = hero.Quirks.ToList();
            hero.Stress = o.Stress;
            if (o.Quirks != null) hero.Quirks = o.Quirks;
            if (o.Trinkets != null) hero.Trinkets = o.Trinkets;
            result.Stress = hero.Stress;
            result.NewQuirks = hero.Quirks.Except(quirksBefore).ToList();
            result.LostQuirks = quirksBefore.Except(hero.Quirks).ToList();
            if (success)
            {
                // Town events can send a party off with a resolve bonus (DD1 resolve_xp_bonus_percent).
                float bonus = expedition.PendingBuffs.TryGetValue(hero.Id, out var buffs)
                    ? buffs.Select(b => dd1.Buffs?.Get(b)).Where(b => b?.Stat == "resolve_xp_bonus_percent").Sum(b => b.Amount) : 0f;
                result.XpGained = (int)Math.Round(ResolveXp(quest) * (1f + bonus));
                hero.ResolveXp += result.XpGained;
                int before = hero.ResolveLevel;
                hero.ResolveLevel = Math.Max(before, Math.Min(6, dd1.HeroResolveLevel(hero.ResolveXp)));
                if (hero.ResolveLevel > before) log.Add($"{hero.Name} reached resolve level {hero.ResolveLevel}.");
            }
            result.ResolveAfter = hero.ResolveLevel;
            result.ResolveXp = hero.ResolveXp;
        }

        estate.Quests.RemoveAll(q => q.Id == quest.Id);
        log.Add(success ? $"Quest complete: {quest}." : expedition.Retreated ? $"The party retreated from {quest}." : $"Quest failed: {quest}.");
        return report;
    }

}

/// <summary>How an expedition ended, for the results screen.</summary>
public sealed class HomecomingReport
{
    public QuestOffer Quest;
    public string Result;                         // complete, retreat, defeat
    public List<HeroResult> Heroes = new();
    public Dictionary<string, int> Loot = new();  // gold and heirlooms brought home
    public Dictionary<string, int> Gems = new();
    public int GemGold;
    public List<string> Trinkets = new();
    public List<Reward> Rewards = new();
    public List<string> Log = new();
}

public sealed class HeroResult
{
    public string Id, Name, ClassId, Cause;
    public bool Died;
    public int ResolveBefore, ResolveAfter, ResolveXp, XpGained, Stress;
    public List<string> NewQuirks = new(), LostQuirks = new();
}
