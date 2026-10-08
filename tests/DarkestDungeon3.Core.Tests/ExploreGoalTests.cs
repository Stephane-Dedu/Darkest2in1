using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExploreGoalTests
{
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    private static ExpeditionState State(int rooms)
    {
        var state = new ExpeditionState
        {
            Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore" },
            Goal = Dd1.Goals.For("explore", "crypts"), Map = new DungeonMap(), Party = { "h" },
        };
        for (int i = 0; i < rooms; i++) state.Map.Rooms.Add(new Room { Id = i });
        return state;
    }

    private static Crawl Crawl(ExpeditionState state) => new(state, new CrawlRules(), new FakeParty("h"));

    [Theory]
    [InlineData(3, 2)]
    [InlineData(9, 8)]
    [InlineData(10, 9)]
    [InlineData(11, 9)]
    [InlineData(20, 18)]
    public void NativeExploreGoalCompletesAtTruncatedRoomTarget(int rooms, int target)
    {
        var state = State(rooms);
        Assert.Equal(0, state.Goal.Amount);
        Assert.Equal(0.9f, state.Goal.Percentage);
        Assert.Equal(target, Core.Expedition.Crawl.ExploreRoomTarget(state));
        var crawl = Crawl(state);
        foreach (var room in state.Map.Rooms.Take(target - 1)) room.Visited = true;
        crawl.CheckQuest();
        Assert.False(state.QuestComplete);
        state.Map.Room(target - 1).Visited = true;
        crawl.CheckQuest();
        Assert.True(state.QuestComplete);
        Assert.False(state.Ended);
        Assert.Equal(0, state.RandomCounter);
    }

    [Fact]
    public void SecretRoomsDoNotCountTowardTheTargetOrVisits()
    {
        var state = State(10);
        state.Map.Rooms.Add(new Room { Id = 10, IsSecret = true, Visited = true });
        Assert.Equal(9, Core.Expedition.Crawl.ExploreRoomTarget(state));
        foreach (var room in state.Map.Rooms.Take(8)) room.Visited = true;
        var crawl = Crawl(state);
        crawl.CheckQuest();
        Assert.False(state.QuestComplete);
        state.Map.Room(8).Visited = true;
        crawl.CheckQuest();
        Assert.True(state.QuestComplete);
        Assert.False(state.Map.Room(9).Visited);
    }

    [Fact]
    public void ExplicitNativeAmountTakesPrecedenceOverPercentage()
    {
        var state = State(10);
        state.Goal = new QuestGoal { Type = "explore_room", Amount = 2, Percentage = 0.9f };
        Assert.Equal(2, Core.Expedition.Crawl.ExploreRoomTarget(state));
        state.Map.Room(0).Visited = true;
        var crawl = Crawl(state);
        crawl.CheckQuest();
        Assert.False(state.QuestComplete);
        state.Map.Room(1).Visited = true;
        crawl.CheckQuest();
        Assert.True(state.QuestComplete);
    }

    [Fact]
    public void ArrivalCompletesOnceAndSavedPartyCanContinueBeforeReturning()
    {
        var state = State(3);
        state.Map.Room(0).Content = RoomContent.Entrance;
        for (int i = 0; i < 2; i++)
        {
            var hall = new Corridor { Id = i, RoomA = i, RoomB = i + 1, Tiles = { new HallTile { Index = 0 } } };
            state.Map.Corridors.Add(hall);
            state.Map.Room(i).CorridorIds.Add(i);
            state.Map.Room(i + 1).CorridorIds.Add(i);
        }
        var crawl = Crawl(state);
        Assert.DoesNotContain(crawl.Begin(), e => e.Type == CrawlEventType.QuestComplete);
        crawl.Travel(1);
        Assert.Single(crawl.Step(true), e => e.Type == CrawlEventType.QuestComplete);
        Assert.True(state.QuestComplete);
        Assert.False(state.Ended);

        state = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        crawl = Crawl(state);
        int rng = state.RandomCounter;
        crawl.CheckQuest();
        Assert.Equal(rng, state.RandomCounter);
        Assert.DoesNotContain(crawl.Travel(2), e => e.Type == CrawlEventType.QuestComplete);
        Assert.DoesNotContain(crawl.Step(true), e => e.Type == CrawlEventType.QuestComplete);
        Assert.True(state.Map.Room(2).Visited);
        Assert.False(state.Ended);
        Assert.True(crawl.TryLeave());
        Assert.True(state.Ended);
        Assert.False(crawl.TryLeave());
    }
}
