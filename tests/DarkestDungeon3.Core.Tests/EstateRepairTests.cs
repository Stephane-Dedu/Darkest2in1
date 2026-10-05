using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class EstateRepairTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());
    private static readonly Buildings Buildings = Buildings.Load(Dd1.Install);
    private static readonly FakeCatalog Catalog = new();
    private static Estate NewEstate() => Hamlet.NewEstate(37, Dd1, Buildings, Catalog);
    private static Hamlet Town(Estate estate) => new(estate, Dd1, Buildings, Catalog);

    [Fact]
    public void CleanRepeatedLoadDoesNotChangeSavedStateOrTheNextGameplaySeed()
    {
        var estate = NewEstate();
        estate.QuirksRepaired = true;
        var before = new SaveFile { Estate = estate }.ToJson();
        var control = SaveFile.FromJson(before).Estate;
        for (int i = 0; i < 3; i++)
        {
            Assert.Empty(Town(estate).RepairEstate());
            Assert.Equal(before, new SaveFile { Estate = estate }.ToJson());
            estate = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        }
        Assert.Equal(control.NextSeed(), estate.NextSeed());
    }

    [Fact]
    public void AlreadyPresentQuirksFinishMigrationOnceWithoutConsumingARandomStream()
    {
        var estate = NewEstate();
        Assert.All(estate.Roster.Concat(estate.Recruits), h => Assert.NotEmpty(h.Quirks));
        int counter = estate.RandomCounter;
        var quirks = estate.Roster.Concat(estate.Recruits).Select(h => string.Join(",", h.Quirks)).ToArray();
        var repairs = Town(estate).RepairEstate();
        Assert.Contains("hero quirk migration completed", repairs); // Session persists flag-only repairs
        Assert.True(estate.QuirksRepaired);
        Assert.Equal(counter, estate.RandomCounter);
        Assert.Equal(quirks, estate.Roster.Concat(estate.Recruits).Select(h => string.Join(",", h.Quirks)));
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        Assert.Empty(Town(loaded).RepairEstate());
        Assert.Equal(counter, loaded.RandomCounter);
    }

    [Fact]
    public void MissingQuirksKeepTheExistingRepairSequenceWithoutRestockingSoldOutWagon()
    {
        var estate = NewEstate();
        estate.Roster[0].Quirks.Clear();
        estate.WagonStock.Clear();
        var control = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        int counter = estate.RandomCounter;
        var rng = control.NextRng();
        var hero = control.Roster[0];
        var expectedQuirks = Catalog.StartingQuirks(hero.ClassId, rng, 1 + hero.ResolveLevel / 2, 1 + hero.ResolveLevel / 3);
        var repairs = Town(estate).RepairEstate();
        Assert.Equal(expectedQuirks, estate.Roster[0].Quirks);
        Assert.Empty(estate.WagonStock);
        Assert.Equal(counter + 1, estate.RandomCounter);
        Assert.DoesNotContain(repairs, s => s.StartsWith("wagon restocked"));
        Assert.Empty(Town(estate).RepairEstate());
        Assert.Equal(counter + 1, estate.RandomCounter);
    }

    [Fact]
    public void NonrandomEquipmentRepairDoesNotConsumeAStream()
    {
        var estate = NewEstate();
        estate.QuirksRepaired = true;
        estate.Roster[0].ClassId = "highwayman";
        estate.Roster[0].SetTrinket(0, "jester_only");
        int counter = estate.RandomCounter;
        Assert.Contains(Town(estate).RepairEstate(), s => s.Contains("can't wear"));
        Assert.Empty(estate.Roster[0].WornTrinkets);
        Assert.Contains("jester_only", estate.Trinkets);
        Assert.Equal(counter, estate.RandomCounter);
        Assert.Empty(Town(estate).RepairEstate());
        Assert.Equal(counter, estate.RandomCounter);
    }

    [Fact]
    public void BuyingLastWagonItemStaysSoldOutAcrossReloadAndReplenishesNextWeek()
    {
        var estate = NewEstate();
        estate.QuirksRepaired = true;
        estate.Add(Currency.Gold, 20000);
        int counter = estate.RandomCounter;
        int gold = estate.Get(Currency.Gold);
        var stock = estate.WagonStock.ToList();
        Assert.NotEmpty(stock);
        int totalPrice = stock.Sum(Town(estate).WagonPrice);
        foreach (var trinket in stock) Assert.True(Town(estate).BuyTrinket(trinket));
        Assert.Empty(estate.WagonStock);
        Assert.Equal(stock.OrderBy(t => t), estate.Trinkets.OrderBy(t => t));
        Assert.Equal(gold - totalPrice, estate.Get(Currency.Gold));
        for (int i = 0; i < 3; i++)
        {
            estate = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
            Assert.Empty(Town(estate).RepairEstate());
            Assert.Empty(estate.WagonStock);
            Assert.Equal(counter, estate.RandomCounter);
            Assert.Equal(gold - totalPrice, estate.Get(Currency.Gold));
            Assert.False(Town(estate).BuyTrinket(stock[0]));
        }
        Town(estate).EndWeek();
        Assert.Equal(1, estate.Week);
        Assert.NotEmpty(estate.WagonStock);
        Assert.Equal(gold - totalPrice, estate.Get(Currency.Gold));
    }

    [Fact]
    public void LegacyAbsentStockRemainsEmptyRatherThanInventingLostItems()
    {
        var estate = SaveFile.FromJson("{\"Estate\":{\"RegionLayoutVersion\":1,\"QuirksRepaired\":true,\"RandomCounter\":7}}").Estate;
        Town(estate).RepairEstate();
        Assert.Empty(estate.WagonStock);
        Assert.Equal(7, estate.RandomCounter);
        Town(estate).EndWeek();
        Assert.NotEmpty(estate.WagonStock);
    }
}
