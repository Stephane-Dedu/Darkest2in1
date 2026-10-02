using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>
/// Where DD1 stands its town buildings (campaign/town/town.layout.darkest). Each building has a 2D ".pos"
/// (the projection of its 3D ".pos3d" through the town camera) and a depth used for draw order.
/// </summary>
public sealed class TownLayout
{
    public sealed class Spot
    {
        public string Id;            // building id, also the fx folder stem: fx/town_&lt;id&gt;_level01
        public float X, Y;           // screen position of the skeleton origin (feet), 1920x1080, y down
        public float Depth;          // larger is farther
        public float Scale = 1f;
        public float NameOffsetX, NameOffsetY;
    }

    /// <summary>DD1's 2D town space puts y = 0 at 862 px from the top (the camera height minus the screen middle).</summary>
    public const float GroundLine = 862f;

    /// <summary>Back to front: draw in this order.</summary>
    public readonly List<Spot> Spots = new();

    public Spot Ground => Spots.FirstOrDefault(s => s.Id == "ground");
    public Spot this[string id] => Spots.FirstOrDefault(s => s.Id == id);

    public static TownLayout Load(Dd1Install dd1) => Parse(File.ReadAllText(dd1.PathOf("campaign", "town", "town.layout.darkest")));

    public static TownLayout Parse(string text)
    {
        var layout = new TownLayout();
        foreach (var r in DarkestFile.Parse(text))
        {
            if (!r.Type.EndsWith("_layout") || !r.Has("pos") || !r.Has("pos3d")) continue;
            var pos = Floats(r, "pos");
            var pos3d = Floats(r, "pos3d");
            if (pos.Length < 2 || pos3d.Length < 3) continue;
            var text_ = Floats(r, "text_offset");
            layout.Spots.Add(new Spot
            {
                Id = r.Type.Substring(0, r.Type.Length - "_layout".Length),
                X = pos[0],
                Y = GroundLine - pos[1],
                Depth = pos3d[2],
                Scale = Floats(r, "scale").FirstOrDefault() is var s && s > 0 ? s : 1f,
                NameOffsetX = text_.Length > 0 ? text_[0] : 0,
                NameOffsetY = text_.Length > 1 ? text_[1] : 0,
            });
        }
        layout.Spots.Sort((a, b) => b.Depth.CompareTo(a.Depth));
        return layout;
    }

    // Some values carry stray characters (".text_offset -30 -75#"): read the number part only.
    private static float[] Floats(DarkestRecord r, string key) =>
        r.Values(key).Select(v =>
        {
            string num = new string(v.TakeWhile(c => char.IsDigit(c) || c == '-' || c == '.' || c == '+').ToArray());
            return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN;
        }).Where(f => !float.IsNaN(f)).ToArray();

    /// <summary>
    /// The art folder for a building: fx/town_&lt;id&gt;_level01..03 by how much of it is upgraded (DD1's
    /// level_thresholds 0.33 / 0.66), _locked before it opens; buildings without levels use fx/town_&lt;id&gt;.
    /// </summary>
    public static string ArtFolder(Dd1Install dd1, string id, bool open, float upgradedFraction)
    {
        string Folder(string suffix) => dd1.PathOf("fx", "town_" + id + suffix);
        if (!open && Directory.Exists(Folder("_locked"))) return Folder("_locked");
        int level = upgradedFraction >= 0.66f ? 3 : upgradedFraction >= 0.33f ? 2 : 1;
        for (int l = level; l >= 1; l--)
            if (Directory.Exists(Folder($"_level0{l}"))) return Folder($"_level0{l}");
        return Folder("");
    }
}
