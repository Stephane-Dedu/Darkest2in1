using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>Private town plate and cropped, top-down visible-alpha masks. No Unity or campaign writes.</summary>
public sealed class TownArtPack
{
    public const int CanvasWidth = 1920, CanvasHeight = 1080;
    public string Background { get; private set; }
    public IReadOnlyList<Building> Buildings { get; private set; }
    public IEnumerable<string> Images => new[] { Background }.Concat(Buildings.SelectMany(b => new[] { b.Tint, b.Outline }));

    public sealed class Building
    {
        public string Id, Tint, Outline;
        public int X, Y, Width, Height, Order;
        internal byte[] Alpha;
        public bool Hit(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || x < X || y < Y || x >= X + Width || y >= Y + Height) return false;
            return Alpha[((int)y - Y) * Width + (int)x - X] >= 48;
        }
    }

    public Building Hit(float x, float y)
    {
        for (int i = Buildings.Count - 1; i >= 0; i--)
            if (Buildings[i].Hit(x, y)) return Buildings[i];
        return null;
    }

    /// <summary>Missing pack is optional. A partial/invalid pack rejects as a whole so scenery and doors agree.</summary>
    public static TownArtPack Load(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "manifest.json"))) return null;
        string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string FileInPack(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Town art path must be relative.");
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("Town art file is missing or outside its pack: " + relative);
            return path;
        }
        var json = JObject.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        if ((int?)json["version"] != 1 || (int?)json["width"] != CanvasWidth || (int?)json["height"] != CanvasHeight)
            throw new InvalidDataException("Unsupported town art canvas/version.");
        var items = json["buildings"] as JArray ?? throw new InvalidDataException("Town art buildings are missing.");
        var buildings = new List<Building>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var b = new Building { Id = (string)item["id"], X = (int)item["x"], Y = (int)item["y"],
                Width = (int)item["width"], Height = (int)item["height"], Order = (int)item["order"] };
            if (!global::DarkestDungeon3.Core.Campaign.Town.Buildings.All.Contains(b.Id) || !ids.Add(b.Id)) throw new InvalidDataException("Unknown or duplicate town building.");
            if (b.X < 0 || b.Y < 0 || b.Width < 1 || b.Height < 1 || b.Width > CanvasWidth || b.Height > CanvasHeight
                || b.X > CanvasWidth - b.Width || b.Y > CanvasHeight - b.Height)
                throw new InvalidDataException("Town building bounds are outside the canvas.");
            string mask = FileInPack((string)item["mask"]);
            if (new FileInfo(mask).Length != b.Width * b.Height) throw new InvalidDataException("Town mask dimensions do not match.");
            b.Alpha = File.ReadAllBytes(mask);
            if (!b.Alpha.Any(a => a >= 48)) throw new InvalidDataException("Town building has no visible doorway area.");
            b.Tint = FileInPack((string)item["tint"]);
            b.Outline = FileInPack((string)item["outline"]);
            buildings.Add(b);
        }
        if (ids.Count != global::DarkestDungeon3.Core.Campaign.Town.Buildings.All.Length) throw new InvalidDataException("Town art must include every service building.");
        return new TownArtPack { Background = FileInPack((string)json["background"]), Buildings = buildings.OrderBy(b => b.Order).ToArray() };
    }
}
