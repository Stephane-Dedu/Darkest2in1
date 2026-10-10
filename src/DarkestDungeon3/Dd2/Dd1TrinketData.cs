using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Condition;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;

namespace DarkestDungeon3.Dd2;

internal static class Dd1TrinketData
{
    public static Dd1Trinket Get(string id) => id?.StartsWith(FadedMemory.TrinketPrefix, StringComparison.Ordinal) == true
        ? Session.Current?.MemoryTrinkets?.Get(id.Substring(FadedMemory.TrinketPrefix.Length)) : null;
    public static string Name(string id) => Get(id) is { } t ? Session.Current.Lore.Text("str_inventory_title_trinket" + t.Id)
        ?? Ui.HamletUi.Pretty(t.Id) : null;
    public static IEnumerable<Dd1TrinketEffect> Effects(string id) => Get(id) is { } t
        ? t.BuffIds.Select(b => Dd1TrinketEffect.Adapt(Session.Current.Content.Buffs.Get(b))).Where(b => b != null)
        : Enumerable.Empty<Dd1TrinketEffect>();
    public static string Description(string id) => string.Join("\n", Effects(id).Select(b => b.Text));

    public static bool Register(string id)
    {
        var t = Get(id);
        var items = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
        if (t == null || items?.IsInitializationFinished() != true) return false;
        if (items.GetHasLibraryKey(id)) return true;
        var buffIds = new List<string>();
        int index = 0;
        foreach (var effect in Effects(id))
        {
            string buffId = id + "_" + index++;
            if (effect.Stats == null) continue;
            var stats = new ActorDataStats(buffId, effect.Stats); stats.Init(buffId);
            SingletonMonoBehaviour<Library<string, ActorDataStats>>.Instance.AddLibraryElement(stats, overrideCSV: true);
            if (effect.Condition != null)
            {
                var condition = new ConditionDefinition(buffId, effect.Condition);
                SingletonMonoBehaviour<Library<string, ConditionDefinition>>.Instance.AddLibraryElement(condition, overrideCSV: true);
            }
            var buff = new BuffDefinition(buffId, "m_DurationType,infinite,\nm_IsVisible,False,\nm_showPopText,False,\n"); buff.Init();
            SingletonMonoBehaviour<Library<string, BuffDefinition>>.Instance.AddLibraryElement(buff, overrideCSV: true);
            buffIds.Add(buffId);
        }
        var external = new DataExternalBuffs(id, "buffs," + string.Join(",", buffIds) + ",\n"); external.Init(id);
        SingletonMonoBehaviour<Library<string, DataExternalBuffs>>.Instance.AddLibraryElement(external, overrideCSV: true);
        var item = new ItemDefinition(id, "m_type,trinket,\nm_possessionLimit,1,\nm_maxQty,1,\nm_combinable,False,\nm_isConsumable,False,\nsub_type," + (t.Rarity == "ancestral" ? "epic" : "rare") + ",\n", 0);
        items.AddLibraryElement(item, overrideCSV: true);
        Plugin.Log.LogInfo($"[DD1 trinket] registered {id}: {buffIds.Count} native modifiers");
        return true;
    }

    public static float StressMultiplier(ActorInstance actor) => actor?.GetTrinketInventory()?.GetItemIds() is { } ids
        ? Math.Max(0, 1f + ids.Sum(id => Effects(id).Sum(b => b.Stress))) : 1f;
}

[HarmonyLib.HarmonyPatch(typeof(Assets.Code.Locale.Localization), nameof(Assets.Code.Locale.Localization.GetString))]
internal static class Dd1TrinketNames
{
    private static bool Prefix(string key, ref string __result)
    {
        if (key?.StartsWith("item_name_" + FadedMemory.TrinketPrefix, StringComparison.Ordinal) != true) return true;
        string name = Dd1TrinketData.Name(key.Substring(10));
        if (name == null) return true;
        __result = name; return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Assets.Code.Locale.Localization), nameof(Assets.Code.Locale.Localization.TryGetString))]
internal static class Dd1TrinketNamesTry
{
    private static bool Prefix(string key, ref string __result)
    {
        if (key?.StartsWith("item_name_" + FadedMemory.TrinketPrefix, StringComparison.Ordinal) != true) return true;
        string name = Dd1TrinketData.Name(key.Substring(10));
        if (name == null) return true;
        __result = name; return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ItemDescription), nameof(ItemDescription.GetDescription), new[] { typeof(ItemDefinition), typeof(int), typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(string) })]
internal static class Dd1NativeTrinketDescription
{
    private static bool Prefix(ItemDefinition itemDef, bool hideTitle, ref string __result)
    {
        if (Dd1TrinketData.Get(itemDef?.m_id) is not { } item) return true;
        __result = (hideTitle ? "" : Dd1TrinketData.Name(itemDef.m_id) + "\n" + item.Rarity.Replace('_', ' ') + "\n")
            + (item.HeroClasses.Count == 0 ? "" : string.Join(", ", item.HeroClasses) + " only\n") + Dd1TrinketData.Description(itemDef.m_id);
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.ApplyStressDamage))]
internal static class Dd1TrinketStress
{
    private static readonly Random Rng = new();
    private static void Prefix(ActorInstance __instance, ref float damage)
    {
        if (damage <= 0 || Runtime.Driver.Instance?.Phase is not (Phase.Crawling or Phase.Fighting)) return;
        float k = Dd1TrinketData.StressMultiplier(__instance);
        if (k == 1) return;
        float n = damage * k, whole = (float)Math.Floor(n);
        damage = whole + (Rng.NextDouble() < n - whole ? 1 : 0);
    }
}
