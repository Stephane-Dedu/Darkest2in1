using System.Security.Cryptography;
using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintBakerTests
{
    // Portable reference run of the original GroundPaint/ValleyPaint loops with deterministic RGBA tiles,
    // including variable source alpha. The worker must preserve colour, world projection and lane blending.
    [Theory]
    [InlineData(true, "6886AAD111A88DF81D5700C817EA232743BB87F7D9B5C8DF0E24B565D279B786",
        "8D841C40E97C1B88DBC0200253D308DABB1FD4F3EFE1F6907C7C54D1E6952A5B",
        "445ED5643A84FE5C5DA963825769E79208381D08D4C1C4FC102436394496F7D5")]
    [InlineData(false, "F37E04EC288111E175DB5E2FB576CF76ABFC49874169B8F87E1C9E3DB4F3694C",
        "FCBF4E3D5179D8C2E95B07C11CD726E211EDE7F7BED216C99405E700FF9B6C7B",
        "F4A1C31604FCB0403117FB0951F1D9E2D46B545A6108C693C718044A01EAAB48")]
    public async Task WorkerPreservesTheExistingPaint(bool grass, string ground, string lanes, string valley)
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
