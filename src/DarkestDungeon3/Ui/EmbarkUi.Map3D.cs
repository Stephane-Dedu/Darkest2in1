using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// The pre-rendered 3D expedition map: one overview frame of the valley and one closer frame per map position,
/// rendered privately from installed DD2 scenery. Clicking a region's scenery flies toward it: the overview
/// grows about the region and cross-fades into its own frame, where only that position's name and quests show.
/// Quests, region pairing, party, roster and provisioning are the ordinary selector's. Experimental and
/// reversible: the Painted map button, a missing pack or an invalid layout all retain the painted map.
/// </summary>
internal sealed partial class EmbarkUi
{
    internal static readonly Rect MapModeButton = new(1190, 42, 320, 58);
    private static readonly Rect MapBackButton = new(548, 122, 280, 54);
    private const float ZoomSeconds = 0.6f;
    private static readonly string[] Map3DOrder = { "crypts", "warrens", "weald", "cove", QuestBoard.DarkestDungeon };
    private static bool? _map3dPreviewMode;   // the unsaved preview never writes the owner's config

    private string _zoomLocation;   // the map position being flown to, shown or left
    private bool _zoomIn;
    private float _zoomT, _zoomClock = -1;
    private int _zoomWarm;

    private static bool Map3DWanted => ExpeditionPreview.OwnsSession && _map3dPreviewMode.HasValue
        ? _map3dPreviewMode.Value : Plugin.ExpeditionMap3D.Value;

    private static bool Map3DAvailable => ExpeditionMapArt.Map3D != null && ExpeditionMapArt.Map3DLayout != null;

