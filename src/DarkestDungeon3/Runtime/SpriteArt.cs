using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkestDungeon3.Runtime;

/// <summary>Atlas sprites drawn through IMGUI, including tight meshes and rotated packing.</summary>
internal static class SpriteArt
{
    private sealed class Frame
    {
        public Texture Source;
        public RenderTexture Image;
        public Vector2 Size;
        public int Pixels;
        public long Used;
    }

    private const int PixelBudget = 8 * 1024 * 1024, MaxDimension = 1024;
    private static readonly Dictionary<Sprite, Frame> Frames = new();
    private static readonly Dictionary<Sprite, float> Retry = new();
    private static Material _copy;
    private static int _pixels;
    private static long _used;

    public static void Draw(Rect rect, Sprite sprite, bool fit = true, bool flipX = false)
    {
        if (Event.current.type != EventType.Repaint || sprite == null || sprite.texture == null) return;
        if (Retry.TryGetValue(sprite, out float next) && Time.unscaledTime < next) return;
        try
        {
            Texture texture = sprite.texture;
            Rect uv;
            Vector2 size;
            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
            {
                if (Frames.TryGetValue(sprite, out var frame) && (frame.Source != texture || frame.Image == null || !frame.Image.IsCreated()))
                {
                    Remove(sprite, frame);
                    frame = null;
                }
                if (frame == null)
                {
                    frame = Bake(sprite);
                    while (_pixels + frame.Pixels > PixelBudget && Frames.Count > 0)
                    {
                        var oldest = Frames.OrderBy(pair => pair.Value.Used).First();
                        Remove(oldest.Key, oldest.Value);
                    }
                    Frames[sprite] = frame;
                    _pixels += frame.Pixels;
                }
                frame.Used = ++_used;
                texture = frame.Image;
                size = frame.Size;
                uv = new Rect(0, 0, 1, 1);
            }
            else
            {
                var region = sprite.textureRect;
                size = region.size;
                uv = new Rect(region.x / texture.width, region.y / texture.height, region.width / texture.width, region.height / texture.height);
            }
            if (size.x <= 0 || size.y <= 0) return;
            if (fit)
            {
                float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
                float width = size.x * scale, height = size.y * scale;
                rect = new Rect(rect.x + (rect.width - width) / 2, rect.yMax - height, width, height);
            }
            if (flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            Retry.Remove(sprite);
        }
        catch (Exception e)
        {
            Retry[sprite] = Time.unscaledTime + 5;
            Plugin.Log.LogWarning($"[art] sprite {sprite.name}: {e.Message}");
        }
    }

    private static Frame Bake(Sprite sprite)
    {
        // Copy with blending off: preserve straight alpha so IMGUI blends the result only once.
        if (_copy == null)
        {
            var shader = Shader.Find("Hidden/BlitCopy");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Sprite copy shader unavailable.");
            _copy = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (_copy.HasProperty("_Color")) _copy.SetColor("_Color", Color.white);
        }
        var vertices = sprite.vertices;
        var coords = sprite.uv;
        var indices = sprite.triangles;
        if (vertices.Length == 0 || vertices.Length != coords.Length || indices.Length == 0)
            throw new InvalidOperationException("Sprite mesh is empty.");
        float left = vertices.Min(v => v.x), bottom = vertices.Min(v => v.y);
        float width = vertices.Max(v => v.x) - left, height = vertices.Max(v => v.y) - bottom;
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Sprite mesh has no area.");
        float scale = Mathf.Min(sprite.pixelsPerUnit, MaxDimension / Mathf.Max(width, height));
        int w = Mathf.Max(1, Mathf.CeilToInt(width * scale)), h = Mathf.Max(1, Mathf.CeilToInt(height * scale));
        var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture image = null;
        var previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite, pushed = false, complete = false;
        try
        {
            mesh.vertices = vertices.Select(v => new Vector3((v.x - left) / width * w, (v.y - bottom) / height * h, 0)).ToArray();
            mesh.uv = coords;
            mesh.triangles = indices.Select(i => (int)i).ToArray();
            image = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!image.Create()) throw new InvalidOperationException("Sprite render texture allocation failed.");
            RenderTexture.active = image;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            pushed = true;
            GL.LoadPixelMatrix(0, w, 0, h);
            _copy.mainTexture = sprite.texture;
            if (!_copy.SetPass(0)) throw new InvalidOperationException("Sprite copy shader pass unavailable.");
            Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            complete = true;
            return new Frame { Source = sprite.texture, Image = image, Size = new Vector2(width, height), Pixels = w * h };
        }
        finally
        {
            if (pushed) GL.PopMatrix();
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previous;
            Object.Destroy(mesh);
            if (!complete && image != null) { image.Release(); Object.Destroy(image); }
        }
    }

    private static void Remove(Sprite sprite, Frame frame)
    {
        _pixels -= frame.Pixels;
        if (frame.Image != null)
        {
            frame.Image.Release();
            Object.Destroy(frame.Image);
        }
        Frames.Remove(sprite);
    }
}
