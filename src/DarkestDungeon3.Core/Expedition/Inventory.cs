using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>DD1 supply ids. DD1 calls food type "provision" with an empty id; we use "food".</summary>
public static class Supply
{
    public const string Food = "food";
    public const string Torch = "torch";
    public const string Shovel = "shovel";
    public const string Bandage = "bandage";
    public const string Antivenom = "antivenom";
    public const string Herbs = "medicinal_herbs";
    public const string Key = "skeleton_key";
    public const string HolyWater = "holy_water";
    public const string Laudanum = "laudanum";
    public const string Firewood = "firewood";

    public static readonly string[] Provisioner = { Food, Torch, Shovel, Bandage, Antivenom, Herbs, Key, HolyWater, Laudanum };
}

public sealed class ItemDef
{
    public string Type;     // provision, supply, gold, heirloom, gem, quest_item...
    public string Id;
    public int StackLimit;
    public int BuyPrice;
    public int SellPrice;
}

/// <summary>DD1 item definitions from <c>inventory/*.inventory.items.darkest</c>.</summary>
public sealed class ItemCatalog
{
    public Dictionary<string, ItemDef> Items { get; } = new();

    public static ItemCatalog Load(Dd1Install dd1)
    {
        var catalog = new ItemCatalog();
        foreach (var file in Directory.GetFiles(dd1.PathOf("inventory"), "base.*.inventory.items.darkest"))
            foreach (var r in DarkestFile.Load(file).Where(r => r.Type == "inventory_item"))
            {
                var def = new ItemDef
                {
                    Type = r.Str("type"),
                    Id = r.Str("id"),
                    StackLimit = r.Int("base_stack_limit", fallback: 1),
                    BuyPrice = r.Int("purchase_gold_value"),
                    SellPrice = r.Int("sell_gold_value"),
                };
                catalog.Items[KeyOf(def.Type, def.Id)] = def;
            }
        return catalog;
    }

    /// <summary>Our item key: the id, except DD1's id-less types (food is "provision", gold is "gold").</summary>
    public static string KeyOf(string type, string id) =>
        type == "provision" ? Supply.Food : string.IsNullOrEmpty(id) ? type : id;

    public ItemDef Get(string key) => Items.TryGetValue(key, out var d) ? d : null;

    public int StackLimit(string key) => Get(key)?.StackLimit ?? 1;
}

/// <summary>The party's pack: supplies and loot in DD1's stacked slots.</summary>
public sealed class Inventory
{
    public const int Slots = 16;

    public Dictionary<string, int> Items = new();

    public int Count(string id) => Items.TryGetValue(id, out var n) ? n : 0;

    public void Add(string id, int amount)
    {
        int n = Count(id) + amount;
        if (n <= 0) Items.Remove(id);
        else Items[id] = n;
    }

    public bool TryUse(string id, int amount = 1)
    {
        if (Count(id) < amount) return false;
        Add(id, -amount);
        return true;
    }

    public int SlotsUsed(ItemCatalog catalog) =>
        Items.Sum(kv => Stacks(kv.Value, catalog.StackLimit(kv.Key)));

    public bool HasRoomFor(string id, int amount, ItemCatalog catalog)
    {
        int stack = catalog.StackLimit(id);
        return SlotsUsed(catalog) - Stacks(Count(id), stack) + Stacks(Count(id) + amount, stack) <= Slots;
    }

    private static int Stacks(int count, int stackLimit) => (count + stackLimit - 1) / System.Math.Max(1, stackLimit);
}
