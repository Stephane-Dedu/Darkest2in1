using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>One bounded perspective view per hallway, with no repeated architecture or heading flip.</summary>
internal static class CorridorPanoramaUi
{
    public static void Draw(Texture2D texture, float progress, float alpha, Texture2D[] layers = null, float blend = 0, int tileCount=0)
    {
        if (texture == null || alpha <= 0) return;
        // The native geometry is already projected into a perspective painting.
        // Independent depth crops break its occlusion edges and move stationary
        // landmarks when companions arrive or a battle cover changes opacity.
        DrawPlate(texture, progress, alpha,tileCount);
    }

    private static void DrawPlate(Texture2D texture, float progress, float alpha,int tileCount)
    {
        var old = GUI.color;
        try
        {
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            var crop = CorridorPanoramaLayout.Crop(texture.width, texture.height, progress,tileCount);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1920, 720), texture,
                new Rect(crop.X, crop.Y, crop.Width, crop.Height));
        }
        finally { GUI.color = old; }
    }
}
