using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>DD1's provisioner (<c>campaign/provision/provision.json</c>): stock, free items, food warnings.</summary>
public sealed class Provisioner
{
    private JArray _byLength = new(), _byClass = new(), _stock = new(), _confirm = new();
    private ItemCatalog _items;

    public static Provisioner Load(Dd1Install dd1, ItemCatalog items)
    {
        var path = dd1.PathOf("campaign", "provision", "provision.json");
        var d = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject();
        return new Provisioner
        {
            _items = items,
            _byLength = d["raid_starting_length_inventory_item_lists"] as JArray ?? new JArray(),
            _byClass = d["raid_starting_hero_class_item_lists"] as JArray ?? new JArray(),
            _stock = d["default_store_inventory_item_lists"] as JArray ?? new JArray(),
            _confirm = d["confirm_datas"] as JArray ?? new JArray(),
        };
    }

    private static IEnumerable<(string Key, int Amount)> Items(JToken list) =>
        (list as JArray ?? new JArray()).Select(i => (ItemCatalog.KeyOf((string)i["type"], (string)i["id"]), (int?)i["amount"] ?? 0))
                                        .Where(i => i.Item2 > 0);

    /// <summary>Free supplies for the quest length (firewood) and for each hero's class (a DD1 namesake's item).</summary>
    public Inventory FreeItems(int length, IEnumerable<string> heroClasses)
    {
        var inv = new Inventory();
        if (length >= 0 && length < _byLength.Count) foreach (var (k, n) in Items(_byLength[length])) inv.Add(k, n);
        foreach (var cls in heroClasses)
            foreach (var entry in _byClass.Where(e => (string)e["hero_class"] == cls))
                foreach (var (k, n) in Items(entry["item_lists"])) inv.Add(k, n);
        return inv;
    }

    /// <summary>What the provisioner has on the shelf for a quest of this length.</summary>
    public Dictionary<string, int> Stock(int length)
    {
        var list = length >= 0 && length < _stock.Count ? _stock[length] : _stock.LastOrDefault();
        return Items(list).GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
    }

    public int Price(string key) => _items.Get(key)?.BuyPrice ?? 0;

    /// <summary>DD1 warns before leaving with less food than this.</summary>
    public int MinimumFood(int length) =>
        length >= 0 && length < _confirm.Count ? (int?)_confirm[length]["minimum_food"] ?? 0 : 12;
}

/// <summary>Turning a quest, four heroes and a pack into an expedition.</summary>
public static class Embark
{
    /// <summary>
    /// DD1's warning before a harder quest (difficulty ≥ trinkets_equipped_warning_dungeon_min_difficulty, 3) when
    /// fewer than trinkets_equipped_warning_min_percent (50%) of the party's trinket slots (two each) are filled.
    /// </summary>
    public static bool TrinketWarning(Dd1Campaign dd1, QuestOffer quest, IReadOnlyList<HeroRecord> party)
    {
        if (quest == null || party == null || party.Count == 0) return false;
        int minDifficulty = (int?)dd1?.Rules?["trinkets_equipped_warning_dungeon_min_difficulty"] ?? 3;
        float minShare = (float?)dd1?.Rules?["trinkets_equipped_warning_min_percent"] ?? 0.5f;
        if (quest.Difficulty < minDifficulty) return false;
        int worn = party.Sum(h => System.Math.Min(2, h.Trinkets.Count));
        return worn < minShare * 2 * party.Count;
    }

    public static string WhyCantEmbark(Estate estate, QuestOffer quest, IReadOnlyList<HeroRecord> party, bool anyResolve = false)
    {
        if (quest == null) return "Choose a quest.";
        if (party.Count == 0) return "Choose your party.";
        if (party.Count > 4) return "At most four heroes.";
        foreach (var h in party)
        {
            if (!h.IsAvailable) return $"{h.Name} is not available.";
            if (anyResolve) continue;
            if (h.ResolveLevel > Homecoming.MaxResolveFor(quest.Difficulty)) return $"{h.Name} refuses: too experienced for this quest.";
            if (h.ResolveLevel < Homecoming.MinResolveFor(quest.Difficulty)) return $"{h.Name} is not ready for the Darkest Dungeon.";
        }
        return null;
    }

    public static ExpeditionState Create(Dd1Campaign dd1, QuestOffer quest, IReadOnlyList<HeroRecord> party, Inventory bought,
                                         Provisioner provisioner = null)
    {
        var goal = dd1.Goals?.For(quest);
        var p = dd1.MapGen.Find(quest.Dungeon, quest.Size, quest.Type);
        var map = MapGenerator.Generate(p, quest.MapSeed, dd1.Props(quest.Dungeon), goal);

        var state = new ExpeditionState
        {
            Quest = quest,
            Goal = goal,
            Map = map,
            Seed = quest.MapSeed,
            Party = party.Select(h => h.Id).ToList(),
            Pack = bought ?? new Inventory(),
        };
        if (provisioner != null)
            foreach (var kv in provisioner.FreeItems(quest.Length, party.Select(h => h.ClassId)).Items)
                state.Pack.Add(kv.Key, kv.Value);
        if (goal != null)
            foreach (var (id, n) in goal.StartingItems) state.Pack.Add(ItemCatalog.QuestKey(id), n);

        foreach (var h in party)
        {
            state.HeroClasses[h.Id] = h.ClassId;
            state.CampSkills[h.Id] = new List<string>(h.CampingSkills);
            if (h.PendingBuffs.Count > 0)
            {
                state.PendingBuffs[h.Id] = new List<string>(h.PendingBuffs);
                h.PendingBuffs.Clear();   // town buffs (hangovers...) last one expedition
            }
        }
        return state;
    }
}
