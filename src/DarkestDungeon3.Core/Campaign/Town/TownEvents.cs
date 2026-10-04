using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>One of DD1's town events (campaign/town_events/*.events.json).</summary>
public sealed class TownEvent
{
    public string Id;
    public float BaseChance, PerNotRolled;
    public int Cooldown;
    public bool Unique;
    public string Tone;                                   // good, bad, neutral
    public int MinimumWeek, DeadHeroes;
    public List<string> RequiredUpgrades = new();         // "treeId:code"
    public List<(int Level, int Count)> HeroLevelCounts = new();
    public List<(string Type, string Str, float Num)> Data = new();
}

/// <summary>
/// DD1's town events: one may happen each town visit (chance grows with each visit without one: 33%, 67%, 75%,
/// then certain), picked by weight among those whose requirements are met and that aren't cooling down.
/// </summary>
public sealed class TownEvents
{
    public List<TownEvent> Events { get; } = new();
    public List<float> ChanceByMisses { get; } = new() { 0.33f, 0.67f, 0.75f, 1f };

    /// <summary>Effects with no DD2-hero meaning yet (plot invasion, trinket retention, arena) are not rolled.</summary>
    private static readonly HashSet<string> Supported = new()
    {
        "free_activity", "activity_cost_change", "activity_lock", "in_activity_buff", "idle_buff", "embark_party_buff",
        "bonus_recruit", "idle_resolve_level", "provision_item_type_cost_change", "provision_item_type_amount_change",
        "upgrade_tag_discount", "upgrade_tag_free", "remove_quest_hero_level_restriction", "dead_recruit",
    };

    public static TownEvents Load(Dd1Install dd1)
    {
        var lib = new TownEvents();
        string dir = dd1.PathOf("campaign", "town_events");
        if (!Directory.Exists(dir)) return lib;
        foreach (var name in new[] { "base.town_events.events.json", "shared_dlc.town_events.events.json" })
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path)) continue;
            foreach (var e in JToken.Parse(File.ReadAllText(path))["events"] ?? new JArray())
            {
                var req = e["requirements"] ?? new JObject();
                var ev = new TownEvent
                {
                    Id = (string)e["id"],
                    BaseChance = (float?)e["base_chance"] ?? 0f,
                    PerNotRolled = (float?)e["per_not_rolled_additional_chance"] ?? 0f,
                    Cooldown = (int?)e["cooldown"] ?? 0,
                    Unique = (bool?)e["is_unique"] ?? false,
                    Tone = (string)e["tone"] ?? "neutral",
                    MinimumWeek = (int?)req["minimum_week"] ?? 0,
                    DeadHeroes = (int?)req["dead_heroes"] ?? 0,
                    RequiredUpgrades = (req["upgrades_purchased"] ?? new JArray()).Select(u => (string)u["tree_id"] + ":" + (string)u["requirement_code"]).ToList(),
                    HeroLevelCounts = (req["hero_level_counts"] ?? new JArray()).Select(h => ((int)h["level"], (int)h["count"])).ToList(),
                    Data = (e["data"] ?? new JArray()).Select(d => ((string)d["type"], (string)d["string_data"] ?? "", (float?)d["number_data"] ?? 0f)).ToList(),
                };
                if (ev.Id != null && ev.Data.Count > 0 && ev.Data.All(d => Supported.Contains(d.Type))) lib.Events.Add(ev);
            }
        }
        string settings = Path.Combine(dir, "town_events.settings.json");
        if (File.Exists(settings))
        {
            var normal = (JToken.Parse(File.ReadAllText(settings))["settings"] ?? new JArray()).FirstOrDefault(s => (string)s["id"] == "normal");
            if (normal?["event_chance_per_town_visits"] is JArray chances && chances.Count > 0)
            {
                lib.ChanceByMisses.Clear();
                lib.ChanceByMisses.AddRange(chances.Select(c => (float)c));
            }
        }
        return lib;
    }

    public TownEvent Get(string id) => id == null ? null : Events.FirstOrDefault(e => e.Id == id);

    public bool Eligible(TownEvent e, Estate estate)
    {
        if (estate.Week < e.MinimumWeek || estate.Graveyard.Count < e.DeadHeroes) return false;
        if (e.Unique && estate.TownEventLastWeek.ContainsKey(e.Id)) return false;
        if (estate.TownEventLastWeek.TryGetValue(e.Id, out int last) && estate.Week - last <= e.Cooldown) return false;
        if (!e.RequiredUpgrades.All(estate.Upgrades.Contains)) return false;
        return e.HeroLevelCounts.All(r => estate.Roster.Count(h => h.ResolveLevel >= r.Level) >= r.Count);
    }

    /// <summary>This visit's event, or null. Updates the estate's miss counter and cooldowns.</summary>
    public TownEvent Roll(Estate estate, Rng rng)
    {
        float chance = ChanceByMisses[System.Math.Min(estate.TownEventMisses, ChanceByMisses.Count - 1)];
        var candidates = Events.Where(e => Eligible(e, estate) && e.BaseChance > 0).ToList();
        if (candidates.Count == 0 || !rng.Chance(chance))
        {
            estate.TownEventMisses++;
            return null;
        }
        float total = candidates.Sum(e => e.BaseChance), pick = (float)rng.NextDouble() * total;
        var chosen = candidates.Last();
        foreach (var e in candidates)
        {
            pick -= e.BaseChance;
            if (pick <= 0) { chosen = e; break; }
        }
        estate.TownEventMisses = 0;
        estate.TownEventLastWeek[chosen.Id] = estate.Week;
        return chosen;
    }
}
