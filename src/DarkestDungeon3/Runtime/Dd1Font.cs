using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Minimal TGA reader for DD1's font pages: 24/32-bit, uncompressed (type 2) or RLE (type 10).</summary>
internal static class Tga
{
    public static Texture2D Load(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        int idLen = d[0], type = d[2];
        int w = d[12] | d[13] << 8, h = d[14] | d[15] << 8;
        int bytes = d[16] / 8;
        bool topDown = (d[17] & 0x20) != 0;
        if ((type != 2 && type != 10) || bytes < 3) throw new NotSupportedException($"TGA type {type}, {d[16]} bpp: {path}");

        var px = new Color32[w * h];
        int p = 18 + idLen, i = 0;
        Color32 Read()
        {
            var c = new Color32(d[p + 2], d[p + 1], d[p], bytes == 4 ? d[p + 3] : (byte)255);
            p += bytes;
            return c;
        }
        if (type == 2)
            for (; i < px.Length; i++) px[i] = Read();
        else
            while (i < px.Length)
            {
                int header = d[p++], n = (header & 0x7f) + 1;
                if ((header & 0x80) != 0) { var c = Read(); for (int k = 0; k < n && i < px.Length; k++) px[i++] = c; }
                else for (int k = 0; k < n && i < px.Length; k++) px[i++] = Read();
            }

        // Unity rows go bottom-up; flip images stored top-down.
        if (topDown)
            for (int y = 0; y < h / 2; y++)
                for (int x = 0; x < w; x++)
                    (px[y * w + x], px[(h - 1 - y) * w + x]) = (px[(h - 1 - y) * w + x], px[y * w + x]);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(false, makeNoLongerReadable: true);
        return tex;
    }
}

/// <summary>
/// DD1's bitmap fonts (BMFont text format + TGA pages), drawn glyph by glyph with IMGUI. DwarvenAxe is DD1's
/// heading face, Ubuntu its body text.
/// </summary>
internal sealed class Dd1Font
{
    private struct Glyph { public Rect Uv; public float W, H, XOff, YOff, Advance; }

    private readonly Dictionary<int, Glyph> _glyphs = new();
    private Texture2D _page;
    private float _lineHeight;

    private static readonly Dictionary<string, Dd1Font> Cache = new();

    public static Dd1Font Heading => Get("dwarvenaxe-m");
    public static Dd1Font HeadingLarge => Get("dwarvenaxe-l");
    public static Dd1Font Body => Get("ubuntu");
    public static Dd1Font BodyBold => Get("ubuntu_m");

    public static Dd1Font Get(string name)
    {
        if (Cache.TryGetValue(name, out var f)) return f;
        try { f = Load(Session.Current.Dd1.PathOf("fonts", name + ".fnt")); }
        catch (Exception e) { Plugin.Log.LogWarning($"DD1 font {name}: {e.Message}"); f = null; }
        Cache[name] = f;
        return f;
    }

    private static Dictionary<string, string> Pairs(string line)
    {
        var map = new Dictionary<string, string>();
        foreach (var part in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) map[part.Substring(0, eq)] = part.Substring(eq + 1).Trim('"');
        }
        return map;
    }

    private static float F(Dictionary<string, string> m, string k) =>
        m.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : 0f;

    private static Dd1Font Load(string fntPath)
    {
        var font = new Dd1Font();
        var raw = new List<(int id, float x, float y, float w, float h, float xo, float yo, float adv)>();
        foreach (var line in File.ReadAllLines(fntPath))
        {
            var m = Pairs(line);
            if (line.StartsWith("common ")) font._lineHeight = F(m, "lineHeight");
            else if (line.StartsWith("page ") && m.TryGetValue("file", out var file))
                font._page ??= Tga.Load(Path.Combine(Path.GetDirectoryName(fntPath), file));
            else if (line.StartsWith("char "))
                raw.Add(((int)F(m, "id"), F(m, "x"), F(m, "y"), F(m, "width"), F(m, "height"), F(m, "xoffset"), F(m, "yoffset"), F(m, "xadvance")));
        }
        if (font._page == null) throw new FileNotFoundException("no font page for " + fntPath);
        // Glyph coordinates fit the real page size (some headers claim a larger one), so UVs use the texture.
        float tw = font._page.width, th = font._page.height;
        foreach (var g in raw)
            font._glyphs[g.id] = new Glyph
            {
                Uv = new Rect(g.x / tw, 1f - (g.y + g.h) / th, g.w / tw, g.h / th),
                W = g.w, H = g.h, XOff = g.xo, YOff = g.yo, Advance = g.adv,
            };
        if (font._lineHeight <= 0) font._lineHeight = 30;
        return font;
    }

    private float LineWidth(string line, float scale)
    {
        float w = 0;
        foreach (char c in line) w += (_glyphs.TryGetValue(c, out var g) ? g.Advance : _lineHeight * 0.3f) * scale;
        return w;
    }

    /// <summary>Break text into lines no wider than maxWidth (at the given pixel size).</summary>
    public List<string> Wrap(string text, float size, float maxWidth)
    {
        float scale = size / _lineHeight;
        var lines = new List<string>();
        foreach (var para in (text ?? "").Split('\n'))
        {
            var sb = new StringBuilder();
            foreach (var word in para.Split(' '))
            {
                string attempt = sb.Length == 0 ? word : sb + " " + word;
                if (sb.Length > 0 && maxWidth > 0 && LineWidth(attempt, scale) > maxWidth)
                {
                    lines.Add(sb.ToString());
                    sb.Clear().Append(word);
                }
                else { sb.Clear().Append(attempt); }
            }
            lines.Add(sb.ToString());
        }
        return lines;
    }

    public Vector2 Measure(string text, float size, float maxWidth = 0)
    {
        float scale = size / _lineHeight;
        var lines = Wrap(text, size, maxWidth);
        float w = 0;
        foreach (var l in lines) w = Mathf.Max(w, LineWidth(l, scale));
        return new Vector2(w, lines.Count * size);
    }

    /// <summary>Draw text in a rect. <paramref name="size"/> is the line height in virtual pixels.</summary>
    public void Draw(Rect r, string text, float size, Color colour, TextAnchor align = TextAnchor.UpperLeft, bool shadow = true)
    {
        if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
        float scale = size / _lineHeight;
        var lines = Wrap(text, size, r.width);
        float total = lines.Count * size;
        float y = align switch
        {
            TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => r.y + (r.height - total) / 2f,
            TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => r.yMax - total,
            _ => r.y,
        };
        var old = GUI.color;
        foreach (var line in lines)
        {
            float w = LineWidth(line, scale);
            float x = align switch
            {
                TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => r.x + (r.width - w) / 2f,
                TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => r.xMax - w,
                _ => r.x,
            };
            if (shadow) { GUI.color = new Color(0, 0, 0, 0.85f * colour.a); DrawLine(line, x + 2, y + 2, scale); }
            GUI.color = colour;
            DrawLine(line, x, y, scale);
            y += size;
        }
        GUI.color = old;
    }

    private void DrawLine(string line, float x, float y, float scale)
    {
        foreach (char c in line)
        {
            if (!_glyphs.TryGetValue(c, out var g)) { x += _lineHeight * 0.3f * scale; continue; }
            if (g.W > 0 && g.H > 0)
                GUI.DrawTextureWithTexCoords(new Rect(x + g.XOff * scale, y + g.YOff * scale, g.W * scale, g.H * scale), _page, g.Uv, true);
            x += g.Advance * scale;
        }
    }
}
