using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>The light band the party is in and what it changes (DD1's "darkness" table).</summary>
public sealed class DarknessBand
{
    public float Lower, Upper;
    public float StressChanceIncrease, StressDamageIncrease;
    public float ScoutingIncrease;
    public float MonstersSurprisedIncrease, HeroesSurprisedIncrease;
    public float LootGoldIncrease;
}

/// <summary>DD1's crawl rules from <c>shared/rules.json</c>, converted to DD2 scales where needed.</summary>
public sealed class CrawlRules
{
    /// <summary>DD1 stress runs to 100 before an affliction, DD2 to 10 before a meltdown.</summary>
    public const float Dd1StressPerDd2Point = 10f;

    public float LightLossNewTile = 6f, LightLossVisitedTile = 1f;
    public float TorchLight = 25f;
    public float StressChanceForward = 0.3f, StressDd1Forward = 2f;
    public float StressChanceBack = 0.55f, StressDd1Back = 5f;
    public float HungerHealFraction = 0.05f, StarveHpFraction = 0.2f, StarveStressDd1 = 15f;
    public float ScoutChanceBase = 0.25f;
    public float SurpriseCorridorParty = 0.1f, SurpriseCorridorMonsters = 0.1f;
    public float SurpriseRoomParty = 0.1f, SurpriseRoomMonsters = 0.1f;
    public float SurpriseMaxParty = 0.65f;
    public float TrapScoutDisarmBonus = 0.4f;
    public float[] TrapDifficultyPenalty = { 0, 0, 0, 0.2f, 0.2f, 0.4f, 0.5f };
    public float ReturnBattleChance = 0.05f, ReturnHungerChance = 0.075f;
    public float AmbushCampChance = 0.33f;
    public int CampPoints = 12;
    public List<DarknessBand> Darkness = new();

    public static CrawlRules Defaults() => new();

    public static CrawlRules FromDd1(JObject rules)
    {
        var r = new CrawlRules();
        if (rules == null) return r;

        foreach (var e in rules["tile_light_loss"] ?? new JArray())
        {
            string key = (string)e["key"];
            float v = (float)e["data"];
            if (key == "clear") r.LightLossVisitedTile = v;
            else r.LightLossNewTile = v;
        }

        var hs = rules["hallway_stress"];
        if (hs != null)
        {
            r.StressChanceForward = (float)hs["hallway_per_tile_stress_damage_chance_fwd"];
            r.StressDd1Forward = (float)hs["hallway_per_tile_stress_damage_fwd"];
            r.StressChanceBack = (float)hs["hallway_per_tile_stress_damage_chance_backing_up"];
            r.StressDd1Back = (float)hs["hallway_per_tile_stress_damage_backing_up"];
        }

        r.HungerHealFraction = Get(rules, "hallway_hunger_HPrestore", r.HungerHealFraction);
        r.StarveHpFraction = Get(rules, "hallway_hunger_starve_HPdmg", r.StarveHpFraction);
        var none = rules["meals_table"]?.FirstOrDefault(m => (string)m["type"] == "none");
        if (none != null) r.StarveStressDd1 = (float)none["stress"];

        r.ScoutChanceBase = Get(rules, "scouting_chance_base", r.ScoutChanceBase);
        r.SurpriseCorridorParty = Get(rules, "surprise_corridor_party_base_chance", r.SurpriseCorridorParty);
        r.SurpriseCorridorMonsters = Get(rules, "surprise_corridor_monsters_base_chance", r.SurpriseCorridorMonsters);
        r.SurpriseRoomParty = Get(rules, "surprise_room_party_base_chance", r.SurpriseRoomParty);
        r.SurpriseRoomMonsters = Get(rules, "surprise_room_monsters_base_chance", r.SurpriseRoomMonsters);
        r.SurpriseMaxParty = Get(rules, "surprise_max_party_surprised_chance", r.SurpriseMaxParty);
        r.TrapScoutDisarmBonus = Get(rules, "trap_scout_disarm_bonus", r.TrapScoutDisarmBonus);
        if (rules["difficulty_trap_base"] is JArray trap) r.TrapDifficultyPenalty = trap.Select(t => (float)t).ToArray();
        r.AmbushCampChance = Get(rules, "ambush_camping_base_chance", r.AmbushCampChance);
        r.CampPoints = (int)Get(rules, "camp_start_camping_points", r.CampPoints);

        foreach (var e in rules["corridor_return_content"] ?? new JArray())
        {
            float chance = (float)e["data"]["base_chance"];
            if ((string)e["key"] == "ac_battle") r.ReturnBattleChance = chance;
            if ((string)e["key"] == "ac_hunger") r.ReturnHungerChance = chance;
        }

        foreach (var band in rules["darkness"]?["range_table"] ?? new JArray())
        {
            var v = band["value"];
            r.Darkness.Add(new DarknessBand
            {
                Lower = (float)band["range"]["lower"],
                Upper = (float)band["range"]["upper"],
                StressChanceIncrease = (float?)v["stress_chance_increase"] ?? 0,
                StressDamageIncrease = (float?)v["stress_damage_increase"] ?? 0,
                ScoutingIncrease = (float?)v["player_scouting_increase"] ?? 0,
                MonstersSurprisedIncrease = (float?)v["monsters_surprised_increase"] ?? 0,
                HeroesSurprisedIncrease = (float?)v["heroes_surprised_increase"] ?? 0,
                LootGoldIncrease = (float?)v["loot_increase_gold"] ?? 0,
            });
        }
        return r;
    }

    /// <summary>DD1 ranges exclude their lower bound ("lowest_excluded"), so 75 light is Dim, not Radiant.</summary>
    public DarknessBand Band(float light) =>
        Darkness.FirstOrDefault(b => light > b.Lower && light <= b.Upper)
        ?? Darkness.LastOrDefault()
        ?? new DarknessBand();

    public static string BandName(float light) =>
        light > 75 ? "Radiant" : light > 50 ? "Dim" : light > 25 ? "Shadowy" : light > 0 ? "Dark" : "Pitch Black";

    private static float Get(JObject o, string key, float fallback) => o[key] != null ? (float)o[key] : fallback;
}
