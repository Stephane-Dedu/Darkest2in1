using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorPanoramaUiTests
{
    [Theory]
    [InlineData(1600, 900, false, false)]
    [InlineData(1600, 900, false, true)]
    [InlineData(1587, 924, true, false)]
    [InlineData(1587, 924, true, true)]
    [InlineData(2560, 1080, false, true)]
    [InlineData(2560, 1440, true, false)]
    public void ActualPanoramaDrawKeepsFloorCoverageAndMatchingEdgesWhileWalking(int width, int height, bool reverse, bool mirror)
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
            foreach (float camera in new[] { -9723.5f, -1, 0, 720, 7359.25f })
            {
                draws.Clear();
                CorridorPanoramaUi.Draw(texture, camera, reverse, mirror, .5f);
                var row = draws.OrderBy(d => d.Rect.x).ToArray();
                Assert.NotEmpty(row);
                Assert.True(row[0].Rect.x <= 0);
                Assert.True(row[^1].Rect.xMax >= 1920);
                for (int i = 0; i < row.Length; i++)
                {
                    var d = row[i];
                    Assert.Equal(0, d.Rect.y); Assert.Equal(720, d.Rect.yMax);
                    Assert.Equal(0, d.Uv.y); Assert.Equal(1, d.Uv.height);
                    Assert.Equal(.3f, d.Color.a);
                    Assert.Equal(tint.r, d.Color.r);
                    Assert.Equal(screen, d.Matrix);
                    if (i == 0) continue;
                    Assert.Equal(row[i-1].Rect.xMax, d.Rect.x, 2);
                    Assert.Equal(row[i-1].Uv.xMax, d.Uv.x); // Same source edge at each join.
                }
                Assert.Equal(tint, GUI.color);
                Assert.Equal(screen, GUI.matrix);
            }
            var oldDraws = draws.Count;
            CorridorPanoramaUi.Draw(null, 0, false, false, 1);
            CorridorPanoramaUi.Draw(texture, 0, false, false, 0);
            Assert.Equal(oldDraws, draws.Count);
            GUI.ThrowOnTextureDraw = true;
            Assert.Throws<InvalidOperationException>(() => CorridorPanoramaUi.Draw(texture, 0, false, false, 1));
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
