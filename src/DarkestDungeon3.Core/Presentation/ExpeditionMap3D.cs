using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>
/// Layout of the private pre-rendered 3D expedition map (<c>map3d.json</c> beside <c>map3d.png</c>): where each
/// map position's region sits in the 1920x1080 overview, where its name and quests go, which pixels zoom into
/// it, and how the overview has to grow so that it lines up with that region's own zoomed frame.
/// Positions are keyed by the legacy map location (crypts, warrens, weald, cove, darkestdungeon), as the
/// selector's paired regions are. A malformed entry is dropped; a file with no usable entry is no layout.
/// </summary>
public sealed class ExpeditionMap3D
{
    public const float Width = 1920, Height = 1080;
    public const float MinZoom = 1.05f, MaxZoom = 8f;

    public readonly struct MapRect
    {
        public readonly float X, Y, Width, Height;
        public MapRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        public bool Contains(float x, float y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    }

    public sealed class Spot
    {
        public string Location;
        /// <summary>The region's ground point in the overview and in its zoomed frame.</summary>
        public float HubX, HubY, ZoomHubX, ZoomHubY;
        /// <summary>Bottom centre of the region's label (name banner over one quest row), which stands above the
        /// region, in the overview and when zoomed.</summary>
        public float LabelX, LabelY, ZoomLabelX, ZoomLabelY;
        /// <summary>How much larger the region is in its zoomed frame than in the overview.</summary>
        public float ZoomScale;
        /// <summary>Overview pixels that zoom into the region when clicked.</summary>
        public MapRect Click;
    }

    private readonly Dictionary<string, Spot> _spots = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, Spot> Spots => _spots;

    /// <summary>Null when the text is not a layout or holds no valid position.</summary>
    public static ExpeditionMap3D Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JObject root;
        try { root = JObject.Parse(json); }
        catch (Exception) { return null; }
        if (root["spots"] is not JObject spots) return null;
        var map = new ExpeditionMap3D();
        foreach (var entry in spots)
        {
            if (entry.Value is not JObject o) continue;
            if (!Point(o["hub"], out float hx, out float hy) || !Point(o["label"], out float lx, out float ly)
                || !Point(o["zoomHub"], out float zx, out float zy) || !Point(o["zoomLabel"], out float zlx, out float zly)
                || !Number(o["zoomScale"], out float scale) || scale < MinZoom || scale > MaxZoom
                || o["click"] is not JArray click || click.Count != 4
                || !Number(click[0], out float cx) || !Number(click[1], out float cy)
                || !Number(click[2], out float cw) || !Number(click[3], out float ch)) continue;
            if (!OnScreen(hx, hy) || !OnScreen(lx, ly) || !OnScreen(zx, zy) || !OnScreen(zlx, zly)
                || cw <= 0 || ch <= 0 || !OnScreen(cx, cy) || cx + cw > Width || cy + ch > Height) continue;
            map._spots[entry.Key] = new Spot
            {
                Location = entry.Key, HubX = hx, HubY = hy, ZoomHubX = zx, ZoomHubY = zy,
                LabelX = lx, LabelY = ly, ZoomLabelX = zlx, ZoomLabelY = zly, ZoomScale = scale,
                Click = new MapRect(cx, cy, cw, ch),
            };
        }
        return map._spots.Count > 0 ? map : null;
    }

    /// <summary>Smooth start and stop for the camera move; 0 and 1 stay fixed.</summary>
    public static float Ease(float t)
    {
        if (float.IsNaN(t)) return 0;
        t = Math.Max(0, Math.Min(1, t));
        return t * t * (3 - 2 * t);
    }

    /// <summary>Opacity of the zoomed frame over the growing overview: it arrives in the second half of the move.</summary>
    public static float ZoomFade(float t)
    {
        if (float.IsNaN(t)) return 0;
        float f = Math.Max(0, Math.Min(1, (t - 0.45f) / 0.5f));
        return f * f * (3 - 2 * f);
    }

    /// <summary>
    /// Where the full overview image is drawn at <paramref name="t"/> (0 overview, 1 zoomed): it grows about the
    /// region's ground point while that point travels to where it stands in the zoomed frame. The size grows
    /// geometrically, so the move feels like a steady approach. The image always covers the whole screen.
    /// </summary>
    public static MapRect OverviewRect(Spot spot, float t)
    {
        if (spot == null) return new MapRect(0, 0, Width, Height);
        float e = Ease(t);
        float k = (float)Math.Pow(spot.ZoomScale, e);
        float x = spot.HubX + (spot.ZoomHubX - spot.HubX) * e, y = spot.HubY + (spot.ZoomHubY - spot.HubY) * e;
        // Never uncover the screen: a region near an edge stops sliding once the image's border reaches it.
        float left = Math.Max(Width - Width * k, Math.Min(0, x - spot.HubX * k));
        float top = Math.Max(Height - Height * k, Math.Min(0, y - spot.HubY * k));
        return new MapRect(left, top, Width * k, Height * k);
    }

    private static bool OnScreen(float x, float y) => x >= 0 && x <= Width && y >= 0 && y <= Height;

    private static bool Point(JToken token, out float x, out float y)
    {
        x = y = 0;
        return token is JArray a && a.Count == 2 && Number(a[0], out x) && Number(a[1], out y);
    }

    private static bool Number(JToken token, out float value)
    {
        value = 0;
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) return false;
        value = token.Value<float>();
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
