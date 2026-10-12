using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class RoomPanoramaUiTests
{
    [Fact]
    public void NativeArenaIncludesItsFloorWithoutBlendingOrRepeatingAndRestoresTintOnFailure()
    {
        var texture = new Texture2D(2880, 1080, TextureFormat.RGBA32, false);
        var tint = new Color(.7f, .8f, .9f, .6f);
        var draws = new List<(Rect Rect, Rect Uv, Color Color)>();
        GUI.color = tint;
        GUI.ObserveTextureDraw = (rect, tex, uv, matrix, color) => draws.Add((rect, uv, color));
        try
        {
            RoomPanoramaUi.Draw(texture, false, .5f, true, 610);
            var draw = Assert.Single(draws);
            Assert.Equal(0, draw.Rect.x); Assert.Equal(0, draw.Rect.y);
            Assert.Equal(1920, draw.Rect.width); Assert.Equal(720, draw.Rect.height);
            Assert.Equal(0, draw.Uv.x); Assert.Equal(0, draw.Uv.y);
            Assert.Equal(1, draw.Uv.width); Assert.Equal(1, draw.Uv.height);
            Assert.Equal(.3f, draw.Color.a); Assert.Equal(tint, GUI.color);
            GUI.ThrowOnTextureDraw = true;
            Assert.Throws<InvalidOperationException>(() => RoomPanoramaUi.Draw(texture, false, 1, true, 610));
            Assert.Equal(tint, GUI.color);
        }
        finally
        {
            GUI.ObserveTextureDraw = null; GUI.ThrowOnTextureDraw = false;
            GUI.color = new Color(1, 1, 1, 1);
        }
    }
}
