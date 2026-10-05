using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class EndedCrawlTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData("room", false)]
    [InlineData("room", true)]
    [InlineData("hall", false)]
    [InlineData("hall", true)]
    [InlineData("camp", false)]
    [InlineData("camp", true)]
    [InlineData("battle", false)]
    [InlineData("battle", true)]
    public void LateActionsCannotChangeAnEndedExpeditionOrItsOutcome(string context, bool retreat)
    {
        var state = State(context);
        state.Ended = true;
        state.Retreated = retreat;
        state.QuestComplete = !retreat;
        foreach (bool reload in new[] { false, true })
        {
            if (reload) state = SaveFile.FromJson(Json(state)).Expedition;
            string before = Json(state);
            var party = new FakeParty("a", "b");
            party.Hp["a"] = 0.5f; party.Stress["a"] = 5;
            var crawl = Crawl(state, party);
            var left = new List<LootDrop> { new() { Type = "gold", Id = "", Amount = 100 } };
            var taken = new List<LootDrop>();

            Blocked(crawl.Begin());
            Blocked(crawl.Travel(1));
            Blocked(crawl.Step(true));
            Blocked(crawl.Step(false));
            Blocked(crawl.EnterSecretRoom());
            Blocked(crawl.ExitSecretRoom());
            Blocked(crawl.FleeBattle());
            Blocked(crawl.ResolveBattle());
            Assert.Null(crawl.LastSpoils);
            Assert.Empty(crawl.FightMonsters("hall"));
            Blocked(crawl.ClearObstacle());
            Blocked(crawl.DisarmTrap("a"));
            Blocked(crawl.UseTorch());
            Blocked(crawl.SnuffTorch());
            Blocked(crawl.Darken(20));
            Assert.Null(crawl.UseSupply("a", Supply.Food));
            Assert.Null(crawl.UseSupply("a", Supply.HolyWater));
            Assert.Null(crawl.InteractCurio("a", "skeleton_key", out var overflow));
            Assert.Empty(overflow);
            crawl.SkipCurio();
            Assert.False(crawl.Discard(Supply.Torch));
            Assert.False(crawl.TakeLeftBehind(left, 0, taken));
            Assert.Single(left); Assert.Empty(taken);
            Assert.False(crawl.CanCamp);
            Blocked(crawl.MakeCamp());
            Assert.False(crawl.EatMeal(Meal.Full));
            Assert.Equal("Expedition ended.", crawl.WhyCantUseCampSkill("a", "encourage"));
            Assert.False(crawl.UseCampSkill("a", "encourage", "b"));
            Blocked(crawl.BreakCamp());
            crawl.CheckQuest();
            crawl.Retreat();

            Assert.Equal(before, Json(state));
            Assert.Equal(0.5f, party.Hp["a"]);
            Assert.Equal(5, party.Stress["a"]);
            Assert.Empty(party.Log); Assert.Empty(party.Quirks);
        }
    }

    [Fact]
    public void QuestCompletionAllowsContinuedExplorationUntilLeaving()
    {
        var state = State("room");
        state.QuestComplete = true;
        var crawl = Crawl(state, new FakeParty("a", "b"));
        Assert.True(crawl.CanCamp);
        Assert.Contains(crawl.UseTorch(), e => e.Type == CrawlEventType.LightChanged);
        Assert.NotNull(crawl.InteractCurio("a", "skeleton_key", out _));
        Assert.Contains(crawl.Travel(1), e => e.Type == CrawlEventType.EnteredTile);
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.EnteredTile);
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.EnteredTile);
        Assert.Contains(crawl.Step(true), e => e.Type == CrawlEventType.EnteredRoom);
        Assert.True(crawl.CanCamp);
        crawl.MakeCamp();
        Assert.True(crawl.EatMeal(Meal.Full));
        crawl.BreakCamp();
        Assert.False(state.Ended);
        state.Ended = true;
        string before = Json(state);
        crawl.Retreat();
        Assert.Equal(before, Json(state));
        Assert.False(state.Retreated);
    }

    private static void Blocked(List<CrawlEvent> events) => Assert.Equal(CrawlEventType.Blocked, Assert.Single(events).Type);
    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
    private static Crawl Crawl(ExpeditionState state, FakeParty party) => new(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);

    private static ExpeditionState State(string context)
    {
        var state = new ExpeditionState
        {
            Seed = 61, RandomCounter = 13, Light = 30, Party = { "a", "b" },
            Quest = new QuestOffer { Dungeon = "crypts", Type = "gather", Difficulty = 1 },
            Goal = Dd1.Goals.For("gather", "crypts"), GoalProgress = 3,
            RoomId = context == "hall" ? -1 : 0, CorridorId = context == "hall" ? 0 : -1,
            TileIndex = context == "hall" ? 1 : -1, HeadingRoomId = 1,
            CameFromCorridorId = 0, CameFromTileIndex = 2,
            Camp = context == "camp" ? new CampState { RespiteLeft = 12 } : null,
            CampSkills = { ["a"] = new List<string> { "encourage" } },
            Map = new DungeonMap
            {
                Rooms =
                {
                    new Room { Id = 0, Content = context == "battle" ? RoomContent.Battle : RoomContent.Curio, CurioId = "heirloom_chest", CorridorIds = { 0 } },
                    new Room { Id = 1, Content = RoomContent.Empty, CorridorIds = { 0 } }
                },
                Corridors =
                {
                    new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles =
                    {
                        new HallTile { Index = 0 },
                        new HallTile { Index = 1, Content = HallContent.Trap, ContentId = "crypts", Scouted = true },
                        new HallTile { Index = 2 }
                    } }
                }
            }
        };
        if (context != "hall") state.Map.Corridors[0].Tiles[1].Content = HallContent.Empty;
        foreach (var key in new[] { Supply.Torch, Supply.Firewood, Supply.Shovel, Supply.HolyWater, "skeleton_key" }) state.Pack.Add(key, 2);
        state.Pack.Add(Supply.Food, 8);
        return state;
    }
}
