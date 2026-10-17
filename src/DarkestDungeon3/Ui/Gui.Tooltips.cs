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
        internal float Size, Height;
        internal bool Heading;
    }

    private static List<TipLine> _tip;
    private static string _tipKey;
    private static float _tipScroll, _tipShownAt = -10, _tipBodySize;
    private static int _tipDrawFrame = -1;
    private static Vector2 _tipPointer;
    private static TooltipLayout _tipLayout;
    private static float TooltipBodySize => SingletonMonoBehaviour<CommonUiBhv>.Instance?.UseLargeTooltips == true ? 30 : 24;

    /// <summary>DD2 item hierarchy: Dunkel title at120%, Alegreya text, an80% interaction hint.</summary>
    public static void EquipmentTip(string name, string rarity, Color rarityColour, string requirement, string effects, string hint = null)
    {
        if (Event.current.type != EventType.Repaint) return;
        float size = TooltipBodySize;
        var neutral = Dd2Palette.Get("item_effectline", new Color(.733f, .725f, .682f));
        _tip = new List<TipLine>
        {
            new() { Text = name, Size = size * 1.2f, Heading = true, Colour = Dd2Palette.Get("item_nameline", Gold) },
        };
        AddTipLines(rarity, size, rarityColour);
        AddTipLines(requirement, size, Dd2Palette.Get("item_typeline", Dim));
        AddTipLines(effects, size, neutral);
        AddTipLines(hint, size * .8f, Dd2Palette.Get("item_discardline", Dim));
    }

    public static void Tip(string text, Color? title = null)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
        float size = TooltipBodySize;
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
        float Measure(float width)
        {
            foreach (var line in _tip)
                line.Height = Mathf.Max(line.Size, ExpeditionSkin.PanelTextStyle(line.Size, TextAnchor.UpperLeft, line.Heading)
                    .CalcHeight(new GUIContent(line.Text ?? ""), width)) + 3;
            return _tip.Sum(line => line.Height);
        }
        float width = TooltipLayout.Width(Measure, H);
        _tipLayout = new TooltipLayout(Event.current.mousePosition, width, Measure(width - TooltipLayout.Padding * 2), W, H);
        _tipScroll = _tipLayout.Scroll(_tipScroll, 0, TooltipBodySize);
        _tipBodySize = TooltipBodySize; _tipPointer = Event.current.mousePosition; _tipShownAt = Time.unscaledTime;
        _tipDrawFrame = Time.frameCount;
        ExpeditionSkin.TooltipFrame(_tipLayout.Frame);
        GUI.BeginGroup(_tipLayout.Viewport);
        try
        {
            float y = -_tipScroll;
            foreach (var line in _tip)
            {
                var style = ExpeditionSkin.PanelTextStyle(line.Size, TextAnchor.UpperLeft, line.Heading);
                style.normal.textColor = line.Colour;
                // This rectangle was measured at this exact font size. It grows
                // and scrolls; fixed-row fitting must never shrink tooltip text.
                GUI.Label(new Rect(0, y, _tipLayout.Viewport.width, line.Height), line.Text ?? "", style);
                y += line.Height;
            }
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
