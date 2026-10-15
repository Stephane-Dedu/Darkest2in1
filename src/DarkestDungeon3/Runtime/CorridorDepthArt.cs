using System;
using System.IO;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>One corridor's optional depths, with worker IO and at most one main-thread upload per update.</summary>
internal static class CorridorDepthArt
{
    private sealed class Bytes { public byte[][] Images; public string Error; }
    private static string _folder, _requested, _active;
    private static Task<Bytes> _loading;
    private static byte[][] _pending;
    private static Texture2D[] _layers;
    private static int _uploaded;
    private static float _readyAt = -1;

    public static void Configure(string folder) { Clear(); _folder = folder; }

    // OnGUI can request a view, but cannot read files, decode or destroy textures.
    public static Texture2D[] Request(string scene, out float blend)
    {
        _requested = PrivateCorridorLayers.FileName(scene, 0) == null ? null : scene;
        bool ready = scene != null && scene == _active && _readyAt >= 0;
        blend = ready ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _readyAt) / .35f)) : 0;
        return ready ? _layers : null;
    }

    public static void Update()
    {
        if (_requested != _active)
        {
            ReleaseCurrent();
            _active = _requested;
            if (_active != null && !string.IsNullOrWhiteSpace(_folder))
            {
                string folder = _folder, scene = _active;
                _loading = Task.Run(() => Read(folder, scene));
            }
        }
        if (_loading != null && _loading.IsCompleted)
        {
            var result = _loading.GetAwaiter().GetResult();
            _loading = null;
            if (result.Error != null)
                Plugin.Log.LogWarning($"[scenery-depth] {_active}: {result.Error}; retaining complete panorama");
            _pending = result.Images;
            if (_pending != null) _layers = new Texture2D[PrivateCorridorLayers.Count];
        }
        if (_pending == null) return;
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.LoadImage(_pending[_uploaded], markNonReadable: true)
                || texture.width != PrivateCorridorLayers.Width || texture.height != PrivateCorridorLayers.Height)
                throw new InvalidDataException("invalid depth image");
            _layers[_uploaded] = texture;
            texture = null;
            _pending[_uploaded++] = null;
            if (_uploaded == PrivateCorridorLayers.Count)
            {
                _pending = null;
                _readyAt = Time.unscaledTime;
                Plugin.Log.LogInfo($"[scenery-depth] {_active}: three native depths ready (36 MiB maximum)");
            }
        }
        catch (Exception e)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            ReleaseCurrent();
            Plugin.Log.LogWarning($"[scenery-depth] {_active}: {e.Message}; retaining complete panorama");
        }
    }

    private static Bytes Read(string folder, string scene)
    {
        try
        {
            var images = new byte[PrivateCorridorLayers.Count][];
            // Missing companions are normal for older private packs. Do not retry
            // them every frame or decode a partial scene over the complete plate.
            for (int i = 0; i < images.Length; i++)
            {
                string name = PrivateCorridorLayers.FileName(scene, i);
                string path = Path.Combine(folder, name);
                if (!File.Exists(path)) return new Bytes();
                if (new FileInfo(path).Length > PrivateRoomScenery.MaxFileBytes)
                    return new Bytes { Error = "depth PNG exceeds 16 MB" };
                images[i] = File.ReadAllBytes(path);
                if (!PrivateCorridorLayers.ValidPng(images[i]))
                    return new Bytes { Error = "invalid depth PNG header or dimensions" };
            }
            return new Bytes { Images = images };
        }
        catch (Exception e) { return new Bytes { Error = e.Message }; }
    }

    private static void ReleaseCurrent()
    {
        _loading = null; // Workers own only managed bytes; discarded tasks never touch Unity.
        _pending = null;
        if (_layers != null)
            foreach (var texture in _layers) if (texture != null) UnityEngine.Object.Destroy(texture);
        _layers = null;
        _uploaded = 0;
        _readyAt = -1;
    }

    public static void Clear()
    {
        ReleaseCurrent();
        _folder = _requested = _active = null;
    }
}
