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
    public bool TrinketFits(string trinketId, string classId) => true;
    public IReadOnlyList<string> RecruitableClasses { get; } =
        new[] { "highwayman", "plague_doctor", "grave_robber", "leper", "man_at_arms", "hellion", "jester", "occultist", "runaway", "flagellant", "vestal" };

    public string RandomName(string classId, Rng rng) => classId + "_" + rng.Next(1000);
    public IReadOnlyList<string> StartingQuirks(string classId, Rng rng, int positives, int negatives) =>
        Enumerable.Range(0, positives).Select(i => "pos_" + rng.Next(5)).Concat(Enumerable.Range(0, negatives).Select(i => "neg_" + rng.Next(5))).Distinct().ToList();
    public string MapDd1Quirk(string dd1QuirkId) => dd1QuirkId == null ? null : "dd2_" + dd1QuirkId;
    public bool IsDisease(string q) => q.StartsWith("disease_");
    public bool IsPositive(string q) => q.StartsWith("pos_");
    public string RandomTrinket(string rarity, Rng rng, string forClass = null) => rarity + "_trinket_" + rng.Next(3);
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
    public void AtMostThreeLockedPositiveQuirks()
    {
        var h = NewHamlet();
        var hero = h.Estate.Roster[0];
        hero.Quirks.AddRange(new[] { "pos_a", "pos_b", "pos_c", "pos_d" });
        hero.LockedQuirks.AddRange(new[] { "pos_a", "pos_b", "pos_c" });
        Assert.Equal(3, h.Dd1.QuirkLimits.MaxLockedPositive);                 // DD1 quirks_max_locked_positive
        Assert.NotNull(h.WhyCantLock(hero, "pos_d"));
        Assert.False(h.StartTreatment(hero.Id, "pos_d"));                     // the Sanitarium refuses a fourth
        hero.LockedQuirks.Remove("pos_c");
        Assert.Null(h.WhyCantLock(hero, "pos_d"));
    }

    [Fact]
    public void TrinketsSellForFifteenPercentAndCanAllBeUnequipped()
    {
        var h = NewHamlet();
        h.Estate.Trinkets.Add("t1");
        int gold = h.Estate.Get(Currency.Gold);
        Assert.Equal(150, h.TrinketSellValue("t1"));          // DD1: price 1000 less the wagon's 85% sell discount
        Assert.True(h.SellTrinket("t1"));
        Assert.Equal(gold + 150, h.Estate.Get(Currency.Gold));
        Assert.False(h.SellTrinket("t1"));

        h.Estate.Roster[0].Trinkets.Add("a");
        h.Estate.Roster[1].Trinkets.Add("b");
        Assert.Equal(2, h.UnequipAllTrinkets());
        Assert.Contains("a", h.Estate.Trinkets);
        Assert.Empty(h.Estate.Roster[0].Trinkets);
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
    [Fact]
    public void Blacksmith_sells_dd1_weapon_ranks_behind_its_own_upgrades_and_resolve()
    {
        var h = NewHamlet(5);
        var hero = h.Estate.Roster[0];
        hero.ClassId = "highwayman";
        h.Estate.Add(Currency.Gold, 20000);
        var next = h.NextEquipment(hero, Hamlet.Weapon);
        Assert.Equal(750, next.Gold);                                    // DD1 highwayman.weapon level 0
        Assert.NotNull(h.WhyCantUpgradeEquipment(hero, Hamlet.Weapon));  // the Blacksmith isn't open / upgraded yet

        h.Estate.QuestsCompleted = 10;                                   // opens the Blacksmith
        h.Estate.Upgrades.Add("blacksmith.weapon:a");
        hero.ResolveLevel = 0;
        Assert.Equal("Needs resolve 1", h.WhyCantUpgradeEquipment(hero, Hamlet.Weapon));
        hero.ResolveLevel = 1;
        int gold = h.Estate.Get(Currency.Gold);
        Assert.True(h.UpgradeEquipment(hero.Id, Hamlet.Weapon));
        Assert.Equal(1, hero.WeaponRank);
        Assert.Equal(gold - 750, h.Estate.Get(Currency.Gold));
        Assert.Equal("Needs a Blacksmith upgrade", h.WhyCantUpgradeEquipment(hero, Hamlet.Weapon));

        // Classes DD1 never had use a stand-in's costs.
        hero.ClassId = "runaway";
        Assert.NotNull(h.NextEquipment(hero, Hamlet.Armour));
    }
    [Fact]
    public void Survivalist_teaches_unknown_class_skills_for_gold()
    {
        var camping = DarkestDungeon3.Core.Expedition.CampingSkills.Load(Install);
        var estate = Hamlet.NewEstate(9, Dd1, B, Catalog, camping);
        var h = new Hamlet(estate, Dd1, B, Catalog, camping);
        var hero = estate.Roster[0];
        hero.ClassId = "highwayman";
        hero.CampingSkills = camping.Starting("highwayman", new Rng(2));
        string unknown = camping.ForClass("highwayman").First(id => !hero.CampingSkills.Contains(id));
        estate.QuestsCompleted = 10;
        estate.Add(Currency.Gold, 5000);
        int cost = h.CampSkillCost(camping.Get(unknown));
        Assert.True(cost > 0);
        Assert.Null(h.WhyCantLearnCampSkill(hero, unknown));
        Assert.True(h.LearnCampSkill(hero.Id, unknown));
        Assert.Contains(unknown, hero.CampingSkills);
        Assert.Equal("Known", h.WhyCantLearnCampSkill(hero, unknown));

        // DD1's camping buffs read as words; damage low/high pairs merge.
        var damageSkill = camping.ForClass("highwayman").Select(camping.Get)
            .First(sk => sk.Effects.Any(e => e.Type == "buff" && e.SubType != null && e.SubType.Contains("DMGLow")));
        var finesse = camping.DescribeAll(damageSkill);
        Assert.Contains(finesse, l => l.Contains("damage") && l.Contains("(self)"));
        Assert.Equal(finesse.Count, finesse.Distinct().Count());
        Assert.Contains("-1.5 stress (ally)", camping.DescribeAll(camping.Get("encourage")));
    }
    [Fact]
    public void Guild_prices_dd2_skills_like_their_dd1_trees()
    {
        var h = NewHamlet(11);
        var hero = h.Estate.Roster[0];
        hero.ClassId = "highwayman";
        Assert.NotNull(Dd1.HeroUpgrades.SkillTree("highwayman", "hwm_wicked_slice"));
        Assert.NotNull(Dd1.HeroUpgrades.SkillTree("highwayman", "hwm_duelists_advance"));   // DD1: duelist_advance
        Assert.NotNull(Dd1.HeroUpgrades.SkillTree("highwayman", "hwm_grapeshot_blast"));    // DD1: grape_shot_blast
        Assert.Null(Dd1.HeroUpgrades.SkillTree("highwayman", "hwm_highway_robbery"));       // DD2 only
        Assert.Equal(1000, h.SkillLearnCost(hero, "hwm_wicked_slice"));
        Assert.Equal(1000, h.SkillMasterCost(hero, "hwm_wicked_slice"));                   // 250 + 750
        Assert.Equal(1000, h.SkillLearnCost(hero, "hwm_highway_robbery"));

        h.Estate.QuestsCompleted = 10;
        h.Estate.Add(Currency.Gold, 5000);
        Assert.True(h.LearnSkill(hero.Id, "hwm_highway_robbery"));
        Assert.Equal("Needs a Guild upgrade", h.WhyCantMasterSkill(hero, "hwm_wicked_slice", known: true));
        h.Estate.Upgrades.Add("guild.skill_levels:a");
        hero.ResolveLevel = 1;
        Assert.Equal("Learn it first", h.WhyCantMasterSkill(hero, "hwm_open_vein", known: false));
        Assert.True(h.MasterSkill(hero.Id, "hwm_wicked_slice", known: true));
        Assert.Contains("hwm_wicked_slice", hero.MasteredSkills);
    }
    [Fact]
    public void Town_events_happen_and_change_the_week()
    {
        Assert.True(Dd1.TownEvents.Events.Count > 30);
        var h = NewHamlet(21);
        h.Estate.Week = 20;
        int events = 0;
        for (int week = 0; week < 40; week++)
        {
            h.EndWeek();
            if (h.Estate.TownEventId != null) events++;
        }
        Assert.InRange(events, 10, 40);                 // DD1: at least every fourth visit

        // A free Abbey week: meditation costs nothing.
        h.Estate.TownEventId = "free_abbey";
        var meditation = B.Activities.First(a => a.Id == "meditation");
        Assert.Equal(0, h.ActivityCost(meditation).Amount);
        // The resolve limit lifted for the week.
        h.Estate.TownEventId = "remove_quest_hero_level_restriction";
        Assert.True(h.AnyResolveCanEmbark);
        var veteran = h.Estate.Roster[0];
        veteran.ResolveLevel = 6;
        Assert.True(Homecoming.WillEmbark(veteran, new QuestOffer { Difficulty = 1 }, h.AnyResolveCanEmbark));
    }
    [Fact]
    public void Homecoming_sells_gems_keeps_trinkets_and_reports_the_heroes()
    {
        var items = DarkestDungeon3.Core.Expedition.ItemCatalog.Load(Install);
        var h = NewHamlet(31);
        var hero = h.Estate.Roster[0];
        var exp = new DarkestDungeon3.Core.Expedition.ExpeditionState
        {
            Quest = new QuestOffer { Id = "q", Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 },
            Party = { hero.Id },
            QuestComplete = true,
        };
        exp.Pack.Add("gold", 500);
        exp.Pack.Add("citrine", 2);
        exp.Pack.Add("trinket:common", 1);
        int gold = h.Estate.Get(Currency.Gold), trinkets = h.Estate.Trinkets.Count;
        var report = Homecoming.Report(h.Estate, Dd1, exp, new[] { new HeroOutcome { HeroId = hero.Id, Stress = 3 } }, items, _ => "sun_ring");
        Assert.Equal("complete", report.Result);
        Assert.True(report.GemGold > 0);
        Assert.Equal(gold + 500 + report.GemGold, h.Estate.Get(Currency.Gold));
        Assert.Equal(trinkets + 1, h.Estate.Trinkets.Count);
        var r = Assert.Single(report.Heroes);
        Assert.Equal(2, r.XpGained);
        Assert.Equal(3, r.Stress);
    }

    [Fact]
    public void AbandoningAQuestCostsTheStressOfDefeat()
    {
        var h = NewHamlet(32);
        var hero = h.Estate.Roster[0];
        var exp = new DarkestDungeon3.Core.Expedition.ExpeditionState
        {
            Quest = new QuestOffer { Id = "q", Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 },
            Party = { hero.Id },
            Retreated = true,
        };
        Assert.Equal(20f, Dd1.AbandonStressDd1);                  // DD1 quest.exit_penalty.json fail_penalty
        var report = Homecoming.Report(h.Estate, Dd1, exp, new[] { new HeroOutcome { HeroId = hero.Id, Stress = 3 } });
        Assert.Equal(5, Assert.Single(report.Heroes).Stress);     // 3 + 20/10
        Assert.Equal(5, hero.Stress);
        var exp2 = new DarkestDungeon3.Core.Expedition.ExpeditionState { Quest = exp.Quest, Party = { hero.Id }, Retreated = true };
        Homecoming.Report(h.Estate, Dd1, exp2, new[] { new HeroOutcome { HeroId = hero.Id, Stress = 9 } });
        Assert.Equal(10, hero.Stress);                            // capped at DD2's 10
    }
}
