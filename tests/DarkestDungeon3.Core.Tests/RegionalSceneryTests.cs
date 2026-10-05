using System.Text;
using System.Text.Json;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class RegionalSceneryTests
{
    [Fact]
    public void PrimarySceneryUsesRealNativeAddressKeysAndOneContinuousFloor()
    {
        var assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../game/Darkest Dungeon II_Data/StreamingAssets"));
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "aa/catalog.json")));
        var internalIds = catalog.RootElement.GetProperty("m_InternalIds").EnumerateArray().Select(value => value.GetString()).ToHashSet();
        string addressKeys = Encoding.UTF8.GetString(Convert.FromBase64String(catalog.RootElement.GetProperty("m_KeyDataString").GetString()!));
        foreach (string region in CampaignRegions.Primary)
        {
            var plan = RegionalScenery.For(region);
            Assert.NotNull(plan);
            Assert.InRange(plan.AssetKeys.Count(), 2, 4);
            foreach (string key in plan.AssetKeys)
            {
                Assert.Contains(key, addressKeys);
                if (key.StartsWith("Assets/")) Assert.Contains(key, internalIds);
                else Assert.Contains(internalIds, id => id!.EndsWith("/" + key + ".png"));
            }
            var ground = Assert.Single(plan.Layers.Where(layer => layer.Ground));
            Assert.True(ground.Top < 680 && ground.Top + ground.Height == 720);
            Assert.Equal(1, ground.Parallax);
            Assert.All(plan.Layers, layer => Assert.InRange(layer.Top + layer.Height, 1, 720));
        }
        Assert.Null(RegionalScenery.For("crypts"));
        Assert.Null(RegionalScenery.For("weald"));
        Assert.Null(RegionalScenery.For("dd2_cave"));
        Assert.Null(RegionalScenery.For(null));
    }

    [Fact]
    public void MirroredStripsCoverTheViewportWithoutGapsEvenAtNegativeCoordinates()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (float camera in new[] { -9732.5f, -1, 0, 600, 9824.5f })
        foreach (float width in new[] { 720f, 970f, 2440f })
        {
            var tiles = CorridorSceneryLayout.Tiles(camera, width, reverse).OrderBy(tile => tile.X).ToList();
            Assert.InRange(tiles.Count, 1, 5);
            Assert.True(tiles[0].X <= 0);
            Assert.True(tiles[^1].X + width >= CorridorSceneryLayout.ViewWidth);
            for (int index = 1; index < tiles.Count; index++)
            {
                Assert.Equal(tiles[index - 1].X + width, tiles[index].X, 2);
                Assert.NotEqual(tiles[index - 1].Mirror, tiles[index].Mirror);
            }
        }
    }

    [Fact]
    public void RetracingUsesTheSamePhysicalArtWithReversedScreenPlacement()
    {
        float camera = CorridorSceneryLayout.Camera(8, 3, false, 0);
        Assert.Equal(camera, CorridorSceneryLayout.Camera(8, 3, true, 0));
        var forward = CorridorSceneryLayout.Tiles(camera, 970, false).ToDictionary(tile => tile.Index);
        var backward = CorridorSceneryLayout.Tiles(camera, 970, true).ToDictionary(tile => tile.Index);
        foreach (int index in forward.Keys.Intersect(backward.Keys))
        {
            Assert.Equal(2 * CorridorSceneryLayout.Anchor - forward[index].X - 970, backward[index].X, 2);
            Assert.NotEqual(forward[index].Mirror, backward[index].Mirror);
        }
        // Discrete tile advancement with a full slide starts exactly where the previous square ended.
        Assert.Equal(camera, CorridorSceneryLayout.Camera(8, 4, false, 720));
        Assert.Equal(camera, CorridorSceneryLayout.Camera(8, 2, true, 720));
    }

    [Fact]
    public void OpeningsEaseIntoBothRoomsWithoutChangingWhenSquaresAdvance()
    {
        Assert.Equal(0, CorridorSceneryLayout.Opening(2, 6, false, 0));
        Assert.Equal(0.5f, CorridorSceneryLayout.Opening(0, 6, false, 0));
        Assert.Equal(0.5f, CorridorSceneryLayout.Opening(5, 6, false, 0));
        Assert.Equal(1, CorridorSceneryLayout.Opening(6, 6, false, 0));
        Assert.Equal(1, CorridorSceneryLayout.Opening(-1, 6, false, 0));
        float nearEnd = CorridorSceneryLayout.Opening(5, 6, false, -360);
        Assert.InRange(nearEnd, 0.5f, 1);
        Assert.Equal(nearEnd, CorridorSceneryLayout.Opening(5, 6, true, 360));
        Assert.Equal(CorridorSceneryLayout.Opening(4, 6, false, 0), CorridorSceneryLayout.Opening(5, 6, false, 720));
        Assert.Equal(0, CorridorSceneryLayout.Opening(0, 0, false, 0));
        Assert.Equal(0, CorridorSceneryLayout.Opening(0, 6, false, float.NaN));
    }

    [Fact]
    public void InvalidStripGeometryCannotCauseAnUnboundedDrawLoop()
    {
        Assert.Empty(CorridorSceneryLayout.Tiles(0, 0, false));
        Assert.Empty(CorridorSceneryLayout.Tiles(float.NaN, 720, false));
        Assert.Empty(CorridorSceneryLayout.Tiles(0, float.PositiveInfinity, false));
    }
}
