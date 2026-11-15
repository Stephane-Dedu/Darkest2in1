using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Defects found by tracing the whole loop (town, embark, crawl, fight, camp, return) end to end.</summary>
public class JourneyRegressionTests
{
    private static readonly Dd1Install Install = Dd1TestInstall.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = new() { Buffs = Dd1.Buffs };

    /// <summary>Two rooms joined by one hall of <paramref name="tiles"/> squares; the party starts in room 0.</summary>
    private static (Crawl Crawl, ExpeditionState State) Hall(int seed, CrawlRules rules, params HallTile[] tiles)
    {
        for (int i = 0; i < tiles.Length; i++) tiles[i].Index = i;
        var corridor = new Corridor { Id = 0, RoomA = 0, RoomB = 1 };
        corridor.Tiles.AddRange(tiles);
        var state = new ExpeditionState
        {
            Seed = seed, Party = { "a", "b" }, Started = true, RoomId = 0,
            Quest = new QuestOffer { Dungeon = "crypts", Type = "cleanse", ScoutingEnabled = false },
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, Visited = true, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
                Corridors = { corridor },
            },
        };
        return (new Crawl(state, rules, new FakeParty("a", "b"), Content), state);
    }

    private static CrawlRules Quiet()
    {
        var rules = CrawlRules.Defaults();
        rules.ReturnBattleChance = 0f;
        rules.ReturnHungerChance = 0f;
        rules.StressChanceForward = rules.StressChanceBack = 0f;
        return rules;
    }

    [Fact]
    public void FleeingACampAmbushWithNoWayBackStillEndsTheEncounter()
    {
        // The party camped in the entrance room: there is no hall it came from.
        var (crawl, state) = Hall(1, Quiet(), new HallTile());
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = 0, ContentId = "camp", HeroesSurprised = true };
        Assert.False(crawl.CanNavigate);
        Assert.True(crawl.FightWaiting);

        var events = crawl.FleeBattle();

        Assert.Contains(events, e => e.Type == CrawlEventType.Retreated);
        Assert.Equal(0, state.RoomId);
        Assert.Null(state.PendingEncounter);
        Assert.Null(state.FightCheckpoint);
        Assert.True(crawl.CanNavigate);          // previously: stuck with no fight to restart and no way to walk
        Assert.False(crawl.FightWaiting);
        Assert.True(crawl.CanLeaveExpedition);
    }

    [Fact]
    public void FleeingIgnoresAnEntrySquareThatBelongsToAnotherRoom()
    {
        var (crawl, state) = Hall(1, Quiet(), new HallTile(), new HallTile());
        state.Map.Rooms.Add(new Room { Id = 2, Visited = true });
        state.RoomId = 2;                         // e.g. a secret room: the remembered hall is not one of its doors
        state.CameFromCorridorId = 0; state.CameFromTileIndex = 1;
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = 2, ContentId = "camp" };

        crawl.FleeBattle();

        Assert.Equal(2, state.RoomId);
        Assert.Equal(-1, state.CorridorId);
        Assert.Null(state.PendingEncounter);
    }

    [Fact]
    public void AFledHallFightStartsAgainWhenThePartyWalksBackIntoIt()
    {
        var (crawl, state) = Hall(3, Quiet(), new HallTile(), new HallTile { Content = HallContent.Battle }, new HallTile());
        crawl.Travel(1);
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.Battle);
        Assert.Contains(crawl.FleeBattle(), e => e.Type == CrawlEventType.Retreated);
        Assert.Equal(0, state.TileIndex);
        Assert.Null(state.PendingEncounter);

        var back = crawl.Step(true);

        Assert.Equal(1, state.TileIndex);
        var battle = Assert.Single(back.Where(e => e.Type == CrawlEventType.Battle));
        Assert.Equal(1, battle.TileIndex);
        Assert.NotNull(state.PendingEncounter);
        Assert.True(crawl.FightWaiting);
        crawl.ResolveBattle();
        Assert.True(state.Map.Corridor(0).Tiles[1].Resolved);
        Assert.False(crawl.FightWaiting);
    }

    [Theory]
    [InlineData(HallContent.Curio, true)]
    [InlineData(HallContent.Curio, false)]
    [InlineData(HallContent.Obstacle, false)]
    [InlineData(HallContent.Trap, false)]
    public void AWanderingFightNeverErasesWhatASquareStillHolds(HallContent content, bool questGoal)
    {
        var rules = Quiet();
        rules.ReturnBattleChance = 1f;
        for (int seed = 0; seed < 20; seed++)
        {
            var held = new HallTile { Content = content, ContentId = "thing", IsQuestGoal = questGoal, Visited = true, Scouted = true };
            var (crawl, state) = Hall(seed, rules, held, new HallTile { Visited = true, Resolved = true });

            var events = crawl.Travel(1);        // onto the visited, unresolved square

            Assert.DoesNotContain(events, e => e.Type is CrawlEventType.Ambush or CrawlEventType.Battle);
            Assert.Equal(content, held.Content);
            Assert.False(held.Resolved);
            Assert.Null(state.PendingEncounter);
            if (content == HallContent.Curio) Assert.Equal("thing", crawl.CurioHere);
        }
    }

    [Fact]
    public void AWanderingFightStillTakesAnEmptyOrFinishedSquare()
    {
        var rules = Quiet();
        rules.ReturnBattleChance = 1f;
        var (crawl, state) = Hall(5, rules, new HallTile { Visited = true },
            new HallTile { Visited = true, Resolved = true, Content = HallContent.Curio, ContentId = "done" });
        Assert.Contains(crawl.Travel(1), e => e.Type == CrawlEventType.Ambush);
        crawl.ResolveBattle();
        Assert.True(crawl.DismissSpoils(crawl.LastSpoils));
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.Ambush);
        Assert.Equal(1, state.TileIndex);
    }

    [Fact]
    public void AnEncounterWhoseStartFailedCanBeRetriedByHand()
    {
        // A camp ambush leaves no battle on the map: only the saved encounter says a fight is due.
        var (crawl, state) = Hall(1, Quiet(), new HallTile());
        Assert.False(crawl.FightWaiting);
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = 0, ContentId = "camp", HeroesSurprised = true };
        Assert.False(crawl.IsBlocked);
        Assert.True(crawl.FightWaiting);
        state.Camp = new CampState();
        Assert.False(crawl.FightWaiting);         // camp controls own the screen until camp is struck
        state.Camp = null;
        crawl.ResolveBattle();
        Assert.False(crawl.FightWaiting);
    }

    [Fact]
    public void HeroesLeavingOnTheExpeditionDoNotRestInTown()
    {
        var buildings = Buildings.Load(Install);
        var catalog = new FakeCatalog();
        var estate = Hamlet.NewEstate(1, Dd1, buildings, catalog);
        var hamlet = new Hamlet(estate, Dd1, buildings, catalog);
        var away = estate.Roster[0];
        var home = estate.Roster[1];
        away.Stress = home.Stress = 8;

        var log = hamlet.EndWeek(new[] { away.Id });

        Assert.Equal(8, away.Stress);
        Assert.True(home.Stress < 8);
        Assert.DoesNotContain(log, line => line.StartsWith(away.Name + " rested"));
        Assert.Contains(log, line => line.StartsWith(home.Name + " rested"));
        Assert.Equal(1, estate.Week);
    }

    [Fact]
    public void LeavingHeroesDoNotChangeTheWeeksRolls()
    {
        var buildings = Buildings.Load(Install);
        var catalog = new FakeCatalog();
        var a = Hamlet.NewEstate(7, Dd1, buildings, catalog);
        var b = Hamlet.NewEstate(7, Dd1, buildings, catalog);
        new Hamlet(a, Dd1, buildings, catalog).EndWeek();
        new Hamlet(b, Dd1, buildings, catalog).EndWeek(new[] { b.Roster[0].Id });
        Assert.Equal(a.RandomCounter, b.RandomCounter);
        Assert.Equal(a.Recruits.Select(r => r.ClassId), b.Recruits.Select(r => r.ClassId));
        Assert.Equal(a.Quests.Select(q => q.Dungeon + q.Type + q.Length + q.MapSeed), b.Quests.Select(q => q.Dungeon + q.Type + q.Length + q.MapSeed));
        Assert.Equal(a.TownEventId, b.TownEventId);
    }
}
