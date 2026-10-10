using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>DD1 minimap drawing and pointer handling, shared by the raid screen and headless input tests.</summary>
internal sealed class CrawlMapUi
{
    private const float MapUnit = 40;
    private static Vector2 Pos(int x, int y) => new(x * MapUnit, y * MapUnit);

    private Vector2 _mapPan, _mapPress;
    private bool _mapDragging, _mapPressed;
    private string _mapSpot;
    private DungeonMap _map;
    private float _zoom = 1f;

    public void Draw(ExpeditionState exp, Action<int, int> walkToTile, Action<int> selectRoom)
    {
        var map = exp.Map;
        var area = new Rect(976, 739, 649, 321);   // DD1 map clip region inside panel_map
        if (_map != map)
        {
            _map = map;
            _mapSpot = null;
            _zoom = 1f;
            _mapPressed = _mapDragging = false;
        }
        Vector2 here;
        if (exp.InRoom) here = Pos(map.Room(exp.RoomId).X, map.Room(exp.RoomId).Y);
        else
        {
            var t = map.Corridor(exp.CorridorId).Tiles[exp.TileIndex];
            here = HallPos(map, map.Corridor(exp.CorridorId), t.Index);
        }
        // DD1: drag the map to look around; it comes back to the party when the party moves.
        string spot = exp.InRoom ? "r" + exp.RoomId : $"c{exp.CorridorId}:{exp.TileIndex}";
        if (spot != _mapSpot) { _mapSpot = spot; _mapPan = Vector2.zero; }
        var e = Event.current;
        var center = new Vector2(area.width / 2f, area.height / 2f);
        if (GUI.enabled && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
        {
            float zoom = Mathf.Clamp(_zoom * (float)Math.Pow(1.12, -e.delta.y), 0.5f, 2f);
            // Keep the map position under the pointer fixed while scaling the map and its icons.
            var anchor = e.mousePosition - area.position - center;
            _mapPan = anchor + (_mapPan - anchor) * (zoom / _zoom);
            _zoom = zoom;
            // A wheel adjustment during a press must not become a room click on release.
            if (_mapPressed) { _mapDragging = true; GUIUtility.hotControl = 0; }
            e.Use();
        }
        if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { _mapPress = e.mousePosition; _mapDragging = false; _mapPressed = true; }
        else if (e.type == EventType.MouseDrag && _mapPressed)
        {
            if (!_mapDragging && (e.mousePosition - _mapPress).magnitude > 5) _mapDragging = true;
            if (_mapDragging) { _mapPan += e.delta; e.Use(); }
        }
        else if (e.rawType == EventType.MouseUp && _mapPressed)
        {
            _mapPressed = false;
            if (_mapDragging) { _mapDragging = false; GUIUtility.hotControl = 0; e.Use(); }   // a drag is not a click
        }
        var offset = center - here * _zoom + _mapPan;
        bool hoveringMap = GUI.enabled && !_mapDragging && area.Contains(e.mousePosition);

        GUI.BeginGroup(area);
        var visitedNear = new HashSet<int>(map.Rooms.Where(r => r.Visited).SelectMany(r => map.Neighbours(r.Id)));

        foreach (var c in map.Corridors)
        {
            bool known = map.Room(c.RoomA).Visited || map.Room(c.RoomB).Visited || c.Tiles.Any(t => t.Scouted);
            if (!known) continue;
            foreach (var t in c.Tiles)
            {
                var p = HallPos(map, c, t.Index) * _zoom + offset;
                var r = new Rect(p.x - 12 * _zoom, p.y - 12 * _zoom, 24 * _zoom, 24 * _zoom);
                string baseIcon = t.Visited ? "hall_clear" : t.Scouted ? "hall_dim" : "hall_dark";
                GUI.DrawTexture(r, Art.MapIcon(baseIcon) ?? Texture2D.whiteTexture);
                string marker = (t.Visited || t.Scouted) && !t.Resolved ? t.Content switch
                {
                    HallContent.Battle => "marker_battle",
                    HallContent.Curio => "marker_curio",
                    HallContent.Trap => "marker_trap",
                    HallContent.Obstacle => "marker_obstacle",
                    _ => null,
                } : null;
                if (marker != null && Art.MapIcon(marker) is { } m) GUI.DrawTexture(r, m);
                bool secretKnown = t.SecretRoomId >= 0 && (map.Room(t.SecretRoomId).Scouted || map.Room(t.SecretRoomId).Visited);
                if (secretKnown && Art.MapIcon("marker_secret") is { } star) GUI.DrawTexture(r, star);
                if (hoveringMap && r.Contains(e.mousePosition))
                {
                    string tip = marker switch
                    {
                        "marker_battle" => Label("ac_battle", "Battle"),
                        "marker_curio" => t.IsQuestGoal ? Label("quest_location", "Quest Location") : Label("ac_curio", "Curio"),
                        "marker_trap" => Label("ac_trap", "Trap"),
                        "marker_obstacle" => Label("ac_obstacle", "Obstacle"),
                        _ => null,
                    };
                    if (secretKnown) tip = Append(tip, Label("ac_hidden_door", "Secret Door"));
                    Gui.Tip(tip);
                }
                if (Gui.Hotspot(r)) walkToTile(c.Id, t.Index);
            }
        }

        foreach (var room in map.Rooms)
        {
            bool known = room.Visited || room.Scouted || visitedNear.Contains(room.Id);
            if (!known) continue;
            var p = Pos(room.X, room.Y) * _zoom + offset;
            var r = new Rect(p.x - 32 * _zoom, p.y - 32 * _zoom, 64 * _zoom, 64 * _zoom);
            bool seen = room.Visited || room.Scouted;
            string icon = !seen ? "room_unknown"
                : room.Id == map.EntranceRoomId ? "room_entrance"
                : room.Content == RoomContent.Boss && !room.Cleared ? "room_boss"
                : room.HasBattle && !room.Cleared ? "room_battle"
                : room.CurioId != null && !room.CurioTaken && room.Content is RoomContent.Treasure or RoomContent.GuardedTreasure ? "room_treasure"
                : room.CurioId != null && !room.CurioTaken ? "room_curio"
                : "room_empty";
            GUI.DrawTexture(r, Art.MapIcon(icon) ?? Texture2D.whiteTexture);
            if (room.IsSecret && Art.MapIcon("marker_secret") is { } secretMark)
                GUI.DrawTexture(new Rect(p.x - 14 * _zoom, p.y - 14 * _zoom, 28 * _zoom, 28 * _zoom), secretMark);
            if (room.Visited && room.Id != map.EntranceRoomId && Art.MapIcon("marker_room_visited") is { } v) GUI.DrawTexture(r, v);
            if (hoveringMap && r.Contains(e.mousePosition))
            {
                string tip = icon switch
                {
                    "room_boss" => Label("boss", "Boss"),
                    "room_battle" when room.CurioId != null && !room.CurioTaken && room.Content == RoomContent.GuardedTreasure
                        => Label("ac_guarded_treasure", "Room Battle with Treasure"),
                    "room_battle" when room.CurioId != null && !room.CurioTaken && room.Content == RoomContent.GuardedCurio
                        => Label("ac_guarded_curio", "Room Battle with Curio"),
                    "room_battle" => Label("ac_battle", "Battle"),
                    "room_treasure" => Label("treasure", "Treasure"),
                    "room_curio" => room.IsQuestGoal ? Label("quest_location", "Quest Location") : Label("ac_curio", "Curio"),
                    _ => null,
                };
                if (seen && room.IsQuestGoal && room.CurioId != null && !room.CurioTaken && icon != "room_curio")
                    tip = Append(tip, Label("quest_location", "Quest Location"));
                Gui.Tip(tip);
            }
            if (Gui.Hotspot(r)) selectRoom(room.Id);
        }

        var ind = Art.MapIcon("indicator");
        var hp = here * _zoom + offset;
        if (ind != null) GUI.DrawTexture(new Rect(hp.x - 25 * _zoom, hp.y - (exp.InRoom ? 70 : 52) * _zoom, 51 * _zoom, 48 * _zoom), ind);
        GUI.EndGroup();
    }

    private static string Label(string name, string fallback) => Dd1Text.Get("miscellaneous", "str_map_" + name + "_tooltip") ?? fallback;
    private static string Append(string text, string extra) => string.IsNullOrEmpty(text) ? extra : text + "\n" + extra;

    /// <summary>Plot maps keep DD1's square positions and turns; generated halls fit between the room icons.</summary>
    private static Vector2 HallPos(DungeonMap map, Corridor c, int index)
    {
        if (map.Size == "plot")
        {
            var tile = c.Tiles[index];
            return Pos(tile.X, tile.Y);
        }
        var a = Pos(map.Room(c.RoomA).X, map.Room(c.RoomA).Y);
        var b = Pos(map.Room(c.RoomB).X, map.Room(c.RoomB).Y);
        var dir = (b - a).normalized;
        var start = a + dir * 32f;
        var end = b - dir * 32f;
        float step = (end - start).magnitude / c.Tiles.Count;
        return start + dir * (step * (index + 0.5f));
    }

}
