using System.Security.Cryptography;
using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintBakerTests
{
    // Deterministic RGBA tiles on the cinematic layout, including variable source alpha.
    // Footprint/door changes intentionally update these paint fixtures; source pixels remain immutable.
    [Theory]
    [InlineData(true, "27A25A22CA43FB00865B0326FCBC1BC69380DA105F6756855CE9C3D2802B597D",
        "7FFF8E80E30B9A52F5668A43DFE1B97545C8ABDE6C5C12E222E7BC1D77ACD36C",
        "8548D720F407636B42498DFAB8C1CBBF4BAE296182DA25DFE5EEFD62ADD28134")]
    [InlineData(false, "46C54BDC16924B0D597C46DF8216F05F00AAF17B13A092A4600713C0DDAD38B4",
        "E1275335FF351153B93B655AA129BE2767713416FAB450630EBFF200FF55B659",
        "7B5E7D410AA1AB66AB27D70D09DD56A69848F6DF7AF8059F84909EC47F449B7F")]
    public async Task WorkerPreservesTheCinematicLayoutPaint(bool grass, string ground, string lanes, string valley)
    {
        var soil = Tile(11); var stone = Tile(63); var turf = grass ? Tile(123) : null; var rock = Tile(209);
        var baked = await Task.Run(() => HamletPaintBaker.Bake(soil, stone, turf, rock, size: 64, fieldSize: 16));
        Assert.Equal(64, baked.Size);
        Assert.Equal(ground, Digest(baked.Ground)); Assert.Equal(lanes, Digest(baked.Lanes)); Assert.Equal(valley, Digest(baked.Valley));
        Assert.Equal(Tile(11), soil); Assert.Equal(Tile(63), stone); Assert.Equal(Tile(209), rock);
        if (grass) Assert.Equal(Tile(123), turf);
    }

    [Fact]
    public void LaneMarginsReuseTheExactGroundPixels()
    {
        const int size=96;
        var paint=HamletPaintBaker.Bake(Tile(11),Tile(63),Tile(123),Tile(209),size:size,fieldSize:64);
        int equal=0,different=0;
        for (int i=0;i<paint.Ground.Length;i+=4)
        {
            bool same=paint.Ground.AsSpan(i,4).SequenceEqual(paint.Lanes.AsSpan(i,4));
            if (same) equal++; else different++;
        }
        Assert.True(equal>size*size*.7, "Most of the lane image must exactly match the underlying ground.");
        Assert.True(different>20, "Actual roads must retain visible stone paint.");
    }

    [Fact]
    public void AnAbandonedTownCanCancelBeforePainting()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var tile = Tile(11);
        Assert.Throws<OperationCanceledException>(() => HamletPaintBaker.Bake(tile, tile, null, tile, cancelled.Token));
    }

    [Fact]
    public void WorkerRejectsIncompleteReadbacksAndUnsafeDimensions()
    {
        var tile = Tile(11);
        Assert.Throws<ArgumentException>(() => HamletPaintBaker.Bake(new byte[4], tile, null, tile));
        Assert.Throws<ArgumentException>(() => HamletPaintBaker.Bake(tile, tile, new byte[4], tile));
        Assert.Throws<ArgumentOutOfRangeException>(() => HamletPaintBaker.Bake(tile, tile, null, tile, size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HamletPaintBaker.Bake(tile, tile, null, tile, fieldSize: 1));
    }

    private static byte[] Tile(int seed) => Enumerable.Range(0, HamletPaintBaker.TileSize * HamletPaintBaker.TileSize * 4)
        .Select(i => (byte)((i * 73 + i / 2048 * 19 + seed) % 256)).ToArray();
    private static string Digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}
