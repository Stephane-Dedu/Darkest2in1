using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorRegistrationTests
{
    [Fact]
    public void LateDepthArrivalAndBattleCoversCannotMoveAStationaryPerspectiveScene()
    {
        var plate = new Texture2D(4096, 768, TextureFormat.RGBA32, false);
        var depths = Enumerable.Range(0, 3).Select(_ => new Texture2D(4096, 768, TextureFormat.RGBA32, false)).ToArray();
        var draws = new List<(Texture2D Texture, Rect Uv)>();
        GUI.ObserveTextureDraw = (rect, texture, uv, matrix, color) => draws.Add(((Texture2D)texture, uv));
        try
        {
            foreach (float position in new[] { .05f, .25f, .5f, .95f })
            {
                GUI.color = Color.white; draws.Clear();
                CorridorPanoramaUi.Draw(plate, position, 1);
                var expected = Assert.Single(draws);
                foreach (float blend in new[] { 0f, .01f, .5f, 1f })
                foreach (float cover in new[] { .4f, .999f, 1f })
                {
                    draws.Clear(); GUI.color = new Color(1, 1, 1, cover);
                    CorridorPanoramaUi.Draw(plate, position, 1, depths, blend);
                    var actual = Assert.Single(draws);
                    Assert.Same(plate, actual.Texture);
                    Assert.Equal(expected.Uv, actual.Uv);
                }
            }
        }
        finally { GUI.ObserveTextureDraw = null; GUI.color = Color.white; }
    }
}
