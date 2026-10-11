using System.Linq;
using DarkestDungeon3.Core.Campaign.Town;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Uploads the private plate/feedback textures through the campaign's bounded PNG queue.</summary>
internal static class HamletArt
{
    private static TownArtPack _prepared;
    private static bool _failed, _reported;
    public static TownArtPack Pack => Session.Current?.TownArt;

    public static void Prepare()
    {
        var pack = Pack;
        if (pack == null || ReferenceEquals(pack, _prepared)) return;
        _prepared = pack; _failed = _reported = false;
        foreach (string path in pack.Images) Art.PrepareImage(path);
    }

    public static bool Ready
    {
        get
        {
            Prepare();
            if (_prepared == null || _failed || !_prepared.Images.All(Art.ImageFinished)) return false;
            if (_prepared.Images.Any(p => Art.Png(p) == null))
            {
                _failed = true;
                Plugin.Log.LogWarning("[hamlet art] PNG unavailable; keeping the installed DD1 town.");
                return false;
            }
            // A decoded but mis-sized plate cannot share the mask's coordinate space.
            var background = Art.Png(_prepared.Background);
            if (background.width != TownArtPack.CanvasWidth || background.height != TownArtPack.CanvasHeight
                || _prepared.Buildings.Any(b => Art.Png(b.Tint).width != b.Width || Art.Png(b.Tint).height != b.Height
                    || Art.Png(b.Outline).width != b.Width || Art.Png(b.Outline).height != b.Height))
            {
                _failed = true;
                Plugin.Log.LogWarning("[hamlet art] Texture bounds mismatch; keeping the installed DD1 town.");
                return false;
            }
            if (!_reported)
            {
                _reported = true;
                Plugin.Log.LogInfo("[hamlet art] private hillside ready: 10 services, visible masks, 1920x1080");
            }
            return true;
        }
    }

    public static void Feedback(TownArtPack.Building building, Color colour, bool outline = false)
    {
        var old = GUI.color;
        try
        {
            GUI.color = colour;
            GUI.DrawTexture(new Rect(building.X, building.Y, building.Width, building.Height), Art.Png(outline ? building.Outline : building.Tint));
        }
        finally { GUI.color = old; }
    }
}
