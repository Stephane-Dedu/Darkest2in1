using System;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>Optional native-painted depths for one canonical corridor. The complete plate remains the fallback.</summary>
public static class PrivateCorridorLayers
{
    public const int Count = 3;
    public const int Width = 4096, Height = 768;
    // One active corridor, at most 36 MiB of decoded RGBA companions. Never load a region's entire depth pack.
    public const int MaxDecodedBytes = Count * Width * Height * 4;
    private static readonly string[] Bands = { "back", "middle", "front" };

    public static string FileName(string scene, int layer)
    {
        if (layer < 0 || layer >= Count || scene == null) return null;
        int marker = scene.IndexOf("-corridor-", StringComparison.Ordinal);
        if (marker < 0 || RegionalScenery.For(scene.Substring(0, marker)) == null) return null;
        string number = scene.Substring(marker + 10);
        if (number.Length != 6 || number[0] < '0' || number[0] > '9' || number[1] < '0' || number[1] > '9'
            || number.Substring(2) != ".png" || number.StartsWith("00", StringComparison.Ordinal)) return null;
        return scene.Substring(0, scene.Length - 4) + ".depth-" + Bands[layer] + ".png";
    }

    public static bool ValidPng(byte[] data) => PrivateRoomScenery.ValidPng(data, corridor: true)
        && Read(data, 16) == Width && Read(data, 20) == Height;

    private static uint Read(byte[] data, int offset) => (uint)data[offset] << 24 | (uint)data[offset + 1] << 16
        | (uint)data[offset + 2] << 8 | data[offset + 3];
}
