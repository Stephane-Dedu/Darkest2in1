using System;
using System.Collections.Generic;
using System.Linq;
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

    public static void Prepare(string region)
    {
        var plan = RegionalScenery.For(region);
        if (plan == null) { if (_region != null) Clear(); return; }
        if (_region == region && (!_failed || Time.unscaledTime < _retryAt)) return;
        Clear();
        _region = region;
        int generation = _generation;
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

    public static void Clear()
    {
        _generation++;
        foreach (var handle in Handles.Values)
            if (handle.IsValid()) Addressables.Release(handle);
        Handles.Clear();
        _region = null;
        _failed = false;
        _readyAt = -1;
    }
}
