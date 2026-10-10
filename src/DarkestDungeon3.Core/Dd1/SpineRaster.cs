using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>An RGBA8 image, rows from the top.</summary>
public sealed class RgbaImage
{
    public readonly int Width, Height;
    public readonly byte[] Pixels;

    public RgbaImage(int width, int height, byte[] pixels = null)
    {
        Width = width;
        Height = height;
        Pixels = pixels ?? new byte[width * height * 4];
    }
}

/// <summary>
/// Draws a Spine setup pose (see <see cref="SpineSkeleton.SetupPose"/>) into a still image on the CPU: textured
/// triangles, bilinear sampling, 2x2 supersampling, straight-alpha "over" compositing (additive slots brighten instead). Small pictures (a
/// building, a curio) take a few milliseconds and need no GPU state.
/// </summary>
public static class SpineRaster
{
    public sealed class Result
    {
        public RgbaImage Image;
        /// <summary>Where the skeleton's origin lands, in image pixels from the top-left.</summary>
        public float PivotX, PivotY;
    }

    /// <param name="pixelsPerUnit">Output pixels per skeleton unit.</param>
    public static Result Render(IReadOnlyList<SpineSkeleton.Piece> pieces, Func<SpineAtlas.Page, RgbaImage> pageImage,
                                float pixelsPerUnit = 1f, int pad = 2, int maxSize = 4096)
    {
        if (pieces.Count == 0) return null;
        var (minX, minY, maxX, maxY) = SpineSkeleton.Bounds(pieces);
        int w = Math.Min(maxSize, (int)Math.Ceiling((maxX - minX) * pixelsPerUnit) + pad * 2);
        int h = Math.Min(maxSize, (int)Math.Ceiling((maxY - minY) * pixelsPerUnit) + pad * 2);
        var image = new RgbaImage(Math.Max(1, w), Math.Max(1, h));

        // Skeleton y points up; image rows go down.
        float ToX(float x) => (x - minX) * pixelsPerUnit + pad;
        float ToY(float y) => (maxY - y) * pixelsPerUnit + pad;

        var pages = new Dictionary<SpineAtlas.Page, RgbaImage>();
        foreach (var piece in pieces)
        {
            if (!pages.TryGetValue(piece.Page, out var page)) pages[piece.Page] = page = pageImage(piece.Page);
            if (page == null) continue;
            // An atlas "size" may differ from the PNG if it was rescaled; sample in the PNG's own pixels.
            float su = piece.Page.Width > 0 ? (float)page.Width / piece.Page.Width : 1f;
            float sv = piece.Page.Height > 0 ? (float)page.Height / piece.Page.Height : 1f;
            var tint = piece.Color;
            for (int t = 0; t + 2 < piece.Triangles.Length; t += 3)
            {
                int i0 = piece.Triangles[t], i1 = piece.Triangles[t + 1], i2 = piece.Triangles[t + 2];
                Triangle(image, page,
                    ToX(piece.Positions[i0 * 2]), ToY(piece.Positions[i0 * 2 + 1]), piece.PagePixels[i0 * 2] * su, piece.PagePixels[i0 * 2 + 1] * sv,
                    ToX(piece.Positions[i1 * 2]), ToY(piece.Positions[i1 * 2 + 1]), piece.PagePixels[i1 * 2] * su, piece.PagePixels[i1 * 2 + 1] * sv,
                    ToX(piece.Positions[i2 * 2]), ToY(piece.Positions[i2 * 2 + 1]), piece.PagePixels[i2 * 2] * su, piece.PagePixels[i2 * 2 + 1] * sv,
                    tint, piece.Additive);
            }
        }
        return new Result { Image = image, PivotX = ToX(0), PivotY = ToY(0) };
    }

    private static readonly float[] Sub = { 0.25f, 0.75f };

