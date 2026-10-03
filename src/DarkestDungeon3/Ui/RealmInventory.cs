using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's Trinket Inventory (campaign/town/realm_inventory, realm_inventory.layout and the DD1 Unity port's window):
/// its chest background, icon and title, the "Hold [SHIFT] to sell" line, the unequip-all and three sort buttons
/// with the current-sort marker, and the trinkets in a 7-column grid (80 x 160) with DD1's grid lines and scroll
/// arrows. Opened from the estate bar; it stays open beside a hero's sheet, whose trinket slots take trinkets
/// dragged from it (and give them back when dropped on it). Shift-click sells, after DD1's question.
/// </summary>
internal static class RealmInventory
{
    public static readonly Vector2 O = new(881, 128);
    private const int Columns = 7, VisibleRows = 3;
    private static readonly Vector2 Grid = new(30, 195);       // inventory_grid_pos
    private static readonly Vector2 Cell = new(80, 160);       // grid offset
    private static int _top;                                   // first visible row
    private static string _sort;                               // "class", "rarity", "name"
    private static bool _descending;
    private static string _sellAsk;                            // the trinket DD1 asks about before selling
    private static bool _sellAlways;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;
    private static Texture2D Ri(string f) => Art.Dd1("campaign", "town", "realm_inventory", f);
    private static Rect At(float x, float y, float w, float h) => new(O.x + x, O.y + y, w, h);
    private static string Str(string id, string fallback) => S?.Lore?.Text(id) ?? fallback;

    public static Rect Panel => At(0, 0, 667, 780);

    /// <summary>Draw it. <paramref name="hero"/>: the hero whose sheet is open beside it (trinkets that don't fit
    /// them are dimmed), or null. <paramref name="close"/> closes it.</summary>
    public static void Draw(HeroRecord hero, Action close)
    {
        var panel = Panel;
        // Its clicks are its own: nothing underneath reacts (as with DD1's windows).
        if (Ri("realminv_bg.png") is { } bg) GUI.DrawTexture(panel, bg); else Gui.Fill(panel, new Color(0.03f, 0.025f, 0.02f, 0.97f));
        if (Ri("realm_inventory.icon.png") is { } icon) GUI.DrawTexture(At(23, 22, 113, 113), icon);
        Gui.Text(At(150, 24, 300, 66), Str("town_name_realm_inventory", "Trinket Inventory"), 40, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);

        bool shift = Event.current.shift;
        string hovered = null;

        // Unequip all, then the three sorts (sort_button_pos 567,22, 42 apart leftwards), then close (610,22).
        if (IconButton(At(441, 22, 32, 32), Ri("realm_inventory_unequip_trinkets.png"), "Unequip all trinkets"))
        {
            int n = S.Hamlet.UnequipAllTrinkets();
            if (n > 0) { Dd1Audio.Play("/ui/dun/trink_unqeuip"); S.Persist(); }
        }
        SortButton(At(483, 22, 32, 32), "class", Ri("realm_inventory_sort_class.png"), "Sort by class");
        SortButton(At(525, 22, 32, 32), "rarity", Ri("realm_inventory_sort_rarity.png"), "Sort by rarity");
        SortButton(At(567, 22, 32, 32), "name", Ri("realm_inventory_sort_alphabetical.png"), "Sort alphabetically");
        var closeRect = At(604, 16, 44, 44);
        if (Art.Dd1("shared", "progression", "progression_close.png") is { } x) GUI.DrawTexture(closeRect, x);
        if (Gui.Hotspot(closeRect) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && hero == null))
        {
            Dd1Audio.Play("/ui/town/trinket_close");
            close();
            return;
        }

        // A worn trinket dropped anywhere on the window comes off (back to the stash).
        if (Drag.Hovering<TrinketDrag>(panel) && Drag.Payload is TrinketDrag worn && worn.FromHero != null)
            Gui.Fill(new Rect(panel.x + 20, panel.yMax - 14, panel.width - 40, 4), Gui.Gold);
        if (Drag.Drop<TrinketDrag>(panel, out var back) && back.FromHero != null && E.Hero(back.FromHero) is { } wearer && wearer.Trinkets.Remove(back.TrinketId))
        {
            E.Trinkets.Add(back.TrinketId);
            Dd1Audio.Play("/ui/dun/trink_unqeuip");
            S.Persist();
            return;
        }

