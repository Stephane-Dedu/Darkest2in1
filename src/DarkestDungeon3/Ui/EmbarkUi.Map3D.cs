using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// A continuous camera over the privately rendered DD2 valley. The same image and regional atmosphere
/// remain throughout an approach, reversal or retarget. Quest controls retain their overview positions.
/// Quests, region pairing, party, roster and provisioning are the ordinary selector's. This is the standard
/// view; unavailable private artwork retains automatic recovery through the painted map.
/// </summary>
internal sealed partial class EmbarkUi
{
    private static readonly Rect MapBackButton = new(548, 122, 280, 54);
    private static readonly string[] Map3DOrder = { "crypts", "warrens", "weald", "cove", QuestBoard.DarkestDungeon };

    private readonly ExpeditionMapCamera _mapCamera = new();
    private float _mapClock = -1;

    private static bool Map3DAvailable => ExpeditionMapArt.Map3D != null && ExpeditionMapArt.Map3DLayout != null;

    /// <summary>One persistent image: selecting a quest performs no IO, decode or image replacement.</summary>
    private bool DrawMap3DBackground()
    {
        if (!Map3DAvailable) { _mapCamera.Reset(); _mapClock = -1; return false; }
        if (Event.current.type == EventType.Repaint)
        {
            float now = Time.unscaledTime;
            _mapCamera.Step(_mapClock < 0 ? 0 : now - _mapClock);
            _mapClock = now;
        }
        var frame = _mapCamera.Frame;
        GUI.DrawTexture(new Rect(frame.X, frame.Y, frame.Width, frame.Height), ExpeditionMapArt.Map3D);
        ExpeditionMapAtmosphere.Draw(ExpeditionMapArt.Map3DLayout, frame, Time.unscaledTime);
        return true;
    }

    /// <summary>All quest rows remain visible and clickable while the scenery approaches a region.</summary>
    private void DrawMap3DRegions()
    {
        var layout = ExpeditionMapArt.Map3DLayout;
        foreach (string location in Map3DOrder)
        {
            if (!layout.Spots.TryGetValue(location, out var spot)) continue;
            var anchor = new Vector2(spot.LabelX, spot.LabelY);
            if (location == QuestBoard.DarkestDungeon)
            {
                DrawMap3DLabel(location, anchor, new[] { location }, 60);
                continue;
            }
            var areas = CampaignRegions.AtLocation(E, location);
            if (areas.Count == 0) continue;
            if (!_mapAreas.TryGetValue(location, out var zone) || !areas.Contains(zone)) _mapAreas[location] = zone = areas[0];
            DrawMap3DLabel(zone, anchor, areas, 60);
        }

        bool key = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
        bool right = Event.current.type == EventType.MouseDown && Event.current.button == 1 && OverMap(Event.current.mousePosition);
        if (!_mapCamera.IsOverview && (ExpeditionButton(MapBackButton, "Back to the map", size: 25)
            || ((key || right) && GUI.enabled && _sheetHeroId == null)))
        {
            if (key || right) Event.current.Use();
            _mapCamera.Overview();
        }

        // The scenery moves; its hit areas follow the same transform. Controls above keep their clicks.
        var mouse = Event.current.mousePosition;
        foreach (string location in Map3DOrder)
        {
            if (!layout.Spots.TryGetValue(location, out var spot)) continue;
            if (location != QuestBoard.DarkestDungeon && CampaignRegions.AtLocation(E, location).Count == 0) continue;
            var transformed = ExpeditionMapCamera.Transform(spot.Click, _mapCamera.Frame);
            var r = new Rect(transformed.X, transformed.Y, transformed.Width, transformed.Height);
            if (!OverMap(mouse)) continue;
            if (GUI.enabled && r.Contains(mouse)) ExpeditionSkin.Tip(r, "Click to look closer");
            if (!Gui.Hotspot(r)) continue;
            string zone = location == QuestBoard.DarkestDungeon ? location
                : _mapAreas.TryGetValue(location, out var shown) ? shown : null;
            if (zone != null && zone != _focusedZone) FocusArea(zone);
            _mapCamera.Focus(spot);
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
            ExpeditionMapCheck.RecordSwitch(zone, next);
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
            ExpeditionMapCheck.RecordQuest(q, r);
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
        if (quest == null || !Map3DAvailable) return;
        string location = quest.Dungeon == QuestBoard.DarkestDungeon ? QuestBoard.DarkestDungeon : ZoneBase.Of(quest.Dungeon);
        if (location == null || !ExpeditionMapArt.Map3DLayout.Spots.ContainsKey(location)) return;
        _mapCamera.Focus(ExpeditionMapArt.Map3DLayout.Spots[location]);
    }

    /// <summary>Pixels of the map itself: right of the quest dossier, left of the roster, above the party bar.</summary>
    private static bool OverMap(Vector2 p) => p.x > 530 && p.x < RosterColumn.X - 16 && p.y > 110 && p.y < 830;
}
