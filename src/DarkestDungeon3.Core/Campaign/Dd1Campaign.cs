using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>DD1's campaign rules, read from the user's DD1 install.</summary>
public sealed class Dd1Campaign
{
    public Dd1Install Install { get; private set; }
    public MapGenTable MapGen { get; private set; }

    /// <summary>Zone id → number of finished quests needed before it appears on the board.</summary>
    public Dictionary<string, int> ZoneUnlocks { get; } = new();
    /// <summary>Per zone, per progression index (0..7): weighted (type, length) options.</summary>
    public Dictionary<string, List<List<(string Type, int Length, float Chance)>>> QuestTables { get; } = new();
    /// <summary>Quests on the board per town visit, by progression index.</summary>
    public List<int> QuestsPerVisit { get; } = new();
    /// <summary>Zone XP gained per finished quest, by difficulty.</summary>
    public List<int> ZoneXpPerQuest { get; } = new();
    /// <summary>Zone XP needed for each zone level.</summary>
    public List<int> ZoneLevelThresholds { get; } = new();
    /// <summary>Zone → heirloom types its quests pay out.</summary>
    public Dictionary<string, List<string>> HeirloomTypes { get; } = new();
    /// <summary>heirloom type → [difficulty][length] amount.</summary>
    public Dictionary<string, JArray> HeirloomAmounts { get; } = new();
    /// <summary>[difficulty][length] → gold.</summary>
    public JArray GoldTable { get; private set; }
    public JObject Rules { get; private set; }
    public QuestGoals Goals { get; private set; }
    public Town.HeroUpgrades HeroUpgrades { get; private set; }
    public Town.TownEvents TownEvents { get; private set; }
    public Dd1Buffs Buffs { get; private set; }
    /// <summary>DD1's heirloom exchange (campaign/heirloom_exchange): give so many of one kind for so many of another.</summary>
    public List<(string From, int FromAmount, string To, int ToAmount)> HeirloomRates { get; } = new();
    /// <summary>Hero resolve levels (campaign/roster/roster.variables.json), not the dungeons' table.</summary>
    public List<int> HeroResolveThresholds { get; } = new() { 0, 2, 8, 14, 24, 36, 48 };
    /// <summary>DD1 stress an idle hero sheds each town visit.</summary>
    public float IdleStressHeal { get; private set; } = 5f;

    private readonly Dictionary<string, ZoneProps> _props = new();

    public static Dd1Campaign Load(Dd1Install dd1)
    {
        var c = new Dd1Campaign { Install = dd1 };
        c.MapGen = MapGenTable.Load(dd1.MapGenerator);

        var gen = (JObject)ReadJson(dd1.PathOf("campaign", "quest", "quest.generation.json"))["generation"];
        foreach (var z in gen["dungeon"]["generated_dungeons"])
            c.ZoneUnlocks[(string)z["id"]] = (int)z["required_number_of_quests_finished"];

        foreach (var entry in gen["type"]["available_quests_table"])
        {
            var tables = new List<List<(string, int, float)>>();
            foreach (JArray row in entry["generated_quest_table"])
                tables.Add(row.Select(o => ((string)o["type"], (int)o["length"], (float)o["chance"])).ToList());
            c.QuestTables[(string)entry["dungeon"]] = tables;
        }

        var rewards = gen["rewards"];
        foreach (var m in rewards["heirloom_type_map"])
            c.HeirloomTypes[(string)m["dungeon"]] = m["types"].Select(t => (string)t).ToList();
        foreach (var a in rewards["heirloom_amount_table"])
            c.HeirloomAmounts[(string)a["type"]] = (JArray)a["amounts"];
        c.GoldTable = (JArray)rewards["item_table"];

        var number = ReadJson(dd1.PathOf("campaign", "quest", "number.quest.generation.json"));
        c.QuestsPerVisit.AddRange(number["generation"]["number"]["number_of_quests_per_town_visit_table"].Select(t => (int)t));

        var progression = ReadJson(dd1.PathOf("campaign", "progression", "progression.json"));
        c.ZoneXpPerQuest.AddRange(progression["dungeon"]["quest_completion_xp_table"].Select(t => (int)t));
        c.ZoneLevelThresholds.AddRange(progression["dungeon"]["level_threshold_table"].Select(t => (int)t));

        c.Rules = (JObject)ReadJson(dd1.Rules);
        c.Goals = QuestGoals.Load(dd1);
        c.HeroUpgrades = Town.HeroUpgrades.Load(dd1);
        c.TownEvents = Town.TownEvents.Load(dd1);
        c.Buffs = Dd1Buffs.Load(dd1);
        var roster = dd1.PathOf("campaign", "roster", "roster.variables.json");
        if (File.Exists(roster))
        {
            var vars = ReadJson(roster);
            if (vars["resolve_level_thresholds"] is JArray t && t.Count > 0)
            {
                c.HeroResolveThresholds.Clear();
                c.HeroResolveThresholds.AddRange(t.Select(x => (int)x));
            }
            c.IdleStressHeal = (float?)vars["town_visit_town_progression"]?["idle_hero_stress_heal"] ?? 5f;
        }
        var exchange = dd1.PathOf("campaign", "heirloom_exchange", "heirloom_exchange.json");
        if (File.Exists(exchange))
            foreach (var r in ReadJson(exchange)["exchange_rates"] ?? new JArray())
                c.HeirloomRates.Add(((string)r["exchange_from_type"], (int)r["exchange_from_amount"], (string)r["exchange_to_type"], (int)r["exchange_to_amount"]));
        return c;
    }

    public ZoneProps Props(string zone)
    {
        if (_props.TryGetValue(zone, out var p)) return p;
        var path = Install.ZoneProps(zone);
        return _props[zone] = File.Exists(path) ? ZoneProps.Load(path) : ZoneProps.Empty;
    }

    /// <summary>DD1 hero resolve level from resolve XP (0..6).</summary>
    public int HeroResolveLevel(int xp)
    {
        int level = 0;
        for (int i = 0; i < HeroResolveThresholds.Count; i++)
            if (xp >= HeroResolveThresholds[i]) level = i;
        return level;
    }

    /// <summary>DD1 zone level from XP (0..thresholds-1).</summary>
    public int ZoneLevel(int xp)
    {
        int level = 0;
        for (int i = 0; i < ZoneLevelThresholds.Count; i++)
            if (xp >= ZoneLevelThresholds[i]) level = i;
        return level;
    }

    /// <summary>Highest quest difficulty a zone offers: levels 0-2 apprentice, 3-4 veteran, 5+ champion.</summary>
    public static int MaxDifficultyForZoneLevel(int level) => level >= 5 ? 5 : level >= 3 ? 3 : 1;

    /// <summary>Gold for a finished quest. DD1's table cells are item lists: [difficulty][length] → [{type, amount}].</summary>
    public int Gold(int difficulty, int length) =>
        Cell(GoldTable, difficulty, length) is JArray items
            ? items.Where(i => (string)i["type"] == Currency.Gold).Sum(i => (int)i["amount"])
            : 0;

    /// <summary>Heirlooms for a finished quest. Cells are plain numbers: [difficulty][length].</summary>
    public int HeirloomAmount(string type, int difficulty, int length) =>
        HeirloomAmounts.TryGetValue(type, out var t) && Cell(t, difficulty, length) is JValue v ? (int)v : 0;

    private static JToken Cell(JArray table, int row, int col) =>
        table != null && row >= 0 && row < table.Count && table[row] is JArray r && col >= 0 && col < r.Count ? r[col] : null;

    private static JToken ReadJson(string path) => JToken.Parse(File.ReadAllText(path));
}
