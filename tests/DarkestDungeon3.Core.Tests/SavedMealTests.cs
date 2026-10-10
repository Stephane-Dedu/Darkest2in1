using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SavedMealTests
{
    [Theory]
    [InlineData(Meal.None, 8, .3f)]
    [InlineData(Meal.Half, 6, .5f)]
    [InlineData(Meal.Full, 4, .6f)]
    [InlineData(Meal.Feast, 0, .75f)]
    public void NativeMealChoiceAndPartyConditionSurviveSaveWithoutEatingOrRollingAgain(Meal meal, int foodLeft, float health)
    {
        var dd1 = Dd1Campaign.Load(Dd1Install.Find());
        var rules = CrawlRules.FromDd1(dd1.Rules);
        var state = new ExpeditionState
        {
            Started = true, Seed = 74, RandomCounter = 9, Party = { "a", "b", "c", "d" },
            Map = new DungeonMap { Rooms = { new Room { Id = 0 } } },
            Camp = new CampState { RespiteLeft = 12 }, Light = 31
        };
        state.Pack.Add(Supply.Food, 8);
        var party = new FakeParty("a", "b", "c", "d");
        foreach (var id in state.Party) { party.Hp[id] = .5f; party.Stress[id] = 6; }
        var crawl = new Crawl(state, rules, party);
        Assert.True(crawl.EatMeal(meal));
        Assert.Equal(foodLeft, state.Pack.Count(Supply.Food));
        Assert.All(party.Hp.Values, hp => Assert.Equal(health, hp, 3));
        ExpeditionParty.Capture(state, state.Party.Select(id => new ExpeditionHeroState
        {
            Hp = party.Hp[id] * 30, HpMax = 30, Stress = party.Stress[id], Outcome = new HeroOutcome { HeroId = id }
        }));
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        string before = new SaveFile { Expedition = loaded }.ToJson();
        var restored = new FakeParty("a", "b", "c", "d");
        foreach (var id in loaded.Party)
        {
            restored.Hp[id] = loaded.PartyStates[id].Hp / 30;
            restored.Stress[id] = (int)loaded.PartyStates[id].Stress;
        }
        var resumed = new Crawl(loaded, rules, restored);
        Assert.Single(resumed.Resume());
        Assert.False(resumed.EatMeal(meal));
        Assert.False(resumed.EatMeal(Meal.Feast));
        Assert.Equal(before, new SaveFile { Expedition = loaded }.ToJson());
        Assert.Equal(10, loaded.RandomCounter); Assert.True(loaded.Camp.Ate);
        Assert.Equal(12, loaded.Camp.RespiteLeft); Assert.Equal(31, loaded.Light);
        Assert.Empty(restored.Log);
        Assert.All(restored.Hp.Values, hp => Assert.Equal(health, hp, 3));
        foreach (var id in state.Party) Assert.Equal(party.Stress[id], restored.Stress[id]);
    }
}
