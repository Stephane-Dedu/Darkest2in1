using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// The Hamlet, DD1 style: the town painted from DD1's own art (sky, the hill, every building at its DD1 spot,
/// at the level its upgrades have reached), the estate nameplate, the roster down the right, the building
/// quick-nav down the left, the heirlooms along the bottom. Clicking a building opens its window.
/// </summary>
internal sealed class HamletUi
{
    private enum Panel { Town, StageCoach, Abbey, Tavern, Sanitarium, Wagon, Graveyard, Upgrades, Hero, Log, Guild, Blacksmith, Survivalist }

    private Panel _panel = Panel.Town;
    private string _building;          // the building whose window is open
    private string _heroId;
    private Vector2 _panelScroll, _logScroll;

    public bool WantsEmbark;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;

    // ---- DD1 layout (campaign/town/*.layout.darkest) ----
    private const float RosterX = RosterColumn.X;
    private static readonly Rect Window = new(144, 132, 1395, 776);
    private const float NavX = 70, NavY = 230, NavSpacing = 68;
    private const float BarY = 958;

    private static readonly string[] NavOrder =
        { Buildings.StageCoach, Buildings.Blacksmith, Buildings.Guild, Buildings.Survivalist, Buildings.Tavern, Buildings.Abbey, Buildings.Sanitarium, Buildings.NomadWagon, Buildings.Graveyard };

    private TownLayout _layout;
    private string _hovered;

    public void Draw()
    {
        var hamlet = S.Hamlet;
        _layout ??= TryLoadLayout();

        // DD1's town crier announces the week's event once per visit.
        bool eventOpen = E.TownEventId != null && _eventSeenWeek != E.Week;
        bool windowOpen = _panel != Panel.Town || eventOpen;
        DrawTown(interactive: !windowOpen);
        DrawEstateTitle();
        DrawNav();
        DrawRoster();
        DrawEstateBar();

        if (eventOpen)
        {
            Gui.Fill(new Rect(0, 0, 1550, 958), new Color(0, 0, 0, 0.55f));
            DrawTownEvent(hamlet);
        }
        else if (_panel == Panel.Hero && E.Hero(_heroId) is { } sheetHero)
        {
            Gui.Fill(new Rect(0, 0, 1550, 958), new Color(0, 0, 0, 0.55f));
            HeroSheet.Draw(sheetHero, id => _heroId = id, () => { _panel = Panel.Town; _heroId = null; });
        }
        else if (windowOpen)
        {
            Gui.Fill(new Rect(0, 0, 1550, 958), new Color(0, 0, 0, 0.55f));
            DrawWindow(hamlet);
        }
        else if (HeroSheet.RealmOpen) DrawRealmAlone();
    }

