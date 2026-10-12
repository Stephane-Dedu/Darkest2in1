using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionMap3DTests
{
    private const string Valid = """
        { "version": 1, "spots": {
          "warrens": { "hub": [684, 507], "label": [560, 540], "click": [540, 330, 330, 210],
                       "zoomScale": 2.2, "zoomHub": [1030, 650], "zoomLabel": [890, 700] },
          "cove": { "hub": [1360, 579], "label": [1230, 600], "click": [1160, 420, 370, 180],
                    "zoomScale": 2.1, "zoomHub": [1030, 650], "zoomLabel": [890, 700] } } }
        """;

    [Fact]
    public void ParsesPositionsKeyedByMapLocation()
    {
        var map = ExpeditionMap3D.Parse(Valid);
        Assert.NotNull(map);
        Assert.Equal(new[] { "cove", "warrens" }, map.Spots.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var farm = map.Spots["warrens"];
        Assert.Equal("warrens", farm.Location);
        Assert.Equal((684f, 507f, 1030f, 650f), (farm.HubX, farm.HubY, farm.ZoomHubX, farm.ZoomHubY));
        Assert.Equal((560f, 540f, 890f, 700f), (farm.LabelX, farm.LabelY, farm.ZoomLabelX, farm.ZoomLabelY));
        Assert.Equal(2.2f, farm.ZoomScale, 3);
        Assert.True(farm.Click.Contains(700, 400));
        Assert.False(farm.Click.Contains(539, 400));
        Assert.False(farm.Click.Contains(870, 400));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"spots\": [] }")]
    [InlineData("{ \"spots\": {} }")]
    public void TextWithoutAnyPositionIsNoLayout(string json) => Assert.Null(ExpeditionMap3D.Parse(json));

    [Theory]
    [InlineData("\"hub\": [684, 507]", "\"hub\": [684]")]
    [InlineData("\"hub\": [684, 507]", "\"hub\": [-4, 507]")]
    [InlineData("\"hub\": [684, 507]", "\"hub\": [\"684\", 507]")]
    [InlineData("\"label\": [560, 540]", "\"label\": [560, 1200]")]
    [InlineData("\"zoomScale\": 2.2", "\"zoomScale\": 1.0")]
    [InlineData("\"zoomScale\": 2.2", "\"zoomScale\": 40")]
    [InlineData("\"zoomScale\": 2.2", "\"zoomScale\": \"big\"")]
    [InlineData("\"click\": [540, 330, 330, 210]", "\"click\": [540, 330, 0, 210]")]
    [InlineData("\"click\": [540, 330, 330, 210]", "\"click\": [1700, 330, 330, 210]")]
    [InlineData("\"click\": [540, 330, 330, 210]", "\"click\": [540, 330, 330]")]
    [InlineData("\"zoomHub\": [1030, 650], \"zoomLabel\": [890, 700] },", "\"zoomHub\": [1030, 650] },")]
    public void MalformedPositionIsDroppedAndTheOthersKept(string from, string to)
    {
        Assert.Contains(from, Valid);
        var map = ExpeditionMap3D.Parse(Valid.Replace(from, to));
        Assert.NotNull(map);
        Assert.Equal(new[] { "cove" }, map.Spots.Keys);
    }

    [Fact]
    public void OverviewStartsFullScreenAndEndsAlignedWithTheZoomedFrame()
    {
        var spot = ExpeditionMap3D.Parse(Valid).Spots["warrens"];
        var start = ExpeditionMap3D.OverviewRect(spot, 0);
        Assert.Equal((0f, 0f, 1920f, 1080f), (start.X, start.Y, start.Width, start.Height));

        var end = ExpeditionMap3D.OverviewRect(spot, 1);
        Assert.Equal(1920 * 2.2f, end.Width, 2);
        Assert.Equal(1080 * 2.2f, end.Height, 2);
        // The region's ground point lands where it stands in the zoomed frame.
        Assert.Equal(1030f, end.X + spot.HubX * 2.2f, 2);
        Assert.Equal(650f, end.Y + spot.HubY * 2.2f, 2);
    }

    [Fact]
    public void TheMoveOnlyEverGrowsAndCoversTheScreen()
    {
        var spot = ExpeditionMap3D.Parse(Valid).Spots["cove"];
        float width = 0;
        for (int i = 0; i <= 100; i++)
        {
            var r = ExpeditionMap3D.OverviewRect(spot, i / 100f);
            Assert.True(r.Width >= width);
            width = r.Width;
            Assert.InRange(r.Height - r.Width * 1080 / 1920, -0.01f, 0.01f);
            Assert.True(r.X <= 0.01f && r.Y <= 0.01f, $"gap at the top or left at {i}%");
            Assert.True(r.X + r.Width >= 1919.99f && r.Y + r.Height >= 1079.99f, $"gap at the bottom or right at {i}%");
        }
    }

    [Fact]
    public void RegionNearTheTopKeepsTheScreenCoveredInsteadOfReachingItsZoomedPosition()
    {
        const string mountain = """
            { "spots": { "darkestdungeon": { "hub": [1176, 301], "label": [1040, 150], "click": [900, 120, 500, 200],
                         "zoomScale": 1.4, "zoomHub": [1030, 760], "zoomLabel": [890, 200] } } }
            """;
        var spot = ExpeditionMap3D.Parse(mountain).Spots["darkestdungeon"];
        for (int i = 0; i <= 20; i++)
        {
            var r = ExpeditionMap3D.OverviewRect(spot, i / 20f);
            Assert.True(r.X <= 0 && r.Y <= 0 && r.X + r.Width >= 1920 && r.Y + r.Height >= 1080);
        }
        Assert.Equal(0f, ExpeditionMap3D.OverviewRect(spot, 1).Y);
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(-3f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(9f, 1f)]
    public void EaseIsClampedAndSymmetric(float t, float expected) => Assert.Equal(expected, ExpeditionMap3D.Ease(t), 4);

    [Fact]
    public void MissingRegionRetainsTheOverview()
    {
        var none = ExpeditionMap3D.OverviewRect(null, 0.5f);
        Assert.Equal((0f, 0f, 1920f, 1080f), (none.X, none.Y, none.Width, none.Height));
    }
}
