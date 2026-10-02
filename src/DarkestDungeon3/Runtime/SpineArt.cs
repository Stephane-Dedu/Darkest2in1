using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DarkestDungeon3.Core.Dd1;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// Still pictures of DD1's Spine animations (town buildings, curios), read from the user's DD1 install and
/// drawn on the CPU (Core's SpineRaster). Baking is spread over frames: callers get null until theirs is ready.
/// </summary>
internal static class SpineArt
{
    public sealed class Picture
    {
        public Texture2D Texture;
        public RgbaImage Pixels;      // kept for hit tests
        public Vector2 Pivot;         // skeleton origin, pixels from the top-left
        public float PixelsPerUnit;

        /// <summary>Screen rect when the skeleton origin sits at <paramref name="origin"/>, drawn at <paramref name="scale"/>.</summary>
        public Rect RectAt(Vector2 origin, float scale = 1f)
        {
            float k = scale / PixelsPerUnit;
            return new Rect(origin.x - Pivot.x * k, origin.y - Pivot.y * k, Texture.width * k, Texture.height * k);
        }

        public void Draw(Vector2 origin, float scale = 1f, bool flipX = false)
        {
            var r = RectAt(origin, scale);
            if (flipX)
            {
                r.x = origin.x - (Texture.width - Pivot.x) * scale / PixelsPerUnit;
                GUI.DrawTextureWithTexCoords(r, Texture, new Rect(1, 0, -1, 1), true);
            }
            else GUI.DrawTexture(r, Texture);
        }

        /// <summary>Is the picture opaque under this screen point?</summary>
        public bool Hit(Vector2 origin, float scale, Vector2 point)
        {
            var r = RectAt(origin, scale);
            if (!r.Contains(point)) return false;
            int x = (int)((point.x - r.x) / r.width * Pixels.Width), y = (int)((point.y - r.y) / r.height * Pixels.Height);
            if (x < 0 || y < 0 || x >= Pixels.Width || y >= Pixels.Height) return false;
            return Pixels.Pixels[(y * Pixels.Width + x) * 4 + 3] > 40;
        }
    }

    private static readonly Dictionary<string, Picture> Cache = new();
    private static readonly Dictionary<string, RgbaImage> Pages = new();
    private static int _frame = -1;
    private static readonly Stopwatch Budget = new();
    private const double BudgetMs = 40;

    /// <summary>
    /// The setup pose of the animation in <paramref name="folder"/>, drawn at <paramref name="pixelsPerUnit"/>.
    /// <paramref name="variant"/> names the slot filter for caching. Null while not baked yet, or if missing.
    /// </summary>
    public static Picture Get(string folder, string variant, Func<SpineSkeleton.Slot, bool> include, float pixelsPerUnit = 1f)
    {
        string key = $"{folder}|{variant}|{pixelsPerUnit:0.00}";
        if (Cache.TryGetValue(key, out var pic)) return pic;

        if (_frame != Time.frameCount) { _frame = Time.frameCount; Budget.Reset(); }
        if (Budget.Elapsed.TotalMilliseconds > BudgetMs) return null;   // try again next frame
        Budget.Start();
        try { pic = Bake(folder, include, pixelsPerUnit); }
        catch (Exception e) { Plugin.Log.LogWarning($"[spine] {folder} ({variant}): {e.Message}"); pic = null; }
        finally { Budget.Stop(); }
        Cache[key] = pic;
        return pic;
    }

    private static Picture Bake(string folder, Func<SpineSkeleton.Slot, bool> include, float pixelsPerUnit)
    {
        var files = Dd1Install.SpineIn(folder);
        if (files == null) return null;
        var (skelPath, atlasPath) = files.Value;
        var skel = SpineSkeleton.Load(skelPath);
        var atlas = SpineAtlas.Parse(File.ReadAllText(atlasPath));
        var pieces = skel.SetupPose(atlas, include);
        string dir = Path.GetDirectoryName(atlasPath);
        var result = SpineRaster.Render(pieces, page => LoadPage(Path.Combine(dir, page.File)), pixelsPerUnit);
        if (result == null) return null;

        var img = result.Image;
        // Texture rows run bottom-up.
        var raw = new byte[img.Pixels.Length];
        int stride = img.Width * 4;
        for (int y = 0; y < img.Height; y++)
            Buffer.BlockCopy(img.Pixels, y * stride, raw, (img.Height - 1 - y) * stride, stride);
        var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            name = "DD1 " + Path.GetFileName(folder),
        };
        tex.LoadRawTextureData(raw);
        tex.Apply(false, true);
        return new Picture { Texture = tex, Pixels = img, Pivot = new Vector2(result.PivotX, result.PivotY), PixelsPerUnit = pixelsPerUnit };
    }

    private static RgbaImage LoadPage(string path)
    {
        if (Pages.TryGetValue(path, out var page)) return page;
        page = null;
        if (File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path)))
            {
                var px = tex.GetPixels32();   // bottom-up
                int w = tex.width, h = tex.height;
                var bytes = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int src = (h - 1 - y) * w, dst = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[src + x];
                        int i = dst + x * 4;
                        bytes[i] = c.r; bytes[i + 1] = c.g; bytes[i + 2] = c.b; bytes[i + 3] = c.a;
                    }
                }
                page = new RgbaImage(w, h, bytes);
            }
            UnityEngine.Object.Destroy(tex);
        }
        Pages[path] = page;
        return page;
    }

    /// <summary>Atlas pages are only needed while baking; free them once a screen has everything it needs.</summary>
    public static void ReleasePages() => Pages.Clear();
}
