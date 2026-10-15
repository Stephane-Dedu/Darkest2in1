using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class PrivateCorridorLayersTests
{
    [Theory]
    [InlineData("dd2_forest-corridor-01.png", 0, "dd2_forest-corridor-01.depth-back.png")]
    [InlineData("dd2_coast-corridor-06.png", 1, "dd2_coast-corridor-06.depth-middle.png")]
    [InlineData("dd2_city-corridor-12.png", 2, "dd2_city-corridor-12.depth-front.png")]
    [InlineData("../dd2_city-corridor-01.png", 0, null)]
    [InlineData("dd2_city-corridor-01.png/other.png", 0, null)]
    [InlineData("dd2_city-corridor-00.png", 0, null)]
    [InlineData("dd2_city-corridor-1.png", 0, null)]
    [InlineData("dd2_city-arena-01.png", 0, null)]
    [InlineData("dd2_city-corridor-01.png", 3, null)]
    public void OnlyCanonicalPrivateCorridorsHaveDepthCompanions(string scene, int layer, string? expected) =>
        Assert.Equal(expected, PrivateCorridorLayers.FileName(scene, layer));

    [Fact]
    public void LayerDimensionsMustMatchTheAuthoredCameraAndDecodedBudget()
    {
        var png = new byte[33];
        new byte[] { 137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82 }.CopyTo(png,0);
        png[16] = 0; png[17] = 0; png[18] = 16; png[19] = 0;
        png[20] = 0; png[21] = 0; png[22] = 3; png[23] = 0;
        png[24] = 8; png[25] = 6;
        Assert.True(PrivateCorridorLayers.ValidPng(png));
        Assert.Equal(36 * 1024 * 1024, PrivateCorridorLayers.MaxDecodedBytes);
        png[23] = 1;
        Assert.False(PrivateCorridorLayers.ValidPng(png));
        png[23] = 0; png[18] = 8;
        Assert.False(PrivateCorridorLayers.ValidPng(png));
        Assert.False(PrivateCorridorLayers.ValidPng(null!));
    }

    [Fact]
    public void DepthsShareTheCentreAndFootlineAndHaveBoundedDifferentTravel()
    {
        var distances = new float[3];
        for (int layer = 0; layer < 3; layer++)
        {
            Assert.Equal(.5f, CorridorPanoramaLayout.LayerProgress(.5f, layer));
            var start = CorridorPanoramaLayout.Crop(4096, 768, CorridorPanoramaLayout.LayerProgress(0, layer));
            var end = CorridorPanoramaLayout.Crop(4096, 768, CorridorPanoramaLayout.LayerProgress(1, layer));
            Assert.Equal(start.Y, end.Y);
            Assert.True(start.X >= 0 && end.X + end.Width <= 1.00001f);
            distances[layer] = end.X - start.X;
            Assert.Equal(CorridorPanoramaLayout.LayerProgress(0, layer), CorridorPanoramaLayout.LayerProgress(-1, layer));
            Assert.Equal(CorridorPanoramaLayout.LayerProgress(1, layer), CorridorPanoramaLayout.LayerProgress(2, layer));
        }
        Assert.True(distances[0] < distances[1] && distances[1] < distances[2]);
        Assert.Equal(0, CorridorPanoramaLayout.LayerProgress(0, 2));
        Assert.Equal(1, CorridorPanoramaLayout.LayerProgress(1, 2));
    }
}
