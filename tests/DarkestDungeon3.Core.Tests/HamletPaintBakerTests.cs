using System.Security.Cryptography;
using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintBakerTests
{
    // Deterministic RGBA tiles on the cinematic layout, including variable source alpha.
    // Footprint/door/illumination changes intentionally update these fixtures; source pixels remain immutable.
    [Theory]
    [InlineData(true, "27A25A22CA43FB00865B0326FCBC1BC69380DA105F6756855CE9C3D2802B597D",
        "242F42D8B492D5DA7EF7112F6E657FF603706BC26355AA70C9C14546C2F60820",
        "8548D720F407636B42498DFAB8C1CBBF4BAE296182DA25DFE5EEFD62ADD28134")]
    [InlineData(false, "46C54BDC16924B0D597C46DF8216F05F00AAF17B13A092A4600713C0DDAD38B4",
        "43B73759BE21BD5CB2987C15DA756BEEF50FDD6A96E4F038134EE4AB70C09BAA",
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
    public void PavedApproachesKeepContactShadeAndOpenRoadsStayBright()
    {
        const int size = 256;
        var white = Enumerable.Repeat((byte)255, HamletPaintBaker.TileSize * HamletPaintBaker.TileSize * 4).ToArray();
        var paint = HamletPaintBaker.Bake(white, white, null, white, size: size, fieldSize: 256);
        int shaded = 0, open = 0;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float px = HamletLayout.MinX + (x + .5f) / size * (HamletLayout.MaxX - HamletLayout.MinX);
            float pz = HamletLayout.MinZ + (y + .5f) / size * (HamletLayout.MaxZ - HamletLayout.MinZ);
            if (HamletGroundPaint.LaneCover(px, pz) < .999f || HamletSetDressing.EntranceLight(px, pz) > 0) continue;
            int i = (y * size + x) * 4;
            float contact = HamletGroundPaint.ContactShade(px, pz);
            if (contact < .80f)
            {
                // White paving beside a foundation must keep its contact shadow.
                Assert.InRange(paint.Lanes[i], 0, 220);
                shaded++;
            }
            else if (contact > .999f && Math.Abs(px + 40) < .7f && Math.Abs(pz + 30) < .7f)
            {
                // The centre of the southern approach is clear of foundations
                // and of the interpolated road margins.
                Assert.InRange(paint.Lanes[i], 250, 255);
                open++;
            }
        }
        Assert.True(shaded > 10, "Check actual paved building approaches.");
        Assert.True(open > 4, "Check the unaffected open southern road as well.");
    }

    [Fact]
    public void DoorwayLightReachesFullyPavedStone()
    {
        const int size = 256;
        var black = new byte[HamletPaintBaker.TileSize * HamletPaintBaker.TileSize * 4];
        var paint = HamletPaintBaker.Bake(black, black, null, black, size: size, fieldSize: 256);
        int lit = 0;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float px = HamletLayout.MinX + (x + .5f) / size * (HamletLayout.MaxX - HamletLayout.MinX);
            float pz = HamletLayout.MinZ + (y + .5f) / size * (HamletLayout.MaxZ - HamletLayout.MinZ);
            if (HamletGroundPaint.LaneCover(px, pz) < .999f || HamletSetDressing.EntranceLight(px, pz) < .04f) continue;
            int i = (y * size + x) * 4;
            Assert.True(paint.Lanes[i] > paint.Lanes[i + 1] && paint.Lanes[i + 1] > paint.Lanes[i + 2],
                "Warm entry light must survive complete stone coverage.");
            lit++;
        }
        Assert.True(lit > 5, "Check actual paved entrances inside the native lamp pools.");
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
