using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.Rendering;
using Assets.Code.Rendering.MaterialPropertyBlocks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Dd2;

/// <summary>Use DD2's warm combat material settings for the corridor and fights in place.</summary>
internal sealed class HeroCombatPalette
{
    // Owner's reference is a Foetor fight. Its native material preset replaces the prefab's blue shadow colour
    // and supplies that battle's hero colour controls, without loading an arena scene.
    private const string Asset = "Assets/Data/Affinity/MaterialPropertySettings/combat_arenas/MaterialPropertySettings_Combat_Arena_Farm.asset";
    private readonly Object _source;
    private AsyncOperationHandle<MaterialPropertySettings> _load;
    private static IResourceLocation _presetLocation;
    private bool _started, _reported;
    private readonly Dictionary<ActorBhv, int> _applied = new();
    // The arena preset assumes DD2's final scene grading. Fights in place and the corridor omit that grading:
    // its 1.6 grey tint intensity and 1.5 brightness wash out the ungraded models. Keep the warm shadow palette,
    // lower the lift and retain a little more texture colour in both presentations.
    private readonly MaterialPropertyOverride[] _ungraded;
    private readonly MaterialPropertyOverride _corridorTint;

    internal HeroCombatPalette(Object source, bool corridor = false)
    {
        _source = source;
        var values = new List<MaterialPropertyOverride>
        {
            new("_Brightness", 1.25f),
            new("_ColorTintIntensity", 1f),
            new("_Saturation", 1.10f),
        };
        // The isolated camera still renders neutral cloth blue with the arena's warm shadow preset.
        // Balance its final material tint against the native fight view. Keep this off fight actors:
        // they already receive the arena lighting, and would be warmed a second time.
        if (corridor)
        {
            _corridorTint = new MaterialPropertyOverride("_ColorTintColor", new Color(0.96f, 0.90f, 0.74f, 1f), MaterialPropertyBlendType.Override);
            values.Add(_corridorTint);
        }
        _ungraded = values.ToArray();
    }

    internal void SetArenaLighting(bool arenaLoaded)
    {
        // Stage heroes remain visible while the arena loads behind the cover. Its render environment
        // changes before the crossfade; continuing to compensate for the road then makes them yellow.
        // Mutate the registered override: AddOverride with the same source only updates its weight.
        if (_corridorTint != null)
            _corridorTint.Color = arenaLoaded ? new Color(0.89f, 0.89f, 0.89f, 1f) : new Color(0.96f, 0.90f, 0.74f, 1f);
    }

    private static IResourceLocation FindPreset()
    {
        if (_presetLocation != null) return _presetLocation;
        // The shipping catalog exposes GUID keys; the asset path is an InternalId, not a loadable key.
        foreach (var locator in Addressables.ResourceLocators)
            foreach (var key in locator.Keys)
                if (locator.Locate(key, typeof(MaterialPropertySettings), out var locations))
                    foreach (var location in locations)
                        if (string.Equals(location.InternalId, Asset, System.StringComparison.OrdinalIgnoreCase))
                            return _presetLocation = location;
        return null;
    }

    internal void Apply(ActorBhv actor)
    {
        if (!_started)
        {
            _started = true;
            var location = FindPreset();
            if (location != null) _load = Addressables.LoadAssetAsync<MaterialPropertySettings>(location);
            else Plugin.Log.LogWarning("[hero-palette] Foetor combat material preset was not found in the installed catalog");
        }
        if (!_load.IsValid() || !_load.IsDone || actor == null || actor.IsLoading || !actor.IsInitialized) return;
        if (_load.Status != AsyncOperationStatus.Succeeded || _load.Result == null)
        {
            if (!_reported) Plugin.Log.LogWarning("[hero-palette] combat palette unavailable: " + _load.OperationException?.Message);
            _reported = true;
            return;
        }
        int count = actor.MaterialPropertyBlocks.Count;
        int signature = count;
        foreach (var block in actor.MaterialPropertyBlocks)
            signature = unchecked(signature * 31 + (block != null ? block.GetInstanceID() : 0));
        if (count == 0 || _applied.TryGetValue(actor, out int previous) && previous == signature) return;
        _load.Result.AddTarget(actor, 10, _source, 1f);
        foreach (var block in actor.MaterialPropertyBlocks)
            foreach (var value in _ungraded)
                block.AddOverride(value, 11, 1f, _source);
        _applied[actor] = signature;
        Plugin.Log.LogInfo($"[hero-palette] {actor.GetActorGuid()}: Foetor combat materials applied ({count} blocks)");
    }

    internal void Clear()
    {
        if (_started && _load.IsValid() && _load.IsDone && _load.Status == AsyncOperationStatus.Succeeded && _load.Result != null)
            foreach (var actor in _applied.Keys)
                if (actor != null)
                {
                    _load.Result.RemoveTarget(actor, _source);
                    foreach (var block in actor.MaterialPropertyBlocks)
                        foreach (var value in _ungraded)
                            block.RemoveOverride(value, _source);
                }
        _applied.Clear();
    }

    internal void Dispose()
    {
        Clear();
        if (_started && _load.IsValid()) Addressables.Release(_load);
    }
}
