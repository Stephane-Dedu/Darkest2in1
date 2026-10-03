using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1 battle loot from the user's DD1 install: mash encounters and monster loot codes.</summary>
public class BattleLootTests
{
    [Fact]
    public void LootTrinketsAreRolledWhenTheyDrop()
    {
        // DD1 rolls the trinket itself at the drop (the spoils show it); a rarity token is only a fallback.
        var rarity = new LootDrop { Type = "trinket", Id = "very_common", Amount = 1 };
        var picked = LootDrop.ResolveTrinket(rarity, (r, rng) => r == "very_common" ? "sun_ring" : null, new Rng(1));
        Assert.Equal("sun_ring", picked.Id);
        Assert.Equal("trinket:sun_ring", picked.Key);
        Assert.Same(rarity, LootDrop.ResolveTrinket(rarity, null, new Rng(1)));            // no picker: unchanged
        var gold = new LootDrop { Type = "gold", Id = "gold", Amount = 250 };
        Assert.Same(gold, LootDrop.ResolveTrinket(gold, (r, rng) => "x", new Rng(1)));       // not a trinket
        var concrete = new LootDrop { Type = "trinket", Id = "sun_ring", Amount = 1 };
        Assert.Same(concrete, LootDrop.ResolveTrinket(concrete, (r, rng) => "x", new Rng(1))); // already a trinket
    }

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly BattleLoot Battles = BattleLoot.Load(Install);
    private static readonly LootTables Tables = LootTables.Load(Install);

    [Fact]
    public void Ruins_encounters_are_dd1_monsters_with_loot()
    {
        Assert.True(Battles.MonsterCount > 100);
        Assert.Contains(("C", 1), Battles.LootOf("brigand_cutthroat_A"));
        var rng = new Rng(7);
        var hall = Battles.RollEncounter("crypts", 1, "hall", rng);
        Assert.InRange(hall.Count, 2, 4);
        Assert.All(hall, m => Assert.EndsWith("_A", m));                 // apprentice tier
        Assert.Contains(Battles.RollEncounter("crypts", 1, "boss", rng), m => m.StartsWith("necromancer") || m.StartsWith("pew") || m.StartsWith("prophet"));
        Assert.All(Battles.RollEncounter("crypts", 5, "room", rng), m => Assert.EndsWith("_C", m)); // champion tier
    }

    [Fact]
    public void Won_fights_usually_leave_something_and_are_deterministic()
    {
        int withLoot = 0;
        for (int seed = 1; seed <= 50; seed++)
        {
            var a = Battles.Roll(Tables, "weald", 3, "room", new Rng(seed), out var monsters);
            var b = Battles.Roll(Tables, "weald", 3, "room", new Rng(seed), out _);
            Assert.Equal(a.Select(d => d.ToString()), b.Select(d => d.ToString()));
            Assert.NotEmpty(monsters);
            if (a.Count > 0) withLoot++;
        }
        Assert.True(withLoot > 25, $"only {withLoot}/50 fights left loot");
        // DD2 zones have no DD1 tables: a common draw instead.
        Assert.NotNull(Battles.Roll(Tables, "dd2_sluice", 1, "hall", new Rng(3), out var none));
        Assert.Empty(none);
    }
}
