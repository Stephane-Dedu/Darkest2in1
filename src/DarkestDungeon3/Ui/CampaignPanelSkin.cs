using System;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>DD2's installed type and painted chrome around the DD1 campaign's original content.</summary>
internal static class CampaignPanelSkin
{
    internal static readonly Color Coal = new(.028f, .032f, .031f, .985f);
    internal static readonly Color Surface = new(.052f, .057f, .053f, .97f);
    internal static readonly Color Ivory = new(.91f, .88f, .80f);
    internal static readonly Color Brass = new(.82f, .69f, .43f);
    internal static readonly Color Muted = new(.70f, .70f, .65f);
    internal static readonly Color Red = new(.87f, .37f, .29f);
    private static int _depth;
    internal static bool Active => _depth > 0;

    internal static void Prepare() => ExpeditionSkin.Prepare();

    internal static IDisposable Scope(bool enabled = true) => new SkinScope(enabled);
    private sealed class SkinScope : IDisposable
    {
        private bool _enabled;
        internal SkinScope(bool enabled) { _enabled = enabled; if (enabled) { Prepare(); _depth++; } }
        public void Dispose() { if (_enabled) { _enabled = false; _depth--; } }
    }

    internal static Color Colour(Color colour)
    {
        Color result = colour == Gui.Dd1Name || colour == Gui.Gold ? Brass
            : colour == Gui.Dd1Text || colour == Gui.Parchment ? Ivory
            : colour == Gui.Dd1Class || colour == Gui.Dim ? Muted
            : colour == Gui.Blood || colour == Gui.Dd1Health ? Red : colour;
        result.a = colour.a;
        return result;
    }

    // A size means the same thing inside and outside a campaign window. DD1's
    // bitmap line-height multipliers do not apply to DD2's vector font em size.
    private static int Size(float size, bool heading) => Mathf.RoundToInt(Mathf.Max(16, size));

    internal static float TextHeight(string text, float size, float width, bool heading) =>
        ExpeditionSkin.PanelTextStyle(Size(size, heading), TextAnchor.UpperLeft, heading).CalcHeight(new GUIContent(text ?? ""), width);

    internal static void Text(Rect rect, string text, float size, Color colour, TextAnchor align, bool heading)
    {
        if (string.IsNullOrEmpty(text) || rect.width <= 0 || rect.height <= 0) return;
        int pixels = Size(size, heading), minimum = Mathf.Min(pixels, rect.height < 25 ? 16 : 18);
        var content = new GUIContent(text);
        var style = ExpeditionSkin.PanelTextStyle(pixels, align, heading);
        bool singleLine = text.IndexOf('\n') < 0 && rect.height < style.lineHeight * 1.8f;
        style = ExpeditionSkin.PanelTextStyle(pixels, align, heading, wrap: !singleLine);
        bool Fits(GUIContent value) => singleLine ? style.CalcSize(value).x <= rect.width : style.CalcHeight(value, rect.width) <= rect.height;
        while (pixels > minimum && !Fits(content))
            style = ExpeditionSkin.PanelTextStyle(--pixels, align, heading, wrap: !singleLine);
        // Fixed rows stay fixed. Long names get an ellipsis and retain their full text on hover.
        // Larger prose rectangles continue to wrap, measured by the very same native font/style.
        if (!Fits(content))
        {
            string shortened = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            while (shortened.Length > 1 && !Fits(new GUIContent(shortened + "…")))
                shortened = shortened.Substring(0, shortened.Length - 1);
            content.text = shortened.TrimEnd() + "…";
            if (rect.Contains(Event.current.mousePosition)) Gui.Tip(text);
        }
        style.normal.textColor = Active ? Colour(colour) : colour;
        GUI.Label(rect, content, style);
    }

    internal static void Frame(Rect rect, Texture art = null, float artOpacity = .20f)
    {
        Gui.Fill(new Rect(rect.x - 10, rect.y + 10, rect.width + 20, rect.height + 12), new Color(0, 0, 0, .44f));
        Gui.Fill(rect, Coal);
        if (art != null)
        {
            var old = GUI.color;
            GUI.color = new Color(.76f, .77f, .70f, artOpacity);
            GUI.DrawTexture(rect, art);
            GUI.color = old;
        }
        ExpeditionSkin.Rule(new Rect(rect.x + 8, rect.y - 3, rect.width - 16, 8), Brass);
        ExpeditionSkin.Rule(new Rect(rect.x + 8, rect.yMax - 5, rect.width - 16, 8), Muted);
    }

    internal static void Section(Rect rect)
    {
        Gui.Fill(rect, Surface);
        ExpeditionSkin.Rule(new Rect(rect.x + 12, rect.y, rect.width - 24, 5), new Color(.46f, .42f, .31f));
    }

    internal static void ServiceWindow(Rect window, Texture background)
    {
        Frame(window, background, .64f);
        // Preserve the keeper's painted surroundings; the interactive work area gets a quiet, legible surface.
        Gui.Fill(new Rect(window.x + 586, window.y + 10, window.width - 600, window.height - 20), Coal);
        ExpeditionSkin.Banner(new Rect(window.x + 107, window.y + 22, 324, 104), new Color(.58f, .48f, .31f));
    }

    internal static bool Button(Rect rect, string label, bool enabled, float size)
    {
        bool hover = enabled && GUI.enabled && rect.Contains(Event.current.mousePosition);
        ExpeditionSkin.Banner(rect, !enabled ? new Color(.32f, .33f, .30f) : hover ? ExpeditionSkin.Red : ExpeditionSkin.Brass);
        Text(new Rect(rect.x + 8, rect.y + 2, rect.width - 16, rect.height - 4), label, size,
            enabled ? Ivory : Muted, TextAnchor.MiddleCenter, heading: true);
        bool clicked = enabled && GUI.enabled && Gui.Hotspot(rect);
        if (clicked) Dd1Audio.Play("/ui/shared/button_click");
        return clicked;
    }
}