        // The grid: DD1's lines under the trinkets, three rows at a time, scrolled by the wheel or the arrows.
        var trinkets = E.Trinkets;
        int rows = Mathf.Max(1, (trinkets.Count + Columns - 1) / Columns);
        _top = Mathf.Clamp(_top, 0, Mathf.Max(0, rows - VisibleRows));
        var gridArea = At(Grid.x, Grid.y, Columns * Cell.x, VisibleRows * Cell.y);
        if (Event.current.type == EventType.ScrollWheel && gridArea.Contains(Event.current.mousePosition))
        {
            _top = Mathf.Clamp(_top + (Event.current.delta.y > 0 ? 1 : -1), 0, Mathf.Max(0, rows - VisibleRows));
            Event.current.Use();
        }
        var vGrid = Ri("realminventory_v_grid.png");
        var hGrid = Ri("realminventory_h_grid.png");
        for (int row = 0; row < VisibleRows; row++)
        {
            float y = O.y + Grid.y + row * Cell.y;
            if (vGrid != null) GUI.DrawTexture(new Rect(O.x + Grid.x + 74, y - 10, vGrid.width, vGrid.height), vGrid);
            if (hGrid != null && row < VisibleRows - 1) GUI.DrawTexture(new Rect(O.x + Grid.x + 280 - hGrid.width / 2f, y + 150, hGrid.width, hGrid.height), hGrid);
        }
        for (int i = 0; i < VisibleRows * Columns; i++)
        {
            int index = _top * Columns + i;
            if (index >= trinkets.Count) break;
            string id = trinkets[index];
            var r = At(Grid.x + (i % Columns) * Cell.x, Grid.y + (i / Columns) * Cell.y, 72, 144);
            bool fits = hero == null || S.Catalog.TrinketFits(id, hero.ClassId);
            if (!shift) Drag.Source(r, new TrinketDrag(id), rect => HeroSheet.TrinketIcon(rect, id));
            var old = GUI.color;
            if (!fits) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            if (!(Drag.Payload is TrinketDrag d && d.FromHero == null && d.TrinketId == id && Drag.Active)) HeroSheet.TrinketIcon(r, id);
            GUI.color = old;
            if (!r.Contains(Event.current.mousePosition) || Drag.Active) continue;
            hovered = id;
            if (shift && Gui.Hotspot(r))
            {
                if (_sellAlways) Sell(id);
                else _sellAsk = id;
            }
        }
        if (rows > VisibleRows)
        {
            var up = At(594, Grid.y - 6, 50, 40);
            var down = At(594, Grid.y + VisibleRows * Cell.y - 46, 50, 40);
            if (_top > 0 && Ri("realm_inventory_uparrow.png") is { } ua) GUI.DrawTexture(up, ua, ScaleMode.ScaleToFit);
            if (_top < rows - VisibleRows && Ri("realm_inventory_downarrow.png") is { } da) GUI.DrawTexture(down, da, ScaleMode.ScaleToFit);
            if (_top > 0 && Gui.Hotspot(up)) _top--;
            if (_top < rows - VisibleRows && Gui.Hotspot(down)) _top++;
        }
        if (trinkets.Count == 0)
            Gui.Text(At(40, Grid.y + 120, 580, 40), "No trinkets. The Nomad Wagon sells them; expeditions find them.", 19, Gui.Dd1Class, TextAnchor.MiddleCenter);

        // DD1's line under the title; holding shift over a trinket shows what it sells for.
        if (shift && hovered != null)
        {
            Gui.Text(At(150, 92, 220, 30), Str("realm_inventory_trinket_sell_description", "Sell trinket for:"), 19, Gui.Dd1Text, TextAnchor.MiddleLeft);
            if (Art.Dd1("shared", "estate", "currency.gold.icon.png") is { } gold) GUI.DrawTexture(At(372, 96, 24, 24), gold);
            Gui.Text(At(400, 92, 120, 30), Gui.Num(S.Hamlet.TrinketSellValue(hovered), "#,0"), 19, Gui.Gold, TextAnchor.MiddleLeft);
        }
        else Gui.Text(At(150, 92, 300, 30), Str("realm_inventory_trinket_sell_instruction", "Hold [SHIFT] to Sell Trinkets"), 17, Gui.Dd1Class, TextAnchor.MiddleLeft);

