using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>Owner's native or generated art is optional and stays outside the distributed mod.</summary>
public static class PrivateRoomScenery
{
    public const int MaxVariants = 12;
    public const int MaxFileBytes = 16 * 1024 * 1024;
    private static readonly string[] Roles = { "battle", "boss", "entrance", "quiet", "treasure" };

    public static IReadOnlyList<string> Find(string folder, string region) => Find(folder, region, "arena");

    public static IReadOnlyList<string> FindCorridors(string folder, string region) => Find(folder, region, "corridor");

    private static IReadOnlyList<string> Find(string folder, string region, string kind)
    {
        if (RegionalScenery.For(region) == null || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();
        string prefix = region + "-" + kind + "-";
        return Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => ValidName(name, prefix) || kind == "arena" && SceneRole(name, region) != null)
            // A role pack supersedes old generic extensions without requiring deletion of the rollback art.
            .OrderBy(name => SceneRole(name, region) == null ? 1 : 0)
            .ThenBy(name => name, StringComparer.Ordinal).Take(MaxVariants).ToArray();
    }

    private static bool ValidName(string name, string prefix) => name != null && name.Length == prefix.Length + 6
        && name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal)
        && name[prefix.Length] >= '0' && name[prefix.Length] <= '9'
        && name[prefix.Length + 1] >= '0' && name[prefix.Length + 1] <= '9'
        && name.Substring(prefix.Length, 2) != "00";

    public static string SceneRole(string fileName, string region) => Roles.FirstOrDefault(role =>
        ValidName(fileName, region + "-arena-" + role + "-"));

    /// <summary>Keep the room's original purpose after clearing, revisiting and reloading; never consume gameplay RNG.</summary>
    public static RoomSceneryChoice Choose(RegionalScenery plan, int seed, Room room, IReadOnlyList<string> files)
    {
        if (room == null || room.Id < 0) return null;
        string role = room.IsSecret ? "treasure" : room.Content == RoomContent.Boss ? "boss"
            : room.Content == RoomContent.Entrance ? "entrance" : room.HasBattle ? "battle"
            : room.Content == RoomContent.Treasure ? "treasure" : "quiet";
        var available = files ?? Array.Empty<string>();
        var matching = available.Where(file => SceneRole(file, plan.Region) == role).ToArray();
        if (matching.Length == 0 && role == "boss")
            matching = available.Where(file => SceneRole(file, plan.Region) == "battle").ToArray();
        if (matching.Length > 0)
        {
            var choice = plan.RoomBackground(seed, room.Id, matching);
            return new RoomSceneryChoice(choice.FileName, false); // Native focal compositions keep their orientation.
        }
        var legacy = available.Where(file => ValidName(file, plan.Region + "-arena-")).ToArray();
        return plan.RoomBackground(seed, room.Id, legacy.Length > 0 ? legacy : plan.RoomBackgrounds);
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
