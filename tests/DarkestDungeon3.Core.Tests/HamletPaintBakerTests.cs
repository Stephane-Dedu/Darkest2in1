using System.Security.Cryptography;
using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintBakerTests
{
    // Deterministic RGBA tiles on the cinematic layout, including variable source alpha.
    // Footprint/door changes intentionally update these paint fixtures; source pixels remain immutable.
    [Theory]
    [InlineData(true, "08D1CA1E36B698AFBCC6218EA745876C4BD964C354301639EC58AC9BF6000425",
        "55EF6DC12CF451939F4E6BD7DE37F02807A03086BD333483D3510AC311C7FD8E",
        "8D5EBFB5E3034CC802F9C1983A385081F8896B0D058A4352DD9DCC1AAB45D952")]
    [InlineData(false, "ACC5A591AE68097163C48B9F1F65DCF2067F58DFB5755444F4CDAC4EED7C4E3A",
        "D76DBC9A21EBF537316F870C6CFA0BA1885A1FE0EC63696E7AA858D13D0398F0",
        "686B73FE5C66949DC91D14849F03723CEA7799FF128B797C0F46E6FC14EFC474")]
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
