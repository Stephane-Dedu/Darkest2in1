using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ContinuousWalkTests
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

    private static Driver Ready(int count, int width = 4096, int height = 768, float leg = 102.6f)
    {
        var hall = new Corridor { Id = 0, RoomA = 0, RoomB = 1 };
        for (int i = 0; i < count; i++) hall.Tiles.Add(new HallTile { Index = i, Visited = true });
        var state = new ExpeditionState { Started = true, Seed = 42, RoomId = -1, CorridorId = 0,
            TileIndex = 2, HeadingRoomId = 1, Party = { "hero" },
            Quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Difficulty = 1 },
            Map = new DungeonMap { Rooms = { new Room { Id = 0, Visited = true, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
                Corridors = { hall } } };
        return new Driver { SceneryWidth = width, SceneryHeight = height, ProjectedLegPixels = leg,
            Crawl = new Crawl(state, CrawlRules.FromDd1(Dd1Campaign.Load(Dd1Install.Find()).Rules), new Party()) };
    }

    private static float Camera(Driver driver) => CorridorSceneMotion.Camera(driver.SceneryWidth, driver.SceneryHeight,
        driver.Expedition.TileIndex, driver.Crawl.CurrentCorridor.Tiles.Count, false,
        CorridorSceneMotion.Slide(Time.unscaledTime - driver.LastStepTime, driver.LastStepDir, driver.WalkProgress));

    private static (float Speed, float MaximumSteps) Replay(Driver driver, bool forward = true, float dt = 1f / 60, int frames = 72)
    {
        Time.unscaledTime = 100;
        var cycle = new CorridorWalkCycle(.2);
        float first = Camera(driver), prior = first, maximum = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            Time.unscaledTime += dt;
            driver.AdvanceHeldTick(forward, dt);
            float camera = Camera(driver);
            float rate = CorridorWalkCycle.Rate(camera - prior, driver.ProjectedLegPixels, dt);
            cycle.Advance(rate, dt, true);
            maximum = Math.Max(maximum, Math.Abs(rate) * 2);
            prior = camera;
        }
        return ((prior - first) / (frames * dt), maximum);
    }

    [Theory]
    [InlineData(6, 4096, 768)]
    [InlineData(12, 4096, 768)]
    [InlineData(20, 4096, 768)]
    [InlineData(6, 0, 0)]
    public void ContinuousWalkingKeepsAStablePhysicalSpeedAcrossPaintingZoomAndLegacyFallback(int count, int width, int height)
    {
        var result = Replay(Ready(count, width, height));
        Assert.True(result.Speed >= 160 && result.Speed <= 190,
            $"Continuous ground speed is {result.Speed:0.0}px/s, giving {result.MaximumSteps:0.00} steps/s on the recorded rig.");
    }

    [Theory]
    [InlineData(55)]
    [InlineData(102.6f)]
    [InlineData(134.5f)]
    public void ContinuousWalkingDoesNotForceShortOrRecordedNativeRigsIntoRapidShuffling(float leg)
    {
        var result = Replay(Ready(6, leg: leg));
        Assert.True(result.MaximumSteps <= 2.7f,
            $"The actual held-walk and crop force {result.MaximumSteps:0.00} steps/s on a {leg:0.0}px projected leg.");
    }

    [Theory]
    [InlineData(6, 4096, 768, 55f)]
    [InlineData(6, 4096, 768, 102.6f)]
    [InlineData(6, 4096, 768, 134.5f)]
    [InlineData(20, 4096, 768, 102.6f)]
    [InlineData(6, 0, 0, 102.6f)]
    public void EachVisibleFootfallAdvancesTheActualFloorByOneProjectedLegLength(int count, int width, int height, float leg)
    {
        var driver = Ready(count, width, height, leg);
        var cycle = new CorridorWalkCycle(.2);
        const float dt = 1f/120;
        Time.unscaledTime = 100;
        float prior = Camera(driver), lastLanding = 0;
        bool haveLanding = false;
        int steps = 0;
        for (int frame = 0; frame < 480; frame++)
        {
            bool left = CorridorWalkCycle.Sample(cycle.Phase).Planted;
            bool right = CorridorWalkCycle.Sample(cycle.Phase+.5).Planted;
            Time.unscaledTime += dt;
            driver.AdvanceHeldTick(true,dt);
            float ground = Camera(driver);
            cycle.Advance(CorridorWalkCycle.Rate(ground-prior,leg,dt),dt,true);
            bool landed = (!left && CorridorWalkCycle.Sample(cycle.Phase).Planted)
                       || (!right && CorridorWalkCycle.Sample(cycle.Phase+.5).Planted);
            if (landed)
            {
                if (haveLanding)
                {
                    float distance = (ground-lastLanding)/leg;
                    Assert.True(distance >= .95f && distance <= 1.05f,
                        $"The floor advances only {distance:0.000} projected leg lengths between visible footfalls.");
                    steps++;
                }
                lastLanding = ground;
                haveLanding = true;
            }
            prior = ground;
        }
        Assert.True(steps >= 3,"The replay must include multiple alternating footfalls.");
    }

    [Theory]
    [InlineData(154.1f)]
    [InlineData(161.4f)]
    [InlineData(170.5f)]
    [InlineData(201.6f)]
    public void RecordedFullSizeNativeRigsKeepAWalkingCadence(float leg)
    {
        var result = Replay(Ready(6,leg:leg));
        Assert.True(result.MaximumSteps >= 1.3f && result.MaximumSteps <= 1.85f,
            $"The fitted {leg:0.0}px native rig takes only {result.MaximumSteps:0.00} steps/s.");
    }

    [Theory]
    [InlineData(4096, 768)]
    [InlineData(0, 0)]
    public void ARenderStallCannotTeleportTheFloorByMoreThanAReadableContactStep(int width, int height)
    {
        var driver = Ready(6, width, height);
        Time.unscaledTime = 100;
        float before = Camera(driver);
        Time.unscaledTime += .8f;
        driver.AdvanceHeldTick(true, .8f);
        float jump = Camera(driver) - before;
        Assert.True(jump <= 30, $"One delayed frame moves the ground {jump:0.0}px beneath the planted feet.");
    }

    [Theory]
    [InlineData(4096, 768)]
    [InlineData(0, 0)]
    public void BacktrackingRemainsHalfTheSamePhysicalSpeed(int width, int height)
    {
        var result = Replay(Ready(6, width, height), forward: false);
        Assert.InRange(result.Speed, -92.4f, -92.3f);
        Assert.InRange(result.MaximumSteps, 0, 1.3f);
    }

    [Fact]
    public void NormalFrameRateDoesNotChangeSustainedGroundTravel()
    {
        var thirty = Replay(Ready(6), dt: 1f / 30, frames: 36);
        var high = Replay(Ready(6), dt: 1f / 144, frames: 173);
        Assert.InRange(Math.Abs(thirty.Speed - high.Speed), 0, .02f);
    }

    [Fact]
    public void CrossingALogicalSquareRetainsTheExactPhysicalCarry()
    {
        var driver = Ready(6); Time.unscaledTime = 100;
        driver.SeedWalkProgress(.499f);
        float before = Camera(driver);
        Time.unscaledTime += 1f / 60;
        driver.AdvanceHeldTick(true, 1f / 60);
        Assert.Equal(3, driver.Expedition.TileIndex);
        Assert.InRange(driver.WalkProgress, -.5f, .5f);
        Assert.InRange(Camera(driver) - before, 3.077f, 3.079f);
    }

    [Fact]
    public void PhysicalWalkingStopsAtThePassageWithoutAutoEntryOrRepeatedPanning()
    {
        var driver = Ready(6); Time.unscaledTime = 100;
        driver.Expedition.TileIndex = 5;
        driver.SeedWalkProgress(.49f);
        driver.AdvanceHeldTick(true, .1f);
        Assert.False(driver.Expedition.InRoom);
        Assert.Equal(.5f, driver.WalkProgress);
        float atDoor = Camera(driver);
        driver.AdvanceHeldTick(true, .1f);
        Assert.Equal(atDoor, Camera(driver));
        Assert.False(driver.Expedition.InRoom);
    }
}