    /// <summary>The realm inventory opened from the estate bar, without a hero: just the stash to look through.</summary>
    private void DrawRealmAlone()
    {
        Gui.Fill(new Rect(0, 0, 1550, 958), new Color(0, 0, 0, 0.4f));
        var panel = new Rect(881, 128, 667, 780);
        if (Art.Dd1("campaign", "town", "realm_inventory", "realminv_bg.png") is { } bg) GUI.DrawTexture(panel, bg); else Gui.Fill(panel, new Color(0.03f, 0.025f, 0.02f, 0.96f));
        Gui.Text(new Rect(921, 148, 500, 46), "Trinkets", 34, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(921, 194, 580, 50), "Open a hero from the roster to equip them.", 17, Gui.Dd1Class);
        for (int i = 0; i < E.Trinkets.Count && i < 21; i++)
        {
            var r = new Rect(911 + (i % 7) * 80, 323 + (i / 7) * 160, 72, 144);
            HeroSheet.TrinketIcon(r, E.Trinkets[i]);
            if (r.Contains(Event.current.mousePosition)) Gui.Text(new Rect(921, 248, 580, 70), HeroSheet.TrinketText(E.Trinkets[i]), 18, Gui.Dd1Text);
        }
        var close = new Rect(881 + 610 - 6, 128 + 22 - 6, 46, 46);
        if (Art.Dd1("shared", "progression", "progression_close.png") is { } x) GUI.DrawTexture(close, x);
        if (Gui.Hotspot(close) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)) HeroSheet.RealmOpen = false;
    }

    private bool _exchangeOpen;

    private void DrawHeirloomExchange()
    {
        var rates = S.Campaign.HeirloomRates;
        var r = new Rect(400, BarY - 40 - rates.Count / 3 * 64 - 70, 720, 70 + (rates.Count + 2) / 3 * 64);
        Gui.Fill(r, new Color(0.03f, 0.025f, 0.02f, 0.95f));
        Gui.Fill(new Rect(r.x, r.y, r.width, 2), new Color(0.45f, 0.38f, 0.24f));
        Gui.Text(new Rect(r.x, r.y + 8, r.width, 40), "Heirloom exchange", 30, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        Texture2D Icon(string type) => Art.Dd1("shared", "estate", $"currency.{type}.icon.png");
        for (int i = 0; i < rates.Count; i++)
        {
            var (from, fromAmount, to, toAmount) = rates[i];
            var cell = new Rect(r.x + 12 + (i % 3) * 236, r.y + 56 + (i / 3) * 64, 228, 58);
            bool can = E.Get(from) >= fromAmount;
            bool hover = can && cell.Contains(Event.current.mousePosition);
            Gui.Fill(cell, hover ? new Color(0.2f, 0.16f, 0.1f, 0.95f) : new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var old = GUI.color;
            if (!can) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            Gui.Text(new Rect(cell.x + 6, cell.y + 12, 30, 34), fromAmount.ToString(), 24, Gui.Dd1Text, TextAnchor.MiddleRight, heading: true);
            if (Icon(from) is { } fi) GUI.DrawTexture(new Rect(cell.x + 40, cell.y + 9, 40, 40), fi);
            Gui.Text(new Rect(cell.x + 84, cell.y + 12, 40, 34), "→", 24, Gui.Dd1Class, TextAnchor.MiddleCenter);
            Gui.Text(new Rect(cell.x + 124, cell.y + 12, 30, 34), toAmount.ToString(), 24, Gui.Dd1Text, TextAnchor.MiddleRight, heading: true);
            if (Icon(to) is { } ti) GUI.DrawTexture(new Rect(cell.x + 158, cell.y + 9, 40, 40), ti);
            GUI.color = old;
            if (can && Gui.Hotspot(cell) && S.Hamlet.Exchange(from, to)) S.Persist();
        }
    }

    private int _eventSeenWeek = -1;
    private static readonly System.Text.RegularExpressions.Regex Markup = new("\\{[^}]*\\}");

    private static Texture2D EventArt(string file) => Art.Dd1("campaign", "town", "town_event", file);

    private void DrawTownEvent(Hamlet hamlet)
    {
        var ev = hamlet.CurrentEvent;
        if (ev == null) { _eventSeenWeek = E.Week; return; }
        var bg = EventArt("town_event.background.png");
        if (bg != null) GUI.DrawTexture(Window, bg); else Gui.Fill(Window, new Color(0.04f, 0.035f, 0.03f, 0.96f));
        var crier = EventArt("town_event.character.png");
        if (crier != null)
        {
            float k = Mathf.Min(1f, (Window.height - 4) / crier.height);
            GUI.DrawTexture(new Rect(Window.x - 5, Window.yMax - crier.height * k - 2, crier.width * k, crier.height * k), crier);
        }
        float cx = Window.x + 1030;
        var frame = EventArt($"town_event.tone_frame_{ev.Tone}.png");
        if (frame != null) GUI.DrawTexture(new Rect(cx - frame.width / 2f, Window.y + 115 - 20, frame.width, frame.height), frame);
        string title = Dd1Text.Get("miscellaneous", "town_event_title_" + ev.Id) ?? Pretty(ev.Id);
        Gui.Text(new Rect(cx - 300, Window.y + 128, 600, 48), title, 40, ev.Tone == "bad" ? Gui.Blood : Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var image = EventArt($"town_event.image_{ev.Id}.png");
        if (image != null) GUI.DrawTexture(new Rect(cx - 250, Window.y + 216, 500, 240), image);
        string text = Dd1Text.Get("miscellaneous", "town_event_description_" + ev.Id) ?? "";
        text = Markup.Replace(text, "");
        var ink = new Color(0.16f, 0.1f, 0.06f);   // the description sits on parchment
        Gui.Text(new Rect(cx - 242, Window.y + 476, 485, 150), text, 21, ink, TextAnchor.UpperCenter);
        Gui.Text(new Rect(cx - 242, Window.y + 630, 485, 30), "This week only", 18, new Color(0.3f, 0.2f, 0.12f), TextAnchor.MiddleCenter);
        if (Gui.DdButton(new Rect(cx - 110, Window.y + 676, 220, 54), "Continue", true, 26)
            || (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Escape)))
            _eventSeenWeek = E.Week;
    }

    private static TownLayout TryLoadLayout()
    {
        try { return TownLayout.Load(S.Dd1); }
        catch (System.Exception e) { Plugin.Log.LogWarning("[hamlet] town layout: " + e.Message); return new TownLayout(); }
    }

    // ---------------------------------------------------------------- the town

    private static bool IdleSlot(Core.Dd1.SpineSkeleton.Slot s) => s.Name != "active" && !s.Name.StartsWith("smoke");
    private static bool ActiveSlot(Core.Dd1.SpineSkeleton.Slot s) => s.Name != "idle" && !s.Name.StartsWith("smoke");

    private float Upgraded(string building)
    {
        var trees = S.Buildings.Trees.Trees.Where(t => t.Key.StartsWith(building + ".")).ToList();
        int total = trees.Sum(t => t.Value.Count);
        return total == 0 ? 0f : trees.Sum(t => t.Value.Count(l => E.Upgrades.Contains(l.Key))) / (float)total;
    }

    private bool IsOpen(string building) =>
        building == Buildings.Memorial || building == Buildings.Graveyard || S.Buildings.IsOpen(building, E);

    private void DrawTown(bool interactive)
    {
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
        Gui.Image(new Rect(0, 0, Gui.W, Gui.H), Art.TownBackdrop);
        if (_layout == null) return;

        var mouse = Event.current.mousePosition;
        // Pictures: DD1 shows a highlighted ("active") version of the building under the mouse.
        var pictures = new List<(TownLayout.Spot spot, SpineArt.Picture idle, SpineArt.Picture active)>();
        foreach (var spot in _layout.Spots)
        {
            if (spot.Id == "circus") continue;   // DLC
            bool ground = spot.Id == "ground";
            string folder = ground ? S.Dd1.PathOf("fx", "town_ground") : TownLayout.ArtFolder(S.Dd1, spot.Id, IsOpen(spot.Id), Upgraded(spot.Id));
            var idle = SpineArt.Get(folder, "idle", IdleSlot);
            var active = ground ? null : SpineArt.Get(folder, "active", ActiveSlot);
            pictures.Add((spot, idle, active));
        }

        string hovered = null;
        if (interactive && mouse.x < RosterX && mouse.y < BarY)
            for (int i = pictures.Count - 1; i >= 0 && hovered == null; i--)
            {
                var (spot, idle, _) = pictures[i];
                if (spot.Id == "ground" || idle == null) continue;
                if (idle.Hit(new Vector2(spot.X, spot.Y), spot.Scale, mouse)) hovered = spot.Id;
            }
        _hovered = hovered;

        foreach (var (spot, idle, active) in pictures)
        {
            var pic = spot.Id == hovered && active != null ? active : idle;
            pic?.Draw(new Vector2(spot.X, spot.Y), spot.Scale);
        }

        if (hovered != null)
        {
            var (spot, idle, _) = pictures.First(p => p.spot.Id == hovered);
            var r = idle.RectAt(new Vector2(spot.X, spot.Y), spot.Scale);
            DrawBuildingName(hovered, new Vector2(spot.X + spot.NameOffsetX, r.y + 10));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                Open(hovered);
            }
        }
    }

    private void DrawBuildingName(string building, Vector2 at)
    {
        var splat = Art.Dd1("campaign", "town", "buildings", "blg_name_background.png");
        if (splat != null) GUI.DrawTexture(new Rect(at.x - 150, at.y - 70, 300, 120), splat);
        string name = Name(building);
        Gui.Text(new Rect(at.x - 200, at.y - 40, 400, 44), name, 30, IsOpen(building) ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleCenter, heading: true);
        if (!IsOpen(building))
            Gui.Text(new Rect(at.x - 200, at.y, 400, 30), "Not yet restored", 20, Gui.Dim, TextAnchor.MiddleCenter);
    }

    private static string Name(string building) => building switch
    {
        Buildings.StageCoach => "Stage Coach",
        Buildings.NomadWagon => "Nomad Wagon",
        Buildings.Survivalist => "Survivalist",
        Buildings.Memorial => "Memorial",
        _ => Pretty(building),
    };

    private void Open(string building)
    {
        // DD1's door sound for the building (the coach is "coach" there).
        Dd1Audio.Play("/town/enter_" + (building == Buildings.StageCoach ? "coach" : building));
        _building = building;
        _panelScroll = Vector2.zero;
        _panel = building switch
        {
            Buildings.StageCoach => Panel.StageCoach,
            Buildings.Abbey => Panel.Abbey,
            Buildings.Tavern => Panel.Tavern,
            Buildings.Sanitarium => Panel.Sanitarium,
            Buildings.NomadWagon => Panel.Wagon,
            Buildings.Graveyard => Panel.Graveyard,
            Buildings.Guild => Panel.Guild,
            Buildings.Blacksmith => Panel.Blacksmith,
            Buildings.Survivalist => Panel.Survivalist,
            _ => Panel.Log,
        };
        if (!IsOpen(building)) _panel = Panel.Upgrades;
    }

    // ---------------------------------------------------------------- chrome

    private void DrawEstateTitle()
    {
        var plate = Art.Dd1("campaign", "town", "estate_title", "estate_nameplate.png");
        if (plate != null) GUI.DrawTexture(new Rect(0, 0, 893, 281), plate);
        Gui.Text(new Rect(286, 70 - 30, 560, 56), E.Name, 44, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(286, 70 + 24, 560, 32), $"Week {E.Week + 1}", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);
    }

    private void DrawNav()
    {
        for (int i = 0; i < NavOrder.Length; i++)
        {
            string b = NavOrder[i];
            var r = new Rect(NavX - 28, NavY + i * NavSpacing, 56, 56);
            bool open = IsOpen(b);
            bool selected = _building == b && _panel != Panel.Town;
            if (selected || r.Contains(Event.current.mousePosition)) r.y -= 5;   // DD1's "selected hop"
            var old = GUI.color;
            if (!open) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            var icon = Art.Dd1("campaign", "town", "buildings", b, b + ".town_button.png");
            if (icon != null) GUI.DrawTexture(r, icon); else Gui.Fill(r, new Color(0.2f, 0.2f, 0.2f));
            GUI.color = old;
            if (!open)
            {
                var lockIcon = Art.Dd1("campaign", "town", "building_navigation", "bld_quick_nav_locked_icon.png");
                if (lockIcon != null) GUI.DrawTexture(new Rect(r.x + 14, r.y + 14, 28, 28), lockIcon, ScaleMode.ScaleToFit);
            }
            if (r.Contains(Event.current.mousePosition))
                Gui.Text(new Rect(r.xMax + 12, r.y + 8, 300, 40), Name(b), 24, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            if (Gui.Hotspot(r)) Open(b);
        }
    }

    private void DrawRoster()
    {
        bool service = _panel is Panel.Blacksmith or Panel.Guild or Panel.Survivalist;
        bool slots = _panel is Panel.Abbey or Panel.Tavern or Panel.Sanitarium || service;
        var clicked = RosterColumn.Draw(E, S.Buildings.RosterSize(E), h => new RosterColumn.Look(highlight: (_panel == Panel.Hero || service) && _heroId == h.Id), draggable: slots);
        if (Drag.Drop<HeroDrag>(RosterColumn.Area, out var back) && back.FromSlot >= SlotBase)
        {
            S.Hamlet.CancelActivity(back.HeroId);
            S.Persist();
        }
        if (clicked != null)
        {
            _heroId = clicked.Id;
            if (!service)
            {
                _panel = Panel.Hero;
                _building = null;
            }
        }
    }

    /// <summary>
    /// DD1's estate summary (campaign/town/estate_summary + shared/estate layouts): the gold pile and amount at
    /// currency_pos (200,42) of the bar at (0,958), the heirlooms 200 further then 74 apart with their counts beside
    /// them, the heirloom exchange at (700,51), and the navigation buttons from (1800,45) leftwards 110 apart: realm
    /// inventory, activity log, town event. The embark plate sits at DD1's embark_party_pos (754,871).
    /// </summary>
    private void DrawEstateBar()
    {
        Gui.Fill(new Rect(0, BarY, 1550, Gui.H - BarY), new Color(0, 0, 0, 0.8f));
        Gui.Fill(new Rect(0, BarY, 1550, 2), new Color(0.35f, 0.29f, 0.18f));
        var anchor = new Vector2(200, BarY + 42);

        // estate_large_currency_layout: icon at (0,-50), number at (90,-16).
        var gold = Art.Dd1("shared", "estate", "currency.gold.large_icon.png");
        if (gold != null) GUI.DrawTexture(new Rect(anchor.x - 30, anchor.y - 50, 88, 88), gold);
        Gui.Text(new Rect(anchor.x + 60, anchor.y - 36, 150, 40), Gui.Num(E.Get(Currency.Gold), "#,0"), 32, Gui.Gold, TextAnchor.MiddleLeft, heading: true);

        // estate_currency_heirloom_layout: icon at (0,-10), number at (38,-4); currency_spacing 74.
        var heirlooms = new[] { (Currency.Bust, "bust"), (Currency.Portrait, "portrait"), (Currency.Deed, "deed"), (Currency.Crest, "crest") };
        for (int i = 0; i < heirlooms.Length; i++)
        {
            var (cur, icon) = heirlooms[i];
            float x = anchor.x + 200 + i * 74;
            var tex = Art.Dd1("shared", "estate", $"currency.{icon}.icon.png");
            if (tex != null) GUI.DrawTexture(new Rect(x - 4, anchor.y - 30, 40, 40), tex);
            Gui.Text(new Rect(x + 34, anchor.y - 28, 44, 32), E.Get(cur).ToString(), 22, Gui.Dd1Text, TextAnchor.MiddleLeft, heading: true);
            if (new Rect(x - 4, anchor.y - 30, 74, 40).Contains(Event.current.mousePosition))
                Gui.Text(new Rect(x - 60, anchor.y - 64, 170, 26), HamletUi.Pretty(icon) + "s", 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
        }

        var he = Art.Dd1("campaign", "town", "heirloom_exchange", _exchangeOpen ? "he_icon_selected.png" : "he_icon_idle.png");
        var heRect = new Rect(700, BarY + 22, 58, 59);
        if (he != null) GUI.DrawTexture(heRect, he, ScaleMode.ScaleToFit); else Gui.Fill(heRect, new Color(0.2f, 0.17f, 0.12f));
        if (heRect.Contains(Event.current.mousePosition)) Gui.Text(new Rect(heRect.x - 80, heRect.y - 30, 220, 28), "Heirloom exchange", 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
        if (Gui.Hotspot(heRect)) _exchangeOpen = !_exchangeOpen;
        if (_exchangeOpen) DrawHeirloomExchange();

        // Navigation buttons (113 px), centred from x 1800 leftwards every 110.
        NavButton(1800, Art.Dd1("campaign", "town", "realm_inventory", "realm_inventory.icon.png"), $"Trinkets ({E.Trinkets.Count})", HeroSheet.RealmOpen,
                  () => HeroSheet.RealmOpen = !HeroSheet.RealmOpen);
        NavButton(1690, Art.Dd1("campaign", "town", "activity_log", "activity_log.icon.png"), "Activity log", _panel == Panel.Log,
                  () => _panel = _panel == Panel.Log ? Panel.Town : Panel.Log);
        if (E.TownEventId != null)
            NavButton(1580, EventArt("town_event.icon.png"), "The week's event", false, () => _eventSeenWeek = -1);

        var embark = Art.Dd1("campaign", "town", "embark_party", "embark_party.background.png");
        var er = new Rect(754, 871, 412, 113);
        if (embark != null) GUI.DrawTexture(er, embark);
        bool hover = er.Contains(Event.current.mousePosition);
        Gui.Text(er, "Embark", 44, hover ? Color.white : Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        if (Gui.Hotspot(er)) WantsEmbark = true;

        // Ours, not DD1's: the DD2 regions option and the way back to DD2's menu, small at the bar's left end.
        var regionsButton = new Rect(14, BarY + 22, 110, 34);
        Gui.Text(regionsButton, "Regions", 20, regionsButton.Contains(Event.current.mousePosition) || _regionsOpen ? Color.white : Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (Gui.Hotspot(regionsButton)) _regionsOpen = !_regionsOpen;
        if (_regionsOpen) DrawRegions();
        var leave = new Rect(14, BarY + 62, 110, 34);
        Gui.Text(leave, "Leave", 20, leave.Contains(Event.current.mousePosition) ? Color.white : Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (Gui.Hotspot(leave)) Driver.Instance.LeaveHamlet();
    }

    private void NavButton(float centreX, Texture2D icon, string tip, bool selected, System.Action click)
    {
        var r = new Rect(centreX - 56, BarY + 45 - 56, 113, 113);
        bool hover = r.Contains(Event.current.mousePosition);
        if (selected || hover) r.y -= 5;   // DD1's "selected hop"
        var overlay = Art.Dd1("campaign", "town", "estate_summary", "estate_summary.selected_overlay.png");
        if (selected && overlay != null) GUI.DrawTexture(new Rect(r.x + 5, r.y - 13, 103, 139), overlay);
        if (icon != null) GUI.DrawTexture(r, icon); else Gui.Fill(r, new Color(0.15f, 0.12f, 0.09f));
        if (hover) Gui.Text(new Rect(centreX - 120, r.y - 30, 240, 26), tip, 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
        if (Gui.Hotspot(r)) click();
    }

    private bool _regionsOpen;

    /// <summary>
    /// Estate option: DD2's regions as extra DD1-style zones. Each borrows a DD1 zone's maps, curios and loot, is
    /// fought by its DD2 natives and has its lair boss at zone levels 2, 4 and 6. Switching one on puts its quests on
    /// this week's board.
    /// </summary>
    private void DrawRegions()
    {
        var zones = Core.Dungeon.ZoneBase.ExtraZones.ToList();
        var panel = new Rect(300, BarY - 120 - zones.Count * 74, 860, 110 + zones.Count * 74);
        Gui.Fill(panel, new Color(0.03f, 0.025f, 0.02f, 0.96f));
        Gui.Fill(new Rect(panel.x, panel.y, panel.width, 2), new Color(0.45f, 0.38f, 0.24f));
        Gui.Text(new Rect(panel.x + 24, panel.y + 12, 600, 44), "DD2 regions", 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(panel.x + 24, panel.y + 54, panel.width - 48, 30), "Extra zones for this estate. Their quests join the board when switched on.", 18, Gui.Dd1Class);
        for (int i = 0; i < zones.Count; i++)
        {
            string z = zones[i];
            bool on = E.IsToggled("zone." + z);
            var row = new Rect(panel.x + 24, panel.y + 96 + i * 74, panel.width - 48, 66);
            if (row.Contains(Event.current.mousePosition)) Gui.Fill(row, new Color(1, 1, 1, 0.04f));
            var box = new Rect(row.x + 4, row.y + 16, 32, 32);
            Gui.Fill(box, new Color(0.12f, 0.1f, 0.07f));
            Gui.Fill(new Rect(box.x, box.y, box.width, 2), new Color(0.45f, 0.38f, 0.24f));
            if (on) Gui.Fill(new Rect(box.x + 7, box.y + 7, 18, 18), Gui.Gold);
            E.ZoneXp.TryGetValue(z, out int xp);
            Gui.Text(new Rect(row.x + 52, row.y + 2, 400, 34), S.Zones.ZoneName(z), 26, on ? Gui.Dd1Name : Gui.Dd1Class, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(row.x + 460, row.y + 2, 300, 34), on ? $"Level {S.Campaign.ZoneLevel(xp)}" : "Off", 20, Gui.Dd1Class, TextAnchor.MiddleRight);
            Gui.Text(new Rect(row.x + 52, row.y + 34, row.width - 60, 28), S.Zones.Blurb(z), 17, Gui.Dd1Text, TextAnchor.MiddleLeft);
            if (Gui.Hotspot(row))
            {
                S.Hamlet.SetZoneToggle(z, !on);
                S.Persist();
                Gui.Announce(!on ? $"{S.Zones.ZoneName(z)}: its quests are on the board." : $"{S.Zones.ZoneName(z)} is closed.");
            }
        }
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) _regionsOpen = false;
    }

    // ---------------------------------------------------------------- building windows

    private Rect Body => new(Window.x + 596, Window.y + 70, Window.width - 596 - 40, Window.height - 100);

    private void DrawWindow(Hamlet hamlet)
    {
        string building = _panel == Panel.Hero || _panel == Panel.Log ? null : _building;
        var bg = building != null ? Art.BuildingBackground(building) : null;
        if (bg != null) GUI.DrawTexture(Window, bg);
        else
        {
            Gui.Fill(Window, new Color(0.04f, 0.035f, 0.03f, 0.96f));
            Gui.Fill(new Rect(Window.x, Window.y, Window.width, 2), new Color(0.35f, 0.29f, 0.18f));
            Gui.Fill(new Rect(Window.x, Window.yMax - 2, Window.width, 2), new Color(0.35f, 0.29f, 0.18f));
        }

        // Left: the building's keeper (or the hero), as DD1 frames it.
        if (building != null)
        {
            var keeper = Art.Dd1("campaign", "town", "buildings", building, building + ".character.png");
            if (keeper != null)
            {
                float k = Mathf.Min(1f, (Window.height - 4) / keeper.height);
                GUI.DrawTexture(new Rect(Window.x + 2, Window.yMax - keeper.height * k - 2, keeper.width * k, keeper.height * k), keeper);
            }
            Gui.Text(new Rect(Window.x + 40, Window.y + 20, 520, 60), Name(building), 46, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        }
        else if (_panel == Panel.Hero && E.Hero(_heroId) is { } hero)
        {
            var figure = Art.HeroFigure(hero.ClassId);
            if (figure != null) Art.DrawSprite(new Rect(Window.x + 20, Window.y + 90, 540, Window.height - 110), figure);
        }

        // Tabs: the building itself, and its upgrades.
        if (building != null && building != Buildings.Graveyard && building != Buildings.Memorial)
        {
            bool upgrades = _panel == Panel.Upgrades;
            if (IsOpen(building) && Gui.DdButton(new Rect(Body.x, Window.y + 16, 220, 46), "The building", upgrades, 22)) Open(building);
            if (Gui.DdButton(new Rect(Body.x + 230, Window.y + 16, 220, 46), "Upgrades", !upgrades, 22)) _panel = Panel.Upgrades;
        }

        var close = new Rect(Window.xMax - 58, Window.y + 12, 46, 46);
        var closeIcon = Art.Dd1("shared", "progression", "progression_close.png");
        if (closeIcon != null) GUI.DrawTexture(close, closeIcon); else Gui.Text(close, "X", 30, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        if (Gui.Hotspot(close) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
        {
            _panel = Panel.Town;
            _building = null;
            return;
        }

        var area = Body;
        switch (_panel)
        {
            case Panel.Log: DrawTownLog(new Rect(Window.x + 40, Window.y + 30, Window.width - 120, Window.height - 60)); break;
            case Panel.StageCoach: DrawStageCoach(area, hamlet); break;
            case Panel.Abbey: DrawActivities(area, hamlet, Buildings.Abbey); break;
            case Panel.Tavern: DrawActivities(area, hamlet, Buildings.Tavern); break;
            case Panel.Sanitarium: DrawSanitarium(area, hamlet); break;
            case Panel.Wagon: DrawWagon(area, hamlet); break;
            case Panel.Graveyard: DrawGraveyard(area); break;
            case Panel.Upgrades: DrawUpgrades(area, hamlet, _building); break;
            case Panel.Hero: DrawHero(area); break;
            case Panel.Blacksmith: DrawBlacksmith(area, hamlet); break;
            case Panel.Survivalist: DrawSurvivalist(area, hamlet); break;
            case Panel.Guild: DrawGuild(area, hamlet); break;
        }
    }

    private static void Frame(Rect area) => Gui.Fill(area, new Color(0, 0, 0, 0.55f));

    private void DrawNotYet(Rect area)
    {
        Frame(area);
        Gui.Text(new Rect(area.x + 20, area.y + 80, area.width - 40, 200),
            "This building's own services are coming. Its upgrades are already in effect.", 26, Gui.Dd1Text);
    }

    private void DrawBlacksmith(Rect area, Hamlet hamlet)
    {
        Frame(area);
        if (Drag.Hovering<HeroDrag>(area)) Gui.Fill(new Rect(area.x, area.y, area.width, 4), Gui.Gold);
        if (Drag.Drop<HeroDrag>(area, out var dropped)) _heroId = dropped.HeroId;   // DD1: drop a hero on the building
        var hero = E.Hero(_heroId);
        if (hero == null)
        {
            Gui.Text(new Rect(area.x + 20, area.y + 60, area.width - 40, 120), "Choose a hero from the roster to see what the Blacksmith can do for their weapon and armour.", 24, Gui.Dd1Text);
            return;
        }
        var icon = Art.HeroIcon(hero.ClassId);
        if (icon != null) Art.DrawSprite(new Rect(area.x + 16, area.y + 12, 90, 90), icon);
        Gui.Text(new Rect(area.x + 120, area.y + 14, 500, 40), hero.Name, 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(area.x + 120, area.y + 54, 500, 30), $"{Pretty(hero.ClassId)}, resolve {hero.ResolveLevel}", 20, Gui.Dd1Class, TextAnchor.MiddleLeft);

        string dd1Class = S.Campaign.HeroUpgrades.Dd1Class(hero.ClassId);
        float y = area.y + 130;
        foreach (var slot in new[] { Hamlet.Weapon, Hamlet.Armour })
        {
            int rank = hamlet.Rank(hero, slot);
            var r = new Rect(area.x + 10, y, area.width - 20, 250);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var eq = Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_{slot}_{Mathf.Min(rank, 4)}.png");
            if (eq != null) GUI.DrawTexture(new Rect(r.x + 14, r.y + 14, 110, 220), eq, ScaleMode.ScaleToFit);
            Gui.Text(new Rect(r.x + 140, r.y + 10, 400, 40), $"{(slot == Hamlet.Weapon ? "Weapon" : "Armour")}  ·  rank {rank + 1}", 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(r.x + 140, r.y + 50, 470, 30), Dd2.Dd2Heroes.EquipmentText(slot, rank), 20, Gui.Dd1Text, TextAnchor.MiddleLeft);
            var next = hamlet.NextEquipment(hero, slot);
            if (next != null)
            {
                Gui.Text(new Rect(r.x + 140, r.y + 96, 470, 30), $"Next: {Dd2.Dd2Heroes.EquipmentText(slot, rank + 1)}", 19, Gui.Dd1Class, TextAnchor.MiddleLeft);
                Gui.Text(new Rect(r.x + 140, r.y + 126, 470, 30), $"{Gui.Num(hamlet.EquipmentCost(next), "#,0")} gold  ·  resolve {next.Resolve}", 19, Gui.Gold, TextAnchor.MiddleLeft);
                string why = hamlet.WhyCantUpgradeEquipment(hero, slot);
                if (Gui.DdButton(new Rect(r.x + 140, r.y + 172, 300, 54), why ?? "Upgrade", why == null, why == null ? 26 : 18))
                {
                    hamlet.UpgradeEquipment(hero.Id, slot);
                    S.Persist();
                }
            }
            else Gui.Text(new Rect(r.x + 140, r.y + 96, 470, 30), "The finest the Hamlet can make.", 19, Gui.Dd1Class, TextAnchor.MiddleLeft);
            y += 262;
        }
    }

    private void DrawGuild(Rect area, Hamlet hamlet)
    {
        Frame(area);
        if (Drag.Hovering<HeroDrag>(area)) Gui.Fill(new Rect(area.x, area.y, area.width, 4), Gui.Gold);
        if (Drag.Drop<HeroDrag>(area, out var dropped)) _heroId = dropped.HeroId;   // DD1: drop a hero on the building
        var hero = E.Hero(_heroId);
        if (hero == null)
        {
            Gui.Text(new Rect(area.x + 20, area.y + 60, area.width - 40, 120), "Choose a hero from the roster: the Guild teaches the skills they haven't unlocked, masters the ones they know, and sets the five they bring.", 24, Gui.Dd1Text);
            return;
        }
        var icon = Art.HeroIcon(hero.ClassId);
        if (icon != null) Art.DrawSprite(new Rect(area.x + 16, area.y + 12, 90, 90), icon);
        Gui.Text(new Rect(area.x + 120, area.y + 14, 500, 40), hero.Name, 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        var skills = Dd2.HeroSkills.ForClass(hero.ClassId);
        if (skills == null) { Gui.Text(new Rect(area.x + 120, area.y + 54, 500, 30), "...", 20, Gui.Dd1Class, TextAnchor.MiddleLeft); return; }
        string loadout = hero.EquippedSkills.Count == 0 ? "DD2 chooses the skills" : $"Brings {hero.EquippedSkills.Count}/{Dd2.HeroSkills.EquipLimit} chosen skills";
        Gui.Text(new Rect(area.x + 120, area.y + 54, 360, 30), $"{Pretty(hero.ClassId)}  ·  {loadout}", 19, Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (hero.EquippedSkills.Count > 0 && Gui.DdButton(new Rect(area.xMax - 200, area.y + 50, 186, 38), "DD2's choice", true, 18))
        {
            hero.EquippedSkills.Clear();
            S.Persist();
        }

        _panelScroll = GUI.BeginScrollView(new Rect(area.x, area.y + 110, area.width, area.height - 120), _panelScroll, new Rect(0, 0, area.width - 30, skills.Count * 86));
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            bool known = Dd2.HeroSkills.Knows(hero, skill), mastered = hero.MasteredSkills.Contains(skill.Id);
            bool equipped = hero.EquippedSkills.Contains(skill.Id);
            var r = new Rect(10, i * 86, area.width - 50, 80);
            Gui.Fill(r, equipped ? new Color(0.16f, 0.13f, 0.08f, 0.95f) : new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var old = GUI.color;
            if (!known) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            if (skill.Icon != null) Art.DrawSprite(new Rect(r.x + 8, r.y + 8, 64, 64), skill.Icon);
            GUI.color = old;
            Gui.Text(new Rect(r.x + 84, r.y + 6, 300, 34), Dd2.HeroSkills.Name(skill.Id) + (mastered ? " +" : ""), 24, known ? Gui.Dd1Name : Gui.Dd1Class, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(r.x + 84, r.y + 40, 300, 30), mastered ? "Mastered" : known ? (skill.Starting ? "Known" : "Learned") : "Not yet learned", 17, mastered ? Gui.Gold : Gui.Dd1Class, TextAnchor.MiddleLeft);

            if (!known)
            {
                string why = hamlet.WhyCantLearnSkill(hero, skill.Id);
                if (Gui.DdButton(new Rect(r.xMax - 330, r.y + 16, 200, 48), why ?? $"Learn  {Gui.Num(hamlet.SkillLearnCost(hero, skill.Id), "#,0")}g", why == null, why == null ? 19 : 15))
                {
                    hamlet.LearnSkill(hero.Id, skill.Id);
                    S.Persist();
                }
            }
            else if (!mastered)
            {
                string why = hamlet.WhyCantMasterSkill(hero, skill.Id, known);
                if (Gui.DdButton(new Rect(r.xMax - 330, r.y + 16, 200, 48), why ?? $"Master  {Gui.Num(hamlet.SkillMasterCost(hero, skill.Id), "#,0")}g", why == null, why == null ? 19 : 15))
                {
                    hamlet.MasterSkill(hero.Id, skill.Id, known);
                    S.Persist();
                }
            }
            if (known)
            {
                bool full = !equipped && hero.EquippedSkills.Count >= Dd2.HeroSkills.EquipLimit;
                if (Gui.DdButton(new Rect(r.xMax - 120, r.y + 16, 108, 48), equipped ? "Bring" : "Leave", !full, 19))
                {
                    if (equipped) hero.EquippedSkills.Remove(skill.Id); else hero.EquippedSkills.Add(skill.Id);
                    S.Persist();
                }
            }
        }
        GUI.EndScrollView();
    }

    private void DrawSurvivalist(Rect area, Hamlet hamlet)
    {
        Frame(area);
        if (Drag.Hovering<HeroDrag>(area)) Gui.Fill(new Rect(area.x, area.y, area.width, 4), Gui.Gold);
        if (Drag.Drop<HeroDrag>(area, out var dropped)) _heroId = dropped.HeroId;   // DD1: drop a hero on the building
        var hero = E.Hero(_heroId);
        if (hero == null)
        {
            Gui.Text(new Rect(area.x + 20, area.y + 60, area.width - 40, 120), "Choose a hero from the roster: the Survivalist teaches the camping skills they don't know yet.", 24, Gui.Dd1Text);
            return;
        }
        var icon = Art.HeroIcon(hero.ClassId);
        if (icon != null) Art.DrawSprite(new Rect(area.x + 16, area.y + 12, 90, 90), icon);
        Gui.Text(new Rect(area.x + 120, area.y + 14, 500, 40), hero.Name, 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(area.x + 120, area.y + 54, 500, 30), $"{Pretty(hero.ClassId)}  ·  knows {hero.CampingSkills.Count} camping skills", 20, Gui.Dd1Class, TextAnchor.MiddleLeft);

        var skills = S.Content.Camping.ForClass(hero.ClassId);
        skills.Sort((a, b) => hero.CampingSkills.Contains(b).CompareTo(hero.CampingSkills.Contains(a)));
        _panelScroll = GUI.BeginScrollView(new Rect(area.x, area.y + 120, area.width, area.height - 130), _panelScroll, new Rect(0, 0, area.width - 30, skills.Count * 98));
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = S.Content.Camping.Get(skills[i]);
            bool known = hero.CampingSkills.Contains(skill.Id);
            var r = new Rect(10, i * 98, area.width - 50, 90);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var tex = Art.Dd1("raid", "camping", "skill_icons", $"camp_skill_{skill.Id}.png");
            var old = GUI.color;
            if (!known) GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            if (tex != null) GUI.DrawTexture(new Rect(r.x + 8, r.y + 9, 72, 72), tex);
            GUI.color = old;
            Gui.Text(new Rect(r.x + 94, r.y + 6, 330, 32), Dd1Text.CampSkillName(skill.Id), 24, known ? Gui.Dd1Name : Gui.Dd1Class, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(r.x + 94, r.y + 38, 400, 48), $"{skill.Cost} respite  ·  " + string.Join(", ", S.Content.Camping.DescribeAll(skill)), 16, Gui.Dd1Text);
            if (known) Gui.Text(new Rect(r.xMax - 170, r.y + 26, 156, 36), "Known", 22, Gui.Gold, TextAnchor.MiddleCenter, heading: true);
            else
            {
                string why = hamlet.WhyCantLearnCampSkill(hero, skill.Id);
                if (Gui.DdButton(new Rect(r.xMax - 190, r.y + 18, 176, 54), why ?? $"Learn  {Gui.Num(hamlet.CampSkillCost(skill), "#,0")}g", why == null, why == null ? 20 : 15))
                {
                    hamlet.LearnCampSkill(hero.Id, skill.Id);
                    S.Persist();
                }
            }
        }
        GUI.EndScrollView();
    }

    private void DrawTownLog(Rect area)
    {
        Frame(area);
        Gui.Text(new Rect(area.x + 20, area.y + 10, 900, 50), "The Hamlet's chronicle", 40, Gui.Dd1Name, heading: true);
        var lines = E.TownLog.Concat(Driver.Instance.HomecomingLog).ToList();
        _logScroll = GUI.BeginScrollView(new Rect(area.x + 20, area.y + 70, area.width - 40, area.height - 90), _logScroll, new Rect(0, 0, area.width - 70, lines.Count * 34 + 40));
        if (lines.Count == 0) Gui.Label(new Rect(0, 0, area.width - 70, 30), "All is quiet. Choose a quest and embark when ready.");
        for (int i = 0; i < lines.Count; i++) Gui.Label(new Rect(0, i * 34, area.width - 70, 32), lines[i]);
        GUI.EndScrollView();
    }

    private void DrawStageCoach(Rect area, Hamlet hamlet)
    {
        Frame(area);
        Gui.Text(new Rect(area.x + 20, area.y + 10, 700, 40),
            $"New recruits arrive each week. Roster: {E.Roster.Count}/{S.Buildings.RosterSize(E)}", 22, Gui.Dd1Text);
        for (int i = 0; i < E.Recruits.Count; i++)
        {
            var h = E.Recruits[i];
            var r = new Rect(area.x + 10, area.y + 60 + i * 104, area.width - 20, 98);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var sprite = Art.HeroIcon(h.ClassId);
            if (sprite != null) Art.DrawSprite(new Rect(r.x + 6, r.y + 6, 86, 86), sprite);
            Gui.Text(new Rect(r.x + 104, r.y + 4, 400, 34), $"{h.Name}", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(r.x + 104, r.y + 34, 480, 60),
                $"{Pretty(h.ClassId)}, resolve {h.ResolveLevel}\n{string.Join(", ", h.Quirks.Select(QuirkName))}", 17, Gui.Dd1Class);
            if (Gui.DdButton(new Rect(r.xMax - 150, r.y + 26, 136, 46), "Recruit", hamlet.CanRecruit, 22))
            {
                hamlet.Recruit(h.Id);
                S.Persist();
            }
        }
    }

    // ---- DD1 activity slots (building.layout.darkest): rows 230 apart, slots 135 apart, in the painted arches ----

    private const int SlotBase = 1000;          // HeroDrag.FromSlot for heroes picked up from an activity slot
    private static readonly Vector2 ActivityBase = new(596 + 70, 102 + 50);   // body_base_pos + activity list base_pos
    private string _treatHero, _treatKind;      // Sanitarium: the hero waiting for a quirk choice

    private sealed class ActivityRow
    {
        public string Key, Name, Icon, Description, Building;
        public int Slots;
        public System.Func<HeroRecord, string> WhyNot;   // null = can take this hero
        public System.Action<HeroRecord> Start;
        public Reward Cost;
    }

    private static Texture2D BuildingArt(string building, string file) => Art.Dd1("campaign", "town", "buildings", building, file);

    private void DrawSlotRows(Hamlet hamlet, string building, List<ActivityRow> rows)
    {
        var slotBg = Art.Dd1("campaign", "town", "hero_slot", "hero_slot.background.png");
        var locked = BuildingArt(building, building + ".locked_hero_slot_overlay.png");
        for (int ai = 0; ai < rows.Count && ai < 3; ai++)
        {
            var row = rows[ai];
            var basePos = new Vector2(ActivityBase.x, ActivityBase.y + ai * 230);
            var icon = BuildingArt(building, row.Icon);
            if (icon != null) GUI.DrawTexture(new Rect(basePos.x + 70, basePos.y + 36, 72, 72), icon);
            Gui.Text(new Rect(basePos.x + 170, basePos.y + 22, 270, 36), row.Name, 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(basePos.x + 170, basePos.y + 72, 260, 110), row.Description, 17, Gui.Dd1Text);
            var inside = E.Roster.Where(h => h.Activity == row.Key).OrderBy(h => h.Name).ToList();
            for (int j = 0; j < 3; j++)
            {
                var r = new Rect(basePos.x + 440 + j * 135, basePos.y + 119, 85, 85);
                if (j >= row.Slots)
                {
                    if (locked != null) GUI.DrawTexture(new Rect(r.x - 42, r.y - 70, 171, 179), locked);
                    else Gui.Fill(r, new Color(0, 0, 0, 0.7f));
                    continue;
                }
                if (slotBg != null) GUI.DrawTexture(r, slotBg);
                if (j < inside.Count)
                {
                    var h = inside[j];
                    string cls = h.ClassId;
                    int from = SlotBase + ai * 10 + j;
                    Drag.Source(r, new HeroDrag(h.Id, from), rect => { var ic = Art.HeroIcon(cls); if (ic != null) Art.DrawSprite(new Rect(rect.x + 4, rect.y + 4, 77, 77), ic); });
                    bool carried = Drag.Payload is HeroDrag hd && hd.FromSlot == from;
                    var sprite = Art.HeroIcon(h.ClassId);
                    if (sprite != null && !carried) Art.DrawSprite(new Rect(r.x + 4, r.y + 4, 77, 77), sprite);
                    if (r.Contains(Event.current.mousePosition) && !Drag.Active)
                        Gui.Text(new Rect(r.x - 60, r.yMax + 2, r.width + 120, 24), h.Activity == row.Key && h.ActivityTarget != null ? $"{h.Name}: {QuirkName(h.ActivityTarget)}" : $"{h.Name} — drag out to cancel", 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
                    if (Gui.Hotspot(r) && !Drag.JustDropped && !h.ActivityLocked)
                    {
                        hamlet.CancelActivity(h.Id);
                        S.Persist();
                    }
                    continue;
                }
                // An empty slot: drop a hero to send them.
                bool hover = Drag.Hovering<HeroDrag>(r);
                string why = null;
                if (hover && Drag.Payload is HeroDrag over && E.Hero(over.HeroId) is { } candidate) why = row.WhyNot(candidate);
                if (hover) Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), why == null ? Gui.Gold : Gui.Blood);
                if (hover && why != null) Gui.Text(new Rect(r.x - 80, r.yMax + 8, r.width + 160, 24), why, 16, Gui.Blood, TextAnchor.MiddleCenter);
                if (Drag.Drop<HeroDrag>(r, out var dropped) && E.Hero(dropped.HeroId) is { } hero)
                {
                    if (dropped.FromSlot >= SlotBase) hamlet.CancelActivity(hero.Id);   // moved between slots
                    if (row.WhyNot(hero) == null) row.Start(hero);
                    S.Persist();
                }
            }
        }
    }

    private void DrawActivities(Rect area, Hamlet hamlet, string building)
    {
        var rows = new List<ActivityRow>();
        foreach (var a in S.Buildings.Activities.Where(a => a.Building == building))
        {
            var cost = hamlet.ActivityCost(a);
            var (lo, hi) = a.StressHeal(E);
            var act = a;
            rows.Add(new ActivityRow
            {
                Key = a.Key, Building = building, Name = Pretty(a.Id), Icon = $"{building}.{a.Id}.icon.png", Slots = a.Slots(E), Cost = cost,
                Description = $"Relieves {Gui.Num(lo / 10f)}-{Gui.Num(hi / 10f)} stress\n{(cost == null || cost.Amount == 0 ? "Free this week" : $"{Gui.Num(cost.Amount, "#,0")} {cost.Type}")}",
                WhyNot = h => hamlet.WhyCantDo(h, act),
                Start = h => hamlet.StartActivity(h.Id, act.Key),
            });
        }
        DrawSlotRows(hamlet, building, rows);
        Gui.Text(new Rect(Window.x + 596 + 40, Window.yMax - 40, 700, 30), "Drag a hero from the roster into a slot. They stay for the week.", 17, Gui.Dd1Class, TextAnchor.MiddleLeft);
    }

    private void DrawSanitarium(Rect area, Hamlet hamlet)
    {
        string Why(HeroRecord h, bool disease)
        {
            if (!h.IsAvailable) return h.MissingWeeks > 0 ? "Missing." : "Busy.";
            bool any = disease ? h.Quirks.Any(S.Catalog.IsDisease) : h.Quirks.Any(q => !S.Catalog.IsDisease(q));
            return any ? null : disease ? "No disease to treat." : "No quirk to treat or lock.";
        }
        var rows = new List<ActivityRow>
        {
            new()
            {
                Key = "sanitarium.treatment", Building = Buildings.Sanitarium, Name = "Quirk treatment", Icon = "sanitarium.slots.icon.png",
                Slots = S.Buildings.SanitariumSlots(E, "treatment"),
                Description = "Treat a negative quirk, or lock in a positive one. Takes the week.",
                WhyNot = h => Why(h, false), Start = h => { _treatHero = h.Id; _treatKind = "treatment"; },
            },
            new()
            {
                Key = "sanitarium.disease_treatment", Building = Buildings.Sanitarium, Name = "Disease treatment", Icon = "sanitarium.disease_quirk_cost.icon.png",
                Slots = S.Buildings.SanitariumSlots(E, "disease_treatment"),
                Description = "Cure a disease. Takes the week.",
                WhyNot = h => Why(h, true), Start = h => { _treatHero = h.Id; _treatKind = "disease_treatment"; },
            },
        };
        DrawSlotRows(hamlet, Buildings.Sanitarium, rows);
        if (_treatHero != null) DrawTreatmentChoice(hamlet);
        else Gui.Text(new Rect(Window.x + 596 + 40, Window.yMax - 40, 700, 30), "Drag a hero into a slot, then choose what to treat.", 17, Gui.Dd1Class, TextAnchor.MiddleLeft);
    }

    /// <summary>DD1's quirk choice: positive quirks to lock on the left, negative ones to treat on the right (diseases alone for a cure).</summary>
    private void DrawTreatmentChoice(Hamlet hamlet)
    {
        var hero = E.Hero(_treatHero);
        if (hero == null) { _treatHero = null; return; }
        bool disease = _treatKind == "disease_treatment";
        var backdrop = BuildingArt(Buildings.Sanitarium, disease ? "disease_treatment_backdrop.png" : "quirk_treatment_backdrop.png");
        var header = BuildingArt(Buildings.Sanitarium, disease ? "diseaseheader.png" : "quirkheader.png");
        var r = new Rect(960 - 420, 330, 840, 500);
        if (backdrop != null) GUI.DrawTexture(r, backdrop); else Gui.Fill(r, new Color(0.03f, 0.025f, 0.02f, 0.96f));
        if (header != null) GUI.DrawTexture(new Rect(960 - 225, r.y - 30, 450, 51), header);
        Gui.Text(new Rect(r.x, r.y - 28, r.width, 46), $"{hero.Name}: {(disease ? "cure a disease" : "treat or lock a quirk")}", 26, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var lists = disease
            ? new[] { (hero.Quirks.Where(S.Catalog.IsDisease).ToList(), r.x + 210, "disease_highlight.png", "Cure") }
            : new[]
            {
                (hero.Quirks.Where(q => S.Catalog.IsPositive(q) && !S.Catalog.IsDisease(q)).ToList(), r.x + 40, "posquirk_highlight.png", "Lock"),
                (hero.Quirks.Where(q => !S.Catalog.IsPositive(q) && !S.Catalog.IsDisease(q)).ToList(), r.x + 440, "negquirk_highlight.png", "Treat"),
            };
        foreach (var (quirks, x, highlight, verb) in lists)
            for (int i = 0; i < quirks.Count; i++)
            {
                string q = quirks[i];
                var row = new Rect(x, r.y + 60 + i * 38, 360, 32);
                var cost = hamlet.TreatmentCost(hero, q);
                bool lockedAlready = hero.LockedQuirks.Contains(q) && S.Catalog.IsPositive(q);
                bool can = cost != null && E.Get(cost.Type) >= cost.Amount && !lockedAlready;
                bool hover = can && row.Contains(Event.current.mousePosition);
                var hl = BuildingArt(Buildings.Sanitarium, highlight);
                if (hover && hl != null) GUI.DrawTexture(new Rect(row.x - 10, row.y, 380, 32), hl);
                Gui.Text(new Rect(row.x, row.y, 230, 32), QuirkName(q) + (hero.LockedQuirks.Contains(q) ? " (locked)" : ""), 19, S.Catalog.IsPositive(q) ? Gui.Gold : Gui.Dd1Health, TextAnchor.MiddleLeft);
                if (cost != null) Gui.Text(new Rect(row.x + 230, row.y, 130, 32), lockedAlready ? "" : $"{verb} {Gui.Num(cost.Amount, "#,0")}", 17, can ? Gui.Dd1Text : Gui.Dim, TextAnchor.MiddleRight);
                if (can && Gui.Hotspot(row))
                {
                    hamlet.StartTreatment(hero.Id, q);
                    S.Persist();
                    _treatHero = null;
                    return;
                }
            }
        if (Gui.DdButton(new Rect(960 - 90, r.yMax - 60, 180, 46), "Cancel", true, 22)) _treatHero = null;
    }

    private void DrawWagon(Rect area, Hamlet hamlet)
    {
        // DD1's wagon grid (nomad_wagon.layout.darkest): background at body + (230,150), 6 columns 100 x 180 apart.
        var origin = new Vector2(596 + 230, 102 + 150);
        if (BuildingArt(Buildings.NomadWagon, "inventory_grid_background.png") is { } grid) GUI.DrawTexture(new Rect(origin.x, origin.y, 684, 360), grid);
        string hovered = null;
        for (int i = 0; i < E.WagonStock.Count && i < 12; i++)
        {
            string t = E.WagonStock[i];
            int price = hamlet.WagonPrice(t);
            var r = new Rect(origin.x + 55 + (i % 6) * 100, origin.y - 10 + (i / 6) * 180 + 20, 72, 144);
            bool afford = E.Get(Currency.Gold) >= price;
            var old = GUI.color;
            if (!afford) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            HeroSheet.TrinketIcon(r, t);
            GUI.color = old;
            Gui.Text(new Rect(r.x - 14, r.yMax + 2, r.width + 28, 22), Gui.Num(price, "#,0"), 17, afford ? Gui.Gold : Gui.Dim, TextAnchor.MiddleCenter);
            if (r.Contains(Event.current.mousePosition)) hovered = t;
            if (afford && Gui.Hotspot(r))
            {
                hamlet.BuyTrinket(t);
                S.Persist();
                break;
            }
        }
        if (E.WagonStock.Count == 0) Gui.Text(new Rect(origin.x, origin.y + 150, 684, 40), "Sold out until next week.", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);
        string info = hovered != null ? HeroSheet.TrinketText(hovered).Replace('\n', ' ') + "  ·  click to buy" : $"Your stash: {E.Trinkets.Count} trinkets. Equip them from a hero's sheet (click a hero in the roster).";
        Gui.Text(new Rect(origin.x, origin.y + 380, 684, 50), info, 18, Gui.Dd1Text, TextAnchor.UpperCenter);
    }

    private void DrawGraveyard(Rect area)
    {
        Frame(area);
        for (int i = 0; i < E.Graveyard.Count; i++)
        {
            var h = E.Graveyard[i];
            Gui.Text(new Rect(area.x + 20, area.y + 14 + i * 40, area.width - 40, 38),
                $"{h.Name} the {Pretty(h.ClassId)}, resolve {h.ResolveLevel}: {h.CauseOfDeath} (week {h.WeekDied + 1})", 19, Gui.Dd1Text, TextAnchor.MiddleLeft);
        }
        if (E.Graveyard.Count == 0) Gui.Text(new Rect(area.x + 20, area.y + 20, area.width - 40, 40), "No one rests here. Yet.", 24, Gui.Dd1Class);
    }

    private void DrawUpgrades(Rect area, Hamlet hamlet, string building)
    {
        Frame(area);
        if (building == null) return;
        var trees = S.Buildings.Trees.Trees.Keys.Where(k => k.StartsWith(building + ".")).ToList();
        if (trees.Count == 0) Gui.Text(new Rect(area.x + 20, area.y + 20, area.width - 40, 40), "Nothing to improve here.", 24, Gui.Dd1Class);
        _panelScroll = GUI.BeginScrollView(new Rect(area.x, area.y + 10, area.width, area.height - 20), _panelScroll, new Rect(0, 0, area.width - 30, trees.Count * 96));
        for (int i = 0; i < trees.Count; i++)
        {
            var levels = S.Buildings.Trees.Trees[trees[i]];
            var next = S.Buildings.Trees.Next(E, trees[i]);
            var r = new Rect(10, i * 96, area.width - 50, 88);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            int owned = levels.Count(l => E.Upgrades.Contains(l.Key));
            Gui.Text(new Rect(r.x + 14, r.y + 4, 420, 36), Pretty(trees[i].Substring(trees[i].IndexOf('.') + 1)), 24, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            for (int l = 0; l < levels.Count; l++)
                Gui.Fill(new Rect(r.x + 14 + l * 26, r.y + 46, 20, 20), l < owned ? Gui.Gold : new Color(0.25f, 0.22f, 0.18f));
            Gui.Text(new Rect(r.x + 14, r.y + 66, 460, 22), next == null ? "Fully upgraded" : string.Join("  ", next.Cost.Select(c => $"{c.Amount} {c.Type}")), 16, Gui.Dd1Class, TextAnchor.MiddleLeft);
            if (next != null && Gui.DdButton(new Rect(r.xMax - 150, r.y + 20, 136, 48), "Buy", S.Buildings.Trees.CanBuy(E, next), 22))
            {
                hamlet.BuyUpgrade(next.TreeId, next.Code);
                S.Persist();
            }
        }
        GUI.EndScrollView();
    }

    private void DrawHero(Rect area)
    {
        var h = E.Hero(_heroId);
        if (h == null) { _panel = Panel.Town; return; }
        Frame(area);
        Gui.Text(new Rect(Window.x + 40, Window.y + 20, 900, 60), $"{h.Name}", 46, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(Window.x + 40, Window.y + 72, 900, 30), $"{Pretty(h.ClassId)}, resolve level {h.ResolveLevel}", 24, Gui.Dd1Class, TextAnchor.MiddleLeft);
        Gui.Text(new Rect(area.x + 20, area.y + 10, area.width - 40, 300),
            $"Stress {h.Stress}/10   ({h.ResolveXp} resolve xp)\n\n" +
            $"Quirks: {string.Join(", ", h.Quirks.Select(QuirkName))}\n\n" +
            $"Camp skills: {string.Join(", ", h.CampingSkills.Select(Pretty))}\n\n" +
            $"Trinkets: {(h.Trinkets.Count == 0 ? "none" : string.Join(", ", h.Trinkets.Select(Pretty)))}" +
            (h.Activity != null ? $"\n\nThis week: {Pretty(h.Activity)}" : ""), 19, Gui.Dd1Text);

        float y = area.y + 330;
        foreach (var t in h.Trinkets.ToList())
        {
            if (Gui.DdButton(new Rect(area.x + 20, y, 330, 42), "Unequip " + Pretty(t), true, 18))
            {
                h.Trinkets.Remove(t);
                E.Trinkets.Add(t);
                S.Persist();
            }
            y += 48;
        }
        if (h.Trinkets.Count < 2)
        {
            int x = 0;
            foreach (var t in E.Trinkets.Distinct().Where(t => S.Catalog.TrinketFits(t, h.ClassId)).Take(8).ToList())
            {
                if (Gui.DdButton(new Rect(area.x + 20 + (x % 2) * 350, y + (x / 2) * 48, 340, 42), "Equip " + Pretty(t), true, 18))
                {
                    E.Trinkets.Remove(t);
                    h.Trinkets.Add(t);
                    S.Persist();
                }
                x++;
            }
        }
        if (Gui.DdButton(new Rect(area.xMax - 230, area.yMax - 56, 220, 46), "Dismiss hero", true, 20))
        {
            S.Hamlet.Dismiss(h.Id);
            S.Persist();
            _panel = Panel.Town;
        }
    }

    /// <summary>A quirk for display: DD2-style ids ("quirk_braggart_neg") lose their prefix and polarity tag.</summary>
    public static string QuirkName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        string s = id.StartsWith("quirk_") ? id.Substring(6) : id;
        foreach (var tag in new[] { "_negative", "_positive", "_neg", "_pos" })
            if (s.EndsWith(tag)) { s = s.Substring(0, s.Length - tag.Length); break; }
        return Pretty(s);
    }

    public static string Pretty(string id) =>
        string.IsNullOrEmpty(id) ? "" : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            (id.StartsWith("quest_item+", System.StringComparison.Ordinal) ? id.Substring(11) : id).Replace('_', ' ').Replace('.', ' '));
}
