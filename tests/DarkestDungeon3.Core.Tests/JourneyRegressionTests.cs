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

    /// <summary>The party stands in secret room 2, found through the door on hall square 1.</summary>
    private static (Crawl Crawl, ExpeditionState State) InSecretRoom(RoomContent content)
    {
        var (crawl, state) = Hall(4, Quiet(), new HallTile { Visited = true, Resolved = true },
            new HallTile { Visited = true, Resolved = true, SecretRoomId = 2 });
        state.Map.Rooms.Add(new Room { Id = 2, IsSecret = true, Visited = true, Scouted = true, Content = content });
        state.RoomId = 2;
        state.CameFromCorridorId = 0; state.CameFromTileIndex = 0;   // the last ordinary door was room 0's
        state.SecretReturnCorridorId = 0; state.SecretReturnTileIndex = 1; state.SecretReturnHeadingRoomId = 1;
        return (crawl, state);
    }

    [Fact]
    public void FleeingACampAmbushInASecretRoomFallsBackThroughItsDoor()
    {
        var (crawl, state) = InSecretRoom(RoomContent.Empty);
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = 2, ContentId = "camp" };

        Assert.Contains(crawl.FleeBattle(), e => e.Type == CrawlEventType.Retreated);

        Assert.False(state.InRoom);
        Assert.Equal((0, 1, 1), (state.CorridorId, state.TileIndex, state.HeadingRoomId));
        Assert.Equal(-1, state.SecretReturnCorridorId);
        Assert.Null(state.PendingEncounter);
        Assert.True(crawl.CanNavigate);
        Assert.True(crawl.CanEnterSecretRoom);                       // the way back in is still there
    }

    [Fact]
    public void FleeingASecretRoomsOwnFightLeavesItThereForTheReturn()
    {
        var (crawl, state) = InSecretRoom(RoomContent.Battle);
        state.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Battle, RoomId = 2 };

        crawl.FleeBattle();

        Assert.Equal((0, 1), (state.CorridorId, state.TileIndex));
        Assert.False(state.Map.Room(2).Cleared);
        Assert.Contains(crawl.EnterSecretRoom(), e => e.Type == CrawlEventType.Battle);
        Assert.True(crawl.FightWaiting);
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

    private static readonly System.Lazy<CrawlContent> Full = new(() => CrawlContent.Load(Install));

    /// <summary>The party stands on a hall square holding <paramref name="curio"/>, with the real curio rules.</summary>
    private static (Crawl Crawl, ExpeditionState State, HallTile Tile) OnCurio(string curio)
    {
        var tile = new HallTile { Content = HallContent.Curio, ContentId = curio, Visited = true };
        var (_, state) = Hall(9, Quiet(), tile);
        state.RoomId = -1; state.CorridorId = 0; state.TileIndex = 0; state.HeadingRoomId = 1;
        return (new Crawl(state, Quiet(), new FakeParty("a", "b"), Full.Value), state, tile);
    }

    [Theory]
    [InlineData(Supply.Torch, 1)]          // carried, but does nothing on a locked chest
    [InlineData("skeleton_key", 0)]        // the right item, but the pack no longer holds one
    public void AnItemThatCannotBeUsedHereLeavesTheCurioAndTheItem(string item, int carried)
    {
        var (crawl, state, tile) = OnCurio("heirloom_chest");
        if (carried > 0) state.Pack.Add(item, carried);
        Assert.Equal("heirloom_chest", crawl.CurioHere);

        Assert.Null(crawl.InteractCurio("a", item, out var overflow));

        Assert.Empty(overflow);
        Assert.False(tile.Resolved);
        Assert.Equal("heirloom_chest", crawl.CurioHere);
        Assert.Null(state.PendingCurio);
        Assert.Equal(carried, state.Pack.Count(item));
    }

    [Fact]
    public void TheRightItemStillOpensTheCurio()
    {
        var (crawl, state, tile) = OnCurio("heirloom_chest");
        state.Pack.Add("skeleton_key", 1);

        var report = crawl.InteractCurio("a", "skeleton_key", out _);

        Assert.NotNull(report);
        Assert.Equal("skeleton_key", report.ItemUsed);
        Assert.True(tile.Resolved);
        Assert.Equal(0, state.Pack.Count("skeleton_key"));
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

    [Theory]
    [InlineData(1, 5, 2)]   // a short champion quest: the table by length, not by difficulty (was 6)
    [InlineData(3, 1, 4)]   // a long apprentice quest (was 2)
    [InlineData(2, 3, 3)]
    public void DungeonXpFollowsTheQuestLength(int length, int difficulty, int expected)
    {
        var quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = length, Difficulty = difficulty };
        Assert.Equal(new[] { 0, 2, 3, 4, 5, 6 }, Dd1.ZoneXpPerQuest);
        Assert.Equal(expected, Homecoming.ZoneXp(Dd1, quest));
    }

    [Fact]
    public void BossQuestsGiveNoDungeonXpButTheTutorialDoes()
    {
        var boss = Dd1.Goals.Plot.First(p => p.Id == "plot_kill_necromancer_1");
        var tutorial = Dd1.Goals.Plot.First(p => p.Id == "plot_tutorial_crypts");
        Assert.False(boss.CompletionDungeonXp);
        Assert.True(tutorial.CompletionDungeonXp);

        var estate = new Estate();
        var offer = QuestBoard.PlotOffer(estate, Dd1, boss);
        Assert.Equal(0, Homecoming.ZoneXp(Dd1, offer));
        Assert.Equal(Dd1.ZoneXpPerQuest[tutorial.Length], Homecoming.ZoneXp(Dd1, QuestBoard.PlotOffer(estate, Dd1, tutorial)));

        int completed = estate.QuestsCompleted;
        var report = Homecoming.Report(estate, Dd1, new ExpeditionState { Quest = offer, QuestComplete = true }, new HeroOutcome[0]);
        Assert.Equal("complete", report.Result);
        Assert.False(estate.ZoneXp.ContainsKey(offer.Dungeon));   // the boss is beaten; the zone does not level from it
        Assert.Equal(completed + 1, estate.QuestsCompleted);
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
