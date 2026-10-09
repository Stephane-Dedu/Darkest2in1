using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>Owner-generated art is optional and stays outside the distributed mod.</summary>
public static class PrivateRoomScenery
{
    public const int MaxVariants = 12;
    public const int MaxFileBytes = 16 * 1024 * 1024;

    public static IReadOnlyList<string> Find(string folder, string region) => Find(folder, region, "arena");

    public static IReadOnlyList<string> FindCorridors(string folder, string region) => Find(folder, region, "corridor");

    private static IReadOnlyList<string> Find(string folder, string region, string kind)
    {
        if (RegionalScenery.For(region) == null || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();
        string prefix = region + "-" + kind + "-";
        return Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name.Length == prefix.Length + 6 && name.StartsWith(prefix, StringComparison.Ordinal)
                && name.EndsWith(".png", StringComparison.Ordinal) && name[prefix.Length] >= '0' && name[prefix.Length] <= '9'
                && name[prefix.Length + 1] >= '0' && name[prefix.Length + 1] <= '9'
                && name.Substring(prefix.Length, 2) != "00")
            .OrderBy(name => name, StringComparer.Ordinal).Take(MaxVariants).ToArray();
    }

    /// <summary>Check allocation bounds before Unity decodes/uploads a PNG.</summary>
    public static bool ValidPng(byte[] data)
    {
        if (data == null || data.Length < 33 || data.Length > MaxFileBytes) return false;
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        for (int i = 0; i < signature.Length; i++) if (data[i] != signature[i]) return false;
        if (data[8] != 0 || data[9] != 0 || data[10] != 0 || data[11] != 13
            || data[12] != 'I' || data[13] != 'H' || data[14] != 'D' || data[15] != 'R') return false;
        uint width = (uint)data[16] << 24 | (uint)data[17] << 16 | (uint)data[18] << 8 | data[19];
        uint height = (uint)data[20] << 24 | (uint)data[21] << 16 | (uint)data[22] << 8 | data[23];
        return width >= 1920 && width <= 4096 && height >= 720 && height <= 2048
            && (double)width / height >= 2.6 && (double)width / height <= 2.7
            && data[24] == 8 && (data[25] == 2 || data[25] == 6);
    }
}
