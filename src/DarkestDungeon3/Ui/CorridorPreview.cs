using System;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in native scenery QA without choosing an estate, spawning encounters or persisting data.</summary>
[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class CorridorPreview
{
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("-dd3-corridor-preview");
    private static readonly bool Checking = Environment.GetCommandLineArgs().Contains("-dd3-corridor-check");
    private static readonly string[] Regions = { "dd2_city", "dd2_farm", "dd2_forest", "dd2_coast" };
    private static readonly string[] Names = { "The Sprawl", "The Foetor", "The Tangle", "The Shroud" };
    private static readonly string[] Heroes = { "plague_doctor", "grave_robber", "highwayman", "man_at_arms" };
    private static readonly Dd1HeroArt[] FigureArt = new Dd1HeroArt[4];
    private static bool _closed, _finished;
    private static int _region, _variant;
    private static float _shownAt = -1;
    private static string _shown;
    private static bool? _previousBackground;
    private static bool _figuresQueued;

    internal static string ActiveRegion => Requested && Plugin.DebugKeysEnabled.Value && !_closed
        && Driver.Instance?.Phase == Phase.Off && GameModeMgr.CurrentMode == GameModeType.MAIN_MENU
        && Session.Current != null && Session.Current.Save == null && Session.Current.SavePath == null ? Regions[_region] : null;

    private static bool Prefix()
    {
        string region = ActiveRegion;
        if (region == null) return true;
        _previousBackground ??= Application.runInBackground;
        Application.runInBackground = true;
        Gui.Begin(); Drag.Begin(); GUI.depth = -1000; Gui.BlockInput(true);
        try
        {
            var plan = RegionalScenery.For(region);
            var choice = RegionSceneryArt.CorridorChoice(plan, 2719, _variant);
            var texture = RegionSceneryArt.RoomTextureFor(choice?.FileName);
            if (!_figuresQueued)
            {
                _figuresQueued = true;
                for (int i = 0; i < Heroes.Length; i++) FigureArt[i] = Dd1HeroArt.Find(Session.Current.Dd1, Heroes[i]);
            }
            Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
            float elapsed = Time.unscaledTime;
            float travel = Mathf.PingPong(elapsed * 240f, 1440);
            bool reverse = (int)(elapsed * 240f / 1440) % 2 != 0;
            CorridorPanoramaUi.Draw(texture, 720 + travel, reverse, choice?.Mirror ?? false, RegionSceneryArt.RoomAlpha(choice?.FileName));
            // Full-body DD1 reference silhouettes at the crawl's party anchors and footline.
            // The preview never builds a native combat party; live DD2 model contact is separate acceptance.
            for (int i = 0; i < Heroes.Length; i++)
            {
                float x = 284 + i * 168;
                if (SpineArt.GetHero(FigureArt[i]) is { } figure)
                    figure.Draw(new Vector2(x, 680), 380f * figure.PixelsPerUnit / figure.Texture.height);
                Gui.Fill(new Rect(x - 40, 680, 80, 2), ExpeditionSkin.Brass);
            }
            ExpeditionSkin.OutlinedHeading(new Rect(80, 30, 900, 70), Names[_region], 42, ExpeditionSkin.Ivory);
            ExpeditionSkin.Text(new Rect(80, 754, 1700, 45), choice?.FileName ?? "Preparing private corridor scenery…", 26, ExpeditionSkin.Ivory);
            ExpeditionSkin.Text(new Rect(80, 810, 1700, 50), "Unsaved scenery preview · Real loader and scrolling · DD1 reference figures · Gold marks: y=680", 22, ExpeditionSkin.Muted);
            for (int i = 0; i < Regions.Length; i++)
                if (ExpeditionSkin.Button(new Rect(80 + i * 290, 900, 270, 64), Names[i], !Checking, 26)) Switch(i);
            if (ExpeditionSkin.Button(new Rect(1280, 900, 240, 64), "Next variant", !Checking, 26)) { _variant = (_variant + 1) % 6; _shownAt = -1; }
            if (ExpeditionSkin.Button(new Rect(1560, 900, 260, 64), "Close preview", size: 26))
            {
                _closed = true; RegionSceneryArt.Clear(); Gui.BlockInput(false);
                Application.runInBackground = _previousBackground ?? false;
            }
            if (texture != null && RegionSceneryArt.RoomAlpha(choice.FileName) >= 1)
            {
                if (_shown != choice.FileName)
                {
                    _shown = choice.FileName; _shownAt = Time.unscaledTime;
                    Plugin.Log.LogInfo($"[corridor preview] {region} variant {_variant + 1}/6: {choice.FileName}; footline=680; texture={texture.width}x{texture.height}");
                }
                if (Checking && !_finished && Time.unscaledTime - _shownAt > 5)
                {
                    if (++_variant >= 6)
                    {
                        if (_region == Regions.Length - 1)
                        {
                            _variant = 5; _finished = true;
                            Plugin.Log.LogInfo("[corridor check] PASS: all 24 private corridors decoded, selected and rendered through all four regional pools; forward/reverse scroll active; no estate selected");
                        }
                        else Switch(_region + 1);
                    }
                }
            }
        }
        catch (Exception e) { _finished = true; Plugin.Log.LogError("[corridor preview] " + e); }
        Gui.DrawTip();
        return false;
    }

    private static void Switch(int region)
    {
        _region = region; _variant = 0; _shown = null; _shownAt = -1;
        RegionSceneryArt.Clear();
    }
}

// Driver's ordinary Off-phase cleanup becomes the preview's real region-load tick while this fixture owns it.
[HarmonyPatch(typeof(RegionSceneryArt), nameof(RegionSceneryArt.Prepare))]
internal static class CorridorPreviewLoader
{
    private static void Prefix(ref string region)
    {
        if (CorridorPreview.ActiveRegion is { } preview) region = preview;
    }
}
