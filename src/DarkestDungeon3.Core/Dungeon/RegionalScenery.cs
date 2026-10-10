using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>Installed DD2 art selected for DD1's side-view exploration. No game assets are distributed.</summary>
public sealed class RegionalScenery
{
    public readonly string Region;
    public readonly string ArrivalName;
    public readonly uint Ambient;
    public readonly IReadOnlyList<SceneryLayer> Layers;
    public readonly IReadOnlyList<string> RoomBackgrounds;
    public IEnumerable<string> AssetKeys => Layers.Select(layer => layer.AssetKey).Distinct();

    private RegionalScenery(string region, string arrivalName, uint ambient, params SceneryLayer[] layers)
    {
        Region = region; ArrivalName = arrivalName; Ambient = ambient; Layers = Array.AsReadOnly(layers);
        RoomBackgrounds = Array.AsReadOnly(new[] { region + "-01.png", region + "-02.png" });
    }

    /// <summary>Stable after reload/revisit, without consuming expedition RNG; consecutive room IDs get different scenes.</summary>
    public RoomSceneryChoice RoomBackground(int expeditionSeed, int roomId) => RoomBackground(expeditionSeed, roomId, RoomBackgrounds);

    /// <summary>One continuous scene per hallway, independent of rooms, heading, tile and gameplay RNG.</summary>
    public RoomSceneryChoice CorridorBackground(int expeditionSeed, int corridorId, IReadOnlyList<string> variants) =>
        RoomBackground(unchecked(expeditionSeed ^ 0x43A91D27), corridorId, variants);

    public RoomSceneryChoice RoomBackground(int expeditionSeed, int roomId, IReadOnlyList<string> variants)
    {
        if (roomId < 0 || variants == null || variants.Count == 0) return null;
        uint seed = unchecked((uint)expeditionSeed), room = (uint)roomId;
        int index = (int)((seed % (uint)variants.Count + room) % (uint)variants.Count);
        bool mirror = ((seed / (uint)variants.Count + room / (uint)variants.Count) & 1) != 0;
        return new RoomSceneryChoice(variants[index], mirror);
    }

    private const string Textures = "Assets/Art/Textures/Environments/";
    private const string Roads = Textures + "Tileable/Roads/";
    private static readonly Dictionary<string, RegionalScenery> Plans = new()
    {
        ["dd2_city"] = new("dd2_city", "Courtyard", 0x241817,
            new("Assets/Data/Biome/City/Skybox/skybox_hemisphere_01.tga", 0, 610, 2440, 0.08f),
            new(Textures + "City/city_back_concrete_01.tif", 30, 620, 1240, 0.22f),
            new(Textures + "City/city_mid_concrete_01.tif", 160, 490, 980, 0.45f),
            new("road_city_cobblestone_01", 610, 110, 720, 1, ground: true, tint: 0xA77B63)),
        ["dd2_farm"] = new("dd2_farm", "Farmstead", 0x514626,
            new(Textures + "Farm/farm_A_background_01.psd", 0, 620, 1240, 0.14f),
            new(Textures + "Farm/farm_A_midground_01.png", 130, 490, 980, 0.38f),
            new(Textures + "Farm/farm_A_wall_01.png", 395, 235, 940, 0.62f),
            new(Roads + "road_farm_01.png", 610, 110, 720, 1, ground: true, tint: 0xA88A52)),
        ["dd2_forest"] = new("dd2_forest", "Clearing", 0x26363c,
            new("Assets/Data/Biome/Forest/Skybox/skybox_biome_forest_long_texture.png", 0, 620, 1240, 0.08f),
            new(Textures + "Forest/Props/forest_skirt_01.tif", 100, 550, 1100, 0.25f),
            new(Textures + "Forest/Props/forest_skirt_02.tif", 300, 350, 1400, 0.55f),
            new(Roads + "road_forest_01.png", 610, 110, 720, 1, ground: true, tint: 0x9BABB1)),
        ["dd2_coast"] = new("dd2_coast", "Landing", 0x31424a,
            new(Textures + "Coast/Skirt/coast_skirt_cliffs_01.tif", 120, 530, 2120, 0.2f),
            new(Textures + "Coast/Skirt/coast_skirt_cliffs_01.tif", 410, 240, 960, 0.52f),
            new(Roads + "coast_road_sand_01.tif", 610, 110, 720, 1, ground: true, tint: 0x748992)),
    };

    public static RegionalScenery For(string region) => region != null && Plans.TryGetValue(region, out var plan) ? plan : null;
}

public sealed class RoomSceneryChoice
{
    public readonly string FileName;
    public readonly bool Mirror;
    public RoomSceneryChoice(string fileName, bool mirror) { FileName = fileName; Mirror = mirror; }
}

public sealed class SceneryLayer
{
    public readonly string AssetKey;
    public readonly float Top, Height, Width, Parallax;
    public readonly bool Ground;
    public readonly uint Tint;
    public SceneryLayer(string assetKey, float top, float height, float width, float parallax, bool ground = false, uint tint = 0xFFFFFF)
    { AssetKey = assetKey; Top = top; Height = height; Width = width; Parallax = parallax; Ground = ground; Tint = tint; }
}

/// <summary>Physical corridor coordinates survive walking backwards; adjacent mirrored strips meet at the same edge.</summary>
public static class CorridorSceneryLayout
{
    public const float TileWidth = 720, Anchor = 600, ViewWidth = 1920;

    public static float Camera(int corridorId, int tileIndex, bool reverse, float slide) =>
        (corridorId * 7 + tileIndex) * TileWidth - (reverse ? -1 : 1) * slide;

    /// <summary>Foreground eases away near either room; the road and distant scenery continue through the opening.</summary>
    public static float Opening(int tileIndex, int tileCount, bool reverse, float slide)
    {
        if (tileCount <= 0 || float.IsNaN(slide) || float.IsInfinity(slide)) return 0;
        float position = tileIndex - (reverse ? -1 : 1) * slide / TileWidth;
        float distance = Math.Min(position + 1, tileCount - position);
        float t = Math.Max(0, Math.Min(1, 1 - distance / 2));
        return t * t * (3 - 2 * t);
    }

    public static IEnumerable<SceneryTile> Tiles(float camera, float width, bool reverse)
    {
        if (float.IsNaN(camera) || float.IsInfinity(camera) || float.IsNaN(width) || float.IsInfinity(width) || width <= 0) yield break;
        float low = reverse ? camera - (ViewWidth - Anchor) : camera - Anchor;
        float high = reverse ? camera + Anchor : camera + (ViewWidth - Anchor);
        int first = (int)Math.Floor(low / width), last = (int)Math.Floor(high / width);
        for (int index = first; index <= last; index++)
        {
            float x = reverse ? Anchor + camera - (index + 1) * width : Anchor + index * width - camera;
            yield return new SceneryTile(index, x, ((index & 1) != 0) ^ reverse);
        }
    }
}

public readonly struct SceneryTile
{
    public readonly int Index;
    public readonly float X;
    public readonly bool Mirror;
    public SceneryTile(int index, float x, bool mirror) { Index = index; X = x; Mirror = mirror; }
}
