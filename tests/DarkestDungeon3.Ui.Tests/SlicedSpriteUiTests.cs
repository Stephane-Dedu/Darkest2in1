using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class SlicedSpriteUiTests
{
    [Fact]
    public void NativeTooltipBordersKeepTheirThicknessAndAtlasOrientation()
    {
        var draws = new List<(Rect Rect, Rect Uv)>();
        GUI.ObserveTextureDraw = (rect, _, uv, _, _) => draws.Add((rect, uv));
        try
        {
            var texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            SlicedSpriteUi.Draw(new Rect(20, 40, 460, 800), texture, new Rect(100, 200, 304, 240), 117, 102, 138, 62, .5f);
            Assert.Equal(9, draws.Count);
            Assert.Equal(58.5f, draws[0].Rect.width);
            Assert.Equal(31, draws[0].Rect.height);
            Assert.Equal(69, draws[2].Rect.width);
            Assert.Equal(51, draws[6].Rect.height);
            Assert.Equal(378f / 1024, draws[0].Uv.y);
            Assert.Equal(100f / 1024, draws[0].Uv.x);
            Assert.Equal(200f / 1024, draws[6].Uv.y);
            Assert.Equal(460 * 800, draws.Sum(d => d.Rect.width * d.Rect.height));
        }
        finally { GUI.ObserveTextureDraw = null; }
    }

    [Fact]
    public void ShortFrameReducesOppositeBordersWithoutCrossingOrPaintingOutside()
    {
        var draws = new List<Rect>();
        GUI.ObserveTextureDraw = (rect, _, _, _, _) => draws.Add(rect);
        try
        {
            SlicedSpriteUi.Draw(new Rect(10, 20, 100, 50), Texture2D.whiteTexture, new Rect(0, 0, 304, 240), 117, 102, 138, 62, .5f);
            Assert.All(draws, r =>
            {
                Assert.InRange(r.x, 10, 110);
                Assert.InRange(r.xMax, 10, 110.001f);
                Assert.InRange(r.y, 20, 70);
                Assert.InRange(r.yMax, 20, 70.001f);
                Assert.True(r.width > 0 && r.height > 0);
            });
            Assert.InRange(draws.Sum(r => r.width * r.height), 4999.99f, 5000.01f);
        }
        finally { GUI.ObserveTextureDraw = null; }
    }
}
