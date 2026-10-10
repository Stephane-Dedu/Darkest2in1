using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign;

public static class Currency
{
    public const string Gold = "gold";
    public const string Bust = "bust";
    public const string Portrait = "portrait";
    public const string Deed = "deed";
    public const string Crest = "crest";

    public static readonly string[] Heirlooms = { Bust, Portrait, Deed, Crest };
}

/// <summary>The whole DD1-style campaign: everything a save file holds.</summary>
public sealed class Estate
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public string Name = "The Hamlet";
    public int Seed;
    public int RandomCounter;
    public int Week;

    public Dictionary<string, int> Currencies = new();
    /// <summary>Purchased building upgrades as "treeId:code", e.g. "stage_coach.rostersize:a".</summary>
    public HashSet<string> Upgrades = new();

    /// <summary>DD1 counts the tutorial as quest one; zones unlock by this count.</summary>
    public int QuestsCompleted = 1;
    public Dictionary<string, int> ZoneXp = new();
    public HashSet<string> CompletedPlotQuests = new();
    /// <summary>DD1 Caretaker roster goals: classes which have reached Resolve Level 6, even after leaving.</summary>
    public HashSet<string> CompletedResolveGoals = new();
    /// <summary>Journal page indices carried home; older estates start with no invented collection.</summary>
    public HashSet<int> CollectedJournalPages = new();

    public List<HeroRecord> Roster = new();
    public List<HeroRecord> Recruits = new();
    public List<HeroRecord> Graveyard = new();
    public List<QuestOffer> Quests = new();
    public List<string> Trinkets = new();
    public List<string> WagonStock = new();
    public int RegionLayoutVersion;
    /// <summary>Messages for the next town screen ("Dismas went missing", ...).</summary>
    public List<string> TownLog = new();
    /// <summary>Saved weekly chronicle. Null identifies an older save whose surviving TownLog needs importing.</summary>
    public List<ActivityWeek> ActivityLog;
    /// <summary>This town visit's DD1 town event (null: none), and what the roll needs to remember.</summary>
    public string TownEventId;
    /// <summary>The plot quest the party last came home from (DD1's town background changes after a Darkest Dungeon part).</summary>
    public string LastReturnPlotId;
    /// <summary>A new estate's first act: DD1's opening raid on the road (the bandits), before the first week.</summary>
    public bool OpeningRaidPending;
    public int TownEventMisses;
    public Dictionary<string, int> TownEventLastWeek = new();
    public int TownEventFreeUpgrades;
    /// <summary>Older builds rolled heroes at the main menu without DD2's quirk data: fixed once.</summary>
    public bool QuirksRepaired;

    /// <summary>Optional extras the user can switch on (e.g. DD2 regions as expedition zones).</summary>
    public Dictionary<string, bool> Toggles = new();

    public int Get(string currency) => Currencies.TryGetValue(currency, out var v) ? v : 0;

    public void Add(string currency, int amount) => Currencies[currency] = Get(currency) + amount;

    public bool CanAfford(IEnumerable<Reward> cost) =>
        cost.GroupBy(c => c.Type).All(g => Get(g.Key) >= g.Sum(c => c.Amount));

    public bool TrySpend(IReadOnlyCollection<Reward> cost)
    {
        if (!CanAfford(cost)) return false;
        foreach (var c in cost) Add(c.Type, -c.Amount);
        return true;
    }

    public bool HasUpgrade(string treeId, string code) => Upgrades.Contains(treeId + ":" + code);

    public bool IsToggled(string toggle) => Toggles.TryGetValue(toggle, out var on) && on;

    /// <summary>
    /// A fresh seed for the next random event. The counter lives in the save, so a reloaded estate keeps
    /// producing the same sequence and players can't reroll by reloading.
    /// </summary>
    public int NextSeed() => unchecked(Seed * 486187739 + ++RandomCounter * 16777619);

    public Rng NextRng() => new(NextSeed());

    public HeroRecord Hero(string id) => Roster.FirstOrDefault(h => h.Id == id);

    public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);
}
