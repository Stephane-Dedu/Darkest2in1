using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>One bounded perspective view per hallway, with no repeated architecture or heading flip.</summary>
internal static class CorridorPanoramaUi
{
    public static void Draw(Texture2D texture, float progress, float alpha, Texture2D[] layers = null, float blend = 0)
    {
        if (texture == null || alpha <= 0) return;
        bool complete = layers != null && layers.Length == PrivateCorridorLayers.Count;
        if (complete)
            foreach (var layer in layers)
                complete &= layer != null && layer.width == PrivateCorridorLayers.Width && layer.height == PrivateCorridorLayers.Height;
        // Fade the complete plate as one image. Fading RGBA depths independently
        // would show doubled walls and brighten overlaps during embark/battle covers.
        if (!complete || blend <= 0 || alpha * GUI.color.a < .999f)
        { DrawPlate(texture, progress, alpha); return; }
        for (int i = 0; i < layers.Length; i++)
        {
            // Start registered with the complete plate, then ease into the stable
            // depth pan. No partial set, translucent architecture or extra render target.
            float depthProgress = CorridorPanoramaLayout.LayerProgress(progress, i);
            DrawPlate(layers[i], progress + (depthProgress - progress) * Mathf.Clamp01(blend), alpha);
        }
    }

    private static void DrawPlate(Texture2D texture, float progress, float alpha)
    {
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
