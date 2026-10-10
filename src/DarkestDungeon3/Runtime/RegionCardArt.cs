using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.DLC;
using Assets.Code.Map.Generation.Biome;
using Assets.Code.Platform;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Ui;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD2's region-choice paintings (the innkeeper's destination cards) for the DD2-style quest select. Each region's
/// BiomeData points at its painting; the inn loads them only for its own choices, so we load them from the Hamlet
/// with our own handles and release them when the screen closes. Without one, a card falls back to other art.
/// </summary>
internal static class RegionCardArt
{
    private static readonly Dictionary<string, Sprite> Paintings = new();
    private static readonly HashSet<string> Requested = new();
    private static readonly List<AsyncOperationHandle> Handles = new();
    private static List<BiomeData> _biomes;
    private static bool _locating;
    private static int _generation;

    /// <summary>The region's DD2 painting, or null while it loads (or for areas DD2 has none for).</summary>
    public static Sprite Painting(string zone)
    {
        var types = BiomesOf(zone);
        if (types == null) return null;
        if (Paintings.TryGetValue(zone, out var sprite)) return sprite;
        if (_biomes == null) { Locate(); return null; }
        if (!Requested.Add(zone)) return null;
        var reference = types.Select(type => _biomes.FirstOrDefault(b => b != null && b.GetBiomeType() == type))
            .Select(biome => biome?.GetChoiceSprite()).FirstOrDefault(r => r != null && r.RuntimeKeyIsValid());
        if (reference == null) { Paintings[zone] = null; return null; }
        int generation = _generation;
        var handle = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
        Handles.Add(handle);
        handle.Completed += op =>
        {
            if (generation != _generation) return;
            Paintings[zone] = op.Status == AsyncOperationStatus.Succeeded ? op.Result : null;
            if (Paintings[zone] == null) Plugin.Log.LogWarning($"[destinations] {zone}: DD2 painting unavailable");
        };
        return null;
    }

    /// <summary>Paint a region picture over <paramref name="r"/> without stretching it (cut to fit).</summary>
    public static void DrawCover(Rect r, Sprite sprite, float fromTop = 0.35f)
    {
        if (Event.current.type != EventType.Repaint || sprite == null || sprite.texture == null) return;
        if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
        {
            Art.DrawSprite(r, sprite);
            return;
        }
        var texture = sprite.texture;
        var crop = DestinationLayout.Cover(sprite.textureRect, r.width, r.height, fromTop);
        GUI.DrawTextureWithTexCoords(r, texture,
            new Rect(crop.x / texture.width, crop.y / texture.height, crop.width / texture.width, crop.height / texture.height), true);
    }

    /// <summary>Let go of every painting (the destination screen closed).</summary>
    public static void Release()
    {
        _generation++;
        foreach (var handle in Handles)
            if (handle.IsValid()) Addressables.Release(handle);
        Handles.Clear();
        Paintings.Clear();
        Requested.Clear();
        _biomes = null;
        _locating = false;
    }

    private static BiomeType[] BiomesOf(string zone) => zone switch
    {
        "dd2_city" => new[] { BiomeType.CITY },
        "dd2_farm" => new[] { BiomeType.FARM },
        "dd2_forest" => new[] { BiomeType.FOREST },
        "dd2_coast" => new[] { BiomeType.COAST },
        "dd2_cave" => new[] { BiomeType.CAVE },
        QuestBoard.DarkestDungeon => new[] { BiomeType.MOUNTAIN_BRAIN, BiomeType.MOUNTAIN_LUNGS, BiomeType.MOUNTAIN_EYES, BiomeType.MOUNTAIN_ARMS, BiomeType.MOUNTAIN_BODY },
        _ => null,
    };

    /// <summary>DD2's biome database when it is up; otherwise the BiomeData assets under the owned DLC labels, as
    /// the database itself finds them.</summary>
    private static void Locate()
    {
        if (_locating) return;
        _locating = true;
        int generation = _generation;
        try
        {
            if (Singleton<ResourceDatabaseBiomeData>.TryGetInstance(out var database) && database.GetBiomeData(BiomeType.CITY) != null)
            {
                _biomes = new[] { BiomeType.CITY, BiomeType.FARM, BiomeType.FOREST, BiomeType.COAST, BiomeType.CAVE,
                        BiomeType.MOUNTAIN_BRAIN, BiomeType.MOUNTAIN_LUNGS, BiomeType.MOUNTAIN_EYES, BiomeType.MOUNTAIN_ARMS, BiomeType.MOUNTAIN_BODY }
                    .Select(database.GetBiomeData).Where(b => b != null).ToList();
                Plugin.Log.LogInfo($"[destinations] DD2 biome database: {_biomes.Count} regions");
                return;
            }
            var dlc = DLCManager.Instance;
            var platform = PlatformMgr.Instance;
            if (dlc == null || platform == null) { _locating = false; return; }
            var labels = dlc.GetAllOwnedDLCLabels(platform.IsForceLoadDLC());
            var locate = Addressables.LoadResourceLocationsAsync((IEnumerable)labels, Addressables.MergeMode.Union, typeof(BiomeData));
            Handles.Add(locate);
            locate.Completed += op =>
            {
                if (generation != _generation) return;
                var locations = op.Status == AsyncOperationStatus.Succeeded ? op.Result.ToList() : new List<IResourceLocation>();
                var found = new List<BiomeData>();
                int pending = locations.Count;
                if (pending == 0) { _biomes = found; Plugin.Log.LogWarning("[destinations] no DD2 biome data found"); }
                foreach (var location in locations)
                {
                    var load = Addressables.LoadAssetAsync<BiomeData>(location);
                    Handles.Add(load);
                    load.Completed += loaded =>
                    {
                        if (generation != _generation) return;
                        if (loaded.Status == AsyncOperationStatus.Succeeded && loaded.Result != null) found.Add(loaded.Result);
                        if (--pending > 0) return;
                        _biomes = found;
                        Plugin.Log.LogInfo($"[destinations] DD2 biome data loaded: {found.Count} regions");
                    };
                }
            };
        }
        catch (Exception e)
        {
            _biomes = new List<BiomeData>();
            Plugin.Log.LogWarning($"[destinations] DD2 region paintings: {e.Message}");
        }
    }
}
