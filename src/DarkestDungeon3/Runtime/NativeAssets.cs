using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Runtime;

/// <summary>Resolves installed assets whose Addressables keys are GUIDs rather than their source paths.</summary>
internal static class NativeAssets
{
    private static readonly Dictionary<Type, Dictionary<string, IResourceLocation>> Locations = new();

    public static AsyncOperationHandle<T> Load<T>(string path) where T : UnityEngine.Object
    {
        IResourceLocation found = null;
        foreach (var locator in Addressables.ResourceLocators)
            if (locator.Locate(path, typeof(T), out var direct) && direct.Count > 0) { found = direct[0]; break; }
        if (found == null)
        {
            if (!Locations.TryGetValue(typeof(T), out var index))
            {
                Locations[typeof(T)] = index = new Dictionary<string, IResourceLocation>(StringComparer.OrdinalIgnoreCase);
                foreach (var locator in Addressables.ResourceLocators)
                    foreach (var key in locator.Keys)
                        if (locator.Locate(key, typeof(T), out var locations))
                            foreach (var location in locations) index[location.InternalId] = location;
            }
            index.TryGetValue(path, out found);
        }
        return found != null ? Addressables.LoadAssetAsync<T>(found) : Addressables.LoadAssetAsync<T>(path);
    }
}
