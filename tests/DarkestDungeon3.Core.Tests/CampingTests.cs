using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class CampingTests
{
    private readonly ITestOutputHelper _out;
    public CampingTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly CrawlContent Content = CrawlContent.Load(Install);
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);

    [Fact]
    public void EveryDd2ClassHasCampSkills()
    {
        foreach (var cls in new[] { "flagellant", "grave_robber", "hellion", "highwayman", "jester", "leper", "man_at_arms", "occultist", "plague_doctor", "runaway", "vestal", "bounty_hunter" })
        {
            var skills = Content.Camping.ForClass(cls);
            _out.WriteLine($"{cls}: {string.Join(", ", skills)}");
            Assert.True(skills.Count >= 6, cls + " has too few camp skills");
            Assert.Equal(4, Content.Camping.Starting(cls, new Rng(1)).Count);
        }
    }

    private static (Crawl, FakeParty) Camped(int food = 8)
    {
        var quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 };
        var map = MapGenerator.Generate(Dd1.MapGen.Find("crypts", "short", "explore"), 3, Dd1.Props("crypts"));
        var state = new ExpeditionState { Quest = quest, Map = map, Seed = 3, Party = { "a", "b", "c", "d" } };
        foreach (var h in state.Party) state.CampSkills[h] = new() { "encourage", "first_aid", "pep_talk", "hobby" };
        state.Pack.Add(Supply.Firewood, 1);
        state.Pack.Add(Supply.Food, food);
        var party = new FakeParty("a", "b", "c", "d");
        var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);
        crawl.Begin();
        return (crawl, party);
    }

    [Fact]
    public void CampMealSkillsAndTorch()
    {
        var (crawl, party) = Camped();
        crawl.State.Light = 30;
        foreach (var h in party.Hp.Keys.ToList()) { party.Hp[h] = 0.5f; party.Stress[h] = 6; }

        Assert.True(crawl.CanCamp);
        crawl.MakeCamp();
        Assert.Equal(0, crawl.State.Pack.Count(Supply.Firewood));
        Assert.Equal(12, crawl.State.Camp.RespiteLeft);

        Assert.Equal(4, crawl.MealCost(Meal.Full));
        Assert.True(crawl.EatMeal(Meal.Full));
        Assert.Equal(4, crawl.State.Pack.Count(Supply.Food));
        Assert.All(party.Hp.Values, hp => Assert.Equal(0.6f, hp, 3));

        var encourage = Content.Camping.Get("encourage");
        Assert.True(encourage.NeedsTarget);
        Assert.False(crawl.UseCampSkill("a", "encourage"));          // needs a target
        Assert.True(crawl.UseCampSkill("a", "encourage", "b"));       // 15 DD1 stress = 1-2 DD2 points
        Assert.InRange(party.Stress["b"], 4, 5);
        Assert.Equal(12 - encourage.Cost, crawl.State.Camp.RespiteLeft);
        Assert.Equal("Already used.", crawl.WhyCantUseCampSkill("a", "encourage"));
        Assert.Equal("Not known.", crawl.WhyCantUseCampSkill("a", "bless"));

        crawl.BreakCamp();
        Assert.Null(crawl.State.Camp);
        Assert.Equal(100f, crawl.State.Light);
    }

    [Fact]
    public void CampAmbushHappensAboutAThirdOfTheTime()
    {
        int ambushes = 0;
        for (int i = 0; i < 300; i++)
        {
            var (crawl, _) = Camped();
            crawl.State.Seed = i;
            crawl.MakeCamp();
            crawl.EatMeal(Meal.Half);
            if (crawl.BreakCamp().Any(e => e.Type == CrawlEventType.Ambush)) ambushes++;
        }
        _out.WriteLine($"camp ambush rate {ambushes / 300.0:P0}");
        Assert.InRange(ambushes, 60, 140); // DD1 base 33%
    }
    [Fact]
    public void Camp_buffs_ride_into_the_next_four_fights_then_end()
    {
        var (crawl, _) = Camped();
        crawl.MakeCamp();
        crawl.EatMeal(Meal.Full);
        Assert.True(crawl.UseCampSkill("a", "pep_talk", "b"));            // -30% stress taken for 4 battles
        var buffs = crawl.FightBuffs();
        var pep = Assert.Single(buffs, b => b.Hero == "b");
        Assert.Equal(4, pep.Buff.Battles);
        crawl.BreakCamp();
        for (int fight = 1; fight <= 4; fight++)
        {
            Assert.Contains(crawl.FightBuffs(), b => b.Hero == "b");
            crawl.ResolveBattle();
        }
        Assert.DoesNotContain(crawl.FightBuffs(), b => b.Hero == "b");
    }
    [Fact]
    public void Holy_water_blesses_a_hero_for_three_battles()
    {
        var (crawl, _) = Camped();
        crawl.State.Pack.Add(Supply.HolyWater, 1);
        Assert.NotNull(crawl.UseSupply("c", Supply.HolyWater));
        Assert.Equal(0, crawl.State.Pack.Count(Supply.HolyWater));
        for (int fight = 1; fight <= 3; fight++)
        {
            Assert.Equal(4, crawl.FightBuffs().Count(b => b.Hero == "c"));
            crawl.ResolveBattle();
        }
        Assert.DoesNotContain(crawl.FightBuffs(), b => b.Hero == "c");
        Assert.Null(crawl.UseSupply("c", Supply.Bandage));
    }
}
