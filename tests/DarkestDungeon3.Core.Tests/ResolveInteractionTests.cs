using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class ResolveInteractionTests
{
    private sealed class Party : FakeParty, IResolveParty
    {
        public string? Refused;
        public List<(string Hero,string Reaction)> Checks=new();
        public Party() : base("a","b") { Hp["a"]=Hp["b"]=.5f;Stress["a"]=Stress["b"]=6; }
        public bool Allows(string heroId,string reaction) { Checks.Add((heroId,reaction));return heroId!="a" || reaction!=Refused; }
    }
    private static ExpeditionState State() => new()
    {
        Started=true,Party={"a","b"},Map=new DungeonMap {Rooms={new Room {Id=0}}},
        Camp=new CampState {Ate=true,RespiteLeft=12},CampSkills={ ["a"]=new() {"test"} }
    };
    private static CrawlContent Content()
    {
        var content=new CrawlContent {Camping=new CampingSkills()};
        content.Camping.Skills["test"]=new CampSkill {Id="test",Cost=2,UseLimit=1,Effects={
            new CampEffect {Selection="party",Type="stress_heal_amount",Amount=2},
            new CampEffect {Selection="party",Type="health_heal_max_health_percent",Amount=.1f}}};
        return content;
    }
    [Fact] public void RefusedCampSkillStillCostsRespiteAndItsUse()
    {
        var state=State();var party=new Party {Refused="block_camping_skill_performer"};
        var crawl=new Crawl(state,new CrawlRules(),party,Content());
        Assert.True(crawl.UseCampSkill("a","test"));
        Assert.Equal(10,state.Camp.RespiteLeft);Assert.Equal(1,state.Camp.Uses["a:test"]);
        Assert.All(party.Stress.Values,s=>Assert.Equal(6,s));Assert.All(party.Hp.Values,h=>Assert.Equal(.5f,h));
        Assert.False(crawl.UseCampSkill("a","test"));Assert.Single(party.Checks);
    }
    [Fact] public void CampTargetRefusalIsRolledOnceForAllEffectsAndSmallStressReliefSurvives()
    {
        var state=State();var party=new Party {Refused="block_camping_skill_target"};
        var crawl=new Crawl(state,new CrawlRules(),party,Content());
        Assert.True(crawl.UseCampSkill("a","test"));
        Assert.Equal(6,party.Stress["a"]);Assert.Equal(.5f,party.Hp["a"]);
        Assert.Equal(5.8f,party.Stress["b"],4);Assert.Equal(.6f,party.Hp["b"],4);
        Assert.Single(party.Checks.Where(c=>c==("a","block_camping_skill_target")));
    }
    [Fact] public void RefusedSupplyDoesNotSpendThePackAndInvalidUsesDoNotRoll()
    {
        var state=State();state.Camp=null;state.Pack.Add(Supply.Food,1);
        var party=new Party {Refused="block_item"};
        var crawl=new Crawl(state,new CrawlRules(),party);
        Assert.Null(crawl.UseSupply("a",Supply.Food));Assert.Equal(1,state.Pack.Count(Supply.Food));
        Assert.Equal(.5f,party.Hp["a"]);Assert.Single(party.Checks);
        Assert.Null(crawl.UseSupply("a",Supply.Bandage));Assert.Null(crawl.UseSupply("a",null));
        Assert.Single(party.Checks);
    }
    [Fact] public void RefusingFoodCannotPreventTheNoMealStarvationPenalty()
    {
        var state=State();state.Camp.Ate=false;var party=new Party {Refused="block_camping_meal"};
        var rules=new CrawlRules();rules.Meals[Meal.None]=(0,-.2f,15);
        var crawl=new Crawl(state,rules,party);
        Assert.True(crawl.EatMeal(Meal.None));Assert.Empty(party.Checks);
        Assert.Equal(.3f,party.Hp["a"],4);Assert.Equal(7.5f,party.Stress["a"]);
    }
}
