using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Shared loot-card drawing; returns the waiting-list index selected by the player.</summary>
internal sealed class LootGridUi
{
    private object _owner;
    private int _page;
    private readonly struct Card
    {
        public readonly LootDrop Drop;
        public readonly int Waiting;
        public Card(LootDrop drop, int waiting) { Drop = drop; Waiting = waiting; }
    }

    public int DrawBattle(BattleSpoils spoils, Rect area, ItemCatalog items) => Draw(spoils,
        spoils.Taken.Select(d => new Card(d, -1)).Concat(spoils.LeftBehind.Select((d, i) => new Card(d, i))).ToList(), area, 2, items);

    public int DrawCurio(CurioReport report, Rect area, ItemCatalog items) => Draw(report,
        report.Loot.Select(d => new Card(d, report.LeftBehind.IndexOf(d))).ToList(), area, 1, items);

    private int Draw(object owner, List<Card> cards, Rect area, int rows, ItemCatalog items)
    {
        if (!ReferenceEquals(owner, _owner)) { _owner = owner; _page = 0; }
        int capacity = rows * 5, pages = Math.Max(1, (cards.Count + capacity - 1) / capacity);
        _page = Math.Max(0, Math.Min(_page, pages - 1));
        if (pages > 1)
        {
            float y = area.y + area.height / 2 - 16;
            if (Arrow(new Rect(area.x + 2, y, 24, 32), previous: true, _page > 0, pages)) _page--;
            if (Arrow(new Rect(area.xMax - 26, y, 24, 32), previous: false, _page + 1 < pages, pages)) _page++;
        }
        int start = _page * capacity, count = Math.Min(capacity, cards.Count - start);
        int clicked = -1;
        for (int i = 0; i < count; i++)
        {
            var card = cards[start + i];
            int row = i / 5, col = i % 5, inRow = Math.Min(5, count - row * 5);
            var r = new Rect(area.x + area.width / 2 - inRow * 40 + col * 80 + 4, area.y + row * 150, 72, 144);
            ItemArt.Stack(r, card.Drop.Key, card.Drop.Amount, Math.Max(1, items.StackLimit(card.Drop.Key)), dim: card.Waiting >= 0);
            if (card.Waiting >= 0 && Gui.Hotspot(r)) clicked = card.Waiting;
        }
        return clicked;
    }

    private bool Arrow(Rect rect, bool previous, bool enabled, int pages)
    {
        var texture = Art.Dd1("shared", "menu", previous ? "menu.left_arrow.png" : "menu.right_arrow.png");
        var old = GUI.color;
        if (!enabled) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1);
        if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        else Gui.Text(rect, previous ? "<" : ">", 22, enabled ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleCenter);
        GUI.color = old;
        if (GUI.enabled && rect.Contains(Event.current.mousePosition))
            Gui.Tip((previous ? "Previous loot page" : "Next loot page") + $"\nPage {_page + 1} of {pages}");
        if (!GUI.enabled || !enabled || !Gui.Hotspot(rect)) return false;
        Event.current.Use(); // the page change must not also pick up a card on the same release
        return true;
    }
}
