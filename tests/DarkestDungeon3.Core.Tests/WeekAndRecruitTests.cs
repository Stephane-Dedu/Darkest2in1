using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Week-end and recruit rules that need no DD1 files: the defaults stand in for the installed data.</summary>
public class WeekAndRecruitTests
{
    [Fact]
    public void ThePartyThatJustLeftGetsNoIdleRest()
    {
        var (hamlet, estate) = Town();

        hamlet.EndWeek(new[] { "away" });

        Assert.Equal(5f, estate.Hero("away").Stress);
        Assert.Equal(4.5f, estate.Hero("home").Stress);   // DD1 idle_hero_stress_heal 5 = half a unit
        Assert.Equal(1, estate.Week);
    }

    [Fact]
    public void AWeekWithNobodyAwayStillRestsEveryIdleHero()
    {
        var (hamlet, estate) = Town();

        hamlet.EndWeek();

        Assert.Equal(4.5f, estate.Hero("away").Stress);
        Assert.Equal(4.5f, estate.Hero("home").Stress);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void AnExperiencedRecruitArrivesWithTheResolveXpOfItsLevel(int level)
    {
        var dd1 = new Dd1Campaign();
        // A zone's level table is another scale entirely; a recruit must never be measured on it.
        dd1.ZoneLevelThresholds.AddRange(new[] { 0, 500, 600, 700, 800, 900, 1000 });
        var hamlet = new Hamlet(new Estate { Seed = 5 }, dd1, new Buildings(), new FakeCatalog());

        var hero = hamlet.MakeHero("vestal", new Rng(11), level);

        Assert.Equal(level, hero.ResolveLevel);
        Assert.Equal(dd1.HeroResolveThresholds[level], hero.ResolveXp);
        Assert.Equal(level, dd1.HeroResolveLevel(hero.ResolveXp));
    }

    [Fact]
    public void ARecruitAboveTheTableTakesItsLastThreshold()
    {
        var dd1 = new Dd1Campaign();
        var hamlet = new Hamlet(new Estate { Seed = 5 }, dd1, new Buildings(), new FakeCatalog());

        var hero = hamlet.MakeHero("vestal", new Rng(11), 9);

        Assert.Equal(dd1.HeroResolveThresholds[dd1.HeroResolveThresholds.Count - 1], hero.ResolveXp);
    }

    [Fact]
    public void AnItemWithNoStackLimitStillTakesASlotEach()
    {
        var catalog = new ItemCatalog();
        catalog.Items["odd"] = new ItemDef { Id = "odd", Type = "supply", StackLimit = 0 };
        var pack = new Inventory();

        pack.Add("odd", 1);
        Assert.Equal(1, pack.SlotsUsed(catalog));
        pack.Add("odd", 2);
        Assert.Equal(3, pack.SlotsUsed(catalog));
        Assert.True(pack.HasRoomFor("odd", Inventory.Slots - 3, catalog));
        Assert.False(pack.HasRoomFor("odd", Inventory.Slots - 2, catalog));
    }

    /// <summary>Two idle heroes at 50 DD1 stress in a Hamlet with no open region, so the week rolls no quest.</summary>
    private static (Hamlet Hamlet, Estate Estate) Town()
    {
        var estate = new Estate { Seed = 3, QuestsCompleted = 0 };
        estate.Roster.Add(new HeroRecord { Id = "away", Name = "Away", ClassId = "vestal", Stress = 5f });
        estate.Roster.Add(new HeroRecord { Id = "home", Name = "Home", ClassId = "vestal", Stress = 5f });
        return (new Hamlet(estate, new Dd1Campaign(), new Buildings(), new FakeCatalog()), estate);
    }
}
