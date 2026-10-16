using System;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal static partial class RosterColumn
{
    private static HeroRecord DrawExpedition(Estate estate, int capacity, Func<HeroRecord, Look> look, bool draggable, bool readOnly)
    {
        var gradient = Art.Dd1("campaign", "town", "roster", "roster_bggrad.png");
        if (gradient != null) GUI.DrawTexture(new Rect(X - 12, 0, 395, 1080), gradient);
        else Gui.Fill(new Rect(X - 12, 0, 395, 1080), ExpeditionSkin.Ink);
        ExpeditionSkin.Text(new Rect(X + 8, 40, 244, 42), "Hero roster", 32, ExpeditionSkin.Ivory, heading: true);
        ExpeditionSkin.Text(new Rect(X + 262, 44, 90, 34), $"{estate.Roster.Count}/{capacity}", 24, ExpeditionSkin.Brass, TextAnchor.MiddleRight);
        string[] labels = { "Activity", "Class", "Resolve", "Stress" };
        for (int i = 0; i < Sorts.Length; i++)
        {
            var r = new Rect(X + 6 + i * 88, 88, 84, 30);
            ExpeditionSkin.Text(r, labels[i], 21, _sort == i ? ExpeditionSkin.Brass : ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
            if (_sort == i) ExpeditionSkin.Rule(new Rect(r.x + 4, r.yMax - 2, 76, 2), ExpeditionSkin.Brass);
            ExpeditionSkin.Tip(r, Sorts[i].Tip + (_sort == i ? (_descending ? " · Descending" : " · Ascending") : ""));
            if (!Gui.Hotspot(r)) continue;
            _descending = _sort == i ? !_descending : i >= 2;
            _sort = i;
            var sorted = (_descending ? estate.Roster.OrderByDescending(Sorts[i].Key) : estate.Roster.OrderBy(Sorts[i].Key)).ToList();
            estate.Roster.Clear(); estate.Roster.AddRange(sorted);
            if (!readOnly) Session.Current?.Persist();
        }

        var area = new Rect(X, FirstY, 370, Spacing * Visible);
        if (Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _top += Event.current.delta.y > 0 ? 1 : -1;
            Event.current.Use();
        }
        _top = Mathf.Clamp(_top, 0, Mathf.Max(0, estate.Roster.Count - Visible));
        HeroRecord clicked = null;
        RightClickedHero = null;
        for (int i = 0; i < Visible && _top + i < estate.Roster.Count; i++)
        {
            var h = estate.Roster[_top + i];
            var l = look?.Invoke(h) ?? default;
            float x = X - 4, y = FirstY + i * Spacing;
            var r = new Rect(x, y, 362, 92);
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            ExpeditionSkin.Sprite(r, "ui_labelbg");
            if (l.Highlight) ExpeditionSkin.Sprite(new Rect(x - 8, y + 4, 18, 82), "ui_selection_indicator", ExpeditionSkin.Brass);
            if (hover) Gui.Fill(r, new Color(.83f, .81f, .75f, .045f));
            ExpeditionSkin.Rule(new Rect(x + 10, y + 93, 340, 2), new Color(.35f, .33f, .29f, .7f));
            if (GUI.enabled && CrawlUi.RightClicked(r)) RightClickedHero = h;
            if (draggable && h.IsAvailable && h.MissingWeeks == 0)
                Drag.Source(r, new HeroDrag(h.Id), rect =>
                {
                    var icon = Art.HeroIcon(h.ClassId);
                    if (icon != null) Art.DrawSprite(new Rect(rect.x + 6, rect.y + 4, 78, 78), icon);
                });
            var portrait = new Rect(x + 6, y + 4, 78, 78);
            ExpeditionSkin.Slot(portrait, l.Highlight || hover);
            var old = GUI.color;
            if (l.Dim && !l.Highlight) GUI.color = new Color(.53f, .51f, .48f);
            var sprite = Art.HeroIcon(h.ClassId);
            if (sprite != null) Art.DrawSprite(new Rect(portrait.x + 3, portrait.y + 3, 72, 72), sprite);
            GUI.color = old;

            ExpeditionSkin.Text(new Rect(x + 98, y + 2, 214, 34), h.Name, 29, l.Dim && !l.Highlight ? ExpeditionSkin.Muted : ExpeditionSkin.Ivory, heading: true);
            ExpeditionSkin.Text(new Rect(x + 98, y + 35, 222, 27), l.Highlight ? "In the party" : l.Note ?? HamletUi.Pretty(h.ClassId), 21,
                l.Highlight ? ExpeditionSkin.Brass : l.Note != null ? ExpeditionSkin.Red : ExpeditionSkin.Muted);
            ExpeditionSkin.Text(new Rect(x + 314, y + 5, 40, 30), h.ResolveLevel.ToString(), 26, ExpeditionSkin.Brass, TextAnchor.MiddleCenter, heading: true);
            for (int p = 0; p < 10; p++)
                ExpeditionSkin.Sprite(new Rect(x + 100 + p * 15, y + 70, 8, 8), "ui_routetype_bg",
                    p < h.Stress-10 ? ExpeditionSkin.Red : p < h.Stress ? ExpeditionSkin.Ivory : new Color(.24f, .23f, .21f));
            ExpeditionSkin.Tip(r, $"{h.Name} · {HamletUi.Pretty(h.ClassId)}\nResolve {h.ResolveLevel} · Stress {Core.Expedition.ResolveStress.Dd1(h.Stress):0.#}/200\n" +
                (h.Resolve?.TraitId is { } trait ? Dd2.Dd1Stress.Name(trait)+"\n" : "")+
                (l.Note ?? "Click to select · Drag into the party") + "\nRight-click to inspect equipment");
            if (Gui.Hotspot(r) && !Drag.JustDropped) clicked = h;
        }
        ExpeditionSkin.Text(new Rect(X + 6, 928, 350, 35), estate.Roster.Count > Visible
            ? $"{_top + 1}–{Mathf.Min(estate.Roster.Count, _top + Visible)} of {estate.Roster.Count} · Scroll roster"
            : "Drag to form your party", 21, ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
        return clicked;
    }
}
