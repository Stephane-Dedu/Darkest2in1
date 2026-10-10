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

    /// <summary>Our item key: the id, except DD1's id-less types (food is "provision", gold is "gold") and quest items,
    /// which DD1 keeps apart from supplies of the same id (the altar quest's holy water isn't a supply).</summary>
    public static string KeyOf(string type, string id) =>
        type == "provision" ? Supply.Food : type == "quest_item" ? QuestKey(id)
        : type == "journal_page" ? Campaign.JournalPages.Prefix + id : string.IsNullOrEmpty(id) ? type : id;

    public static string QuestKey(string id) => "quest_item+" + id;

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

    /// <summary>Take a drop into the pack if it fits (DD1: everything, trinkets included, needs room; a trinket is
    /// one per slot). Returns false when it stays behind.</summary>
    public bool TryTake(LootDrop drop, ItemCatalog catalog)
    {
        if (drop == null || !HasRoomFor(drop.Key, drop.Amount, catalog)) return false;
        Add(drop.Key, drop.Amount);
        return true;
    }

    /// <summary>Fill matching stacks and empty slots, returning how much fit. The source drop stays unchanged;
    /// its owner keeps the remainder on the loot scroll, like DD1's inventory stack merge.</summary>
    public int TakePartial(LootDrop drop, ItemCatalog catalog)
    {
        if (drop == null || drop.Amount <= 0) return 0;
        int stack = System.Math.Max(1, catalog.StackLimit(drop.Key));
        int remainder = Count(drop.Key) % stack;
        long capacity = (long)System.Math.Max(0, Slots - SlotsUsed(catalog)) * stack
                        + (remainder > 0 ? stack - remainder : 0);
        int amount = (int)System.Math.Min(drop.Amount, capacity);
        if (amount > 0) Add(drop.Key, amount);
        return amount;
    }

    public bool HasRoomFor(string id, int amount, ItemCatalog catalog)
    {
        int stack = catalog.StackLimit(id);
        return SlotsUsed(catalog) - Stacks(Count(id), stack) + Stacks(Count(id) + amount, stack) <= Slots;
    }

    private static int Stacks(int count, int stackLimit) => (count + stackLimit - 1) / System.Math.Max(1, stackLimit);

    // ---- DD1's arranged pack: each slot holds one stack, where the player put it ----

    /// <summary>The item key in each slot (null = empty). Counts stay in <see cref="Items"/>; this only remembers
    /// where the stacks sit.</summary>
    public List<string> Layout = new();

    public sealed class Stack
    {
        public int Slot;
        public string Key;
        public int Count;
    }

    /// <summary>
    /// The stacks slot by slot. New stacks take the first empty slots, emptied ones disappear, and each item's
    /// count fills its slots in order (full stacks first, the remainder in its last slot).
    /// </summary>
    public List<Stack> Arrange(ItemCatalog catalog, int slots = Slots)
    {
        while (Layout.Count < slots) Layout.Add(null);
        var need = Items.Where(kv => kv.Value > 0)
                        .ToDictionary(kv => kv.Key, kv => Stacks(kv.Value, System.Math.Max(1, catalog?.StackLimit(kv.Key) ?? 1)));
        for (int i = Layout.Count - 1; i >= 0; i--)
        {
            string k = Layout[i];
            if (k == null) continue;
            if (!need.TryGetValue(k, out int n) || Layout.Count(x => x == k) > n) Layout[i] = null;
        }
        foreach (var kv in need.OrderBy(kv => kv.Key, System.StringComparer.Ordinal))
            for (int have = Layout.Count(x => x == kv.Key); have < kv.Value; have++)
            {
                int empty = Layout.IndexOf(null);
                if (empty < 0) Layout.Add(kv.Key); else Layout[empty] = kv.Key;
            }
        var left = new Dictionary<string, int>(Items);
        var stacks = new List<Stack>();
        for (int i = 0; i < Layout.Count; i++)
        {
            string k = Layout[i];
            if (k == null) continue;
            int limit = System.Math.Max(1, catalog?.StackLimit(k) ?? 1);
            int c = System.Math.Min(limit, left[k]);
            left[k] -= c;
            stacks.Add(new Stack { Slot = i, Key = k, Count = c });
        }
        return stacks;
    }

    /// <summary>Move the stack in one slot to another (swapping with whatever is there).</summary>
    public void Move(int from, int to)
    {
        int n = System.Math.Max(from, to) + 1;
        while (Layout.Count < n) Layout.Add(null);
        (Layout[from], Layout[to]) = (Layout[to], Layout[from]);
    }

    /// <summary>The item in a slot, or null (after <see cref="Arrange"/>).</summary>
    public string At(int slot) => slot >= 0 && slot < Layout.Count ? Layout[slot] : null;
}
