using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class InventoryLayoutTests
{
    private static readonly ItemCatalog Items = ItemCatalog.Load(Dd1Install.Find());

    [Fact]
    public void Stacks_keep_the_slots_the_player_gave_them()
    {
        var pack = new Inventory();
        pack.Add(Supply.Food, 14);      // DD1 food stacks by 12
        pack.Add(Supply.Torch, 4);
        var stacks = pack.Arrange(Items);
        Assert.Equal(3, stacks.Count);
        Assert.Equal(new[] { 12, 2 }, stacks.Where(s => s.Key == Supply.Food).Select(s => s.Count));

        int torch = stacks.Single(s => s.Key == Supply.Torch).Slot;
        pack.Move(torch, 10);
        Assert.Equal(Supply.Torch, pack.Arrange(Items).Single(s => s.Key == Supply.Torch).Key);
        Assert.Equal(10, pack.Arrange(Items).Single(s => s.Key == Supply.Torch).Slot);

        // Eating three food: the second stack empties and its slot frees up; the torch stays put.
        pack.TryUse(Supply.Food, 3);
        stacks = pack.Arrange(Items);
        Assert.Single(stacks, s => s.Key == Supply.Food);
        Assert.Equal(11, stacks.Single(s => s.Key == Supply.Food).Count);
        Assert.Equal(10, stacks.Single(s => s.Key == Supply.Torch).Slot);

        // A new item takes the first empty slot.
        pack.Add(Supply.Shovel, 1);
        Assert.Equal(1, pack.Arrange(Items).Single(s => s.Key == Supply.Shovel).Slot);
    }

    [Fact]
    public void Shift_click_buys_up_to_a_full_stack()
    {
        int stack = Items.StackLimit(Supply.Food);   // 12 in DD1
        var pack = new Inventory();
        Assert.Equal(stack, pack.ToFullStack(Supply.Food, 50, Items));         // an empty pack: one whole stack
        pack.Add(Supply.Food, 5);
        Assert.Equal(stack - 5, pack.ToFullStack(Supply.Food, 50, Items));     // tops up the partial stack
        pack.Add(Supply.Food, stack - 5);
        Assert.Equal(stack, pack.ToFullStack(Supply.Food, 50, Items));         // last stack full: a new one
        Assert.Equal(3, pack.ToFullStack(Supply.Food, 3, Items));              // never more than the shelf holds
        Assert.Equal(0, pack.ToFullStack(Supply.Food, 0, Items));
    }

    [Fact]
    public void Shift_click_never_buys_a_stack_the_pack_cannot_hold()
    {
        int stack = Items.StackLimit(Supply.Food);
        var pack = new Inventory();
        pack.Add(Supply.Food, 5);
        pack.Add(Supply.Torch, (Inventory.Slots - 1) * Items.StackLimit(Supply.Torch));   // every other slot full
        Assert.Equal(stack - 5, pack.ToFullStack(Supply.Food, 50, Items));     // topping up needs no new slot
        pack.Add(Supply.Food, stack - 5);
        Assert.Equal(0, pack.ToFullStack(Supply.Food, 50, Items));             // a new stack would not fit
    }

    [Fact]
    public void Shift_right_click_returns_the_last_stack()
    {
        int stack = Items.StackLimit(Supply.Food);
        var pack = new Inventory();
        Assert.Equal(0, pack.LastStack(Supply.Food, Items));
        pack.Add(Supply.Food, stack + 4);
        Assert.Equal(4, pack.LastStack(Supply.Food, Items));                   // the partial stack first
        pack.Add(Supply.Food, -4);
        Assert.Equal(stack, pack.LastStack(Supply.Food, Items));               // then a whole one
    }
}
