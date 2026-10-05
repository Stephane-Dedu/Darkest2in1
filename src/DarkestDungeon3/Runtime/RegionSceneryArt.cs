using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Dungeon;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkestDungeon3.Runtime;

/// <summary>One expedition's native scenery textures, loaded during embark and released on leaving the expedition.</summary>
internal static class RegionSceneryArt
{
    private static readonly Dictionary<string, AsyncOperationHandle<Texture2D>> Handles = new();
    private static string _region;
    private static int _generation;
    private static bool _failed;
    private static float _readyAt = -1, _retryAt;
    private static readonly Dictionary<string, RoomTexture> Rooms = new();
    private static Task<RoomBytes[]> _roomLoading;
    private static Queue<RoomBytes> _roomPending;

    private sealed class RoomBytes { public string FileName, Error; public byte[] Data; }
    private sealed class RoomTexture { public Texture2D Texture; public float ReadyAt; }

    public static void Prepare(string region)
    {
        var plan = RegionalScenery.For(region);
        if (plan == null) { if (_region != null) Clear(); return; }
        if (_region == region && (!_failed || Time.unscaledTime < _retryAt)) { UpdateRoomTextures(); return; }
        Clear();
        _region = region;
        int generation = _generation;
        string folder = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "data", "scenery");
        _roomLoading = Task.Run(() => plan.RoomBackgrounds.Select(fileName =>
        {
            var result = new RoomBytes { FileName = fileName };
            try
            {
                string path = Path.Combine(folder, fileName);
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 16 * 1024 * 1024) result.Error = "file exceeds 16 MB";
                    else result.Data = File.ReadAllBytes(path);
                }
            }
            catch (Exception e) { result.Error = e.Message; }
            return result;
        }).ToArray());
        try
        {
            foreach (string key in plan.AssetKeys)
            {
                var handle = Addressables.LoadAssetAsync<Texture2D>(key);
                Handles.Add(key, handle);
                handle.Completed += loaded =>
                {
                    if (generation != _generation) return;
                    if (loaded.Status != AsyncOperationStatus.Succeeded || loaded.Result == null)
                    {
                        _failed = true;
                        _retryAt = Time.unscaledTime + 10;
                        Plugin.Log.LogWarning($"[scenery] {region}: unavailable {key}; retaining DD1 exploration art");
                    }
                };
            }
        }
        catch (Exception e)
        {
            _failed = true;
            _retryAt = Time.unscaledTime + 10;
            Plugin.Log.LogWarning($"[scenery] {region}: {e.Message}; retaining DD1 exploration art");
        }
    }

    public static float Alpha(string region)
    {
        if (_region != region || _failed || Handles.Count == 0 || Handles.Values.Any(handle =>
                !handle.IsValid() || !handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)) return 0;
        if (_readyAt < 0)
        {
            _readyAt = Time.unscaledTime;
            Plugin.Log.LogInfo($"[scenery] {region}: {Handles.Count} native textures ready");
        }
        return Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _readyAt) / 0.35f));
    }

    public static Texture2D Texture(string key) => Handles.TryGetValue(key, out var handle) && handle.IsValid()
        && handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;

    // File IO runs off-thread; decode/upload at most one image per update on Unity's main thread during embark.
    private static void UpdateRoomTextures()
    {
        if (_roomLoading != null && _roomLoading.IsCompleted)
        {
            _roomPending = new Queue<RoomBytes>(_roomLoading.GetAwaiter().GetResult());
            _roomLoading = null;
        }
        if (_roomPending == null || _roomPending.Count == 0) return;
        var image = _roomPending.Dequeue();
        if (image.Error != null) Plugin.Log.LogWarning($"[scenery] {image.FileName}: {image.Error}; retaining native region scene");
        if (image.Data == null) return;
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.LoadImage(image.Data, markNonReadable: true) || texture.width > 4096 || texture.height > 2048)
                throw new InvalidDataException("invalid or oversized room image");
            Rooms.Add(image.FileName, new RoomTexture { Texture = texture, ReadyAt = Time.unscaledTime });
            Plugin.Log.LogInfo($"[scenery] {image.FileName}: generated room texture ready ({texture.width}x{texture.height})");
        }
        catch (Exception e)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            Plugin.Log.LogWarning($"[scenery] {image.FileName}: {e.Message}; retaining native region scene");
        }
    }

    public static Texture2D RoomTextureFor(string fileName) => fileName != null && Rooms.TryGetValue(fileName, out var image) ? image.Texture : null;
    public static float RoomAlpha(string fileName) => fileName != null && Rooms.TryGetValue(fileName, out var image)
        ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - image.ReadyAt) / 0.35f)) : 0;

    public static void Clear()
    {
        _generation++;
        foreach (var handle in Handles.Values)
            if (handle.IsValid()) Addressables.Release(handle);
        Handles.Clear();
        foreach (var image in Rooms.Values) UnityEngine.Object.Destroy(image.Texture);
        Rooms.Clear();
        _roomLoading = null;
        _roomPending = null;
        _region = null;
        _failed = false;
        _readyAt = -1;
    }
}
