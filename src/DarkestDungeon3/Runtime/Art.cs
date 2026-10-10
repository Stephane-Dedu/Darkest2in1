using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1 art loaded straight from the user's DD1 install (never shipped), plus DD2 hero portraits from the running
/// game. PNGs become textures on first use and stay cached; missing files return null so screens can fall back.
/// </summary>
internal static class Art
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly PngPreloader TownImages = new();

    public static Texture2D Dd1(params string[] parts)
    {
        var session = Session.Current;
        if (session == null) return null;
        return Png(session.Dd1.PathOf(parts));
    }

    public static void PrepareTown(Dd1Install dd1)
    {
        foreach (string relative in PngPreloader.InitialTownImages)
        {
            string path = dd1.PathOf(relative.Split('/'));
            if (!Cache.ContainsKey(path)) TownImages.Request(path);
        }
    }

    public static void Update() => TownImages.Update();

    // ---- town ----
    public static Texture2D TownBackdrop => Dd1("campaign", "town", "town_bg.png");

    public static Texture2D BuildingIcon(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".icon.png")
        ?? Dd1("campaign", "town", "buildings", building, building + ".character.png");

    public static Texture2D BuildingBackground(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".character_background.png");

    // ---- dungeon scene: the files each zone really has (Core's ZoneArt) ----

    private static readonly Dictionary<string, ZoneArt> Zones = new();

    /// <summary>The zone's art; the Darkest Dungeon's is per quest (from the expedition's plot quest).</summary>
    public static ZoneArt ZoneArtOf(string zone)
    {
        var session = Session.Current;
        if (session == null || zone == null) return null;
        int quest = 1;
        string plot = Driver.Instance?.Expedition?.Quest?.PlotId;
        if (zone == "darkestdungeon" && plot != null && plot.Length > 0 && char.IsDigit(plot[plot.Length - 1])) quest = plot[plot.Length - 1] - '0';
        string key = zone + "/" + quest;
        if (!Zones.TryGetValue(key, out var art)) Zones[key] = art = ZoneArt.Load(session.Dd1, zone, quest);
        return art;
    }

    /// <summary>A PNG by full path (cached).</summary>
    public static Texture2D Png(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Cache.TryGetValue(path, out var tex)) return tex;
        if (TownImages.TryGet(path, out tex, out bool finished))
        {
            if (finished) Cache[path] = tex;
            return tex;
        }
        tex = null;
        if (System.IO.File.Exists(path))
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(path), markNonReadable: true)) { Object.Destroy(tex); tex = null; }
        }
        Cache[path] = tex;
        return tex;
    }

    public static Texture2D CorridorBackground(string zone) => Png(ZoneArtOf(zone)?.Background);
    public static Texture2D CorridorMid(string zone) => Png(ZoneArtOf(zone)?.Mid);
    public static Texture2D EndHall(string zone) => Png(ZoneArtOf(zone)?.EndHall);
    public static Texture2D CorridorWall(string zone, int index) => Png(ZoneArtOf(zone)?.Wall(index));
    public static Texture2D CorridorDoor(string zone) => Png(ZoneArtOf(zone)?.Door) ?? CorridorWall(zone, 0);
    public static Texture2D ForegroundTop(string zone) => Png(ZoneArtOf(zone)?.ForegroundTop);
    public static Texture2D ForegroundBottom(string zone) => Png(ZoneArtOf(zone)?.ForegroundBottom);
    public static Texture2D RoomWall(string zone, int roomId) => Png(ZoneArtOf(zone)?.Room(roomId)) ?? CorridorWall(zone, roomId);
    public static Texture2D EntranceWall(string zone) => Png(ZoneArtOf(zone)?.Entrance) ?? RoomWall(zone, 0);
    public static Texture2D BossRoomWall(string zone, string boss, int difficulty) =>
        Png(ZoneArtOf(zone)?.BossRoom(boss, "plot_kill_" + (boss?.Length > 2 ? boss.Substring(0, boss.Length - 2) : boss)
            + "_" + (difficulty <= 1 ? 1 : difficulty <= 3 ? 2 : 3))) ?? RoomWall(zone, 0);

    // ---- HUD ----
    public static Texture2D Panel(string file) => Dd1("panels", file);
    public static Texture2D Overlay(string file) => Dd1("overlays", file);
    public static Texture2D MapIcon(string name) => Dd1("panels", "icons_map", name + ".png");

    /// <summary>DD1 inventory art for one of our item keys (food, gold, torch, bust, ruby...).</summary>
    public static Texture2D InventoryIcon(string key, int count, int stackLimit)
    {
        int fill = stackLimit <= 1 ? 3 : Mathf.Clamp((int)(4f * count / stackLimit - 0.01f), 0, 3);
        if (key.StartsWith("quest_item+", System.StringComparison.Ordinal)) return Dd1("panels", "icons_equip", "quest_item", "inv_" + key + ".png");
        if (Core.Campaign.JournalPages.TryPage(key, out _)) return Dd1("panels", "icons_equip", "journal_page", "inv_journal_page.png");
        return key switch
        {
            "food" => Dd1("panels", "icons_equip", "provision", $"inv_provision+_{fill}.png"),
            "gold" => Dd1("panels", "icons_equip", "gold", $"inv_gold+_{fill}.png"),
            "bust" or "portrait" or "deed" or "crest" => Dd1("panels", "icons_equip", "heirloom", $"inv_heirloom+{key}.png"),
            _ => Dd1("panels", "icons_equip", "supply", $"inv_supply+{key}.png")
                 ?? Dd1("panels", "icons_equip", "gem", $"inv_gem+{key}.png")
                 ?? Dd1("panels", "icons_equip", "quest_item", $"inv_quest_item+{key}.png"),
        };
    }

    // ---- DD2 hero portraits ----
    private static readonly Dictionary<string, Sprite> Portraits = new();
    private static readonly Dictionary<string, float> PortraitRetry = new();

    public static Sprite Portrait(string classId, ResourceActor.PortraitIconType type = ResourceActor.PortraitIconType.Color)
    {
        if (classId == null) return null;
        if (Core.Campaign.Town.RecruitClasses.IsDd1Only(classId)) return Dd2.Dd1HeroClasses.Portrait(classId);
        string key = classId + "/" + type;
        if (Portraits.TryGetValue(key, out var s))
        {
            if (s != null && s.texture != null) return s;
            Portraits.Remove(key);
        }
        // Outside a run (DD2's main menu) the portrait atlas may not be loaded yet: retry now and then.
        if (PortraitRetry.TryGetValue(key, out float next) && Time.unscaledTime < next) return null;
        try
        {
            s = Dd2.ActorResources.Get(classId)?.GetPortraitIconByType(type);
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"portrait {key}: {e.Message}"); s = null; }
        if (s != null && s.texture != null) Portraits[key] = s;
        else { PortraitRetry[key] = Time.unscaledTime + 2f; s = null; }
        return s;
    }

    /// <summary>A small hero picture for lists: the colour portrait, else DD2's painted story bust.</summary>
    public static Sprite HeroIcon(string classId) => Portrait(classId) ?? LargePortrait(classId, LargeArt.Story);

    // DD2's larger hero art lives behind addressable references; load once, asynchronously, and cache.
    private static readonly Dictionary<string, Sprite> Large = new();
    private static readonly HashSet<string> LargeRequested = new();
    private static readonly Dictionary<string, float> LargeRetry = new();

    public enum LargeArt { Altar, HeroStory, Story }

    /// <summary>A large DD2 hero picture, or null until it has loaded (callers fall back to the portrait).</summary>
    public static Sprite LargePortrait(string classId, LargeArt kind)
    {
        if (classId == null) return null;
        string key = classId + "/" + kind;
        if (Large.TryGetValue(key, out var s))
        {
            if (s != null && s.texture != null) return s;
            RetryLarge(key);
        }
        if (LargeRetry.TryGetValue(key, out float next) && Time.unscaledTime < next) return null;
        if (LargeRequested.Add(key))
        {
            try
            {
                var res = Dd2.ActorResources.Get(classId);
                if (res == null)
                {
                    RetryLarge(key);   // native resources are still loading
                    return null;
                }
                var reference = kind switch
                {
                    LargeArt.Altar => res?.m_ClassAltarPortraitReference,
                    LargeArt.HeroStory => res?.m_HeroStoryPortrait,
                    _ => res?.StoryPortraitSpriteReference,
                };
                if (reference != null && reference.RuntimeKeyIsValid())
                {
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
                    handle.Completed += h =>
                    {
                        if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && h.Result != null && h.Result.texture != null)
                        {
                            Large[key] = h.Result;
                            LargeRetry.Remove(key);
                            var r = h.Result.rect;
                            Plugin.Log.LogInfo($"[art] {key}: {r.width}x{r.height}");
                        }
                        else
                        {
                            UnityEngine.AddressableAssets.Addressables.Release(h);
                            RetryLarge(key);
                        }
                    };
                }
                else RetryLarge(key);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[art] {key}: {e.Message}"); RetryLarge(key); }
        }
        return null;
    }

    /// <summary>The picture of a hero standing in the dungeon: the largest DD2 art available, else the portrait.</summary>
    private static readonly Dictionary<string, Sprite> Dd1Figures = new();
    public static Sprite HeroFigure(string classId)
    {
        if (Core.Campaign.Town.RecruitClasses.IsDd1Only(classId))
        {
            if (Dd1Figures.TryGetValue(classId, out var saved)) return saved;
            var picture = SpineArt.GetHero(Dd1HeroArt.Find(Session.Current?.Dd1, classId));
            if (picture?.Texture != null)
                return Dd1Figures[classId] = Sprite.Create(picture.Texture,
                    new Rect(0, 0, picture.Texture.width, picture.Texture.height), new Vector2(.5f, .5f), 100f);
            return Portrait(classId);
        }
        return LargePortrait(classId, Plugin.HeroArt.Value) ?? LargePortrait(classId, LargeArt.Story)
            ?? Portrait(classId, ResourceActor.PortraitIconType.Story) ?? Portrait(classId);
    }

    private static void RetryLarge(string key)
    {
        Large.Remove(key);
        LargeRequested.Remove(key);
        LargeRetry[key] = Time.unscaledTime + 2;
    }

    /// <summary>Draw a sprite (from an atlas) into a rect, keeping its aspect ratio.</summary>
    public static void DrawSprite(Rect r, Sprite s, bool fit = true, bool flipX = false)
        => SpriteArt.Draw(r, s, fit, flipX);
}
