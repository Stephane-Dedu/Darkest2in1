using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// The pre-rendered 3D expedition map: one overview frame of the valley and one closer frame per map position,
/// rendered privately from installed DD2 scenery. Clicking a region's scenery or one of its quests flies toward it: the overview
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
                // The label stands above its region: the anchor is the bottom centre of its quest row.
                var anchor = zoomed ? new Vector2(spot.ZoomLabelX, spot.ZoomLabelY) : new Vector2(spot.LabelX, spot.LabelY);
                float marker = zoomed ? 72 : 60;
                if (location == QuestBoard.DarkestDungeon)
                {
                    DrawMap3DLabel(QuestBoard.DarkestDungeon, anchor, new[] { QuestBoard.DarkestDungeon }, marker);
                    continue;
                }
                var areas = CampaignRegions.AtLocation(E, location);
                if (areas.Count == 0) continue;
                if (!_mapAreas.TryGetValue(location, out var zone) || !areas.Contains(zone)) _mapAreas[location] = zone = areas[0];
                DrawMap3DLabel(zone, anchor, areas, marker);
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

    /// <summary>
    /// A region's label on the 3D map, centred above the region: a name banner (name, switch arrow, region level)
    /// over one row of up to four quest markers with DD1's own quest icons. The same rules as the painted map's
    /// plate: the arrow switches paired areas, the name focuses the area, extra quests scroll in rows of four.
    /// </summary>
    private void DrawMap3DLabel(string zone, Vector2 anchor, IReadOnlyList<string> areas, float marker)
    {
        const float gap = 8, plateHeight = 46;
        string name = AreaName(zone);
        bool mountain = zone == QuestBoard.DarkestDungeon;
        bool canSwitch = areas.Count > 1;
        E.ZoneXp.TryGetValue(zone, out int xp);
        int level = S.Campaign.ZoneLevel(xp);
        float nameWidth = ExpeditionSkin.Width(name, 28, heading: true);
        float plateWidth = nameWidth + 48 + (canSwitch ? 30 : 0) + (mountain ? 0 : 34);
        var plate = new Rect(anchor.x - plateWidth / 2, anchor.y - marker - gap - plateHeight, plateWidth, plateHeight);
        ExpeditionSkin.Banner(plate, zone == _focusedZone ? ExpeditionSkin.Brass : ExpeditionSkin.Ivory);
        float x = plate.x + 24;
        ExpeditionSkin.OutlinedHeading(new Rect(x, plate.y + 4, nameWidth + 6, plateHeight - 8), name, 28, ExpeditionSkin.Ivory);
        x += nameWidth + 8;
        if (canSwitch)
        {
            int at = areas.ToList().IndexOf(zone);
            string nextZone = areas[(at + 1) % areas.Count];
            var next = new Rect(x, plate.y + 8, 24, 30);
            ExpeditionSkin.Sprite(next, "ui_arrow", next.Contains(Event.current.mousePosition) ? ExpeditionSkin.Brass : ExpeditionSkin.Ivory, fit: true, flip: true);
            ExpeditionSkin.Tip(next, "Switch to " + AreaName(nextZone));
            if (Gui.Hotspot(next)) FocusArea(nextZone);
            x += 30;
        }
        if (!mountain)
        {
            var levelRect = new Rect(x, plate.y + 5, 30, plateHeight - 10);
            ExpeditionSkin.Text(levelRect, level.ToString(), 25, ExpeditionSkin.Brass, TextAnchor.MiddleCenter, heading: true);
            ExpeditionSkin.Tip(levelRect, $"Region level {level}\n{xp} experience");
        }
        // Registered after the arrow, so the name cannot take its clicks.
        if (Gui.Hotspot(plate)) FocusArea(zone);

        if (!mountain && !CampaignRegions.Unlocked(E, S.Campaign, zone))
        {
            var locked = new Rect(anchor.x - 150, anchor.y - 44, 300, 40);
            ExpeditionSkin.Sprite(locked, "ui_labelbg");
            ExpeditionSkin.Text(new Rect(locked.x + 8, locked.y + 2, locked.width - 16, 36), $"Opens after {CampaignRegions.UnlockAfter(S.Campaign, zone)} quests", 21, ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
            return;
        }
        var quests = E.Quests.Where(q => q.Dungeon == zone).ToList();
        int rows = Mathf.Max(1, (quests.Count + 3) / 4);
        _questRows.TryGetValue(zone, out int row);
        row = Mathf.Clamp(row, 0, rows - 1);
        int first = row * 4, count = Mathf.Min(quests.Count, first + 4) - first;
        float width = count * marker + Mathf.Max(0, count - 1) * gap, left = anchor.x - width / 2;
        var rowArea = new Rect(left - 8, anchor.y - marker - 4, width + 16, marker + 8);
        if (rows > 1 && rowArea.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
        {
            row = Mathf.Clamp(row + (Event.current.delta.y > 0 ? 1 : Event.current.delta.y < 0 ? -1 : 0), 0, rows - 1);
            Event.current.Use();
        }
        for (int i = 0; i < count; i++)
        {
            var q = quests[first + i];
            var r = new Rect(left + i * (marker + gap), anchor.y - marker, marker, marker);
            DrawMap3DQuestMarker(r, q, r.Contains(Event.current.mousePosition));
            if (Gui.Hotspot(r)) { SelectQuest(q); ZoomToQuest(q); }
        }
        if (rows > 1)
        {
            var up = new Rect(left + width + 6, anchor.y - marker, 26, 22);
            var down = new Rect(left + width + 6, anchor.y - 22, 26, 22);
            if (row > 0)
            {
                ExpeditionSkin.Text(up, "−", 24, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter);
                if (Gui.Hotspot(up)) row--;
            }
            if (row < rows - 1)
            {
                ExpeditionSkin.Text(down, "+", 24, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter);
                if (Gui.Hotspot(down)) row++;
            }
            ExpeditionSkin.Tip(new Rect(up.x, up.y, 26, marker), $"Scroll quests: row {row + 1} of {rows}");
        }
        _questRows[zone] = row;
    }

    /// <summary>DD1's quest marker at any size: its length ring, its quest-type badge for the difficulty tier, its selection glow.</summary>
    private void DrawMap3DQuestMarker(Rect r, QuestOffer q, bool hover)
    {
        if (q == _quest && Qs("quest_select_selected.png") is { } selection)
        {
            float grow = r.width * 0.22f;
            GUI.DrawTexture(new Rect(r.x - grow, r.y - grow, r.width + grow * 2, r.height + grow * 2), selection);
        }
        var ring = Qs($"quest_select_length_{(q.IsPlot ? "plot" : "generated")}_{Mathf.Clamp(q.Length, 0, 4)}.png")
            ?? Qs($"quest_select_length_generated_{Mathf.Clamp(q.Length, 0, 5)}.png");
        if (ring != null) GUI.DrawTexture(hover ? new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6) : r, ring);
        int tier = q.Difficulty >= 6 ? 6 : q.Difficulty >= 5 ? 5 : q.Difficulty >= 3 ? 3 : 1;
        var badge = Qs($"quest_select_{q.Type}_{tier}.png") ?? Qs($"quest_select_explore_{tier}.png");
        float size = r.width * 0.56f;
        var inner = new Rect(r.center.x - size / 2, r.center.y - size / 2, size, size);
        if (badge != null) GUI.DrawTexture(inner, badge);
        else ExpeditionSkin.QuestIcon(inner, q.Type, q.Difficulty >= 5 ? ExpeditionSkin.Red : q.Difficulty >= 3 ? ExpeditionSkin.Brass : ExpeditionSkin.Ivory);
        ExpeditionSkin.Tip(r, $"{q.DifficultyName} · {Cap(q.Size)} · {HamletUi.Pretty(q.Type)}" + (q.IsPlot ? "\nPlot quest" : ""));
    }

    /// <summary>Choosing a quest on the 3D map also flies to its region; already there, nothing moves.</summary>
    private void ZoomToQuest(QuestOffer quest)
    {
        if (quest == null || !Map3DWanted || !Map3DAvailable) return;
        string location = quest.Dungeon == QuestBoard.DarkestDungeon ? QuestBoard.DarkestDungeon : ZoneBase.Of(quest.Dungeon);
        if (location == null || !ExpeditionMapArt.Map3DLayout.Spots.ContainsKey(location)) return;
        if (_zoomLocation == location) { _zoomIn = true; return; }
        _zoomLocation = location; _zoomIn = true; _zoomT = 0;
    }

    /// <summary>Pixels of the map itself: right of the quest dossier, left of the roster, above the party bar.</summary>
    private static bool OverMap(Vector2 p) => p.x > 530 && p.x < RosterColumn.X - 16 && p.y > 110 && p.y < 830;
}
