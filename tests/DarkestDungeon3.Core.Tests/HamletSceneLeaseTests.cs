using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class HamletSceneLeaseTests
{
    [Fact]
    public void QuestSelectionKeepsTownWithoutRenderingAndReturnsImmediately()
    {
        var lease = new HamletSceneLease();
        lease.Request(100, visible: true);
        Assert.Equal(HamletSceneActivity.Render, lease.Activity(101));
        for (int frame = 102; frame < 702; frame++)
        {
            lease.Request(frame, visible: false);
            Assert.Equal(HamletSceneActivity.Park, lease.Activity(frame + 1));
        }
        lease.Request(702, visible: true);
        Assert.Equal(HamletSceneActivity.Render, lease.Activity(702));
    }

    [Fact]
    public void ActualDepartureReleasesTownAfterGraceFrames()
    {
        var lease = new HamletSceneLease();
        lease.Request(40, visible: false);
        Assert.Equal(HamletSceneActivity.Park, lease.Activity(43));
        Assert.Equal(HamletSceneActivity.Release, lease.Activity(44));
    }

    [Fact]
    public void LastUseWinsWithinOneFrameAndResetDropsLease()
    {
        var lease = new HamletSceneLease();
        lease.Request(40, visible: true);
        lease.Request(40, visible: false);
        Assert.Equal(HamletSceneActivity.Park, lease.Activity(40));
        lease.Request(40, visible: true);
        Assert.Equal(HamletSceneActivity.Render, lease.Activity(40));
        lease.Reset();
        Assert.Equal(HamletSceneActivity.Release, lease.Activity(40));
    }
}
