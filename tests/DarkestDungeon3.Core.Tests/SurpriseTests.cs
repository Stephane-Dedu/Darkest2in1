using System;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SurpriseTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());
    private static readonly CrawlContent Content = new() { Buffs = Dd1.Buffs };

    private static Crawl RoomBattle(int seed, CrawlRules rules, params string[] heroes)
    {
        var state = new ExpeditionState
        {
            Seed = seed, Party = heroes.ToList(),
            Quest = new QuestOffer { Dungeon = "crypts", Type = "cleanse", ScoutingEnabled = false },
            Map = new DungeonMap { Rooms = { new Room { Id = 0, Content = RoomContent.Battle } } },
        };
        return new Crawl(state, rules, new FakeParty(heroes), Content);
    }

    [Fact]
    public void EncounterUsesNativeWeightedOrderAndNoneFloor()
    {
        var rules = CrawlRules.Defaults();
        rules.SurpriseRoomParty = rules.SurpriseRoomMonsters = 0.65f;
        // Native 1405fe3d0 has weights none=.25, party=.65, monsters=.65.
        // Thus the cumulative intervals are 5/31, 18/31, 1, not two Bernoulli rolls.
        var seen = new bool[3];
        for (int seed = 0; seed < 80; seed++)
        {
            var crawl = RoomBattle(seed, rules, "a", "b");
            double draw = new Rng(unchecked(seed * 31 + 977)).NextDouble();
            int expected = draw < 5.0 / 31 ? 0 : draw < 18.0 / 31 ? 1 : 2;
            var battle = Assert.Single(crawl.Begin().Where(e => e.Type == CrawlEventType.Battle));
            Assert.Equal(expected == 1, battle.HeroesSurprised);
            Assert.Equal(expected == 2, battle.MonstersSurprised);
            seen[expected] = true;
        }
        Assert.All(seen, Assert.True);
    }

    [Theory]
    [InlineData(100f, 0f)]
    [InlineData(0f, 0.4f)]
    public void LoneHeroLosesOnlyTheBaseBeforeTorchModifiers(float light, float expected)
    {
        var crawl = RoomBattle(1, CrawlRules.FromDd1(Dd1.Rules), "a");
        crawl.State.Light = light;
        Assert.Equal(expected, crawl.SurpriseChances(false, false, false).Heroes, 5);
    }

    [Fact]
    public void ActivePendingBuffsOfLivingHeroesModifyBothWeights()
    {
        var crawl = RoomBattle(1, CrawlRules.Defaults(), "a", "b");
        crawl.State.PendingBuffs["a"] = new() { "campingPartySurprise", "campingMonstersSurprise" };
        crawl.State.PendingBuffs["b"] = new() { "campingPartySurprise" };
        crawl.State.PendingBuffs["absent"] = new() { "campingMonstersSurprise" };
        var chance = crawl.SurpriseChances(false, false, false);
        Assert.Equal(0.3f, chance.Heroes, 5);
        Assert.Equal(0.2f, chance.Monsters, 5);
        var lone = RoomBattle(1, CrawlRules.Defaults(), "a");
        lone.State.PendingBuffs["a"] = new() { "campingPartySurprise" };
        Assert.Equal(0.1f, lone.SurpriseChances(false, false, false).Heroes, 5);
    }

    [Fact]
    public void SurpriseWeightsStillRespectBothNativeCaps()
    {
        var crawl = RoomBattle(1, CrawlRules.FromDd1(Dd1.Rules), "a", "b", "c", "d");
        foreach (string hero in crawl.State.Party)
            crawl.State.PendingBuffs[hero] = new() { "campingPartySurprise", "campingMonstersSurprise" };
        crawl.State.Light = 0f;
        Assert.Equal(0.65f, crawl.SurpriseChances(false, false, false).Heroes, 5);
        crawl.State.Light = 100f;
        Assert.Equal(0.65f, crawl.SurpriseChances(false, false, false).Monsters, 5);
    }

    [Fact]
    public void WanderingBattleUsesOrdinarySurpriseRatherThanForcedCampAmbush()
    {
        var rules = CrawlRules.Defaults();
        rules.ReturnBattleChance = 1f;
        rules.SurpriseCorridorParty = 0f;
        rules.SurpriseCorridorMonsters = 0.65f;
        bool sawMonsters = false, sawNone = false;
        for (int seed = 0; seed < 40; seed++)
        {
            var state = new ExpeditionState
            {
                Seed = seed, Party = { "a", "b" },
                Quest = new QuestOffer { Dungeon = "crypts", Type = "cleanse", ScoutingEnabled = false },
                Map = new DungeonMap
                {
                    Rooms = { new Room { Id = 0 }, new Room { Id = 1 } },
                    Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1,
                        Tiles = { new HallTile { Index = 0, Visited = true } } } },
                },
            };
            var crawl = new Crawl(state, rules, new FakeParty("a", "b"), Content);
            var battle = Assert.Single(crawl.Travel(1).Where(e => e.Type == CrawlEventType.Ambush));
            Assert.False(battle.HeroesSurprised);
            sawMonsters |= battle.MonstersSurprised;
            sawNone |= !battle.MonstersSurprised;
        }
        Assert.True(sawMonsters);
        Assert.True(sawNone);
    }

    [Fact]
    public void ReloadPresentsTheRolledEncounterWithoutAnotherDraw()
    {
        var crawl = RoomBattle(17, CrawlRules.Defaults(), "a", "b");
        var battle = Assert.Single(crawl.Begin().Where(e => e.Type == CrawlEventType.Battle));
        var state = SaveFile.FromJson(new SaveFile { Estate = new Estate(), Expedition = crawl.State }.ToJson()).Expedition;
        int counter = state.RandomCounter;
        var resumed = new Crawl(state, CrawlRules.Defaults(), new FakeParty("a", "b"), Content);
        var replay = Assert.Single(resumed.Resume().Where(e => e.Type == CrawlEventType.Battle));
        Assert.Equal(battle.HeroesSurprised, replay.HeroesSurprised);
        Assert.Equal(battle.MonstersSurprised, replay.MonstersSurprised);
        Assert.Equal(counter, state.RandomCounter);
    }
}
