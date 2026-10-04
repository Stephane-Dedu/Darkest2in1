using System;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's roster down the right edge of the town screens (campaign/town/roster): one plate per hero with the
/// portrait, name, class, resolve level and stress pips, the building icon of whoever is busy, scrolled with the
/// mouse wheel. Shared by the Hamlet and the quest select screen.
/// </summary>
internal static class RosterColumn
{
    public const float X = 1550, FirstY = 132, Spacing = 97;
    public const int Visible = 8;

    private static int _top;
    private static int _sort = -1;
    private static bool _descending;

    // DD1's roster sorts (roster_sort_*.png at roster_sort_start_position 148,80, 32 px + 6 apart).
    private static readonly (string Icon, string Tip, Func<HeroRecord, IComparable> Key)[] Sorts =
    {
        ("roster_sort_building", "Sort by activity", h => h.Activity ?? ""),
        ("roster_sort_class", "Sort by class", h => h.ClassId ?? ""),
        ("roster_sort_level", "Sort by resolve level", h => h.ResolveLevel),
        ("roster_sort_stress", "Sort by stress", h => h.Stress),
    };

    /// <summary>DD1: a sort button reorders the roster for good; the same one again reverses it.</summary>
    private static void DrawSortButtons(Estate estate)
    {
        for (int i = 0; i < Sorts.Length; i++)
        {
            var r = new Rect(X + 148 + i * 38, 80, 32, 32);
            var tex = Art.Dd1("campaign", "town", "roster", Sorts[i].Icon + ".png");
            if (tex != null) GUI.DrawTexture(r, tex); else Gui.Fill(r, new Color(0.2f, 0.17f, 0.12f));
            if (_sort == i && Art.Dd1("campaign", "town", "roster", "roster_sort_current_overlay.png") is { } current)
                GUI.DrawTexture(new Rect(r.x - 8, r.y + (_descending ? 24 : -8), 48, 16), current, ScaleMode.ScaleToFit);
            if (r.Contains(Event.current.mousePosition)) Gui.Text(new Rect(r.x - 80, r.yMax + 2, 192, 24), Sorts[i].Tip, 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
            if (!Gui.Hotspot(r)) continue;
            _descending = _sort == i ? !_descending : i >= 2;   // level and stress: highest first
            _sort = i;
            var key = Sorts[i].Key;
            var sorted = _descending ? estate.Roster.OrderByDescending(key).ToList() : estate.Roster.OrderBy(key).ToList();
            estate.Roster.Clear();
            estate.Roster.AddRange(sorted);
            Session.Current?.Persist();
        }
    }

    /// <summary>How a hero shows in the list: greyed out, a short note under the name, highlighted.</summary>
    public readonly struct Look
    {
        public readonly bool Dim, Highlight;
        public readonly string Note;
        public Look(bool dim = false, string note = null, bool highlight = false) { Dim = dim; Note = note; Highlight = highlight; }
    }

    /// <summary>The list's area (a drop target for heroes dragged back out of slots).</summary>
    public static Rect Area => new(X - 12, FirstY, 395, Spacing * Visible);

    /// <summary>Draws the column; returns the hero clicked this frame, if any. With <paramref name="draggable"/>
    /// heroes can be picked up (as <see cref="HeroDrag"/>) and dropped on slots elsewhere.</summary>
    /// <summary>The hero right-clicked during the last Draw (DD1 opens their sheet), or null.</summary>
    public static HeroRecord RightClickedHero { get; private set; }

    public static HeroRecord Draw(Estate estate, int capacity, Func<HeroRecord, Look> look = null, bool draggable = false)
    {
        var grad = Art.Dd1("campaign", "town", "roster", "roster_bggrad.png");
        if (grad != null) GUI.DrawTexture(new Rect(X, 0, 373, 1080), grad);
        else Gui.Fill(new Rect(X, 0, 370, 1080), new Color(0, 0, 0, 0.7f));

        var topFrame = Art.Dd1("campaign", "town", "roster", "roster_topframe.png");
        if (topFrame != null) GUI.DrawTexture(new Rect(X - 6, FirstY - 60, 383, 60), topFrame);
        Gui.Text(new Rect(X + 20, 40, 130, 40), $"{estate.Roster.Count}/{capacity}", 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        DrawSortButtons(estate);

        var area = new Rect(X, FirstY, 370, Spacing * Visible);
        if (Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _top += Event.current.delta.y > 0 ? 1 : -1;
            Event.current.Use();
        }
        _top = Mathf.Clamp(_top, 0, Mathf.Max(0, estate.Roster.Count - Visible));

        var bg = Art.Dd1("campaign", "town", "roster", "rosterelement.background.png");
        var full = Art.Overlay("stress_pip_full.png");
        var over = Art.Overlay("stress_pip_full_overstressed.png");
        var empty = Art.Overlay("stress_pip_empty.png");
        HeroRecord clicked = null;
        RightClickedHero = null;
        for (int i = 0; i < Visible && _top + i < estate.Roster.Count; i++)
        {
            var h = estate.Roster[_top + i];
            var l = look?.Invoke(h) ?? default;
            float x = X - 12, y = FirstY + i * Spacing;
            var r = new Rect(x, y, 383, 100);
            if (GUI.enabled && CrawlUi.RightClicked(r)) RightClickedHero = h;   // DD1: right-click for the hero's sheet
            if (draggable && h.IsAvailable && h.MissingWeeks == 0)
            {
                string cls = h.ClassId;
                Drag.Source(r, new HeroDrag(h.Id), rect =>
                {
                    var icon = Art.HeroIcon(cls);
                    if (icon != null) Art.DrawSprite(new Rect(rect.x + 21, rect.y + 9, 82, 82), icon);
                });
            }
            bool hover = r.Contains(Event.current.mousePosition);
            if (bg != null) GUI.DrawTexture(new Rect(x, y, 395, 104), bg);
            if (hover || l.Highlight) Gui.Fill(new Rect(x + 8, y + 6, 360, 90), new Color(1f, 0.9f, 0.6f, l.Highlight ? 0.12f : 0.06f));

            var old = GUI.color;
            if (l.Dim) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            var sprite = Art.HeroIcon(h.ClassId);
            if (sprite != null) Art.DrawSprite(new Rect(x + 21, y + 9, 82, 82), sprite);
            string busyIn = h.Activity?.Split('.')[0];
            var overlay = h.MissingWeeks > 0 ? Art.Dd1("campaign", "town", "roster", "missing.icon_roster.png")
                : busyIn != null ? Art.Dd1("campaign", "town", "buildings", busyIn, busyIn + ".icon_roster.png") : null;
            if (overlay != null) GUI.DrawTexture(new Rect(x + 20, y + 10, 82, 82), overlay, ScaleMode.ScaleToFit);
            GUI.color = old;

            var nameColour = l.Dim ? Gui.Dim : Gui.Dd1Name;
            Gui.Text(new Rect(x + 116, y + 6, 200, 32), h.Name, 24, nameColour, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(x + 116, y + 30, 170, 24), l.Note ?? HamletUi.Pretty(h.ClassId), 17, l.Note != null ? Gui.Blood : Gui.Dd1Class, TextAnchor.MiddleLeft);
            Gui.Text(new Rect(x + 290, y + 6, 70, 32), h.ResolveLevel.ToString(), 26, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
            for (int p = 0; p < 10; p++)
            {
                var pip = p < h.Stress ? (h.Stress >= 10 ? over ?? full : full) : empty;
                if (pip != null) GUI.DrawTexture(new Rect(x + 116 + p * 13, y + 58, 11, 18), pip, ScaleMode.ScaleToFit);
            }
            if (Gui.Hotspot(r) && !Drag.JustDropped) clicked = h;
        }

        var bottom = Art.Dd1("campaign", "town", "roster", "roster_bottomframe.png");
        float by = FirstY + Spacing * Mathf.Min(Visible, Mathf.Max(1, estate.Roster.Count)) + 4;
        if (bottom != null) GUI.DrawTexture(new Rect(X - 6, by, 383, 60), bottom);
        if (estate.Roster.Count > Visible)
            Gui.Text(new Rect(X + 20, by + 50, 330, 30), $"{_top + 1}-{Mathf.Min(estate.Roster.Count, _top + Visible)} of {estate.Roster.Count}  (scroll)", 18, Gui.Dim, TextAnchor.MiddleCenter);
        return clicked;
    }
}
