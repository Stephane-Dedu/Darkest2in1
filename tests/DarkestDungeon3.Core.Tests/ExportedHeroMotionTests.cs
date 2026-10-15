using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExportedHeroMotionTests
{
    [Fact]
    public void NativeWalkClockFreezesAndReversesAtItsExistingPoseInsteadOfFlippingTheLoop()
    {
        float first = ExportedHeroMotion.AdvanceWalk(.4f,10,100,1.2f);
        Assert.Equal(.5f,first,5);
        Assert.Equal(first,ExportedHeroMotion.AdvanceWalk(first,0,100,1.2f));
        Assert.Equal(.45f,ExportedHeroMotion.AdvanceWalk(first,-5,100,1.2f),5);
        Assert.Equal(1.1f,ExportedHeroMotion.AdvanceWalk(.1f,-20,100,1.2f),5);
        Assert.Equal(.2f,ExportedHeroMotion.AdvanceWalk(1.1f,30,100,1.2f),5);
    }

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
