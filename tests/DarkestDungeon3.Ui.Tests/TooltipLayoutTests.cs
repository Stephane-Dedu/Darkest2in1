using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class TooltipLayoutTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 0)]
    [InlineData(1919, 1079)]
    [InlineData(0, 1079)]
    public void TooltipStaysInsideVirtualScreenAtEveryCorner(float x, float y)
    {
        var layout = new TooltipLayout(new Vector2(x, y), 460, 420, 1920, 1080);
        Assert.InRange(layout.Frame.x, 8, 1920 - 460 - 8);
        Assert.InRange(layout.Frame.y, 8, 1080 - layout.Frame.height - 8);
        Assert.Equal(420, layout.Viewport.height);
        Assert.Equal(0, layout.MaxScroll);
    }

    [Fact]
    public void TallDescriptionKeepsItsFontSizeAndScrollsToTheLastEffect()
    {
        var layout = new TooltipLayout(new Vector2(1800, 1000), 640, 2200, 1920, 1080);
        Assert.Equal(1064, layout.Frame.height);
        Assert.Equal(998, layout.Viewport.height);
        Assert.Equal(1202, layout.MaxScroll);
        Assert.Equal(layout.MaxScroll, layout.Scroll(0, 999, 30));
        Assert.Equal(2200, layout.Scroll(0, 999, 30) + layout.Viewport.height);
        Assert.Equal(0, layout.Scroll(layout.MaxScroll, -999, 30));
        Assert.Equal(60, layout.Scroll(0, 1, 30));
    }

    [Fact]
    public void WrappingExpandsBeforeAddingScrollAndStopsAtTheReadableWidth()
    {
        float width = TooltipLayout.Width(w => w < 480 ? 1500 : 800, 1080);
        Assert.Equal(520, width);
        Assert.Equal(640, TooltipLayout.Width(_ => 2000, 1080));
        Assert.Equal(460, TooltipLayout.Width(_ => 200, 1080));
    }

    [Theory]
    [InlineData(500, 592)]
    [InlineData(1400, 920)]
    public void SlotAnchoredTipUsesTheAvailableSideWithoutCoveringTheSlot(float x, float expected)
    {
        var slot = new Rect(x, 300, 72, 144);
        var tip = new TooltipLayout(slot, 460, 420, 1920, 1080);
        Assert.Equal(expected, tip.Frame.x);
        Assert.Equal(300, tip.Frame.y);
        Assert.True(tip.Frame.xMax <= slot.x || tip.Frame.x >= slot.xMax);
        Assert.Equal(tip.Frame.x + 20, tip.Viewport.x);
    }
}
