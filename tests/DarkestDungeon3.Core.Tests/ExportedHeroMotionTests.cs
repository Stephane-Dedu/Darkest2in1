using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExportedHeroMotionTests
{
    [Theory]
    [InlineData(.55f, 1.2f, 2.6f)]
    [InlineData(.5f, 1.4f, 3.066667f)]
    public void PhaseChangesContinueTheMotionInsteadOfReplayingItsOpening(float windup, float strike, float duration)
    {
        float At(string part, float progress) => ExportedHeroMotion.TimeInClip(part, progress, windup, strike, duration);
        Assert.Equal(0, At("antic", 0));
        Assert.Equal(At("antic", 1), At("idle", .4f));
        Assert.Equal(At("antic", 1), At("action", 0));
        Assert.Equal(At("action", 1), At("recover", 0));
        Assert.Equal(duration, At("recover", 1));
        Assert.Equal(strike, At("action", 4));
        Assert.Equal(windup, At("action", -1));
        Assert.Equal(windup, At("action", float.NaN));
        Assert.InRange(At("action", .5f), windup, strike);
    }
}
