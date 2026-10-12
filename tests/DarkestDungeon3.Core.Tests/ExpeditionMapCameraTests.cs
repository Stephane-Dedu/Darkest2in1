using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionMapCameraTests
{
    private static ExpeditionMap3D.Spot Region(string location, float x, float y, float scale = 2.2f) => new()
        { Location = location, HubX = x, HubY = y, ZoomHubX = 1030, ZoomHubY = 650, ZoomScale = scale };

    private static void Covered(ExpeditionMap3D.MapRect r)
    {
        Assert.True(r.X <= 0 && r.Y <= 0);
        Assert.True(r.X + r.Width >= 1919.99f && r.Y + r.Height >= 1079.99f);
    }

    [Fact]
    public void SelectingAnotherQuestDuringAnApproachKeepsTheCurrentFrame()
    {
        var camera = new ExpeditionMapCamera();
        camera.Focus(Region("warrens", 684, 507));
        for (int i = 0; i < 12; i++) camera.Step(1 / 60f);
        var before = camera.Frame;
        camera.Focus(Region("cove", 1331, 536, 1.812f));
        Assert.Equal(before, camera.Frame);
        camera.Step(1 / 60f);
        Assert.InRange(Math.Abs(before.X - camera.Frame.X), 0, 50);
        Assert.True(camera.Frame.Width > 1920); // It did not restart at the overview.
        Assert.Equal("cove", camera.Location);
        for (int i = 0; i < 180; i++) { camera.Step(1 / 60f); Covered(camera.Frame); }
        var target = ExpeditionMap3D.OverviewRect(Region("cove", 1331, 536, 1.812f), 1);
        Assert.Equal(target.X, camera.Frame.X, 2);
        Assert.Equal(target.Y, camera.Frame.Y, 2);
        Assert.Equal(target.Width, camera.Frame.Width, 2);
    }

    [Fact]
    public void ReversingAndChoosingTheSameRegionDoNotRestartTheCamera()
    {
        var camera = new ExpeditionMapCamera();
        var region = Region("crypts", 896, 367, 1.999f);
        camera.Focus(region);
        for (int i = 0; i < 20; i++) camera.Step(1 / 60f);
        var current = camera.Frame;
        camera.Focus(region);
        Assert.Equal(current, camera.Frame);
        camera.Overview();
        Assert.Equal(current, camera.Frame);
        Assert.True(camera.IsOverview);
        for (int i = 0; i < 180; i++) { camera.Step(1 / 60f); Covered(camera.Frame); }
        Assert.Equal((0f, 0f, 1920f, 1080f), (camera.Frame.X, camera.Frame.Y, camera.Frame.Width, camera.Frame.Height));
    }

    [Fact]
    public void FrameRateDoesNotChangeTheApproach()
    {
        var slow = new ExpeditionMapCamera(); var fast = new ExpeditionMapCamera();
        slow.Focus(Region("weald", 821, 774, 1.468f)); fast.Focus(Region("weald", 821, 774, 1.468f));
        for (int i = 0; i < 12; i++) slow.Step(1 / 30f);
        for (int i = 0; i < 48; i++) fast.Step(1 / 120f);
        Assert.Equal(slow.Frame.X, fast.Frame.X, 2);
        Assert.Equal(slow.Frame.Y, fast.Frame.Y, 2);
        Assert.Equal(slow.Frame.Width, fast.Frame.Width, 2);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidTimeCannotMoveOrCorruptTheView(float dt)
    {
        var camera = new ExpeditionMapCamera(); camera.Focus(Region("cove", 1331, 536));
        var frame = camera.Frame; camera.Step(dt);
        Assert.Equal(frame, camera.Frame);
    }

    [Fact]
    public void RapidAlternationAndLongFramesNeverUncoverTheCanvas()
    {
        var camera = new ExpeditionMapCamera();
        for (int i = 0; i < 100; i++)
        {
            camera.Focus(i % 2 == 0 ? Region("crypts", 896, 367, 1.999f) : Region("weald", 821, 774, 1.468f));
            camera.Step(i % 3 == 0 ? 10 : 1 / 60f);
            Covered(camera.Frame);
        }
        camera.Reset();
        Assert.True(camera.IsOverview);
        Assert.Equal(1920f, camera.Frame.Width);
    }

    [Fact]
    public void SceneryHitRegionsUseTheSameCameraAsTheBackground()
    {
        var camera = new ExpeditionMapCamera(); camera.Focus(Region("cove", 1331, 536));
        for (int i = 0; i < 60; i++) camera.Step(1 / 60f);
        var rect = new ExpeditionMap3D.MapRect(1200, 430, 200, 130);
        var transformed = ExpeditionMapCamera.Transform(rect, camera.Frame);
        float scale = camera.Frame.Width / 1920;
        Assert.True(transformed.Contains(camera.Frame.X + 1331 * scale, camera.Frame.Y + 536 * scale));
        Assert.False(transformed.Contains(rect.X, rect.Y));
    }
}
