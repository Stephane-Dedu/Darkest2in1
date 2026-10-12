using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>One bounded perspective view per hallway, with no repeated architecture or heading flip.</summary>
internal static class CorridorPanoramaUi
{
    public static void Draw(Texture2D texture, float progress, float alpha)
    {
        if (texture == null || alpha <= 0) return;
        var old = GUI.color;
        try
        {
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            var crop = CorridorPanoramaLayout.Crop(texture.width, texture.height, progress);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1920, 720), texture,
                new Rect(crop.X, crop.Y, crop.Width, crop.Height));
        }
        finally { GUI.color = old; }
    }
}
