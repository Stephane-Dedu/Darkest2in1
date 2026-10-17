using System;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>DD2's rectangular nine-slice UI art, retaining the authored corner and border thickness.</summary>
internal static class SlicedSpriteUi
{
    internal static void Draw(Rect rect, Sprite sprite)
    {
        // Native tooltip frames use rectangular, unrotated sprites. Keep the
        // general tight/rotated atlas path for any future asset that differs.
        if (sprite.packed && (sprite.packingMode != SpritePackingMode.Rectangle || sprite.packingRotation != SpritePackingRotation.None))
        {
            Runtime.SpriteArt.Draw(rect, sprite, fit: false);
            return;
        }
        var region = sprite.textureRect;
        var border = sprite.border;
        Draw(rect, sprite.texture, region, border.x, border.y, border.z, border.w, 100f / sprite.pixelsPerUnit);
    }

    internal static void Draw(Rect rect, Texture texture, Rect source, float left, float bottom, float right, float top, float scale)
    {
        float sx = Math.Min(scale, rect.width / Math.Max(1, left + right));
        float sy = Math.Min(scale, rect.height / Math.Max(1, top + bottom));
        var x = new[] { rect.x, rect.x + left * sx, rect.xMax - right * sx, rect.xMax };
        var y = new[] { rect.y, rect.y + top * sy, rect.yMax - bottom * sy, rect.yMax };
        var u = new[] { source.x, source.x + left, source.xMax - right, source.xMax };
        var v = new[] { source.yMax, source.yMax - top, source.y + bottom, source.y };
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var dst = new Rect(x[col], y[row], x[col + 1] - x[col], y[row + 1] - y[row]);
                if (dst.width <= 0 || dst.height <= 0) continue;
                var uv = new Rect(u[col] / texture.width, v[row + 1] / texture.height,
                    (u[col + 1] - u[col]) / texture.width, (v[row] - v[row + 1]) / texture.height);
                GUI.DrawTextureWithTexCoords(dst, texture, uv, true);
            }
    }
}
