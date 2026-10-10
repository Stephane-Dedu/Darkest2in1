using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// The user's own Darkest Dungeon 1 install. The mod reads DD1's data and art from here at runtime and
/// never ships any of it.
/// </summary>
public sealed class Dd1Install
{
    public string Root { get; }

    private Dd1Install(string root) => Root = root;

    public string PathOf(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

    /// <summary>DD1's DLC folders (dlc/&lt;id&gt;, sorted), without the multiplayer arena.</summary>
    public IEnumerable<string> DlcFolders()
    {
        string dlcs = PathOf("dlc");
        if (!Directory.Exists(dlcs)) yield break;
        foreach (var dir in Directory.GetDirectories(dlcs).OrderBy(p => p, StringComparer.Ordinal))
            if (Path.GetFileName(dir).IndexOf("arena", StringComparison.OrdinalIgnoreCase) < 0) yield return dir;
    }

    public string MapGenerator => PathOf("scripts", "map_generator.darkest");

    /// <summary>The Spine skeleton and atlas in a DD1 animation folder (named x.sprite.skel or x.skel), or null.</summary>
    public static (string skel, string atlas)? SpineIn(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        string name = Path.GetFileName(folder.TrimEnd('/', Path.DirectorySeparatorChar));
        foreach (var stem in new[] { name + ".sprite", name })
        {
            string skel = Path.Combine(folder, stem + ".skel"), atlas = Path.Combine(folder, stem + ".atlas");
            if (File.Exists(skel) && File.Exists(atlas)) return (skel, atlas);
        }
        return null;
    }

    /// <summary>A curio's animation folder (props/shared/curios/&lt;id&gt;), or null.</summary>
    public string CurioArt(string curioId) => PathOf("props", "shared", "curios", curioId);
    public string ZoneDir(string zone) => PathOf("dungeons", zone);
    public string ZoneProps(string zone) => PathOf("dungeons", zone, zone + ".props.darkest");
    public string Rules => PathOf("shared", "rules.json");

    public static bool LooksLikeDd1(string dir) =>
        !string.IsNullOrEmpty(dir)
        && File.Exists(Path.Combine(dir, "scripts", "map_generator.darkest"))
        && Directory.Exists(Path.Combine(dir, "dungeons"));

    /// <summary>
    /// Find DD1: an explicit path first, then the default Steam folders and every Steam library listed in
    /// libraryfolders.vdf.
    /// </summary>
    public static Dd1Install Find(string configuredPath = null)
    {
        foreach (var dir in Candidates(configuredPath))
            if (LooksLikeDd1(dir)) return new Dd1Install(Path.GetFullPath(dir));
        return null;
    }

    private static IEnumerable<string> Candidates(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) yield return configuredPath;

        var steamRoots = new List<string>
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        };
        foreach (var drive in DriveLetters())
            steamRoots.Add(drive + @"SteamLibrary");

        var libraries = new List<string>(steamRoots);
        foreach (var root in steamRoots)
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            string text;
            try { text = File.ReadAllText(vdf); } catch { continue; }
            foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            yield return Path.Combine(lib, "steamapps", "common", "DarkestDungeon");
    }

    private static IEnumerable<string> DriveLetters()
    {
        try { return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.Name).ToList(); }
        catch { return Array.Empty<string>(); }
    }
}
