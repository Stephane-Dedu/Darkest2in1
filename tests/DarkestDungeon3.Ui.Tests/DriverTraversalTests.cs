using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class DriverTraversalTests
{
    private sealed class Party : IParty
    {
        public IReadOnlyList<string> Alive => new[] { "hero" };
        public float HpFraction(string id) => 1;
        public void Damage(string id, float amount, string cause) { }
        public void Heal(string id, float amount) { }
        public void AddStress(string id, int amount, string cause) { }
        public void AddStress(string id, float amount, string cause) { }
        public string AddDd1Quirk(string id, string quirk) => quirk;
        public string PurgeNegative(string id) => null;
        public string CureDisease(string id) => null;
    }

    private static Driver Ready(HallContent content)
    {
        var state = new ExpeditionState { Started = true, RoomId = -1, CorridorId = 0,
            TileIndex = 0, HeadingRoomId = 1, Party = { "hero" },
            Quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Difficulty = 1 },
            Map = new DungeonMap { Rooms = { new Room { Id = 0, Visited = true, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
                Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1,
                    Tiles = { new HallTile { Index = 0, Content = content, ContentId = content == HallContent.Trap ? "trap" : null }, new HallTile { Index = 1 } } } } } };
        return new Driver { Crawl = new Crawl(state, CrawlRules.FromDd1(Dd1Campaign.Load(Dd1Install.Find()).Rules), new Party()) };
    }

    [Theory]
    [InlineData(HallContent.Obstacle)]
    [InlineData(HallContent.Trap)]
    public void ARefusedMoveOrTrapActivationCannotInventACameraStep(HallContent content)
    {
        var driver = Ready(content); Time.unscaledTime = 100;
        float before = driver.LastStepTime;
        driver.Step(true);
        Assert.Equal(0, driver.Expedition.TileIndex);
        Assert.Equal(before, driver.LastStepTime);
        Assert.NotNull(driver.TraversalEvents);
    }

    [Fact]
    public void AcceptedStepStartsOnceAndRepeatedInputCannotRestartTheCamera()
    {
        var driver = Ready(HallContent.Empty); Time.unscaledTime = 100;
        driver.Step(true);
        Assert.Equal(1, driver.Expedition.TileIndex);
        Assert.Equal(100, driver.LastStepTime);
        Time.unscaledTime += .1f; driver.Step(true);
        Assert.Equal(1, driver.Expedition.TileIndex);
        Assert.Equal(100, driver.LastStepTime);
    }

    [Fact]
    public void RejectedRoomTravelFromAHallCannotRestartTheCamera()
    {
        var driver = Ready(HallContent.Empty); Time.unscaledTime = 100;
        driver.Step(true);
        float before = driver.LastStepTime;
        driver.Travel(1);
        Assert.Equal(before, driver.LastStepTime);
        Assert.Equal(1, driver.Expedition.TileIndex);
    }

    [Fact]
    public void ANewHallUsesItsDoorFadeWithoutAnExtraUnrequestedCameraStep()
    {
        var driver = Ready(HallContent.Empty);
        driver.Expedition.RoomId = 0; driver.Expedition.CorridorId = driver.Expedition.TileIndex = driver.Expedition.HeadingRoomId = -1;
        Time.unscaledTime = 100;
        driver.Travel(1);
        Assert.False(driver.Expedition.InRoom);
        Assert.Equal(0, driver.Expedition.TileIndex);
        Assert.Equal(0, driver.LastStepDir);
        Assert.Equal(-10, driver.LastStepTime);
    }

    [Fact]
    public void ReachingTheRoomStartsItsFadeOnTheSameFrameAndKeepsRoomPropsStill()
    {
        var driver = Ready(HallContent.Empty); Time.unscaledTime = 100;
        driver.Step(true);
        driver.SeedWalkProgress(.25f);
        Time.unscaledTime = 102; driver.Step(true);
        Assert.True(driver.Expedition.InRoom);
        Assert.Equal(102, driver.RoomTransitionAt);
        Assert.Equal(0, driver.LastStepDir);
        Assert.Equal(-10, driver.LastStepTime);
        Assert.Equal(0, driver.WalkProgress);
    }
}
