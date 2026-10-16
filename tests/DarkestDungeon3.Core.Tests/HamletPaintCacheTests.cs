using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaintCacheTests
{
    private static byte[] Tile(byte value) => Enumerable.Repeat(value, HamletPaintBaker.TileSize * HamletPaintBaker.TileSize * 4).ToArray();

    [Fact]
    public void ASecondTownWithIdenticalReadbacksReusesItsCompletedPaint()
    {
        var cache = new HamletPaintCache();
        var first = cache.GetOrBake(Tile(31), Tile(82), Tile(151), Tile(207), size: 32, fieldSize: 8);
        var second = cache.GetOrBake(Tile(31), Tile(82), Tile(151), Tile(207), size: 32, fieldSize: 8);
        Assert.Same(first, second);
        Assert.Equal(HamletPaintBaker.Bake(Tile(31), Tile(82), Tile(151), Tile(207), size: 32, fieldSize: 8).Lanes, second.Lanes);
    }

    [Fact]
    public void ChangedSourceBytesInvalidateEvenWhenTheArrayIdentityStaysTheSame()
    {
        var cache = new HamletPaintCache(); var earth = Tile(31); var stone = Tile(82);
        var first = cache.GetOrBake(earth, stone, null, stone, size: 32, fieldSize: 8);
        earth[0]++;
        var changed = cache.GetOrBake(earth, stone, null, stone, size: 32, fieldSize: 8);
        Assert.NotSame(first, changed);
        Assert.Same(changed, cache.GetOrBake(earth, stone, null, stone, size: 32, fieldSize: 8));
    }

    [Fact]
    public void TheCacheIsBoundedAndIncludesGrassAndPaintDimensions()
    {
        var cache = new HamletPaintCache(); var tile = Tile(72);
        var first = cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8);
        var grass = cache.GetOrBake(tile, tile, tile, tile, size: 32, fieldSize: 8);
        Assert.NotSame(first, grass);
        Assert.NotSame(first, cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8));
        Assert.Equal(16, cache.GetOrBake(tile, tile, null, tile, size: 16, fieldSize: 8).Size);
        var previous = cache.GetOrBake(tile, tile, null, tile, size: 16, fieldSize: 8);
        Assert.NotSame(previous, cache.GetOrBake(tile, tile, null, tile, size: 16, fieldSize: 4));
    }

    [Fact]
    public void CancelledRequestsCannotReuseOrDisplaceTheLastCompletedTown()
    {
        var cache = new HamletPaintCache(); var tile = Tile(72);
        var first = cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => cache.GetOrBake(tile, tile, null, tile, cancellation.Token, 32, 8));
        Assert.Throws<OperationCanceledException>(() => cache.GetOrBake(Tile(93), tile, null, tile, cancellation.Token, 32, 8));
        Assert.Same(first, cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8));
    }

    [Fact]
    public async Task OverlappingSceneRequestsShareOneCompletedBake()
    {
        var cache = new HamletPaintCache(); var tile = Tile(72);
        var towns = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() => cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8))));
        Assert.All(towns, town => Assert.Same(towns[0], town));
    }
}
