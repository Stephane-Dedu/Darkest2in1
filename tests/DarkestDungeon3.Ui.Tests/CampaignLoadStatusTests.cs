using DarkestDungeon3.Runtime;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CampaignLoadStatusTests
{
    [Theory]
    [InlineData(.9f, 12, 20, 13)]
    [InlineData(1f, 11, 20, 13)]
    [InlineData(1f, 12, 19, 13)]
    [InlineData(1f, 12, 20, 12)]
    public void OnePendingResourceKeepsEstateSelectionClosed(float content, int images, int buildings, int heroes)
    {
        var status = new CampaignLoadStatus(content, images, 12, buildings, 20, heroes, 13, "Reading quests");
        Assert.False(status.Ready);
        Assert.True(status.Progress < 1);
        Assert.NotEqual("The Hamlet is ready", status.Label);
    }

    [Fact]
    public void ContentAloneDoesNotSkipUnrequestedAssets()
    {
        var status = new CampaignLoadStatus(1, 0, 0, 0, 0, 0, 0, "Campaign data ready");
        Assert.False(status.Ready);
        Assert.Equal(.25f, status.Progress);
    }

    [Fact]
    public void CompletedWarmupCanOpenImmediatelyWithoutAnArtificialDelay()
    {
        var status = new CampaignLoadStatus(1, 12, 12, 20, 20, 13, 13, "Campaign data ready");
        Assert.True(status.Ready);
        Assert.Equal(1, status.Progress);
        Assert.Equal("The Hamlet is ready", status.Label);
    }

    [Fact]
    public void FailureBlocksSelectionEvenWithCachedAssetsAndRecoveryUsesFreshStatus()
    {
        var failed = new CampaignLoadStatus(1, 12, 12, 20, 20, 13, 13, "", "Hero resources failed");
        Assert.False(failed.Ready);
        Assert.Equal("Hero resources failed", failed.Error);
        var retried = new CampaignLoadStatus(1, 12, 12, 20, 20, 13, 13, "");
        Assert.True(retried.Ready);
        Assert.Null(retried.Error);
    }
}
