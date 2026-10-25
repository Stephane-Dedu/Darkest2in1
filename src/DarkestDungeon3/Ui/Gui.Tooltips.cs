using System.Collections.Generic;
using System.Linq;
using Assets.Code.UI.Managers;
using Assets.Code.Utils;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal static partial class Gui
{
    private sealed class TipLine
    {
        internal string Text;
        internal Color Colour;
        internal float Size, Height, Indent, Gap;
        internal bool Heading;
    }

    private static List<TipLine> _tip;
    private static string _tipKey;
    private static float _tipScroll, _tipShownAt = -10, _tipBodySize;
    private static int _tipDrawFrame = -1;
    private static Vector2 _tipPointer;
    private static TooltipLayout _tipLayout;
    private static Rect? _tipAnchor;
    private static float TooltipBodySize => SingletonMonoBehaviour<CommonUiBhv>.Instance?.UseLargeTooltips == true ? 30 : 24;

    /// <summary>DD2 item hierarchy: Dunkel title at120%, Alegreya text, an80% interaction hint.</summary>
    public static void EquipmentTip(string name, string rarity, Color rarityColour, string requirement, string effects, string hint = null, Rect? anchor = null)
    {
        if (Event.current.type != EventType.Repaint) return;
        float size = TooltipBodySize;
        _tipAnchor = anchor;
        _tip = new List<TipLine>
        {
            new() { Text = name, Size = size * 1.2f, Heading = true, Colour = Dd2Palette.Get("item_nameline", Gold) },
        };
        AddTipLines(rarity, size, rarityColour);
        AddTipLines(requirement, size, Dd2Palette.Get("item_typeline", Dim));
        AddEffectLines(_tip, effects, size);
        AddTipLines(hint, size * .8f, Dd2Palette.Get("item_discardline", Dim));
        if (!string.IsNullOrEmpty(hint)) _tip[_tip.Count - 1].Gap = 10;
    }

    public static void Tip(string text, Color? title = null)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
        float size = TooltipBodySize;
        _tipAnchor = null;
        _tip = text.Split('\n').Select((line, i) => new TipLine
        {
            Text = line.TrimEnd('\r'), Size = i == 0 ? size * 1.2f : size, Heading = i == 0,
            Colour = i == 0 ? title ?? Dd2Palette.Get("item_nameline", Gold) : Dd2Palette.Get("item_effectline", Parchment)
        }).ToList();
    }

    private static void AddTipLines(string text, float size, Color colour)
    {
        if (string.IsNullOrEmpty(text)) return;
        _tip.AddRange(text.Split('\n').Select(line => new TipLine { Text = line.TrimEnd('\r'), Size = size, Colour = colour }));
    }

    private static void AddEffectLines(List<TipLine> lines, string effects, float size)
    {
        foreach (var line in ItemEffectText.Lines(effects))
            lines.Add(new TipLine { Text = line.Text, Size = size, Indent = line.Indent ? 14 : 0,
                Gap = line.Condition ? 10 : 4,
                Colour = Dd2Palette.Get(line.Condition ? "item_typeline" : "item_effectline", Parchment) });
    }

    private static float MeasureLines(List<TipLine> lines, float width)
    {
        float height = 0;
        foreach (var line in lines)
        {
            line.Height = Mathf.Max(line.Size, ExpeditionSkin.PanelTextStyle(line.Size, TextAnchor.UpperLeft, line.Heading)
                .CalcHeight(new GUIContent(line.Text ?? ""), width - line.Indent)) + 3;
            height += line.Gap + line.Height;
        }
        return height;
    }

    private static void DrawLines(List<TipLine> lines, float width, float scroll)
    {
        float y = -scroll;
        foreach (var line in lines)
        {
            y += line.Gap;
            var style = ExpeditionSkin.PanelTextStyle(line.Size, TextAnchor.UpperLeft, line.Heading);
            style.normal.textColor = line.Colour;
            GUI.Label(new Rect(line.Indent, y, width - line.Indent, line.Height), line.Text ?? "", style);
            y += line.Height;
        }
    }

    /// <summary>A fixed reading area using the same text and trigger hierarchy as item hover tips.</summary>
    internal static void EffectDescription(Rect area, string requirement, string effects, ref Vector2 scroll)
    {
        var lines = new List<TipLine>();
        if (!string.IsNullOrEmpty(requirement))
            lines.Add(new TipLine { Text = requirement, Size = TooltipBodySize, Colour = Dd2Palette.Get("item_typeline", Dim), Gap = 4 });
        AddEffectLines(lines, string.IsNullOrWhiteSpace(effects) ? "Description loading…" : effects, TooltipBodySize);
        float height = MeasureLines(lines, area.width - 12);
        bool overflow = height > area.height;
        var viewport = new Rect(area.x, area.y, area.width - 12, area.height - (overflow ? 26 : 0));
        float max = Mathf.Max(0, height - viewport.height);
        scroll.y = Mathf.Clamp(scroll.y, 0, max);
        if (GUI.enabled && Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition) && overflow)
        {
            scroll.y = Mathf.Clamp(scroll.y + Event.current.delta.y * TooltipBodySize * 2, 0, max);
            Event.current.Use();
        }
        GUI.BeginGroup(viewport);
        try { if (Event.current.type == EventType.Repaint) DrawLines(lines, viewport.width, scroll.y); }
        finally { GUI.EndGroup(); }
        if (!overflow) return;
        float thumbHeight = Mathf.Max(24, viewport.height * viewport.height / height);
        Fill(new Rect(area.xMax - 4, area.y, 2, viewport.height), new Color(.22f, .21f, .18f));
        Fill(new Rect(area.xMax - 5, area.y + (viewport.height - thumbHeight) * scroll.y / max, 4, thumbHeight), CampaignPanelSkin.Brass);
        ExpeditionSkin.Text(new Rect(area.x, area.yMax - 24, area.width, 24), "Scroll to read more", 19, ExpeditionSkin.Muted, TextAnchor.MiddleRight);
    }

    // Capture before underlying scroll views consume the wheel. A tooltip follows
    // its item, so scrolling at the original hover point must work as well.
    private static void TooltipInput()
    {
        if (Event.current.type != EventType.ScrollWheel || Time.unscaledTime - _tipShownAt > .25f || _tipLayout.MaxScroll <= 0) return;
        var pointer = Event.current.mousePosition;
        if (!_tipLayout.Frame.Contains(pointer) && (pointer - _tipPointer).magnitude > 24) return;
        _tipScroll = _tipLayout.Scroll(_tipScroll, Event.current.delta.y, _tipBodySize);
        Event.current.Use();
    }

    public static void DrawTip()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (_tip == null)
        {
            // Embark has a local tip pass followed by UiRoot's shared pass.
            if (_tipDrawFrame != Time.frameCount) { _tipShownAt = -10; _tipKey = null; }
            return;
        }
        string key = string.Join("\n", _tip.Select(line => line.Text));
        if (key != _tipKey) { _tipScroll = 0; _tipKey = key; }
        float Measure(float width) => MeasureLines(_tip, width);
        float width = TooltipLayout.Width(Measure, H);
        float height = Measure(width - TooltipLayout.Padding * 2);
        _tipLayout = _tipAnchor.HasValue ? new TooltipLayout(_tipAnchor.Value, width, height, W, H)
            : new TooltipLayout(Event.current.mousePosition, width, height, W, H);
        _tipScroll = _tipLayout.Scroll(_tipScroll, 0, TooltipBodySize);
        _tipBodySize = TooltipBodySize; _tipPointer = Event.current.mousePosition; _tipShownAt = Time.unscaledTime;
        _tipDrawFrame = Time.frameCount;
        ExpeditionSkin.TooltipFrame(_tipLayout.Frame);
        GUI.BeginGroup(_tipLayout.Viewport);
        try
        {
            DrawLines(_tip, _tipLayout.Viewport.width, _tipScroll);
        }
        finally { GUI.EndGroup(); }
        if (_tipLayout.MaxScroll > 0)
        {
            var frame = _tipLayout.Frame;
            ExpeditionSkin.Text(new Rect(frame.x + 20, frame.yMax - 40, frame.width - 40, 24), "Scroll while hovering to read more", 18,
                Dd2Palette.Get("item_discardline", Dim), TextAnchor.MiddleRight);
        }
        _tip = null;
    }
}
