using System;
using System.Collections.Generic;
using System.IO;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Private selector paintings. Interface text and campaign state never come from these images.</summary>
internal static class ExpeditionMapArt
{
    private static readonly Dictionary<string, Texture2D> Images = new();
    private static readonly HashSet<string> Zones = new()
    {
        "dd2_city", "dd2_farm", "dd2_forest", "dd2_coast", "dd2_cave",
        "crypts", "warrens", "weald", "cove", "darkestdungeon"
    };

    public static Texture2D Background => Image("map.png");

    public static Texture2D Overlay(string zone) => zone is "crypts" or "warrens" or "weald" or "cove" or "dd2_cave"
        ? Image("overlay-" + zone + ".png") : null;

    public static Texture2D Preview(string zone) => zone != null && Zones.Contains(zone)
        ? Image("preview-" + zone + ".png") : null;

    private static readonly HashSet<string> Map3DLocations = new() { "crypts", "warrens", "weald", "cove", "darkestdungeon" };
    private static readonly Dictionary<string, ExpeditionMap3D> Layouts = new();

    /// <summary>The pre-rendered 3D overview (map3d.png); null without it, and then the painted map stays.</summary>
    public static Texture2D Map3D => Image("map3d.png");

    /// <summary>A map position's zoomed frame (map3d-zoom-&lt;location&gt;.png), same camera direction, closer.</summary>
    public static Texture2D Map3DZoom(string location) => location != null && Map3DLocations.Contains(location)
        ? Image("map3d-zoom-" + location + ".png") : null;

    /// <summary>map3d.json beside the images. Null when missing or unusable; read once per folder.</summary>
    public static ExpeditionMap3D Map3DLayout
    {
        get
        {
            string folder = Plugin.ExpeditionArtPath?.Value;
            if (string.IsNullOrWhiteSpace(folder)) return null;
            string path;
            try { path = Path.GetFullPath(Path.Combine(folder, "map3d.json")); }
            catch { return null; }
            if (Layouts.TryGetValue(path, out var layout)) return layout;
            layout = null;
            try
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("layout exceeds 256 KB");
                    layout = ExpeditionMap3D.Parse(File.ReadAllText(path));
                    if (layout == null) throw new InvalidDataException("no valid map position");
                    Plugin.Log.LogInfo($"[embark-art] 3D map layout ready ({layout.Spots.Count} positions)");
                }
            }
            catch (Exception e)
            {
                layout = null;
                Plugin.Log.LogWarning($"[embark-art] map3d.json: {e.Message}; retaining the painted map");
            }
            Layouts[path] = layout;
            return layout;
        }
    }

    private static Texture2D Image(string name)
    {
        string folder = Plugin.ExpeditionArtPath?.Value;
        if (string.IsNullOrWhiteSpace(folder)) return null;
        string path;
        try { path = Path.GetFullPath(Path.Combine(folder, name)); }
        catch { return null; }
        if (Images.TryGetValue(path, out var image)) return image;
        image = null;
        try
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("image exceeds 16 MB");
                image = Art.Png(path);
                if (image != null && (image.width > 4096 || image.height > 2304
                    || image.width < 64 || image.height < 32))
                    throw new InvalidDataException("invalid selector image dimensions");
                if (image != null && (name == "map.png" || name.StartsWith("map3d", StringComparison.Ordinal)))
                {
                    float ratio = (float)image.width / image.height;
                    if (ratio < 1.7f || ratio > 1.9f) throw new InvalidDataException("selector map must be widescreen");
                    if (name == "map.png") Plugin.Log.LogInfo($"[embark-art] approved map ready ({image.width}x{image.height})");
                    else if (name == "map3d.png") Plugin.Log.LogInfo($"[embark-art] 3D map ready ({image.width}x{image.height})");
                }
            }
        }
        catch (Exception e)
        {
            image = null;
            Plugin.Log.LogWarning($"[embark-art] {name}: {e.Message}; retaining existing art");
        }
        Images[path] = image;
        return image;
    }
}
