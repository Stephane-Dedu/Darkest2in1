using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaletteTests
{
    [Theory]
    [InlineData(0,0,0)]
    [InlineData(19,63,121)]
    [InlineData(128,220,255)]
    public void NeutralGradingPreservesPaintWithinOneByte(byte r,byte g,byte b)
    {
        int rgb=HamletPalette.GradeRgb(r,g,b,new(1,1,1,0,1));
        Assert.InRange(Math.Abs((rgb>>16)-r),0,1);
        Assert.InRange(Math.Abs(((rgb>>8)&255)-g),0,1);
        Assert.InRange(Math.Abs((rgb&255)-b),0,1);
    }

    [Fact]
    public void CliffBlackIsLiftedWithoutClippingItsPaintedHighlights()
    {
        int black=HamletPalette.GradeRgb(0,0,0,HamletPalette.Rock);
        int lit=HamletPalette.GradeRgb(82,84,86,HamletPalette.Rock);
        Assert.InRange(black&255,40,55);
        Assert.Equal(black>>16,(black>>8)&255);
        Assert.True((lit&255)>(black&255)+20 && (lit&255)<180);
    }

    [Fact]
    public void DesaturationUsesLuminanceAndBrightnessStaysMonotonic()
    {
        int dull=HamletPalette.GradeRgb(220,60,20,new(1,1,1,0,0));
        Assert.Equal(dull>>16,(dull>>8)&255);
        Assert.Equal(dull&255,(dull>>8)&255);
        int lifted=HamletPalette.GradeRgb(220,60,20,new(1,1,1,.03f,0));
        Assert.True((lifted&255)>(dull&255));
    }
}
