using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Owned, bounded private effect textures. Read off-thread, upload one per frame, release after leaving the map.</summary>
internal static class ExpeditionMapEffects
{
    private sealed class Prepared
    {
        public ExpeditionMapVfx Pack;
        public readonly Queue<(string file, byte[] png)> Images = new();
        public string Error;
    }
    private static readonly Dictionary<string, Texture2D> Images = new();
    private static Task<Prepared> _reading;
    private static Prepared _prepared;
    private static ExpeditionMapVfx _pack;
    private static string _folder;
    private static int _usedFrame = -1, _uploadFrame = -1;
    private static bool _started;
    internal static int TextureCount => Images.Count;
    internal static int ReadyCount
    {
        get
        {
            int count = 0;
            if (_pack != null) foreach (var clip in _pack.Clips.Values) if (Images.ContainsKey(clip.File)) count++;
            return count;
        }
    }

    public static void Prepare()
    {
        _usedFrame = Time.frameCount;
        string folder = Plugin.ExpeditionArtPath?.Value;
        if (_folder != folder) { Clear(); _folder = folder; _usedFrame = Time.frameCount; }
        if (!_started)
        {
            _started = true;
            _reading = Task.Run(() => Read(folder));
        }
        if (_reading?.IsCompleted == true)
        {
            _prepared = _reading.GetAwaiter().GetResult(); _reading = null; _pack = _prepared.Pack;
            if (_prepared.Error != null) Plugin.Log.LogWarning("[expedition vfx] " + _prepared.Error);
        }
        if (_prepared == null || _prepared.Images.Count == 0 || _uploadFrame == Time.frameCount) return;
        _uploadFrame = Time.frameCount;
        var image = _prepared.Images.Dequeue();
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.LoadImage(image.png, markNonReadable: true)) throw new InvalidDataException("PNG decode failed");
            Images.Add(image.file, texture);
            Plugin.Log.LogInfo($"[expedition vfx] {image.file}: {texture.width}x{texture.height}; {ReadyCount}/{_pack.Clips.Count} effects ready");
        }
        catch (Exception e)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            Plugin.Log.LogWarning($"[expedition vfx] {image.file}: {e.Message}; skipping effect");
        }
    }

    public static bool TryGet(string id, out ExpeditionMapVfx.Clip clip, out Texture2D texture)
    {
        clip = null; texture = null;
        return _pack != null && _pack.Clips.TryGetValue(id, out clip) && Images.TryGetValue(clip.File, out texture);
    }

    public static void ReleaseUnused()
    {
        if (_started && Time.frameCount - _usedFrame > 3) Clear();
    }

    public static void Clear()
    {
        foreach (var texture in Images.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
        Images.Clear(); _reading = null; _prepared = null; _pack = null; _folder = null;
        _started = false; _uploadFrame = -1;
    }

    private static Prepared Read(string folder)
    {
        var result = new Prepared();
        try
        {
            if (string.IsNullOrWhiteSpace(folder)) return result;
            string root = Path.Combine(folder, "vfx"), manifest = Path.Combine(root, "vfx-manifest.json");
            if (!File.Exists(manifest)) return result;
            if (new FileInfo(manifest).Length > 256 * 1024) throw new InvalidDataException("manifest exceeds 256 KB");
            result.Pack = ExpeditionMapVfx.Parse(File.ReadAllText(manifest));
            if (result.Pack == null) throw new InvalidDataException("no valid native effect clips");
            var files = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in result.Pack.Clips.Values)
            {
                if (!files.Add(clip.File)) continue;
                string path = Path.Combine(root, clip.File);
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) continue;
                byte[] bytes = File.ReadAllBytes(path);
                // Inspect dimensions before Unity allocates the image; all accepted files are bounded RGBA PNGs.
                if (bytes.Length < 33 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71
                    || bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82
                    || bytes[24] != 8 || bytes[25] != 6) continue;
                int width = BigEndian(bytes, 16), height = BigEndian(bytes, 20);
                if (width < 16 || height < 16 || width > 2048 || height > 2048) continue;
                result.Images.Enqueue((clip.File, bytes));
            }
        }
        catch (Exception e) { result.Error = e.Message; }
        return result;
    }

    private static int BigEndian(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
}

[HarmonyPatch(typeof(Driver), "Update")]
internal static class ExpeditionMapEffectsLifetime
{
    private static void Postfix() => ExpeditionMapEffects.ReleaseUnused();
}
