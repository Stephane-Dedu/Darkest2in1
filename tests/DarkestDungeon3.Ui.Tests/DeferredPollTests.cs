using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class DeferredPollTests
{
    [Fact]
    public void SlowArenaLoadDoesNotSpendEnemyBindingTimeout()
    {
        var poll = new DeferredPoll(0.1f, 8f);
        for (int frame = 0; frame < 3600; frame++) Assert.False(poll.Due(frame / 60f, false));
        Assert.False(poll.Expired(60));
        Assert.True(poll.Due(60, true));
        Assert.False(poll.Expired(67.99f));
        Assert.True(poll.Expired(68));
        poll.Reset();
        Assert.False(poll.Expired(200));
        Assert.True(poll.Due(200, true));
        Assert.False(poll.Expired(207));
    }

    [Fact]
    public void ReadinessRetriesAreBoundedAcrossHighFrameRates()
    {
        var poll = new DeferredPoll(0.1f);
        int scans = 0;
        for (int frame = 0; frame < 600; frame++) if (poll.Due(frame / 120f, true)) scans++;
        Assert.InRange(scans, 45, 50);
        Assert.False(poll.Expired(500));
        poll.Reset();
        Assert.True(poll.Due(500, true));
    }
}