    private static void Triangle(RgbaImage dst, RgbaImage src,
                                 float x0, float y0, float u0, float v0,
                                 float x1, float y1, float u1, float v1,
                                 float x2, float y2, float u2, float v2, uint tint, bool additive = false)
    {
        float area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
        if (Math.Abs(area) < 1e-6f) return;
        int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
        int maxX = Math.Min(dst.Width - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
        int maxY = Math.Min(dst.Height - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
        float tr = ((tint >> 24) & 0xff) / 255f, tg = ((tint >> 16) & 0xff) / 255f, tb = ((tint >> 8) & 0xff) / 255f, ta = (tint & 0xff) / 255f;
        float inv = 1f / area;

        for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                int covered = 0;
                foreach (float sy in Sub)
                    foreach (float sx in Sub)
                    {
                        float x = px + sx, y = py + sy;
                        // Barycentric weights; a shared edge belongs to one side only (half-open test).
                        float w0 = ((x1 - x) * (y2 - y) - (x2 - x) * (y1 - y)) * inv;
                        float w1 = ((x2 - x) * (y0 - y) - (x0 - x) * (y2 - y)) * inv;
                        float w2 = 1f - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 <= 0) continue;
                        covered++;
                        Sample(src, w0 * u0 + w1 * u1 + w2 * u2, w0 * v0 + w1 * v1 + w2 * v2, out float sr, out float sg, out float sb, out float sa);
                        // Accumulate premultiplied so transparent texels don't bleed their colour.
                        r += sr * sa; g += sg * sa; b += sb * sa; a += sa;
                    }
                if (covered == 0) continue;
                // Average over all 4 sub-samples: partial coverage gives soft edges.
                float alpha = a / 4f * ta;
                if (alpha <= 0) continue;
                float cr = r / a * tr, cg = g / a * tg, cb = b / a * tb;
                if (additive) Add(dst, px, py, cr, cg, cb, alpha);
                else Over(dst, px, py, cr, cg, cb, alpha);
            }
    }

    /// <summary>
    /// Spine's additive slots (DD1's building lights and glows): they brighten what is already drawn instead of
    /// painting over it, and add no coverage of their own (drawn "over", they came out as opaque white shapes).
    /// </summary>
    private static void Add(RgbaImage dst, int x, int y, float r, float g, float b, float a)
    {
        int i = (y * dst.Width + x) * 4;
        var p = dst.Pixels;
        float da = p[i + 3] / 255f;
        if (da <= 0) return;
        p[i] = (byte)Math.Min(255, p[i] + r * a * 255f + 0.5f);
        p[i + 1] = (byte)Math.Min(255, p[i + 1] + g * a * 255f + 0.5f);
        p[i + 2] = (byte)Math.Min(255, p[i + 2] + b * a * 255f + 0.5f);
    }

    private static void Over(RgbaImage dst, int x, int y, float r, float g, float b, float a)
    {
        int i = (y * dst.Width + x) * 4;
        var p = dst.Pixels;
        float da = p[i + 3] / 255f;
        float outA = a + da * (1 - a);
        if (outA <= 0) return;
        float k = da * (1 - a);
        p[i] = (byte)Math.Min(255, (r * a + p[i] / 255f * k) / outA * 255f + 0.5f);
        p[i + 1] = (byte)Math.Min(255, (g * a + p[i + 1] / 255f * k) / outA * 255f + 0.5f);
        p[i + 2] = (byte)Math.Min(255, (b * a + p[i + 2] / 255f * k) / outA * 255f + 0.5f);
        p[i + 3] = (byte)Math.Min(255, outA * 255f + 0.5f);
    }

    private static void Sample(RgbaImage img, float u, float v, out float r, out float g, out float b, out float a)
    {
        // u, v in page pixels from the top-left; texel centres at +0.5.
        float fx = u - 0.5f, fy = v - 0.5f;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        r = g = b = a = 0;
        for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                float wgt = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
                if (wgt <= 0) continue;
                int sx = Math.Max(0, Math.Min(img.Width - 1, x0 + i)), sy = Math.Max(0, Math.Min(img.Height - 1, y0 + j));
                int k = (sy * img.Width + sx) * 4;
                float ta = img.Pixels[k + 3] / 255f * wgt;
                r += img.Pixels[k] / 255f * ta;
                g += img.Pixels[k + 1] / 255f * ta;
                b += img.Pixels[k + 2] / 255f * ta;
                a += ta;
            }
        if (a > 0) { r /= a; g /= a; b /= a; }
    }
}
