using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorPanoramaUiTests
{
    [Fact]
    public void CompleteNativeSceneIsDrawnOnceWithoutReflectedTwins()
    {
        var texture = new Texture2D(2880, 1080, TextureFormat.RGBA32, false);
        var draws = new List<(Rect Rect, Rect Uv)>();
        GUI.ObserveTextureDraw = (rect, tex, uv, matrix, color) => draws.Add((rect, uv));
        try
        {
            CorridorPanoramaUi.Draw(texture, .5f, 1);
            var draw = Assert.Single(draws);
            Assert.True(draw.Uv.width > 0, "The scenery must retain its authored orientation.");
        }
        finally { GUI.ObserveTextureDraw = null; }
    }

    [Theory]
    [InlineData(1600, 900)]
    [InlineData(1587, 924)]
    [InlineData(2560, 1080)]
    [InlineData(2560, 1440)]
    public void BoundedPanCoversTheViewportAndPreservesAspectFootlineAndGuiState(int width, int height)
    {
        var texture = new Texture2D(2048, 768, TextureFormat.RGBA32, false);
        var draws = new List<(Rect Rect, Rect Uv, Matrix4x4 Matrix, Color Color)>();
        var screen = Matrix4x4.identity;
        screen.m00 = screen.m11 = MathF.Min(width / 1920f, height / 1080f);
        screen.m03 = (width - 1920 * screen.m00) / 2; screen.m13 = (height - 1080 * screen.m11) / 2;
        GUI.matrix = screen;
        var tint = new Color(.7f, .8f, .9f, .6f);
        GUI.color = tint;
        GUI.ObserveTextureDraw = (rect, tex, uv, matrix, color) =>
        {
            Assert.Same(texture, tex);
            draws.Add((rect, uv, matrix, color));
        };
        try
        {
            float previousX = -1;
            foreach (float progress in new[] { -1f, 0, .25f, .5f, .75f, 1, 2 })
            {
                draws.Clear();
                CorridorPanoramaUi.Draw(texture, progress, .5f);
                var d = Assert.Single(draws);
                Assert.Equal(0, d.Rect.x); Assert.Equal(1920, d.Rect.xMax);
                Assert.Equal(0, d.Rect.y); Assert.Equal(720, d.Rect.yMax);
                Assert.InRange(d.Uv.x, 0, 1); Assert.InRange(d.Uv.xMax, 0, 1.00001f);
                Assert.InRange(d.Uv.y, 0, 1); Assert.InRange(d.Uv.yMax, 0, 1.00001f);
                Assert.True(d.Uv.width > 0 && d.Uv.height > 0);
                Assert.True(d.Uv.x >= previousX); previousX = d.Uv.x;
                Assert.Equal(1920f / 720, texture.width * d.Uv.width / (texture.height * d.Uv.height), 4);
                float sourceFoot = 1 - 680f / 720;
                Assert.Equal(680, 720 * (1 - (sourceFoot - d.Uv.y) / d.Uv.height), 3);
                Assert.Equal(.3f, d.Color.a); Assert.Equal(tint.r, d.Color.r);
                Assert.Equal(screen, d.Matrix);
                Assert.Equal(tint, GUI.color);
                Assert.Equal(screen, GUI.matrix);
            }
            var oldDraws = draws.Count;
            CorridorPanoramaUi.Draw(null, 0, 1);
            CorridorPanoramaUi.Draw(texture, 0, 0);
            Assert.Equal(oldDraws, draws.Count);
            GUI.ThrowOnTextureDraw = true;
            Assert.Throws<InvalidOperationException>(() => CorridorPanoramaUi.Draw(texture, 0, 1));
            Assert.Equal(tint, GUI.color);
            Assert.Equal(screen, GUI.matrix);
        }
        finally
        {
            GUI.ObserveTextureDraw = null; GUI.ThrowOnTextureDraw = false;
            GUI.matrix = Matrix4x4.identity; GUI.color = new Color(1, 1, 1, 1);
        }
    }
}
