using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class StartingPackTests
{
    private static readonly Dd1Campaign Campaign = Dd1Campaign.Load(Dd1Install.Find());
    private static readonly ItemCatalog Items = ItemCatalog.Load(Dd1Install.Find());
    private static readonly Provisioner Store = Provisioner.Load(Dd1Install.Find(), Items);
    private static readonly HeroRecord[] Party = {
        new() { Id = "a", ClassId = "crusader" }, new() { Id = "b", ClassId = "highwayman" },
        new() { Id = "c", ClassId = "plague_doctor" }, new() { Id = "d", ClassId = "vestal" }
    };
    private static QuestOffer Quest => new() { Dungeon = "dd2_coast", Type = "inventory_activate", Length = 2, Difficulty = 1, MapSeed = 1101 };

    [Fact]
    public void Provision_purchases_reserve_starting_goods_and_required_wards()
    {
        var cart = new Inventory();
        var stock = Store.Stock(2);
        foreach (string key in Supply.Provisioner)
            cart.Add(key, Embark.PurchaseAmount(Campaign, Quest, Party, cart, Store, Items, key, stock[key]));
        var counts = new Dictionary<string, int>(cart.Items);
        var state = Embark.Create(Campaign, Quest, Party, cart, Store);
        Assert.InRange(state.Pack.SlotsUsed(Items), 1, 16);
        Assert.All(state.Pack.Arrange(Items), s => Assert.InRange(s.Slot, 0, 15));
        Assert.Equal(counts.OrderBy(x => x.Key), cart.Items.OrderBy(x => x.Key));
        foreach (var (id, amount) in state.Goal.StartingItems)
            Assert.Equal(amount, state.Pack.Count(ItemCatalog.QuestKey(id)));
        Assert.True(state.Pack.Count(Supply.Firewood) > 0);
    }

    [Fact]
    public void Full_paid_cart_cannot_create_an_inaccessible_activation_pack()
    {
        var cart = FullCart();
        Assert.Equal(16, cart.SlotsUsed(Items));
        Assert.Throws<System.InvalidOperationException>(() => Embark.Create(Campaign, Quest, Party, cart, Store));
        Assert.Equal(16, cart.SlotsUsed(Items));
    }

    [Fact]
    public void Existing_overflow_recovers_into_visible_holes_without_losing_items()
    {
        var pack = FullCart();
        pack.Arrange(Items);
        pack.Add(Supply.Firewood, 1);
        var ward = ItemCatalog.QuestKey("eldritch_lantern");
        pack.Add(ward, 3);
        pack.Arrange(Items);
        foreach (var key in new[] { Supply.Food, Supply.Torch, Supply.Shovel, Supply.Bandage })
            pack.Add(key, -pack.LastStack(key, Items));
        var counts = new Dictionary<string, int>(pack.Items);
        var stacks = pack.Arrange(Items);
        Assert.All(stacks, s => Assert.InRange(s.Slot, 0, 15));
        Assert.Equal(3, stacks.Where(s => s.Key == ward).Sum(s => s.Count));
        Assert.Equal(counts.OrderBy(x => x.Key), pack.Items.OrderBy(x => x.Key));
    }

    private static Inventory FullCart()
    {
        var cart = new Inventory();
        var counts = new[] { 13, 17, 5, 7, 7, 7, 1, 1, 1 };
        for (int i = 0; i < counts.Length; i++) cart.Add(Supply.Provisioner[i], counts[i]);
        return cart;
    }
}
