using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class HallwayStressTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    private static (Crawl Crawl, FakeParty Party) Crawl(HallContent content, bool backward = false, int seed = 1, float light = 100)
    {
        var map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
            Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles =
            {
                new HallTile { Index = 0, Content = content },
                new HallTile { Index = 1, Content = HallContent.Empty, Visited = true, Resolved = true },
            } } },
        };
        var state = new ExpeditionState { Started = true, Seed = seed, Light = light, Map = map, Party = { "a", "b", "c", "d" } };
        state.Pack.Add(Supply.Food, 8);
        if (backward) { state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 1; state.HeadingRoomId = 1; }
        var party = new FakeParty("a", "b", "c", "d");
        var rules = CrawlRules.FromDd1(Dd1.Rules);
        rules.StressChanceForward = rules.StressChanceBack = 1;
        rules.StressDd1Forward = rules.StressDd1Back = 10;
        rules.LightLossNewTile = rules.LightLossVisitedTile = 0;
        return (new Crawl(state, rules, party), party);
    }

    [Fact]
    public void EmptyForwardSquareStressesOnlyOneUniformlySelectedLivingHero()
    {
        var counts = new int[4];
        for (int seed = 1; seed <= 120; seed++)
        {
            var (crawl, party) = Crawl(HallContent.Empty, seed: seed);
            var stress = Assert.Single(crawl.Travel(1), e => e.Type == CrawlEventType.Stress && e.ContentId == "hallway");
            Assert.Equal(1, stress.Amount);
            Assert.Equal(1, party.Stress.Values.Sum());
            counts[stress.HeroId[0] - 'a']++;
        }
        Assert.All(counts, count => Assert.InRange(count, 15, 50));
    }

    [Theory]
    [InlineData(HallContent.Battle)]
    [InlineData(HallContent.Trap)]
    [InlineData(HallContent.Obstacle)]
    [InlineData(HallContent.Curio)]
    [InlineData(HallContent.Hunger)]
    public void ForwardContentSquareDoesNotRollHallwayStress(HallContent content)
    {
        var (crawl, _) = Crawl(content);
        Assert.DoesNotContain(crawl.Travel(1), e => e.Type == CrawlEventType.Stress && e.ContentId == "hallway");
    }

    [Theory]
    [InlineData(HallContent.Empty)]
    [InlineData(HallContent.Battle)]
    [InlineData(HallContent.Curio)]
    public void BackingUpCanStressOneHeroRegardlessOfDestinationContent(HallContent content)
    {
        var (crawl, _) = Crawl(content, backward: true);
        Assert.Single(crawl.Step(false), e => e.Type == CrawlEventType.Stress && e.ContentId == "hallway");
    }

    [Fact]
    public void StressUsesLightBeforeTheStepCrossesADarknessBoundary()
    {
        var (crawl, party) = Crawl(HallContent.Empty, light: 76);
        var rules = CrawlRules.FromDd1(Dd1.Rules);
        rules.StressChanceForward = 1;
        rules.StressDd1Forward = 100;
        Assert.Equal(0, rules.Band(76).StressDamageIncrease);
        Assert.True(rules.Band(70).StressDamageIncrease > 0);
        crawl = new Crawl(crawl.State, rules, party);
        var events = crawl.Travel(1);
        Assert.Equal(10, party.Stress.Values.Sum());
        Assert.Equal(70, crawl.State.Light);
        Assert.True(events.FindIndex(e => e.Type == CrawlEventType.Stress) < events.FindIndex(e => e.Type == CrawlEventType.LightChanged));
    }

    [Fact]
    public void TorchBandDoesNotAddToTheHallwayChance()
    {
        var (crawl, party) = Crawl(HallContent.Empty, light: 10);
        var rules = CrawlRules.FromDd1(Dd1.Rules);
        rules.StressChanceForward = 0;
        rules.Darkness.Clear();
        rules.Darkness.Add(new DarknessBand { Lower = 0, Upper = 100, StressChanceIncrease = 100 });
        crawl = new Crawl(crawl.State, rules, party);
        Assert.DoesNotContain(crawl.Travel(1), e => e.Type == CrawlEventType.Stress && e.ContentId == "hallway");
    }
}
