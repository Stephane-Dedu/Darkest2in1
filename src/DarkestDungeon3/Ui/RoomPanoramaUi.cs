using UnityEngine;

namespace DarkestDungeon3.Ui;

internal static class RoomPanoramaUi
{
    public static void Draw(Texture2D texture, bool mirror, float alpha, bool fullFloor, float floorTop)
    {
        if (texture == null || alpha <= 0) return;
        var old = GUI.color;
        try
        {
            void Strip(float y, float height, float opacity)
            {
                GUI.color = new Color(old.r, old.g, old.b, old.a * alpha * opacity);
                var uv = new Rect(mirror ? 1 : 0, 1 - (y + height) / 720f, mirror ? -1 : 1, height / 720f);
                GUI.DrawTextureWithTexCoords(new Rect(0, y, 1920, height), texture, uv);
            }
            if (fullFloor) { Strip(0, 720, 1); return; }
            Strip(0, floorTop, 1);
            // Distributed fallback plates blend into the native road; private native arenas include their floor.
            for (float y = floorTop; y < 720; y += 5)
            {
                float height = Mathf.Min(5, 720 - y);
                Strip(y, height, 1 - (y + height / 2 - floorTop) / (720 - floorTop));
            }
        }
        finally { GUI.color = old; }
    }
}
