using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public sealed class CombatSupplyTests
{
    [Fact]
    public void Selection_and_cancel_do_not_spend_the_expedition_supply()
    {
        var pack = new Inventory();
        pack.Add(Supply.Bandage, 2);
        var combat = new CombatSupplyLedger(pack);

        Assert.Equal("bandages", combat.Select(42, Supply.Bandage, 1, 3)?.NativeId);
        combat.Cancel(42);

        Assert.Equal(2, pack.Count(Supply.Bandage));
        Assert.False(combat.ConfirmUse(42, "bandages", 1, 0));
    }

    [Fact]
    public void Only_a_confirmed_native_charge_spends_one_supply_once()
    {
        var pack = new Inventory();
        pack.Add(Supply.Antivenom, 3);
        var combat = new CombatSupplyLedger(pack);
        combat.Select(42, Supply.Antivenom, 2, 7);

        Assert.False(combat.ConfirmUse(42, "antivenom", 1, 1)); // rejected target/cancel
        Assert.False(combat.ConfirmUse(43, "antivenom", 1, 0)); // another actor
        Assert.False(combat.ConfirmUse(42, "bandages", 1, 0));
        Assert.Equal(3, pack.Count(Supply.Antivenom));
        Assert.True(combat.ConfirmUse(42, "antivenom", 1, 0));
        Assert.False(combat.ConfirmUse(42, "antivenom", 1, 0));
        Assert.Equal(2, pack.Count(Supply.Antivenom));
    }

    [Fact]
    public void Switching_native_items_cannot_bypass_one_supply_per_hero_turn()
    {
        var pack = new Inventory();
        pack.Add(Supply.Bandage, 2);
        pack.Add(Supply.Herbs, 2);
        var combat = new CombatSupplyLedger(pack);
        combat.Select(42, Supply.Bandage, 1, 3);
        Assert.True(combat.ConfirmUse(42, "bandages", 1, 0));

        Assert.Null(combat.Select(42, Supply.Herbs, 1, 3));
        Assert.NotNull(combat.Select(43, Supply.Herbs, 1, 4));
        Assert.NotNull(combat.Select(42, Supply.Herbs, 2, 3));
        Assert.Equal(2, pack.Count(Supply.Herbs));
    }

    [Theory]
    [InlineData("quest_item+holy_water")]
    [InlineData("torch")]
    [InlineData("food")]
    public void Quest_goods_and_exploration_only_items_cannot_be_native_combat_proxies(string supply)
    {
        var pack = new Inventory();
        pack.Add(supply, 1);
        Assert.Null(new CombatSupplyLedger(pack).Select(42, supply, 1, 3));
        Assert.Equal(1, pack.Count(supply));
    }

    [Fact]
    public void Empty_or_removed_pack_items_cannot_be_equipped_or_spent()
    {
        var pack = new Inventory();
        var combat = new CombatSupplyLedger(pack);
        Assert.Null(combat.Select(42, Supply.Laudanum, 1, 3));
        pack.Add(Supply.Laudanum, 1);
        Assert.NotNull(combat.Select(42, Supply.Laudanum, 1, 3));
        pack.TryUse(Supply.Laudanum);
        Assert.False(combat.ConfirmUse(42, "laudanum", 1, 0));
        Assert.Equal(0, pack.Count(Supply.Laudanum));
    }

    [Fact]
    public void A_retained_native_slot_is_charged_to_the_actual_confirmation_turn()
    {
        var pack = new Inventory();
        pack.Add(Supply.Bandage, 2);
        pack.Add(Supply.Herbs, 2);
        var combat = new CombatSupplyLedger(pack);
        combat.Select(42, Supply.Bandage, 1, 3); // target selection cancelled; proxy remains equipped
        combat.Select(42, Supply.Bandage, 2, 8); // native confirmation on later turn rebinds the lease
        Assert.True(combat.ConfirmUse(42, "bandages", 1, 0));
        Assert.True(combat.UsedThisTurn(42, 2, 8));
        Assert.False(combat.UsedThisTurn(42, 1, 3));
        Assert.Null(combat.Select(42, Supply.Herbs, 2, 8));
        Assert.Equal(1, pack.Count(Supply.Bandage));
    }

    [Fact]
    public void Another_hero_spending_the_last_supply_invalidates_a_cancelled_native_slot()
    {
        var pack = new Inventory();
        pack.Add(Supply.Bandage, 1);
        var combat = new CombatSupplyLedger(pack);
        combat.Select(42, Supply.Bandage, 1, 3); // retained cancelled equipment
        combat.Select(43, Supply.Bandage, 1, 4);
        Assert.True(combat.CanUseSelection(42, "bandages", 1, 3));
        Assert.True(combat.ConfirmUse(43, "bandages", 1, 0));

        Assert.False(combat.CanUseSelection(42, "bandages", 2, 8));
        Assert.False(combat.ConfirmUse(42, "bandages", 1, 0));
        Assert.Equal(0, pack.Count(Supply.Bandage));
    }
}
