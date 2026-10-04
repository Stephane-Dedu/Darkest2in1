using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>Installed DD1 palette entries and shared_id aliases, in packed RGBA order.</summary>
public sealed class Dd1Colours
{
    private readonly Dictionary<string, DarkestRecord> _entries;
    private Dd1Colours(IEnumerable<DarkestRecord> records) => _entries = records.Where(r => r.Type == "colour" && r.Str("id") != null)
        .GroupBy(r => r.Str("id")).ToDictionary(g => g.Key, g => g.Last());
    public static Dd1Colours Load(string path) => new(DarkestFile.Load(path));
    public static Dd1Colours Parse(string text) => new(DarkestFile.Parse(text));

    public uint? Rgba(string id)
    {
        var seen = new HashSet<string>();
        while (id != null && seen.Add(id) && _entries.TryGetValue(id, out var entry))
        {
            if (entry.Str("shared_id") is { } shared) { id = shared; continue; }
            var rgba = entry.Values("rgba");
            if (rgba.Count == 1 && rgba[0].StartsWith("#") && uint.TryParse(rgba[0].Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
                return rgba[0].Length == 7 ? (hex << 8) | 255u : rgba[0].Length == 9 ? hex : (uint?)null;
            if (rgba.Count == 4)
            {
                uint packed = 0;
                foreach (string component in rgba)
                {
                    if (!byte.TryParse(component, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value)) return null;
                    packed = (packed << 8) | value;
                }
                return packed;
            }
            return null;
        }
        return null;
    }
}
