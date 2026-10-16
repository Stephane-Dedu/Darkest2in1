using System;
using System.Collections.Generic;
using Assets.Code.Map.Generation.Node;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Ui;

/// <summary>Expedition-only DD2 art and type. Assets stay in the owner's installed game.</summary>
internal static class ExpeditionSkin
{
    public static readonly Color Ivory = new(.83f, .81f, .75f), Muted = new(.57f, .57f, .54f),
        Brass = new(.69f, .57f, .36f), Red = new(.70f, .26f, .19f), Ink = new(.025f, .023f, .022f, .96f);

    private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> Sprites = new();
    private static readonly Dictionary<string, AsyncOperationHandle<ResourceNodeType>> Nodes = new();
    private static readonly Dictionary<(int, TextAnchor, bool), GUIStyle> Styles = new();
    private static readonly Dictionary<(int, TextAnchor, bool), GUIStyle> PanelStyles = new();
    private static readonly Dictionary<Type, Dictionary<string, IResourceLocation>> Locations = new();
    private static readonly Vector2[] OutlineOffsets = { new(-1.4f, 0), new(1.4f, 0), new(0, -1.4f), new(0, 1.4f),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
    private static AsyncOperationHandle<Font> _heading, _body;
    private static bool _started;

    public static void Prepare()
    {
        if (_started) return;
        _started = true;
        _heading = Load<Font>("Assets/Data/Font/AddressableResources/Font/NDDunkelD-Bold.otf");
        _body = Load<Font>("Assets/Data/Font/AlegreyaSans-Regular.ttf");
        foreach (string name in new[] { "ui_button_banner", "ui_labelbg", "ui_divider_long", "ui_arrow",
                     "ui_frame_ability", "ui_frame_ability_selected", "ui_selection_indicator", "ui_routetype_bg", "ui_trinket_slot" })
            Sprites[name] = Load<Sprite>("Assets/Art/UI/HUD/" + name + ".png");
        foreach (string name in new[] { "watch_tower", "story_resist", "dungeon", "cache", "story_hero" })
            Nodes[name] = Load<ResourceNodeType>("Assets/Data/Node/" + name + ".asset");
    }

    private static AsyncOperationHandle<T> Load<T>(string path) where T : UnityEngine.Object
    {
        // Some DD2 groups expose GUID addresses only. Internal IDs still identify the installed asset.
        IResourceLocation found = null;
        foreach (var locator in Addressables.ResourceLocators)
        {
            if (locator.Locate(path, typeof(T), out var direct) && direct.Count > 0) { found = direct[0]; break; }
        }
        if (found == null)
        {
            if (!Locations.TryGetValue(typeof(T), out var index))
            {
                Locations[typeof(T)] = index = new Dictionary<string, IResourceLocation>(StringComparer.OrdinalIgnoreCase);
                foreach (var locator in Addressables.ResourceLocators)
                    foreach (var key in locator.Keys)
                        if (locator.Locate(key, typeof(T), out var locations))
                            foreach (var location in locations) index[location.InternalId] = location;
            }
            index.TryGetValue(path, out found);
        }
        var handle = found != null ? Addressables.LoadAssetAsync<T>(found) : Addressables.LoadAssetAsync<T>(path);
        handle.Completed += h =>
        {
            if (h.Status != AsyncOperationStatus.Succeeded)
                Plugin.Log.LogWarning("[expedition HUD] native asset unavailable: " + path);
            else if (typeof(T) == typeof(Font) || typeof(T) == typeof(ResourceNodeType))
                Plugin.Log.LogInfo("[expedition HUD] native " + h.Result.name + " ready");
        };
        return handle;
    }

    private static Font Font(bool heading)
    {
        Prepare();
        var h = heading ? _heading : _body;
        return h.IsDone && h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;
    }

    private static GUIStyle Style(float size, TextAnchor align, bool heading)
    {
        var key = (Mathf.RoundToInt(size), align, heading);
        if (!Styles.TryGetValue(key, out var style))
            Styles[key] = style = new GUIStyle(GUI.skin.label)
            {
                fontSize = key.Item1, alignment = align, wordWrap = true, richText = false,
                padding = new RectOffset(), margin = new RectOffset()
            };
        style.font = Font(heading);
        return style;
    }

    /// <summary>Campaign panels share the installed typefaces, with their existing inline colour markup.</summary>
    internal static GUIStyle PanelTextStyle(float size, TextAnchor align, bool heading)
    {
        var key = (Mathf.RoundToInt(size), align, heading);
        if (!PanelStyles.TryGetValue(key, out var style))
            PanelStyles[key] = style = new GUIStyle(Style(size, align, heading)) { richText = true };
        style.font = Font(heading);
        return style;
    }

    public static void Text(Rect r, string text, float size, Color colour,
        TextAnchor align = TextAnchor.UpperLeft, bool heading = false)
    {
        var style = Style(size, align, heading);
        style.normal.textColor = colour;
        GUI.Label(r, text ?? "", style);
    }

    public static float Width(string text, float size, bool heading = false)
        => Style(size, TextAnchor.UpperLeft, heading).CalcSize(new GUIContent(text)).x;

    public static void OutlinedHeading(Rect r, string text, float size, Color colour, TextAnchor align = TextAnchor.MiddleLeft)
    {
        if (Event.current.type != EventType.Repaint) return;
        var style = Style(size, align, true);
        style.normal.textColor = new Color(0, 0, 0, .72f * colour.a);
        GUI.Label(new Rect(r.x + 2, r.y + 3, r.width, r.height), text, style);
        style.normal.textColor = new Color(.012f, .010f, .008f, .95f * colour.a);
        foreach (var offset in OutlineOffsets)
            GUI.Label(new Rect(r.x + offset.x, r.y + offset.y, r.width, r.height), text, style);
        style.normal.textColor = colour;
        GUI.Label(r, text, style);
    }

    public static void TextFit(Rect r, string text, int size, Color colour, TextAnchor align = TextAnchor.UpperLeft)
    {
        while (size > 18 && Style(size, align, false).CalcHeight(new GUIContent(text), r.width) > r.height) size--;
        Text(r, text, size, colour, align);
    }

    public static bool Sprite(Rect r, string name, Color? tint = null, bool fit = false, bool flip = false)
    {
        Prepare();
        if (!Sprites.TryGetValue(name, out var h) || !h.IsDone || h.Status != AsyncOperationStatus.Succeeded || h.Result == null) return false;
        var old = GUI.color;
        GUI.color = tint ?? Color.white;
        Art.DrawSprite(r, h.Result, fit, flip);
        GUI.color = old;
        return true;
    }

    public static void Rule(Rect r, Color? colour = null) => Sprite(r, "ui_divider_long", colour ?? Muted);

    public static void Banner(Rect r, Color? colour = null)
    {
        if (!Sprite(r, "ui_button_banner", colour ?? Ivory)) Gui.Fill(r, Ink);
    }

    public static bool Button(Rect r, string text, bool enabled = true, int size = 30)
    {
        bool hover = enabled && GUI.enabled && r.Contains(Event.current.mousePosition);
        Banner(r, !enabled ? Muted : hover ? Red : Brass);
        Text(new Rect(r.x + 16, r.y + 4, r.width - 32, r.height - 8), text, size,
            enabled ? Ivory : Muted, TextAnchor.MiddleCenter, heading: true);
        return enabled && Gui.Hotspot(r);
    }

    public static void Slot(Rect r, bool selected = false)
    {
        Sprite(r, "ui_trinket_slot");
        Sprite(r, selected ? "ui_frame_ability_selected" : "ui_frame_ability", selected ? Brass : Muted);
    }

    public static void QuestIcon(Rect r, string type, Color colour)
    {
        Prepare();
        string key = type switch { "kill_boss" => "dungeon", "cleanse" => "story_resist", "gather" => "cache",
            "activate" or "inventory_activate" => "story_hero", _ => "watch_tower" };
        var h = Nodes[key];
        if (!h.IsDone || h.Status != AsyncOperationStatus.Succeeded || h.Result?.m_Icon == null) return;
        var old = GUI.color; GUI.color = colour;
        Art.DrawSprite(r, h.Result.m_Icon);
        GUI.color = old;
    }

    public static void Tip(Rect r, string text)
    {
        if (Event.current.type != EventType.Repaint || !r.Contains(Event.current.mousePosition) || string.IsNullOrEmpty(text)) return;
        _tip = text;
    }

    private static string _tip;
    public static void DrawTip()
    {
        if (Event.current.type != EventType.Repaint || _tip == null) return;
        const float width = 360;
        var style = Style(22, TextAnchor.UpperLeft, false);
        float height = style.CalcHeight(new GUIContent(_tip), width - 36) + 32;
        var mouse = Event.current.mousePosition;
        var r = new Rect(Mathf.Clamp(mouse.x + 22, 8, Gui.W - width - 8), Mathf.Clamp(mouse.y + 20, 8, Gui.H - height - 8), width, height);
        Gui.Fill(r, Ink);
        Rule(new Rect(r.x, r.y, width, 3), Brass);
        Text(new Rect(r.x + 18, r.y + 16, width - 36, height - 32), _tip, 22, Ivory);
        _tip = null;
    }
}
