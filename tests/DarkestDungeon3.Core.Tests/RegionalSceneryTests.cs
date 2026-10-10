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
    public void GeneratedRoomChoicesStayStableAndAlternateWithoutGameplayRng()
    {
        foreach (string region in CampaignRegions.Primary)
        foreach (int seed in new[] { int.MinValue, -1, 0, 2719, int.MaxValue })
        {
            var plan = RegionalScenery.For(region)!;
            Assert.Equal(2, plan.RoomBackgrounds.Count);
            Assert.Null(plan.RoomBackground(seed, -1));
            var choices = Enumerable.Range(0, 12).Select(room => plan.RoomBackground(seed, room)).ToArray();
            Assert.Equal(4, choices.Select(choice => (choice.FileName, choice.Mirror)).Distinct().Count());
            for (int room = 0; room < choices.Length; room++)
            {
                var revisited = plan.RoomBackground(seed, room);
                Assert.Equal(choices[room].FileName, revisited.FileName);
                Assert.Equal(choices[room].Mirror, revisited.Mirror);
                Assert.Contains(revisited.FileName, plan.RoomBackgrounds);
                Assert.DoesNotContain("/", revisited.FileName);
                if (room > 0) Assert.NotEqual(choices[room - 1].FileName, revisited.FileName);
            }
        }
    }

    [Fact]
    public void GeneratedRoomAssetsAreWideOpaquePngsAtTheSharedSceneRatio()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data/scenery"));
        foreach (string region in CampaignRegions.Primary)
        foreach (string file in RegionalScenery.For(region)!.RoomBackgrounds)
        {
            byte[] data = File.ReadAllBytes(Path.Combine(folder, file));
            Assert.InRange(data.Length, 24, 16 * 1024 * 1024);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, data.Take(8));
            int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            Assert.InRange(width, 1920, 4096);
            Assert.InRange(height, 720, 2048);
            Assert.InRange((double)width / height, 2.6, 2.7);
            Assert.Contains(data[25], new byte[] { 2, 6 }); // RGB or RGBA; opacity is inspected in the generated outputs.
        }
    }

    [Fact]
    public void InvalidStripGeometryCannotCauseAnUnboundedDrawLoop()
    {
        Assert.Empty(CorridorSceneryLayout.Tiles(0, 0, false));
        Assert.Empty(CorridorSceneryLayout.Tiles(float.NaN, 720, false));
        Assert.Empty(CorridorSceneryLayout.Tiles(0, float.PositiveInfinity, false));
    }
}
