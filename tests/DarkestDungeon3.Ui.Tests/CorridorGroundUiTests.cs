using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorGroundUiTests
{
    [Theory]
    [InlineData(1587, 924, false)]
    [InlineData(1587, 924, true)]
    [InlineData(1280, 720, false)]
    [InlineData(1280, 720, true)]
    [InlineData(2560, 1080, false)]
    [InlineData(2560, 1080, true)]
    [InlineData(2560, 1440, false)]
    [InlineData(2560, 1440, true)]
    [InlineData(1920, 1080, false)]
    [InlineData(1920, 1080, true)]
    public void RoadsKeepTheSameFloorAndMeetAtEveryWindowSize(int windowWidth, int windowHeight, bool reverse)
    {
        float scale = MathF.Min(windowWidth / 1920f, windowHeight / 1080f);
        float ox = (windowWidth - 1920 * scale) / 2, oy = (windowHeight - 1080 * scale) / 2;
        var screen = Matrix4x4.identity;
        screen.m00 = screen.m11 = scale; screen.m03 = ox; screen.m13 = oy;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            foreach (var camera in new[] { -9723.5f, 0, 7359.25f })
            {
                float? lastRight = null;
                foreach (var tile in CorridorSceneryLayout.Tiles(camera, 720, reverse).OrderBy(tile => tile.X))
                {
                    GUI.matrix = screen;
                    CorridorGroundUi.Draw(new Rect(tile.X, 610, 720, 110), texture, tile.Mirror);
                    var draw = GUI.LastTextureDraw;
                    var points = new[] { new Vector3(draw.Rect.x, draw.Rect.y, 0),
                        new Vector3(draw.Rect.xMax, draw.Rect.y, 0), new Vector3(draw.Rect.x, draw.Rect.yMax, 0),
                        new Vector3(draw.Rect.xMax, draw.Rect.yMax, 0) }.Select(GUI.LastTextureMatrix.MultiplyPoint3x4).ToArray();
                    AssertClose(ox + tile.X * scale, points.Min(p => p.x));
                    AssertClose(ox + (tile.X + 720) * scale, points.Max(p => p.x));
                    AssertClose(oy + 610 * scale, points.Min(p => p.y));
                    AssertClose(oy + 720 * scale, points.Max(p => p.y));
                    if (lastRight.HasValue) AssertClose(lastRight.Value, points.Min(p => p.x));
                    lastRight = points.Max(p => p.x);
                    Assert.Equal(tile.Mirror ? -1 : 1, draw.Uv.height);
                    Assert.Equal(tile.Mirror ? 1 : 0, draw.Uv.y);
                    Assert.Equal(screen, GUI.matrix);
                }
            }
        }
        finally { GUI.matrix = Matrix4x4.identity; }
    }

    [Fact]
    public void FailedDrawRestoresTheCanvasForLaterHudAndScenery()
    {
        var screen = Matrix4x4.identity;
        screen.m00 = screen.m11 = .7f; screen.m03 = 17; screen.m13 = 40;
        GUI.matrix = screen;
        GUI.ThrowOnTextureDraw = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => CorridorGroundUi.Draw(new Rect(600, 610, 720, 110),
                new Texture2D(2, 2, TextureFormat.RGBA32, false), true));
            Assert.Equal(screen, GUI.matrix);
        }
        finally { GUI.ThrowOnTextureDraw = false; GUI.matrix = Matrix4x4.identity; }
    }

    private static void AssertClose(float expected, float actual) => Assert.InRange(actual, expected - .002f, expected + .002f);
}
