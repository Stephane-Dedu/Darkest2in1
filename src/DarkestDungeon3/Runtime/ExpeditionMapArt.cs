using System;
using System.Collections.Generic;
using System.IO;
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
                if (image != null && name == "map.png")
                {
                    float ratio = (float)image.width / image.height;
                    if (ratio < 1.7f || ratio > 1.9f) throw new InvalidDataException("selector map must be widescreen");
                    Plugin.Log.LogInfo($"[embark-art] approved map ready ({image.width}x{image.height})");
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
