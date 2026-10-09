using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>A hallway keeps one panorama and floor; reflected edges join while the physical camera moves.</summary>
internal static class CorridorPanoramaUi
{
    public static void Draw(Texture2D texture, float camera, bool reverse, bool mirror, float alpha)
    {
        if (texture == null || alpha <= 0) return;
        var old = GUI.color;
        try
        {
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            foreach (var tile in CorridorSceneryLayout.Tiles(camera, 1920, reverse))
                GUI.DrawTextureWithTexCoords(new Rect(tile.X, 0, 1920, 720), texture,
                    tile.Mirror ^ mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
        }
        finally { GUI.color = old; }
    }
}
