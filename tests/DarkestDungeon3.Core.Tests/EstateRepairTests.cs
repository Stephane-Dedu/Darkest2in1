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
    public void MissingQuirksAndWagonKeepTheExistingSingleSharedRepairSequence()
    {
        var estate = NewEstate();
        estate.Roster[0].Quirks.Clear();
        estate.WagonStock.Clear();
        var control = SaveFile.FromJson(new SaveFile { Estate = estate }.ToJson()).Estate;
        int counter = estate.RandomCounter;
        var rng = control.NextRng();
        var hero = control.Roster[0];
        var expectedQuirks = Catalog.StartingQuirks(hero.ClassId, rng, 1 + hero.ResolveLevel / 2, 1 + hero.ResolveLevel / 3);
        Town(control).RestockWagon(rng);
        var repairs = Town(estate).RepairEstate();
        Assert.Equal(expectedQuirks, estate.Roster[0].Quirks);
        Assert.Equal(control.WagonStock, estate.WagonStock);
        Assert.Equal(counter + 1, estate.RandomCounter);
        Assert.Contains(repairs, s => s.StartsWith("wagon restocked"));
        Assert.NotEmpty(estate.WagonStock);
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
}