    /// <summary>The switch sits where the Hamlet's town switch does; it only appears when the pack is usable.</summary>
    private void DrawMapModeSwitch()
    {
        if (!Map3DAvailable) return;
        bool on = Map3DWanted;
        bool hover = GUI.enabled && MapModeButton.Contains(Event.current.mousePosition);
        ExpeditionSkin.Banner(MapModeButton, hover ? ExpeditionSkin.Red : ExpeditionSkin.Brass);
        ExpeditionSkin.Text(new Rect(MapModeButton.x + 12, MapModeButton.y + 4, MapModeButton.width - 24, MapModeButton.height - 8),
            on ? "Painted map" : "3D map", 27, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter, heading: true);
        if (!hover || Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
        Event.current.Use();
        if (ExpeditionPreview.OwnsSession) _map3dPreviewMode = !on; else Plugin.ExpeditionMap3D.Value = !on;
        _zoomLocation = null; _zoomT = 0; _zoomIn = false;
    }

    /// <summary>Draws the 3D background and advances the camera move. False: the painted map must be drawn.</summary>
    private bool DrawMap3DBackground()
    {
        if (!Map3DWanted || !Map3DAvailable) { _zoomLocation = null; _zoomT = 0; return false; }
        var overview = ExpeditionMapArt.Map3D;
        var layout = ExpeditionMapArt.Map3DLayout;
        var canvas = new Rect(0, 0, Gui.W, Gui.H);

        if (Event.current.type == EventType.Repaint)
        {
            float now = Time.unscaledTime;
            float dt = _zoomClock < 0 ? 0 : Mathf.Clamp(now - _zoomClock, 0, 0.1f);
            _zoomClock = now;
            _zoomT = Mathf.MoveTowards(_zoomT, _zoomIn ? 1 : 0, dt / ZoomSeconds);
            if (!_zoomIn && _zoomT <= 0) _zoomLocation = null;
            // Decode one closer frame per repaint while the overview is up, so a click never waits on the disk.
            if (_zoomLocation == null && _zoomWarm < Map3DOrder.Length) ExpeditionMapArt.Map3DZoom(Map3DOrder[_zoomWarm++]);
        }

        ExpeditionMap3D.Spot spot = null;
        if (_zoomLocation != null && !layout.Spots.TryGetValue(_zoomLocation, out spot)) { _zoomLocation = null; _zoomT = 0; }
        if (spot == null) { GUI.DrawTexture(canvas, overview); return true; }

        var closer = ExpeditionMapArt.Map3DZoom(_zoomLocation);
        float fade = closer != null ? ExpeditionMap3D.ZoomFade(_zoomT) : 0;
        if (fade < 1)
        {
            var r = ExpeditionMap3D.OverviewRect(spot, _zoomT);
            GUI.DrawTexture(new Rect(r.X, r.Y, r.Width, r.Height), overview);
        }
        if (fade > 0)
        {
            var colour = GUI.color;
            GUI.color = new Color(1, 1, 1, fade);
            GUI.DrawTexture(canvas, closer);
            GUI.color = colour;
        }
        return true;
    }

    /// <summary>Names, quests and the clickable scenery of the 3D map, in place of the painted map's fixed spots.</summary>
    private void DrawMap3DRegions()
    {
        var layout = ExpeditionMapArt.Map3DLayout;
        bool moving = _zoomLocation != null && _zoomT > 0 && _zoomT < 1;
        bool zoomed = _zoomLocation != null && _zoomIn && _zoomT >= 1;

        if (!moving)
            foreach (string location in Map3DOrder)
            {
                if (!layout.Spots.TryGetValue(location, out var spot) || (zoomed && location != _zoomLocation)) continue;
                var pos = zoomed ? new Vector2(spot.ZoomLabelX, spot.ZoomLabelY) : new Vector2(spot.LabelX, spot.LabelY);
                if (location == QuestBoard.DarkestDungeon)
                {
                    DrawDungeon(QuestBoard.DarkestDungeon, pos, new[] { QuestBoard.DarkestDungeon });
                    continue;
                }
                var areas = CampaignRegions.AtLocation(E, location);
                if (areas.Count == 0) continue;
                if (!_mapAreas.TryGetValue(location, out var zone) || !areas.Contains(zone)) _mapAreas[location] = zone = areas[0];
                DrawDungeon(zone, pos, areas);
            }

        if (zoomed || (_zoomLocation != null && _zoomIn))
        {
            // Back out with the button, Escape or a right-click on the scenery.
            bool key = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            bool right = Event.current.type == EventType.MouseDown && Event.current.button == 1 && OverMap(Event.current.mousePosition);
            if ((zoomed && ExpeditionButton(MapBackButton, "Back to the map", size: 25)) || ((key || right) && GUI.enabled && _sheetHeroId == null))
            {
                if (key || right) Event.current.Use();
                _zoomIn = false;
            }
            return;
        }
        if (_zoomLocation != null) return;   // flying back out

        // Scenery hotspots come after the name plates and quest markers, so those keep their own clicks.
        var mouse = Event.current.mousePosition;
        foreach (string location in Map3DOrder)
        {
            if (!layout.Spots.TryGetValue(location, out var spot)) continue;
            if (location != QuestBoard.DarkestDungeon && CampaignRegions.AtLocation(E, location).Count == 0) continue;
            var r = new Rect(spot.Click.X, spot.Click.Y, spot.Click.Width, spot.Click.Height);
            if (GUI.enabled && r.Contains(mouse) && OverMap(mouse)) ExpeditionSkin.Tip(r, "Click to look closer");
            if (!Gui.Hotspot(r)) continue;
            _zoomLocation = location; _zoomIn = true; _zoomT = 0;
            string zone = location == QuestBoard.DarkestDungeon ? QuestBoard.DarkestDungeon
                : _mapAreas.TryGetValue(location, out var shown) ? shown : null;
            if (zone != null && zone != _focusedZone) FocusArea(zone);
        }
    }

    /// <summary>Pixels of the map itself: right of the quest dossier, left of the roster, above the party bar.</summary>
    private static bool OverMap(Vector2 p) => p.x > 530 && p.x < RosterColumn.X - 16 && p.y > 110 && p.y < 830;
}
