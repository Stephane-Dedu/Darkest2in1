using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>A wandering fight on the way back becomes its square's battle. It must never take a square whose own
/// curio, trap, obstacle or fight still waits: a quest curio erased that way leaves the quest unfinishable.</summary>
public class WanderingFightTests
{
    [Theory]
    [InlineData(HallContent.Curio)]
    [InlineData(HallContent.Trap)]
    [InlineData(HallContent.Obstacle)]
    [InlineData(HallContent.Battle)]
    public void WanderersNeverTakeASquareWhoseContentStillWaits(HallContent content)
    {
        var (crawl, state) = BackOnto(content, resolved: false);
        var tile = state.Map.Corridors[0].Tiles[1];

        var events = crawl.Step(forward: false);

        Assert.Equal(1, state.TileIndex);
        Assert.DoesNotContain(events, e => e.Type is CrawlEventType.Ambush or CrawlEventType.Battle);
        Assert.Null(state.PendingEncounter);
        Assert.Equal(content, tile.Content);
        Assert.Equal("kept", tile.ContentId);
        Assert.False(tile.Resolved);
    }

    [Theory]
    [InlineData(HallContent.Empty)]
    [InlineData(HallContent.Curio)]
    [InlineData(HallContent.Hunger)]
    public void WanderersStillComeWhereNothingIsLeftToDo(HallContent content)
    {
        var (crawl, state) = BackOnto(content, resolved: true);

        var events = crawl.Step(forward: false);

        var ambush = Assert.Single(events.Where(e => e.Type == CrawlEventType.Ambush));
        Assert.Equal(1, ambush.TileIndex);
        Assert.NotNull(state.PendingEncounter);
        Assert.Equal(HallContent.Battle, state.Map.Corridors[0].Tiles[1].Content);
        Assert.False(state.Map.Corridors[0].Tiles[1].Resolved);
    }

    [Fact]
    public void AQuestCurioPassedOnTheWayInStillFinishesTheQuestOnTheWayBack()
    {
        var (crawl, state) = BackOnto(HallContent.Curio, resolved: false);
        state.Quest.Type = "activate";
        state.Goal = new QuestGoal { Id = "activate_altar", Type = "activate", CurioName = "kept", Amount = 1 };
        state.Map.Corridors[0].Tiles[1].IsQuestGoal = true;

        crawl.Step(forward: false);

        Assert.Equal("kept", crawl.CurioHere);
        var report = crawl.InteractCurio("a", null, out var overflow);
        Assert.Equal("Quest", report.OutcomeType);
        Assert.Empty(overflow);
        Assert.True(state.QuestComplete);
    }

    /// <summary>The party stands on the far square of a three-square hall, heading for room 1, about to walk back
    /// onto the middle square it has already crossed. Every walk back meets wanderers.</summary>
    private static (Crawl Crawl, ExpeditionState State) BackOnto(HallContent content, bool resolved)
    {
        var state = new ExpeditionState
        {
            Started = true, Party = { "a" }, RoomId = -1, CorridorId = 0, TileIndex = 2, HeadingRoomId = 1,
            Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1 },
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, Visited = true, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
                Corridors =
                {
                    new Corridor
                    {
                        Id = 0, RoomA = 0, RoomB = 1, Tiles =
                        {
                            new HallTile { Index = 0, Visited = true },
                            new HallTile { Index = 1, Visited = true, Scouted = true, Content = content, ContentId = "kept", Resolved = resolved },
                            new HallTile { Index = 2, Visited = true },
                        }
                    }
                }
            }
        };
        var rules = new CrawlRules { ReturnBattleChance = 1f, ReturnHungerChance = 0f, StressChanceForward = 0f, StressChanceBack = 0f };
        return (new Crawl(state, rules, new FakeParty("a")), state);
    }
}
