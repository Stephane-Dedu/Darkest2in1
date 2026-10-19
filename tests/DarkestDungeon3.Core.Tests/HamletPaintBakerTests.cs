using System.Security.Cryptography;
using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintBakerTests
{
    // Deterministic RGBA tiles on the cinematic layout, including variable source alpha.
    // Footprint/door changes intentionally update these paint fixtures; source pixels remain immutable.
    [Theory]
    [InlineData(true, "6E30ED0271B14ACD0D92E54E2D735C4D57D5CC675A1D4BCEF6F105DDCFACD384",
        "3C092EA4A7A96B91E429FD430B60D5632AE93A20A45D7DB5DB669972A1ED3153",
        "2ECBD9E217F6E329FA21FD3FE0C4582DC7FC774AB76BE560BBFE283A2DAEEA01")]
    [InlineData(false, "95A222F3D469AAD20170B15DAF43A3BDF7EC429825A2665A9FC7DB2C22350164",
        "7CC3FD9111652C1ACDD2283BACE44406170578DC1B2A153AB8B92A296485FA05",
        "A025A87C1C03FA3FC8F5CE581CEDB129A31016787A62DBE49FE4546C56B4F18A")]
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
