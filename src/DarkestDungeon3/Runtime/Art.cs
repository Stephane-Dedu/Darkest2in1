using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1 art loaded straight from the user's DD1 install (never shipped). PNGs become textures on first use and stay
/// cached; missing files return null so screens fall back to plain panels.
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

    public static Texture2D TownBackdrop => Dd1("campaign", "town", "town_bg.png");

    public static Texture2D BuildingIcon(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".icon.png")
        ?? Dd1("campaign", "town", "buildings", building, building + ".character.png");

    public static Texture2D BuildingBackground(string building) =>
        Dd1("campaign", "town", "buildings", building, building + ".character_background.png");

    public static Texture2D CorridorBackground(string zone) => Dd1("dungeons", zone, zone + ".corridor_bg.png");

    public static Texture2D CorridorWall(string zone, int index) =>
        Dd1("dungeons", zone, $"{zone}.corridor_wall.{index % 7:00}.png") ?? Dd1("dungeons", zone, $"{zone}.corridor_wall.00.png");

    public static Texture2D RoomWall(string zone, string kind) =>
        Dd1("dungeons", zone, $"{zone}.room_wall.{kind}.png") ?? Dd1("dungeons", zone, $"{zone}.room_wall.empty.png");

    public static Texture2D EntranceWall(string zone) => Dd1("dungeons", zone, $"{zone}.entrance_room_wall.png");
}
