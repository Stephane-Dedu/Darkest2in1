using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class FadedMemoryLifecycleTests
{
    private static ExpeditionState Ready()
    {
        var state = new ExpeditionState
        {
            Started = true, Seed = 12, RoomId = 0, Party = { "a" },
            Quest = new QuestOffer { Id = "boss", Dungeon = "dd2_city", Type = "kill_boss" },
            Map = new DungeonMap { EntranceRoomId = 0, Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = "quest", IsQuestGoal = true } } }
        };
        Assert.True(FadedMemory.Place(state, "crypts", "necromancer_A", 1));
        state.FadedMemory.Rewards.AddRange(new[] { "dd3_dd1_legendary_bracer", "dd3_dd1_ancestors_pistol" });
        return state;
    }

    [Fact]
    public void PlacementDoesNotReplaceQuestCurioOrDuplicateAfterReload()
    {
        var state = JsonConvert.DeserializeObject<ExpeditionState>(JsonConvert.SerializeObject(Ready()));
        Assert.False(FadedMemory.Place(state, "crypts", "necromancer_A", 1));
        Assert.True(FadedMemory.Here(state));
        Assert.Equal("quest", state.Map.Room(0).CurioId);
        Assert.True(state.Map.Room(0).IsQuestGoal);
    }

    [Theory]
    [InlineData("item")]
    [InlineData("report")]
    [InlineData("fight")]
    [InlineData("hero")]
    [InlineData("camp")]
    public void EntryRejectsOverlappingEventsAndSupplies(string obstacle)
    {
        var state = Ready();
        if (obstacle == "report") state.PendingCurio = new CurioReport();
        if (obstacle == "fight") state.FightCheckpoint = new FightCheckpoint();
        if (obstacle == "camp") state.Camp = new CampState();
        Assert.False(FadedMemory.Enter(state, obstacle == "hero" ? "absent" : "a", obstacle == "item" ? Supply.Key : null));
        Assert.Equal("ready", state.FadedMemory.Stage);
        Assert.Null(state.FadedMemory.ReturnPosition);
    }

    [Fact]
    public void VictoryReturnsOnceWithTwoRealRewardsAndLeavesOrdinaryQuestUntouched()
    {
        var state = Ready();
        Assert.True(FadedMemory.Enter(state, "a", null));
        state = JsonConvert.DeserializeObject<ExpeditionState>(JsonConvert.SerializeObject(state));
        state.Light = 31; state.GoalProgress = 3;
        state.FightCheckpoint = new FightCheckpoint();
        Assert.True(FadedMemory.Finish(state, new ItemCatalog(), true));
        Assert.False(FadedMemory.Finish(state, new ItemCatalog(), true));
        Assert.Equal(2, state.PendingCurio.Loot.Count);
        Assert.Equal(1, state.Pack.Count("trinket:dd3_dd1_ancestors_pistol"));
        Assert.False(state.Map.Room(0).Cleared);
        Assert.False(state.Map.Room(0).CurioTaken);
        Assert.False(state.QuestComplete);
        Assert.Equal(3, state.GoalProgress);
        Assert.Equal(31, state.Light);
        Assert.Null(state.FightCheckpoint);
    }

    [Fact]
    public void FullPackPersistsBothWaitingRewards()
    {
        var state = Ready();
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filled" + i, 1);
        Assert.True(FadedMemory.Enter(state, "a", null));
        Assert.True(FadedMemory.Finish(state, new ItemCatalog(), true));
        state = JsonConvert.DeserializeObject<ExpeditionState>(JsonConvert.SerializeObject(state));
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"));
        Assert.Equal(2, crawl.LastCurio.LeftBehind.Count);
        Assert.False(FadedMemory.Finish(state, new ItemCatalog(), true));
    }

    [Fact]
    public void RetreatIsSpentWithoutRewardsAndCannotReplay()
    {
        var state = Ready();
        Assert.True(FadedMemory.Enter(state, "a", null));
        Assert.True(FadedMemory.Finish(state, new ItemCatalog(), false));
        Assert.Empty(state.PendingCurio.Loot);
        Assert.False(FadedMemory.Here(state));
        Assert.False(FadedMemory.Enter(state, "a", null));
        Assert.Equal("fled", state.FadedMemory.Stage);
    }
}
