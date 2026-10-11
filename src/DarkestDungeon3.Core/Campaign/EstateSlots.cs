using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>Campaign slots, including recoverable backup-only estates. Directory discovery never reads save contents.</summary>
public static class EstateSlots
{
    private static readonly Regex FileName = new(@"^estate_([1-9][0-9]*)\.json(?:\.bak)?$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<int> List(string directory)
    {
        if (!Directory.Exists(directory)) return Array.Empty<int>();
        return Directory.EnumerateFiles(directory, "estate_*")
            .Select(path => FileName.Match(Path.GetFileName(path)))
            .Where(match => match.Success)
            .Select(match => int.TryParse(match.Groups[1].Value, out int slot) ? slot : 0)
            .Where(slot => slot > 0).Distinct().OrderBy(slot => slot).ToArray();
    }

    public static int Next(string directory)
    {
        var slots = List(directory);
        return slots.Count == 0 ? 1 : checked(slots[slots.Count - 1] + 1);
    }

    public static string PathFor(string directory, int slot)
    {
        if (slot <= 0) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(directory, $"estate_{slot}.json");
    }

    public static void Delete(string directory, int slot)
    {
        string path = PathFor(directory, slot);
        // Remove the recovery copy first so a successful deletion cannot revive the estate next launch.
        File.Delete(path + ".bak");
        File.Delete(path);
    }
}
