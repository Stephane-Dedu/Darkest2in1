using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CampLightTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    private static ExpeditionState State(float light) => new()
    {
        Started = true, Light = light, Party = { "h", "t" },
        Map = new DungeonMap { Rooms = { new Room { Id = 0 } } },
        CampSkills = { ["h"] = new() { "dark_ritual" } },
        Pack = new Inventory { Items = { [Supply.Firewood] = 1, [Supply.Food] = 8 } },
    };

    [Theory]
    [InlineData(0f, 100f, 100f)]
    [InlineData(30f, 100f, 100f)]
    [InlineData(100f, 100f, 100f)]
    [InlineData(30f, 25f, 55f)]
    public void CampEntryAddsTorchBeforeMealAndSavedReentryCannotAddItAgain(float light, float restore, float expected)
    {
        var state = State(light);
        var rules = CrawlRules.FromDd1(Dd1.Rules);
        rules.CampRestoreTorch = restore;
        var crawl = new Crawl(state, rules, new FakeParty("h", "t"), Content);
        var events = crawl.MakeCamp();
        Assert.Equal(expected, state.Light);
        Assert.Equal(expected != light ? 1 : 0, events.Count(e => e.Type == CrawlEventType.LightChanged));
        Assert.Equal(0, state.Pack.Count(Supply.Firewood));
        Assert.False(state.Camp.Ate);
        Assert.Equal(0, state.RandomCounter);
        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        crawl = new Crawl(state, rules, new FakeParty("h", "t"), Content);
        string before = new SaveFile { Expedition = state }.ToJson();
        crawl.Resume();
        Assert.Contains(crawl.MakeCamp(), e => e.Type == CrawlEventType.Blocked);
        Assert.Equal(before, new SaveFile { Expedition = state }.ToJson());
        Assert.True(crawl.EatMeal(Meal.Full));
        Assert.Equal(expected, state.Light);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDarkRitualReductionSurvivesSavedRestWithOrWithoutAmbush(bool ambush)
    {
        var state = State(30);
        var rules = CrawlRules.FromDd1(Dd1.Rules);
        rules.AmbushCampChance = ambush ? 1f : 0f;
        Assert.Contains(Content.Camping.Get("dark_ritual").Effects, e => e.Type == "reduce_torch" && e.Amount == 100);
        var party = new FakeParty("h", "t");
        party.Hp["t"] = .2f;
        var crawl = new Crawl(state, rules, party, Content);
        crawl.MakeCamp();
        Assert.True(crawl.EatMeal(Meal.Full));
        Assert.True(crawl.UseCampSkill("h", "dark_ritual", "t"));
        Assert.Equal(0, state.Light);
        Assert.Equal(.8f, party.Hp["t"], 3);
        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        crawl = new Crawl(state, rules, new FakeParty("h", "t"), Content);
        int rng = state.RandomCounter;
        Assert.False(crawl.UseCampSkill("h", "dark_ritual", "t"));
        Assert.Equal(rng, state.RandomCounter);
        var events = crawl.BreakCamp();
        Assert.Equal(0, state.Light);
        Assert.Null(state.Camp);
        Assert.DoesNotContain(events, e => e.Type == CrawlEventType.LightChanged);
        Assert.Equal(ambush, events.Any(e => e.Type == CrawlEventType.Ambush));
        if (ambush)
        {
            Assert.Equal("camp", state.PendingEncounter.ContentId);
            Assert.True(state.PendingEncounter.HeroesSurprised);
            Assert.False(state.PendingEncounter.MonstersSurprised);
        }
    }
}
