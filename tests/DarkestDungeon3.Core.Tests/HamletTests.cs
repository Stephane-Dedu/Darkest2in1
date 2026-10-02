using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class FakeCatalog : IHeroCatalog
{
    public IReadOnlyList<string> RecruitableClasses { get; } =
        new[] { "highwayman", "plague_doctor", "grave_robber", "leper", "man_at_arms", "hellion", "jester", "occultist", "runaway", "flagellant", "vestal" };

    public string RandomName(string classId, Rng rng) => classId + "_" + rng.Next(1000);
    public IReadOnlyList<string> StartingQuirks(string classId, Rng rng, int positives, int negatives) =>
        Enumerable.Range(0, positives).Select(i => "pos_" + rng.Next(5)).Concat(Enumerable.Range(0, negatives).Select(i => "neg_" + rng.Next(5))).Distinct().ToList();
    public string MapDd1Quirk(string dd1QuirkId) => dd1QuirkId == null ? null : "dd2_" + dd1QuirkId;
    public bool IsDisease(string q) => q.StartsWith("disease_");
    public bool IsPositive(string q) => q.StartsWith("pos_");
    public string RandomTrinket(string rarity, Rng rng) => rarity + "_trinket_" + rng.Next(3);
    public int TrinketPrice(string trinketId) => 1000;
}

public class HamletTests
{
    private readonly ITestOutputHelper _out;
    public HamletTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly Buildings B = Buildings.Load(Install);
    private static readonly FakeCatalog Catalog = new();

    private static Hamlet NewHamlet(int seed = 1)
    {
        var estate = Hamlet.NewEstate(seed, Dd1, B, Catalog);
        return new Hamlet(estate, Dd1, B, Catalog);
    }

    [Fact]
    public void LoadsDd1BuildingsAndTrees()
    {
        Assert.Equal(6, B.Activities.Count); // abbey x3 + tavern x3
        Assert.Contains(B.Activities, a => a.Key == "abbey.prayer");
        Assert.Contains(B.Activities, a => a.Key == "tavern.bar");
        Assert.True(B.Trees.Trees.ContainsKey("stage_coach.rostersize"));
        Assert.True(B.Trees.Trees.Count >= 20);
    }

    [Fact]
    public void NewEstateHasFourHeroesRecruitsAndQuests()
    {
        var h = NewHamlet();
        Assert.Equal(4, h.Estate.Roster.Count);
        Assert.Equal(2, h.Estate.Recruits.Count);           // DD1: 2 recruits before upgrades
        Assert.Equal(9, B.RosterSize(h.Estate));            // DD1: roster of 9 before upgrades
        Assert.Equal(2, h.Estate.WagonStock.Count);
        Assert.NotEmpty(h.Estate.Quests);
        Assert.All(h.Estate.Roster, hero => Assert.NotEmpty(hero.Quirks));
        // DD1's skipped tutorial pays out: 500 base + 3000 gold, 4 crests.
        Assert.Equal(3500, h.Estate.Get(Currency.Gold));
        Assert.Equal(4, h.Estate.Get(Currency.Crest));
    }

    [Fact]
    public void AbbeyAndTavernOpenAfterTwoQuests()
    {
        var h = NewHamlet();
        Assert.False(B.IsOpen(Buildings.Abbey, h.Estate));  // QuestsCompleted starts at 1
        h.Estate.QuestsCompleted = 2;
        Assert.True(B.IsOpen(Buildings.Abbey, h.Estate));
        Assert.True(B.IsOpen(Buildings.Tavern, h.Estate));
        Assert.False(B.IsOpen(Buildings.Sanitarium, h.Estate));
    }

    [Fact]
    public void PrayerCostsGoldUsesSlotsAndRelievesStress()
    {
        var h = NewHamlet();
        h.Estate.QuestsCompleted = 5;
        h.Estate.Add(Currency.Gold, 10000);
        var prayer = B.Activity("abbey.prayer");
        Assert.Equal(1250, prayer.Cost(h.Estate).Amount);
        Assert.Equal(1, prayer.Slots(h.Estate));
        Assert.Equal((55, 55), prayer.StressHeal(h.Estate));

        var a = h.Estate.Roster[0];
        var b = h.Estate.Roster[1];
        a.Stress = 8;
        int gold = h.Estate.Get(Currency.Gold);
        Assert.True(h.StartActivity(a.Id, "abbey.prayer"));
        Assert.Equal(gold - 1250, h.Estate.Get(Currency.Gold));
        Assert.False(h.StartActivity(b.Id, "abbey.prayer"));       // one slot
        Assert.Equal("No free slot.", h.WhyCantDo(b, prayer));
        Assert.False(a.IsAvailable);

        var log = h.EndWeek();
        foreach (var l in log) _out.WriteLine(l);
        Assert.InRange(a.Stress, 2, 3);                              // 55 DD1 = 5.5 DD2 points
        Assert.Equal(1, h.Estate.Week);
    }

    [Fact]
    public void UpgradesRaiseTiers()
    {
        var h = NewHamlet();
        h.Estate.QuestsCompleted = 5;
        h.Estate.Add(Currency.Bust, 100);
        h.Estate.Add(Currency.Portrait, 100);
        h.Estate.Add(Currency.Deed, 100);
        h.Estate.Add(Currency.Crest, 100);
        h.Estate.Add(Currency.Gold, 100000);

        Assert.Equal(9, B.RosterSize(h.Estate));
        Assert.False(h.BuyUpgrade("stage_coach.rostersize", "b"));  // needs "a" first
        Assert.True(h.BuyUpgrade("stage_coach.rostersize", "a"));
        Assert.Equal(12, B.RosterSize(h.Estate));

        Assert.True(h.BuyUpgrade("abbey.prayer", "a"));
        Assert.Equal((69, 69), B.Activity("abbey.prayer").StressHeal(h.Estate));
    }

    [Fact]
    public void SideEffectsHappenAtRoughlyDd1Rates()
    {
        int weeks = 0, effects = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var h = NewHamlet(seed);
            h.Estate.QuestsCompleted = 5;
            h.Estate.Add(Currency.Gold, 10000);
            var hero = h.Estate.Roster[0];
            h.StartActivity(hero.Id, "tavern.bar");
            var log = h.EndWeek();
            weeks++;
            if (log.Count > 1) effects++;
        }
        double rate = effects / (double)weeks;
        _out.WriteLine($"bar side-effect rate: {rate:P1}");
        Assert.InRange(rate, 0.2, 0.55);   // DD1 tavern side-effect chance is ~0.375
    }

    [Fact]
    public void SanitariumCuresANegativeQuirk()
    {
        var h = NewHamlet();
        h.Estate.QuestsCompleted = 5;
        h.Estate.Add(Currency.Gold, 50000);
        var hero = h.Estate.Roster[0];
        hero.Quirks.Add("neg_test");
        Assert.True(h.StartTreatment(hero.Id, "neg_test"));
        h.EndWeek();
        Assert.DoesNotContain("neg_test", hero.Quirks);
        Assert.True(hero.IsAvailable);
    }

    [Fact]
    public void RecruitAndDismiss()
    {
        var h = NewHamlet();
        var recruit = h.Estate.Recruits[0];
        Assert.True(h.Recruit(recruit.Id));
        Assert.Equal(5, h.Estate.Roster.Count);
        Assert.True(h.Dismiss(recruit.Id));
        Assert.Equal(4, h.Estate.Roster.Count);
    }
}
