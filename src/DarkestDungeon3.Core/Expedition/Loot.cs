using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>One thing found: an item (gold, heirloom, gem, supply) or a trinket of some rarity.</summary>
public sealed class LootDrop
{
    public string Type;     // gold, heirloom, gem, supply, provision, trinket, journal_page
    public string Id;       // item id, or trinket rarity
    public int Amount;

    /// <summary>The inventory key this lands under (see <see cref="ItemCatalog.KeyOf"/>).</summary>
    public string Key => Type == "trinket" ? "trinket:" + Id : ItemCatalog.KeyOf(Type, Id);

    public override string ToString() => Type == "trinket" ? $"{Id} trinket" : $"{Amount} {Key}";

    /// <summary>DD1's trinket rarities (loot tables name a rarity, not a trinket).</summary>
    public static readonly HashSet<string> TrinketRarities = new() { "very_common", "common", "uncommon", "rare", "very_rare", "ancestral", "crimson_court", "trophy", "kickstarter" };

    public static bool IsTrinketRarity(string id) => id != null && TrinketRarities.Contains(id);

    /// <summary>
    /// DD1 rolls the trinket itself when loot drops (the spoils show it, and it rides in the pack as that trinket):
    /// a "trinket of rarity X" drop becomes a concrete trinket picked by <paramref name="pick"/> (rarity, rng → id).
    /// Anything else, or no picker, comes back unchanged.
    /// </summary>
    public static LootDrop ResolveTrinket(LootDrop drop, Func<string, Rng, string> pick, Rng rng)
    {
        if (drop?.Type != "trinket" || pick == null || !IsTrinketRarity(drop.Id)) return drop;
        string id = pick(drop.Id, rng);
        return id == null ? drop : new LootDrop { Type = "trinket", Id = id, Amount = 1 };
    }
}

/// <summary>DD1's loot tables (<c>loot/*.loot.json</c>): weighted entries that nest by table id.</summary>
public sealed class LootTables
{
    private sealed class Table
    {
        public string Id;
        public int Difficulty;     // 0 = any
        public string Dungeon;     // "" = any
        public List<(float Weight, JObject Entry)> Entries = new();
    }

    private readonly List<Table> _tables = new();

    public static LootTables Load(Dd1Install dd1)
    {
        var loot = new LootTables();
        // loot.json first, then the override files, which replace same-keyed tables.
        var files = Directory.GetFiles(dd1.PathOf("loot"), "*.json")
                             .OrderBy(f => Path.GetFileName(f) == "loot.json" ? 0 : 1);
        foreach (var file in files)
            foreach (var t in JToken.Parse(File.ReadAllText(file))["loot_tables"] ?? new JArray())
            {
                var table = new Table
                {
                    Id = (string)t["id"],
                    Difficulty = (int?)t["difficulty"] ?? 0,
                    Dungeon = (string)t["dungeon"] ?? "",
                    Entries = t["entries"].Select(e => ((float?)e["chances"] ?? 0f, (JObject)e)).ToList(),
                };
                loot._tables.RemoveAll(x => x.Id == table.Id && x.Difficulty == table.Difficulty && x.Dungeon == table.Dungeon);
                loot._tables.Add(table);
            }
        return loot;
    }

    public bool Has(string id) => _tables.Any(t => t.Id == id);

    /// <summary>Most specific table: exact zone and difficulty, then any zone, then any difficulty.</summary>
    private Table Find(string id, int difficulty, string dungeon) => FindIn(id, difficulty, Core.Dungeon.ZoneBase.Of(dungeon));

    private Table FindIn(string id, int difficulty, string dungeon) =>
        _tables.FirstOrDefault(t => t.Id == id && t.Difficulty == difficulty && t.Dungeon == dungeon)
        ?? _tables.FirstOrDefault(t => t.Id == id && t.Difficulty == difficulty && t.Dungeon == "")
        ?? _tables.Where(t => t.Id == id && (t.Dungeon == "" || t.Dungeon == dungeon))
                  .OrderBy(t => t.Difficulty == 0 ? 0 : System.Math.Abs(t.Difficulty - difficulty) + 1)
                  .FirstOrDefault();

    /// <summary>Draw <paramref name="draws"/> times from a table for a zone and difficulty.</summary>
    public List<LootDrop> Roll(string id, int draws, int difficulty, string dungeon, Rng rng)
    {
        var drops = new List<LootDrop>();
        for (int i = 0; i < draws; i++) RollOnce(id, difficulty, dungeon, rng, drops, depth: 0);
        return Merge(drops);
    }

    private void RollOnce(string id, int difficulty, string dungeon, Rng rng, List<LootDrop> drops, int depth)
    {
        var table = Find(id, difficulty, dungeon);
        if (table == null || depth > 8) return;
        float total = table.Entries.Sum(e => e.Weight);
        if (total <= 0) return;
        double roll = rng.NextDouble() * total;
        JObject pick = null;
        foreach (var (w, e) in table.Entries)
        {
            roll -= w;
            if (roll < 0) { pick = e; break; }
        }
        pick ??= table.Entries.Last().Entry;

        var data = pick["data"] as JObject ?? new JObject();
        switch ((string)pick["type"])
        {
            case "table":
                RollOnce((string)data["table"], difficulty, dungeon, rng, drops, depth + 1);
                break;
            case "item":
                drops.Add(new LootDrop { Type = (string)data["type"], Id = (string)data["id"] ?? "", Amount = (int?)data["amount"] ?? 1 });
                break;
            case "trinket":
                drops.Add(new LootDrop { Type = "trinket", Id = (string)data["rarity"], Amount = 1 });
                break;
        }
    }

    private static List<LootDrop> Merge(List<LootDrop> drops) =>
        drops.GroupBy(d => (d.Type, d.Id))
             .Select(g => new LootDrop { Type = g.Key.Type, Id = g.Key.Id, Amount = g.Sum(d => d.Amount) })
             .ToList();
}

/// <summary>Gem sale values in gold (DD1 gems are sold on return; inventory files give their value).</summary>
public static class LootValue
{
    public static int GoldValue(LootDrop drop, ItemCatalog items) =>
        drop.Type == "gold" ? drop.Amount
        : drop.Type == "gem" ? (items.Get(drop.Id)?.SellPrice ?? 0) * drop.Amount
        : 0;

    public static bool IsHeirloom(LootDrop d) => d.Type == "heirloom" && Currency.Heirlooms.Contains(d.Id);
}
