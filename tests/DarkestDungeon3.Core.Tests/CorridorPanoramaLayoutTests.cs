using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class CorridorPanoramaLayoutTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(12)]
    public void WalkingAndDiscreteStepsUseTheSameContinuousPhysicalPositionInBothHeadings(int count)
    {
        for (int tile = 0; tile < count - 1; tile++)
        foreach (float fraction in new[] { 0f, .25f, .5f, .99f, 1 })
        {
            float forward = CorridorPanoramaLayout.Progress(tile, count, false, -720 * fraction);
            float reverse = CorridorPanoramaLayout.Progress(tile + 1, count, true, -720 * (1 - fraction));
            Assert.Equal(forward, reverse, 5);
            float stepForward = CorridorPanoramaLayout.Progress(tile + 1, count, false, 720 * (1 - fraction));
            Assert.Equal(forward, stepForward, 5);
            float stepReverse = CorridorPanoramaLayout.Progress(tile, count, true, 720 * fraction);
            Assert.Equal(forward, stepReverse, 5);
        }
        for (int tile = 0; tile < count; tile++)
            Assert.Equal(CorridorPanoramaLayout.Progress(tile, count, false, 0),
                CorridorPanoramaLayout.Progress(tile, count, true, 0));
    }

    [Fact]
    public void EntryExitOvershootIsBoundedAndSingleSquareHasNoArtificialMovement()
    {
        Assert.Equal(0, CorridorPanoramaLayout.Progress(0, 6, false, 720));
        Assert.Equal(1, CorridorPanoramaLayout.Progress(5, 6, false, -720));
        Assert.Equal(.5f, CorridorPanoramaLayout.Progress(0, 1, false, -720));
        Assert.Equal(.5f, CorridorPanoramaLayout.Progress(0, 1, true, 720));
    }
}
