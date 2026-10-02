using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Utils;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1 art loaded straight from the user's DD1 install (never shipped), plus DD2 hero portraits from the running
/// game. PNGs become textures on first use and stay cached; missing files return null so screens can fall back.
/// </summary>
internal static class Art
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D Dd1(params string[] parts)
    {
        var session = Session.Current;
        if (session == null) return null;
        string path = session.Dd1.PathOf(parts);
        if (Cache.TryGetValue(path, out var tex)) return tex;

        tex = null;
        if (File.Exists(path))
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(File.ReadAllBytes(path), markNonReadable: true))
            {
                Object.Destroy(tex);
                tex = null;
            }
        }
        Cache[path] = tex;
        return tex;
    }

    // ---- town ----
    public static Texture2D TownBackdrop => Dd1("campaign", "town", "town_bg.png");

    public static Texture2D BuildingIcon(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".icon.png")
        ?? Dd1("campaign", "town", "buildings", building, building + ".character.png");

    public static Texture2D BuildingBackground(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".character_background.png");

    // ---- dungeon scene ----
    public static Texture2D CorridorBackground(string zone) => Dd1("dungeons", zone, zone + ".corridor_bg.png");

    public static Texture2D CorridorWall(string zone, int index) =>
        Dd1("dungeons", zone, $"{zone}.corridor_wall.{((index % 7) + 7) % 7:00}.png") ?? Dd1("dungeons", zone, $"{zone}.corridor_wall.00.png");

    public static Texture2D CorridorDoor(string zone) => Dd1("dungeons", zone, $"{zone}.corridor_door.basic.png") ?? CorridorWall(zone, 0);

    public static Texture2D ForegroundTop(string zone) => Dd1("dungeons", zone, $"{zone}.foreground_top.01.png");
    public static Texture2D ForegroundBottom(string zone) => Dd1("dungeons", zone, $"{zone}.foreground_bottom.01.png");

    private static readonly Dictionary<string, List<string>> RoomKinds = new();

    /// <summary>The zone's room backdrops (room_wall.*.png, entrance excluded), picked stably per room.</summary>
    public static Texture2D RoomWall(string zone, int roomId)
    {
        if (!RoomKinds.TryGetValue(zone, out var kinds))
        {
            kinds = new List<string>();
            var dir = Session.Current?.Dd1.ZoneDir(zone);
            if (dir != null && Directory.Exists(dir))
                kinds = Directory.GetFiles(dir, zone + ".room_wall.*.png").Select(Path.GetFileName)
                                 .Where(f => !f.Contains(".entrance.")).OrderBy(f => f).ToList();
            RoomKinds[zone] = kinds;
        }
        return kinds.Count == 0 ? null : Dd1("dungeons", zone, kinds[(roomId * 7 + 3) % kinds.Count]);
    }

    public static Texture2D EntranceWall(string zone) => Dd1("dungeons", zone, $"{zone}.entrance_room_wall.png") ?? RoomWall(zone, 0);

    // ---- HUD ----
    public static Texture2D Panel(string file) => Dd1("panels", file);
    public static Texture2D Overlay(string file) => Dd1("overlays", file);
    public static Texture2D MapIcon(string name) => Dd1("panels", "icons_map", name + ".png");

    /// <summary>DD1 inventory art for one of our item keys (food, gold, torch, bust, ruby...).</summary>
    public static Texture2D InventoryIcon(string key, int count, int stackLimit)
    {
        int fill = stackLimit <= 1 ? 3 : Mathf.Clamp((int)(4f * count / stackLimit - 0.01f), 0, 3);
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

    public static Sprite Portrait(string classId, ResourceActor.PortraitIconType type = ResourceActor.PortraitIconType.Color)
    {
        string key = classId + "/" + type;
        if (Portraits.TryGetValue(key, out var s)) return s;
        try
        {
            var res = Singleton<ResourceDatabaseActors>.Instance?.GetResource(classId, isErrorValid: false);
            s = res?.GetPortraitIconByType(type);
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"portrait {key}: {e.Message}"); s = null; }
        Portraits[key] = s;
        return s;
    }

    /// <summary>Draw a sprite (from an atlas) into a rect, keeping its aspect ratio.</summary>
    public static void DrawSprite(Rect r, Sprite s, bool fit = true)
    {
        if (s == null || s.texture == null) return;
        var t = s.texture;
        var tr = s.textureRect;
        var uv = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
        if (fit)
        {
            float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
            float w = tr.width * k, h = tr.height * k;
            r = new Rect(r.x + (r.width - w) / 2f, r.yMax - h, w, h);   // bottom-aligned: heroes stand on the floor
        }
        GUI.DrawTextureWithTexCoords(r, t, uv, true);
    }
}
