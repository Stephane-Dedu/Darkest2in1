using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Code.Item;
using Assets.Code.UI.Items;
using Assets.Code.Utils;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestDungeon3.Dd2;

/// <summary>Local DD1 artwork in separate native inventory prefabs; shared resources stay untouched.</summary>
internal static class Dd1TrinketIcons
{
    private static readonly Dictionary<string, GameObject> Prefabs = new();

    public static string Id(object item) => item is ItemDefinition definition ? definition.m_id
        : (item as IReadOnlyItemInstance)?.GetItemDefinition()?.m_id;

    // Known custom items wait for the native template. Missing local art retains DD2's fallback.
    public static bool TryGet(string id, out GameObject prefab)
    {
        prefab = null;
        if (Dd1TrinketData.Get(id) is not { } item) return false;
        if (Prefabs.TryGetValue(id, out prefab) && prefab != null) return true;
        var texture = Art.Dd1("panels", "icons_equip", "trinket", "inv_trinket+" + item.Id + ".png");
        if (texture == null) return false;
        var resource = Singleton<ResourceDatabaseItem>.Instance?.GetResource("default_item");
        var loader = Singleton<ItemLoaderBhv>.Instance;
        if (resource?.m_iconPrefab == null || !resource.m_iconPrefab.RuntimeKeyIsValid() || loader == null
            || !loader.TryGetIfReady(resource.m_iconPrefab.AssetGUID, out var template)) return true;

        var clone = Object.Instantiate(template);
        clone.name = id + "_icon";
        clone.SetActive(false);
        Object.DontDestroyOnLoad(clone);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        sprite.name = id;
        foreach (var image in clone.GetComponentsInChildren<Image>(true))
        {
            image.sprite = sprite;
            image.overrideSprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        Prefabs[id] = prefab = clone;
        return true;
    }

    public static IEnumerable<MethodBase> Methods(string name) => AccessTools.GetDeclaredMethods(typeof(InventoryUiUtils))
        .Where(m => m.Name == name && m.GetParameters().Length == 1
            && (m.GetParameters()[0].ParameterType == typeof(ItemDefinition)
                || m.GetParameters()[0].ParameterType == typeof(IReadOnlyItemInstance)));
}

[HarmonyPatch]
internal static class Dd1NativeTrinketIcon
{
    private static IEnumerable<MethodBase> TargetMethods() => Dd1TrinketIcons.Methods(nameof(InventoryUiUtils.GetItemIconPrefab));
    private static bool Prefix(object __0, ref GameObject __result)
    {
        if (!Dd1TrinketIcons.TryGet(Dd1TrinketIcons.Id(__0), out var prefab)) return true;
        __result = prefab;
        return false;
    }
}

[HarmonyPatch]
internal static class Dd1NativeTrinketIconLoaded
{
    private static IEnumerable<MethodBase> TargetMethods() => Dd1TrinketIcons.Methods(nameof(InventoryUiUtils.IsItemIconLoaded));
    private static bool Prefix(object __0, ref bool __result)
    {
        if (!Dd1TrinketIcons.TryGet(Dd1TrinketIcons.Id(__0), out var prefab)) return true;
        __result = prefab != null;
        return false;
    }
}
