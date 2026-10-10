using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class WagonStockTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());
    private static readonly Buildings Buildings = Buildings.Load(Dd1.Install);

    private sealed class RepeatingCatalog : FakeCatalog
    {
        public int Calls;
        public string Pick = "repeat";
        public override string RandomTrinket(string rarity, Rng rng, string forClass = null)
        {
            Calls++;
            return Pick;
        }
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData("a", 4)]
    [InlineData("b", 6)]
    [InlineData("c", 8)]
    [InlineData("d", 12)]
    public void EveryInstalledWagonSlotRetainsItsValidCopyWithoutAdditionalRolls(string code, int slots)
    {
        var estate = new Estate { RandomCounter = 8, WagonStock = { "previous week" } };
        if (code != null)
            foreach (var level in Buildings.Trees.Trees["nomad_wagon.numitems"].TakeWhile(l => string.CompareOrdinal(l.Code, code) <= 0)) estate.Upgrades.Add(level.Key);
        var catalog = new RepeatingCatalog();
        var rng = new Rng(11);
        var control = new Rng(11);
        for (int i = 0; i < slots; i++) Hamlet.WagonRarity(Buildings.WagonRarities(), control);
        new Hamlet(estate, Dd1, Buildings, catalog).RestockWagon(rng);
        Assert.Equal(slots, Buildings.WagonStock(estate));
        Assert.Equal(slots, estate.WagonStock.Count);
        Assert.Equal(slots, catalog.Calls);
        Assert.All(estate.WagonStock, t => Assert.Equal("repeat", t));
        Assert.Equal(control.NextULong(), rng.NextULong());
        Assert.Equal(8, estate.RandomCounter);
    }

    [Fact]
    public void DuplicateStockSellsAndReloadsOneCopyAtATime()
    {
        var estate = new Estate { WagonStock = { "repeat", "repeat" } };
        estate.Add(Currency.Gold, 5000);
        var catalog = new RepeatingCatalog();
        var hamlet = new Hamlet(estate, Dd1, Buildings, catalog);
        int price = hamlet.WagonPrice("repeat");
        Assert.True(hamlet.BuyTrinket("repeat"));
        Assert.Equal("repeat", Assert.Single(estate.WagonStock));
        Assert.Equal("repeat", Assert.Single(estate.Trinkets));
        Assert.Equal(5000 - price, estate.Get(Currency.Gold));
        estate = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        hamlet = new Hamlet(estate, Dd1, Buildings, catalog);
        Assert.True(hamlet.BuyTrinket("repeat"));
        Assert.Empty(estate.WagonStock);
        Assert.Equal(new[] { "repeat", "repeat" }, estate.Trinkets);
        Assert.Equal(5000 - 2 * price, estate.Get(Currency.Gold));
        Assert.False(hamlet.BuyTrinket("repeat"));
        Assert.Equal(5000 - 2 * price, estate.Get(Currency.Gold));
    }

    [Fact]
    public void UnavailableCatalogueChoicesRemainAbsentWithBoundedSlotAttempts()
    {
        var estate = new Estate();
        var catalog = new RepeatingCatalog { Pick = null };
        new Hamlet(estate, Dd1, Buildings, catalog).RestockWagon(new Rng(11));
        Assert.Empty(estate.WagonStock);
        Assert.Equal(Buildings.WagonStock(estate), catalog.Calls);
    }
}
