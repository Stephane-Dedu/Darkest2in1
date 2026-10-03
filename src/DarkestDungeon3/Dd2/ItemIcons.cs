using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.DLC;
using Assets.Code.Item;
using Assets.Code.Platform;
using Assets.Code.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's item pictures (trinkets) for the Hamlet. An item's ResourceItem points at an icon prefab; we load the
/// prefab once and keep the sprite of its image. Works at the main menu (DD2's item database exists only in a run)
/// by finding ResourceItem locations under the owned DLC labels, like ActorResources does for heroes.
/// </summary>
internal static class ItemIcons
{
    private static Dictionary<string, IResourceLocation> _locations;
    private static bool _locating;
    private static readonly Dictionary<string, Sprite> Icons = new();
    private static readonly HashSet<string> Loading = new();

    /// <summary>The item's DD2 picture, or null while it loads (or if DD2 has none).</summary>
    public static Sprite Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (Icons.TryGetValue(itemId, out var icon)) return icon;
        if (_locations == null) { FindLocations(); return null; }
        if (!Loading.Add(itemId)) return null;
        if (!_locations.TryGetValue(itemId, out var location)) { Icons[itemId] = null; return null; }
        var handle = Addressables.LoadAssetAsync<ResourceItem>(location);
        handle.Completed += op =>
        {
            var res = op.Status == AsyncOperationStatus.Succeeded ? op.Result : null;
            if (res?.m_iconPrefab == null || !res.m_iconPrefab.RuntimeKeyIsValid()) { Icons[itemId] = null; return; }
            var prefab = Addressables.LoadAssetAsync<GameObject>(res.m_iconPrefab.RuntimeKey);
            prefab.Completed += p =>
            {
                Icons[itemId] = p.Status == AsyncOperationStatus.Succeeded ? SpriteOf(p.Result) : null;
                if (Icons[itemId] == null) Plugin.Log.LogWarning($"[items] {itemId}: no sprite in its icon prefab");
            };
        };
        return null;
    }

    /// <summary>The biggest sprite among the prefab's images (the item art, not its frame or glow).</summary>
    private static Sprite SpriteOf(GameObject prefab)
    {
        if (prefab == null) return null;
        var sprites = prefab.GetComponentsInChildren<UnityEngine.UI.Image>(includeInactive: true).Select(i => i.sprite)
            .Concat(prefab.GetComponentsInChildren<SpriteRenderer>(includeInactive: true).Select(r => r.sprite))
            .Where(s => s != null && s.texture != null);
        return sprites.OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
    }

    private static void FindLocations()
    {
        if (_locating) return;
        var dlc = DLCManager.Instance;
        var platform = PlatformMgr.Instance;
        if (dlc == null || platform == null) return;
        _locating = true;
        var labels = dlc.GetAllOwnedDLCLabels(platform.IsForceLoadDLC());
        var handle = Addressables.LoadResourceLocationsAsync((IEnumerable)labels, Addressables.MergeMode.Union, typeof(ResourceItem));
        handle.Completed += op =>
        {
            var found = new Dictionary<string, IResourceLocation>();
            if (op.Status == AsyncOperationStatus.Succeeded)
                foreach (var location in op.Result)
                    if (location.TryGetAssetName(out var name)) found[name] = location;
            _locations = found;
            Plugin.Log.LogInfo($"[items] {found.Count} item resources available");
        };
    }
}