        if (hovered != null && _sellAsk == null) Gui.Tip(HeroSheet.TrinketText(hovered), HeroSheet.RarityColour(hovered));
        if (_sellAsk != null) AskToSell();
    }

    private static void Sell(string id)
    {
        int value = S.Hamlet.TrinketSellValue(id);
        if (!S.Hamlet.SellTrinket(id)) return;
        Dd1Audio.Play("/ui/town/sell");
        Gui.Announce($"{HeroSheet.TrinketName(id)} sold for {Gui.Num(value, "#,0")} gold.");
        S.Persist();
    }

    /// <summary>DD1's question: "Really sell X for N gold?" Yes / No / Always.</summary>
    private static void AskToSell()
    {
        string id = _sellAsk;
        var box = At(84, 300, 500, 200);
        Gui.Fill(box, new Color(0.02f, 0.016f, 0.012f, 0.97f));
        Gui.Fill(new Rect(box.x, box.y, box.width, 2), new Color(0.42f, 0.36f, 0.25f));
        Gui.Fill(new Rect(box.x, box.yMax - 2, box.width, 2), new Color(0.42f, 0.36f, 0.25f));
        string format = Str("realm_inventory_sell_trinket_confirm_question_format", "Really sell {?item_name}%s for {?amount}%d gold?");
        string question = format.Replace("{?item_name}%s", HeroSheet.TrinketName(id)).Replace("{?amount}%d", Gui.Num(S.Hamlet.TrinketSellValue(id), "#,0"))
                                .Replace("%s", HeroSheet.TrinketName(id)).Replace("%d", Gui.Num(S.Hamlet.TrinketSellValue(id), "#,0"));
        Gui.Text(new Rect(box.x + 24, box.y + 20, box.width - 48, 90), question, 22, Gui.Dd1Text, TextAnchor.MiddleCenter);
        float bx = box.x + 30;
        if (Gui.DdButton(new Rect(bx, box.y + 128, 136, 48), Str("realm_inventory_sell_trinket_confirm_yes", "Yes"), true, 22)) { _sellAsk = null; Sell(id); return; }
        if (Gui.DdButton(new Rect(bx + 152, box.y + 128, 136, 48), Str("realm_inventory_sell_trinket_confirm_no", "No"), true, 22)) { _sellAsk = null; return; }
        if (Gui.DdButton(new Rect(bx + 304, box.y + 128, 136, 48), Str("realm_inventory_sell_trinket_confirm_always", "Always"), true, 22)) { _sellAsk = null; _sellAlways = true; Sell(id); return; }
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { _sellAsk = null; Event.current.Use(); }
    }

    private static bool IconButton(Rect r, Texture2D icon, string tip)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        var old = GUI.color;
        if (!hover) GUI.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        if (icon != null) GUI.DrawTexture(r, icon); else Gui.Fill(r, new Color(0.2f, 0.17f, 0.12f));
        GUI.color = old;
        if (hover) Gui.Tip(tip);
        return Gui.Hotspot(r);
    }

    /// <summary>A sort button: sorts the stash; again, the other way. DD1's marker shows the current sort and its
    /// direction (sort_button_current_ascending/descending_overlay_offset).</summary>
    private static void SortButton(Rect r, string key, Texture2D icon, string tip)
    {
        if (_sort == key && Ri("realm_inventory_sort_current_overlay.png") is { } mark)
            GUI.DrawTexture(new Rect(r.x - 8, r.y + (_descending ? 24 : -8), mark.width, mark.height), mark);
        if (!IconButton(r, icon, tip)) return;
        _descending = _sort == key && !_descending;
        _sort = key;
        Sort();
        Dd1Audio.Play("/ui/town/sortby");
        S.Persist();
    }

    private static readonly string[] RarityOrder = { "common", "rare", "epic", "ancestral", "cultist" };

    private static void Sort()
    {
        Func<string, IComparable> key = _sort switch
        {
            "class" => id => (Dd2.Dd2Catalog.Tables.Trinkets.TryGetValue(id, out var t) && t.HeroClass != null ? "0" + t.HeroClass : "1") + HeroSheet.TrinketName(id),
            "rarity" => id => (Dd2.Dd2Catalog.Tables.Trinkets.TryGetValue(id, out var t) ? Array.IndexOf(RarityOrder, t.Rarity) : -1) * 1000 + (HeroSheet.TrinketName(id).Length > 0 ? HeroSheet.TrinketName(id)[0] : 0),
            _ => id => HeroSheet.TrinketName(id),
        };
        var sorted = _descending ? E.Trinkets.OrderByDescending(key).ToList() : E.Trinkets.OrderBy(key).ToList();
        E.Trinkets.Clear();
        E.Trinkets.AddRange(sorted);
    }
}
