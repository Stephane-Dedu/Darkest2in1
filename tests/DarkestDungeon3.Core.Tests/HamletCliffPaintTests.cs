using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletCliffPaintTests
{
    [Fact]
    public void SquarePrivatePaintKeepsWorldTexelDensityAndNativeFallbackGeometry()
    {
        var native = HamletTerrain.Build(4);
        var full = HamletTerrain.Build(4, fullCliffTile: true);
        Assert.Equal(native.Positions, full.Positions);
        Assert.Equal(native.Ground, full.Ground);
        Assert.Equal(native.Rock, full.Rock);
        Assert.Equal(native.Shade, full.Shade);
        foreach (int i in native.Ground)
        {
            Assert.Equal(native.Uv[i*2], full.Uv[i*2]);
            Assert.Equal(native.Uv[i*2+1], full.Uv[i*2+1]);
        }
        Check(native.Positions, native.Rock, native.Uv, full.Uv);
        var oldRocks = HamletSetDressing.BuildRocks();
        var newRocks = HamletSetDressing.BuildRocks(fullCliffTile: true);
        Assert.Equal(oldRocks.Positions, newRocks.Positions);
        Assert.Equal(oldRocks.Indices, newRocks.Indices);
        Check(oldRocks.Positions, oldRocks.Indices, oldRocks.Uv, newRocks.Uv);
    }

    private static void Check(float[] positions, int[] indices, float[] native, float[] full)
    {
        foreach (int i in indices)
        {
            // One entire square painting spans the same world length in both axes.
            float x = positions[i*3], y = positions[i*3+1], z = positions[i*3+2];
            Assert.InRange(Math.Abs(full[i*2]-(x+z*.5f)/HamletPalette.RockPeriod), 0, .00001f);
            Assert.InRange(Math.Abs(full[i*2+1]-y/HamletPalette.RockPeriod), 0, .00001f);
            Assert.InRange(Math.Abs(native[i*2+1]*HamletPalette.RockTexelAspect-full[i*2+1]), 0, .00001f);
        }
    }
}
