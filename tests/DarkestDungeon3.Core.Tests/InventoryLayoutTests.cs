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
}
