using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class CorridorProjectionTests
{
    [Fact]
    public void ACurioCannotSlideThreeTimesFasterThanTheFloorInTheMiddleOfAHall()
    {
        float a = CorridorSceneMotion.Camera(4096, 768, 2, 6, false, 0);
        float b = CorridorSceneMotion.Camera(4096, 768, 2, 6, false, -72);
        float prop = CorridorSceneMotion.PropOffset(4096, 768, 6, 0, -72);
        Assert.InRange(Math.Abs(prop + b - a), 0, .001f);
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(6, true)]
    [InlineData(12, false)]
    public void PropsAndPaintedGroundShareOneCameraAtEveryPartOfAHall(int count, bool reverse)
    {
        foreach (float fraction in new[] { -.45f, -.2f, 0, .2f, .45f })
        foreach (int tile in new[] { 0, count / 2, count - 1 })
        {
            const float move = .001f;
            float slide = -fraction * 720;
            float a = CorridorSceneMotion.Camera(4096, 768, tile, count, reverse, slide);
            float b = CorridorSceneMotion.Camera(4096, 768, tile, count, reverse, slide - move * 720);
            float propA = CorridorSceneMotion.PropOffset(4096, 768, count, 0, slide);
            float propB = CorridorSceneMotion.PropOffset(4096, 768, count, 0, slide - move * 720);
            Assert.True(b > a, $"Ground stopped in a {count}-square hall at tile{tile}, fraction{fraction}.");
            Assert.InRange(Math.Abs((propB - propA) + (b - a)), 0, .001f);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossingATileKeepsTheSameCurioFixedToTheSameGround(bool reverse)
    {
        const int count = 6;
        int before = reverse ? 4 : 1, after = reverse ? 3 : 2;
        float oldProp = CorridorSceneMotion.PropOffset(4096, 768, count, 1, -.5f * 720);
        float newProp = CorridorSceneMotion.PropOffset(4096, 768, count, 0, .5f * 720);
        Assert.Equal(oldProp, newProp, 3);
        Assert.Equal(CorridorSceneMotion.Camera(4096, 768, before, count, reverse, -.5f * 720),
            CorridorSceneMotion.Camera(4096, 768, after, count, reverse, .5f * 720), 3);
    }

    [Fact]
    public void LegacyWallsKeepTheirOriginalSquareSpacing()
    {
        Assert.Equal(630, CorridorSceneMotion.PropOffset(0, 0, 6, 1, -90));
        Assert.Equal(-810, CorridorSceneMotion.PropOffset(0, 0, 6, -1, -90));
    }
}
