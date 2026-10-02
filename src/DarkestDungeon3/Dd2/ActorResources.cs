using System.Collections;
using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.DLC;
using Assets.Code.Platform;
using Assets.Code.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's hero resources (portraits, story art) by class id. During a run DD2's own ResourceDatabaseActors has
/// them; at the main menu (where our Hamlet lives) that database doesn't exist, so we find the same addressable
/// locations it would (ResourceActor assets under the owned DLC labels) and load the few we need ourselves.
/// </summary>
internal static class ActorResources
{
    private static Dictionary<string, IResourceLocation> _locations;
    private static bool _locating;
    private static readonly Dictionary<string, ResourceActor> Loaded = new();
    private static readonly HashSet<string> Loading = new();

    /// <summary>The class's ResourceActor, or null while it loads.</summary>
    public static ResourceActor Get(string classId)
    {
        if (string.IsNullOrEmpty(classId)) return null;
        if (Singleton<ResourceDatabaseActors>.HasInstance())
        {
            try
            {
                var res = Singleton<ResourceDatabaseActors>.Instance.GetResource(classId, isErrorValid: false);
                if (res != null) return res;
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[actors] {classId}: {e.Message}"); }
        }
        if (Loaded.TryGetValue(classId, out var actor)) return actor;
        if (_locations == null) { FindLocations(); return null; }
        if (Loading.Add(classId))
        {
            if (!_locations.TryGetValue(classId, out var location))
            {
                Plugin.Log.LogWarning($"[actors] no ResourceActor named {classId}");
                return null;
            }
            var handle = Addressables.LoadAssetAsync<ResourceActor>(location);
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded) Loaded[classId] = op.Result;
                else Plugin.Log.LogWarning($"[actors] {classId} failed to load: {op.OperationException?.Message}");
            };
        }
        return null;
    }

    private static void FindLocations()
    {
        if (_locating) return;
        var dlc = DLCManager.Instance;
        var platform = PlatformMgr.Instance;
        if (dlc == null || platform == null) return;
        _locating = true;
        var labels = dlc.GetAllOwnedDLCLabels(platform.IsForceLoadDLC());
        var handle = Addressables.LoadResourceLocationsAsync((IEnumerable)labels, Addressables.MergeMode.Union, typeof(ResourceActor));
        handle.Completed += op =>
        {
            var found = new Dictionary<string, IResourceLocation>();
            if (op.Status == AsyncOperationStatus.Succeeded)
                foreach (var location in op.Result)
                    if (location.TryGetAssetName(out var name)) found[name] = location;
            _locations = found;
            Plugin.Log.LogInfo($"[actors] {found.Count} hero/monster resources available outside a run");
        };
    }
}
