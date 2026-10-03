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

        // DD1: the building's upgrades open in a panel over the left of the window; its page stays on the right.
        bool hasTrees = building != null && S.Buildings.Trees.Trees.Keys.Any(k => k.StartsWith(building + "."));
        if (hasTrees && IsOpen(building) && Gui.DdButton(new Rect(Window.x + 40, Window.y + 84, 200, 44), _upgradesOpen ? "Close upgrades" : "Upgrades", true, 20))
            _upgradesOpen = !_upgradesOpen;

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
            case Panel.Upgrades: break;   // a building not built yet: only its upgrades
            case Panel.Hero: DrawHero(area); break;
            case Panel.Blacksmith: DrawBlacksmith(area, hamlet); break;
            case Panel.Survivalist: DrawSurvivalist(area, hamlet); break;
            case Panel.Guild: DrawGuild(area, hamlet); break;
        }
        if (building != null && hasTrees && (_upgradesOpen || _panel == Panel.Upgrades)) DrawUpgrades(hamlet, building);
    }

    private bool _upgradesOpen;

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
        // The hero being outfitted, under the rows (drop another hero from the roster to switch).
        var icon = Art.HeroIcon(hero.ClassId);
        if (icon != null) Art.DrawSprite(new Rect(646, 640, 90, 90), icon);
        Gui.Text(new Rect(750, 642, 600, 40), hero.Name, 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(750, 682, 600, 30), $"{Pretty(hero.ClassId)}, resolve {hero.ResolveLevel}  ·  drop another hero here to switch", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);

        // DD1's Blacksmith (blacksmith.layout): a row per slot from body + (50,20), 176 apart, the five equipment
        // pictures 75 apart: owned ranks as they are, the next one highlighted with its cost, later ones dark.
        string dd1Class = S.Campaign.HeroUpgrades.Dd1Class(hero.ClassId);
        var highlight = UpgradeArt("requirement_highlight_overlay.png");
        for (int s = 0; s < 2; s++)
        {
            string slot = s == 0 ? Hamlet.Weapon : Hamlet.Armour;
            int rank = hamlet.Rank(hero, slot);
            var next = hamlet.NextEquipment(hero, slot);
            string why = next == null ? null : hamlet.WhyCantUpgradeEquipment(hero, slot);
            float x0 = 596 + 50, y0 = 102 + 20 + 120 + s * 176;
            Gui.Text(new Rect(x0, y0 - 34, 300, 30), slot == Hamlet.Weapon ? "Weapon" : "Armour", 24, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            for (int k = 0; k < 5; k++)
            {
                var r = new Rect(x0 + k * 75, y0, 72, 144);
                var pic = Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_{slot}_{k}.png");
                var old = GUI.color;
                if (k > rank + 1 || (k == rank + 1 && next == null)) GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                else if (k == rank + 1) GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                if (pic != null) GUI.DrawTexture(r, pic); else Gui.Fill(r, new Color(0.15f, 0.12f, 0.09f));
                GUI.color = old;
                if (k <= rank) Gui.Fill(new Rect(r.x + 4, r.yMax + 3, r.width - 8, 3), Gui.Gold);
                if (k == rank + 1 && next != null)
                {
                    Gui.Text(new Rect(r.x - 10, r.yMax + 2, r.width + 20, 22), Gui.Num(hamlet.EquipmentCost(next), "#,0"), 16, why == null ? Gui.Gold : Gui.Dim, TextAnchor.MiddleCenter);
                    if (r.Contains(Event.current.mousePosition) && highlight != null) GUI.DrawTexture(new Rect(r.x + 11, r.y + 47, 50, 50), highlight);
                    if (why == null && Gui.Hotspot(r))
                    {
                        hamlet.UpgradeEquipment(hero.Id, slot);
                        Runtime.Dd1Audio.Play(slot == Hamlet.Weapon ? "/town/blacksmith_purchase_wep" : "/town/blacksmith_purchase_arm");
                        S.Persist();
                    }
                }
            }
            // What it does now and next (beside the row).
            float tx = x0 + 5 * 75 + 24;
            Gui.Text(new Rect(tx, y0, 1500 - tx, 30), $"Rank {rank + 1}: {Dd2.Dd2Heroes.EquipmentText(slot, rank)}", 19, Gui.Dd1Text, TextAnchor.MiddleLeft);
            if (next != null)
            {
                Gui.Text(new Rect(tx, y0 + 34, 1500 - tx, 30), $"Next: {Dd2.Dd2Heroes.EquipmentText(slot, rank + 1)}", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
                Gui.Text(new Rect(tx, y0 + 64, 1500 - tx, 30), why ?? $"{Gui.Num(hamlet.EquipmentCost(next), "#,0")} gold · needs resolve {next.Resolve} · click it", 17, why == null ? Gui.Gold : Gui.Blood, TextAnchor.MiddleLeft);
            }
            else Gui.Text(new Rect(tx, y0 + 34, 1500 - tx, 30), "The finest the Hamlet can make.", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
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
        // The hero at the top (drop another hero from the roster to switch).
        var icon = Art.HeroIcon(hero.ClassId);
        if (icon != null) Art.DrawSprite(new Rect(646, 140, 72, 72), icon);
        Gui.Text(new Rect(730, 140, 500, 40), hero.Name, 30, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        var skills = Dd2.HeroSkills.ForClass(hero.ClassId);
        if (skills == null) return;
        string loadout = hero.EquippedSkills.Count == 0 ? "DD2 chooses the skills they bring" : $"Brings {hero.EquippedSkills.Count}/{Dd2.HeroSkills.EquipLimit} chosen skills";
        Gui.Text(new Rect(730, 178, 520, 30), $"{Pretty(hero.ClassId)}  ·  {loadout}", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (hero.EquippedSkills.Count > 0 && Gui.DdButton(new Rect(1290, 150, 186, 38), "DD2's choice", true, 18))
        {
            hero.EquippedSkills.Clear();
            S.Persist();
        }

        // DD1's Guild (guild.layout): a row per skill 91 apart, its ranks 75 apart with the cost under each (34,70).
        // A DD2 skill has two: learning it and mastering it (its "+" form). Two columns of six.
        var highlight = UpgradeArt("requirement_highlight_overlay.png");
        string tip = null;
        for (int i = 0; i < skills.Count && i < 12; i++)
        {
            var skill = skills[i];
            bool known = Dd2.HeroSkills.Knows(hero, skill), mastered = hero.MasteredSkills.Contains(skill.Id);
            bool equipped = hero.EquippedSkills.Contains(skill.Id);
            float x = 646 + (i / 6) * 450, y = 236 + (i % 6) * 91;
            string learnWhy = known ? null : hamlet.WhyCantLearnSkill(hero, skill.Id);
            string masterWhy = !known || mastered ? null : hamlet.WhyCantMasterSkill(hero, skill.Id, known);
            for (int rank = 0; rank < 2; rank++)
            {
                var r = new Rect(x + rank * 75, y, 64, 64);
                bool has = rank == 0 ? known : mastered;
                bool next = rank == 0 ? !known : known && !mastered;
                string why = rank == 0 ? learnWhy : masterWhy;
                var old = GUI.color;
                if (!has) GUI.color = next ? new Color(0.65f, 0.65f, 0.65f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f);
                if (skill.Icon != null) Art.DrawSprite(r, skill.Icon); else Gui.Fill(r, new Color(0.15f, 0.12f, 0.09f));
                GUI.color = old;
                if (rank == 1) Gui.Text(new Rect(r.x + 36, r.y + 38, 28, 26), "+", 24, has ? Gui.Gold : Gui.Dim, TextAnchor.MiddleRight, heading: true);
                if (next)
                {
                    int cost = rank == 0 ? hamlet.SkillLearnCost(hero, skill.Id) : hamlet.SkillMasterCost(hero, skill.Id);
                    Gui.Text(new Rect(r.x - 6, r.yMax + 2, r.width + 12, 18), Gui.Num(cost, "#,0"), 14, why == null ? Gui.Gold : Gui.Dim, TextAnchor.MiddleCenter);
                    if (r.Contains(Event.current.mousePosition))
                    {
                        if (highlight != null) GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, 50, 50), highlight);
                        tip = $"{(rank == 0 ? "Learn" : "Master")} {Dd2.HeroSkills.Name(skill.Id)}: {Gui.Num(cost, "#,0")} gold" + (why != null ? " - " + why : " - click to buy.");
                    }
                    if (why == null && Gui.Hotspot(r))
                    {
                        if (rank == 0) hamlet.LearnSkill(hero.Id, skill.Id); else hamlet.MasterSkill(hero.Id, skill.Id, known);
                        Runtime.Dd1Audio.Play("/town/guild_purchase_skill");
                        S.Persist();
                    }
                }
            }
            Gui.Text(new Rect(x + 156, y + 2, 280, 30), Dd2.HeroSkills.Name(skill.Id) + (mastered ? " +" : ""), 21, known ? Gui.Dd1Name : Gui.Dd1Class, TextAnchor.MiddleLeft, heading: true);
            if (known)
            {
                bool full = !equipped && hero.EquippedSkills.Count >= Dd2.HeroSkills.EquipLimit;
                var toggle = new Rect(x + 156, y + 36, 120, 28);
                Gui.Fill(new Rect(toggle.x, toggle.y + 6, 16, 16), new Color(0.12f, 0.1f, 0.07f));
                if (equipped) Gui.Fill(new Rect(toggle.x + 3, toggle.y + 9, 10, 10), Gui.Gold);
                Gui.Text(new Rect(toggle.x + 22, toggle.y, 140, 28), equipped ? "Brought" : full ? "Left (5 chosen)" : "Left behind", 16, equipped ? Gui.Dd1Text : Gui.Dim, TextAnchor.MiddleLeft);
                if (!full && Gui.Hotspot(toggle))
                {
                    if (equipped) hero.EquippedSkills.Remove(skill.Id); else hero.EquippedSkills.Add(skill.Id);
                    S.Persist();
                }
            }
            else Gui.Text(new Rect(x + 156, y + 36, 280, 28), "Not yet learned", 16, Gui.Dim, TextAnchor.MiddleLeft);
        }
        if (tip != null)
        {
            var m = Event.current.mousePosition;
            var tr = new Rect(m.x + 18, m.y + 10, 360, 64);
            Gui.Fill(tr, new Color(0.03f, 0.025f, 0.02f, 0.95f));
            Gui.Text(new Rect(tr.x + 10, tr.y + 6, tr.width - 20, tr.height - 10), tip, 17, Gui.Dd1Text);
        }
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
        if (icon != null) Art.DrawSprite(new Rect(646, 140, 72, 72), icon);
        Gui.Text(new Rect(730, 140, 500, 40), hero.Name, 30, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(730, 178, 600, 30), $"{Pretty(hero.ClassId)}  ·  knows {hero.CampingSkills.Count} camping skills  ·  drop another hero here to switch", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);

        // DD1's camping trainer (camping_trainer.layout): the class's own skills in a grid of four per row, 110 x 90
        // apart, the shared ones in a second grid below; known ones lit, the others dark with their cost under
        // them (32,90); click one to learn it.
        var all = S.Content.Camping.ForClass(hero.ClassId);
        string dd1Class = S.Campaign.HeroUpgrades.Dd1Class(hero.ClassId);
        var own = all.Where(id => S.Content.Camping.Get(id)?.Classes.Contains(dd1Class) == true && S.Content.Camping.Get(id).Classes.Count == 1).ToList();
        var shared = all.Except(own).ToList();
        string tip = null;
        void Grid(List<string> ids, float x0, float y0, string title)
        {
            Gui.Text(new Rect(x0, y0 - 34, 400, 30), title, 22, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            for (int i = 0; i < ids.Count; i++)
            {
                var skill = S.Content.Camping.Get(ids[i]);
                if (skill == null) continue;
                bool known = hero.CampingSkills.Contains(skill.Id);
                var r = new Rect(x0 + (i % 4) * 110, y0 + (i / 4) * 110, 72, 72);
                var tex = Art.Dd1("raid", "camping", "skill_icons", $"camp_skill_{skill.Id}.png");
                var old = GUI.color;
                if (!known) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                if (tex != null) GUI.DrawTexture(r, tex); else Gui.Fill(r, new Color(0.15f, 0.12f, 0.09f));
                GUI.color = old;
                string why = known ? null : hamlet.WhyCantLearnCampSkill(hero, skill.Id);
                if (known) Gui.Fill(new Rect(r.x + 4, r.yMax + 3, r.width - 8, 3), Gui.Gold);
                else Gui.Text(new Rect(r.x - 10, r.yMax + 2, r.width + 20, 20), Gui.Num(hamlet.CampSkillCost(skill), "#,0"), 15, why == null ? Gui.Gold : Gui.Dim, TextAnchor.MiddleCenter);
                if (r.Contains(Event.current.mousePosition))
                    tip = $"{Dd1Text.CampSkillName(skill.Id)} ({skill.Cost} respite)" + (known ? " - known" : why != null ? " - " + why : " - click to learn") +
                          " | " + string.Join(", ", S.Content.Camping.DescribeAll(skill));
                if (!known && why == null && Gui.Hotspot(r))
                {
                    hamlet.LearnCampSkill(hero.Id, skill.Id);
                    Runtime.Dd1Audio.Play("/town/trainer_purchase_skill");
                    S.Persist();
                }
            }
        }
        Grid(own, 656, 270, Pretty(dd1Class) + " skills");
        Grid(shared, 656, 270 + 110 * System.Math.Max(1, (own.Count + 3) / 4) + 60, "Skills any hero can learn");
        if (tip != null)
        {
            var m = Event.current.mousePosition;
            var tr = new Rect(Mathf.Min(m.x + 18, 1500), m.y + 10, 400, 70);
            Gui.Fill(tr, new Color(0.03f, 0.025f, 0.02f, 0.95f));
            Gui.Text(new Rect(tr.x + 10, tr.y + 6, tr.width - 20, tr.height - 10), tip, 16, Gui.Dd1Text);
        }
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

    /// <summary>
    /// DD1's stagecoach (stage_coach.layout): the recruits from body + (450,80), 100 apart, each on its dark band
    /// (hero_background at -100,-8) with the resolve level to the left of the portrait, the name at (100,12) and the
    /// class at (100,42). Drag a recruit onto the roster to hire them; click one to read their sheet.
    /// </summary>
    private void DrawStageCoach(Rect area, Hamlet hamlet)
    {
        var band = Art.Dd1("campaign", "town", "buildings", "stage_coach", "stage_coach.hero_background.png");
        var origin = new Vector2(596 + 450, 102 + 80);
        Gui.Text(new Rect(origin.x - 100, origin.y - 70, 600, 40),
            hamlet.CanRecruit ? "Drag a recruit onto the roster to hire them." : $"The roster is full ({E.Roster.Count}/{S.Buildings.RosterSize(E)}).",
            20, Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (E.Recruits.Count == 0)
            Gui.Text(new Rect(origin.x - 100, origin.y + 200, 600, 40), "No one has come this week.", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);
        for (int i = 0; i < E.Recruits.Count && i < 7; i++)
        {
            var h = E.Recruits[i];
            float x = origin.x, y = origin.y + i * 100;
            var row = new Rect(x - 100, y - 8, 600, 101);
            if (band != null) GUI.DrawTexture(row, band); else Gui.Fill(row, new Color(0.06f, 0.05f, 0.05f, 0.95f));
            bool hover = row.Contains(Event.current.mousePosition);
            if (hover) Gui.Fill(new Rect(row.x + 6, row.y + 8, row.width - 12, row.height - 16), new Color(1f, 0.9f, 0.6f, 0.06f));
            Gui.Text(new Rect(x - 70, y + 12, 60, 60), h.ResolveLevel.ToString(), 34, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
            var portrait = new Rect(x, y, 86, 86);
            string cls = h.ClassId;
            if (hamlet.CanRecruit)
                Drag.Source(row, new RecruitDrag(h.Id), r => { if (Art.HeroIcon(cls) is { } ic) Art.DrawSprite(new Rect(r.x + 100, r.y + 8, 86, 86), ic); });
            if (!(Drag.Payload is RecruitDrag carried && carried.HeroId == h.Id) && Art.HeroIcon(h.ClassId) is { } icon) Art.DrawSprite(portrait, icon);
            Gui.Text(new Rect(x + 100, y + 4, 380, 36), h.Name, 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(x + 100, y + 38, 380, 28), Pretty(h.ClassId), 20, Gui.Dd1Class, TextAnchor.MiddleLeft);
            Gui.Text(new Rect(x + 100, y + 62, 390, 24), string.Join(", ", h.Quirks.Select(QuirkName)), 15, Gui.Dim, TextAnchor.MiddleLeft);
            if (Gui.Hotspot(row) && !Drag.JustDropped) { _heroId = h.Id; _recruitSheet = true; }
        }
        // Hiring: a recruit dropped on the roster.
        if (Drag.Hovering<RecruitDrag>(RosterColumn.Area)) Gui.Fill(new Rect(RosterColumn.Area.x, RosterColumn.Area.yMax, RosterColumn.Area.width, 4), Gui.Gold);
        if (Drag.Drop<RecruitDrag>(RosterColumn.Area, out var hired) && hamlet.CanRecruit)
        {
            hamlet.Recruit(hired.HeroId);
            Runtime.Dd1Audio.Play("/ui/town/character_add");
            S.Persist();
        }
        if (_recruitSheet && E.Recruits.FirstOrDefault(r => r.Id == _heroId) is { } recruit)
        {
            Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0, 0, 0, 0.55f));
            HeroSheet.Draw(recruit, id => _heroId = id, () => _recruitSheet = false, readOnly: true, cycle: E.Recruits.Select(r => r.Id).ToList());
        }
        else _recruitSheet = false;
    }

    private bool _recruitSheet;

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

    /// <summary>
    /// DD1's building upgrades (campaign/town/buildings/building.layout + upgrade/upgrade.layout): blgupgradebg at
    /// upgrade_base_pos (172,259) + frame_offset (-18,-115); each tree 160 apart from (0,195) with its DD1 name above,
    /// the activity's icon, then one requirement per level 70 apart (purchased, purchasable or locked) joined by
    /// connectors, its cost under it, and a divider below. Clicking a purchasable level buys it.
    /// </summary>
    private void DrawUpgrades(Hamlet hamlet, string building)
    {
        var basePos = new Vector2(172, 259);
        var frame = Art.Dd1("campaign", "town", "buildings", "blgupgradebg.png");
        var panel = new Rect(basePos.x - 18, basePos.y - 115, 662, 764);
        if (frame != null) GUI.DrawTexture(panel, frame); else Gui.Fill(panel, new Color(0.04f, 0.035f, 0.03f, 0.97f));
        var lore = S.Lore;
        var trees = S.Buildings.Trees.Trees.Keys.Where(k => k.StartsWith(building + ".")).OrderBy(k => k).ToList();
        int owned = trees.Sum(t => S.Buildings.Trees.Trees[t].Count(l => E.Upgrades.Contains(l.Key)));
        int total = trees.Sum(t => S.Buildings.Trees.Trees[t].Count);
        // The plaque at the panel's top right holds the title (upgrade_title_offset 458,36) and how far the building
        // is upgraded (upgrade_percent_offset 480,62); the description sits at verbose_offset (20,30), 380 wide.
        Gui.Text(new Rect(basePos.x + 458 - 100, basePos.y + 36 - 22, 200, 40), "Upgrades", 28, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(basePos.x + 480 - 100, basePos.y + 62 - 4, 200, 32), total == 0 ? "" : $"{owned * 100 / total}%", 24, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(basePos.x + 20, basePos.y + 30, 380, 110), "Spend heirlooms to improve the building. Hover a level for what it costs; click the next one when you can afford it.", 18, Gui.Dd1Class);

        var bought = UpgradeArt("requirement_purchased_icon.png");
        var buyable = UpgradeArt("requirement_purchasable_icon.png");
        var locked = UpgradeArt("requirement_locked_icon.png");
        var boughtBg = UpgradeArt("requirement_purchased_background.png");
        var connector = UpgradeArt("requirement_purchased_background_connector.png");
        var highlight = UpgradeArt("requirement_highlight_overlay.png");
        var divider = UpgradeArt("tree_divider_medium.png");
        float firstY = basePos.y + (trees.Count > 3 ? 45 : 195);
        string tip = null;
        for (int i = 0; i < trees.Count; i++)
        {
            string tree = trees[i];
            var levels = S.Buildings.Trees.Trees[tree];
            var next = S.Buildings.Trees.Next(E, tree);
            float y = firstY + i * 160;
            string name = lore?.Text("upgrade_tree_name_" + tree) ?? Pretty(tree.Substring(tree.IndexOf('.') + 1));
            Gui.Text(new Rect(basePos.x + 20, y - 35, 560, 32), name, 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            var icon = Art.Dd1("campaign", "town", "buildings", building, tree + ".icon.png") ?? Art.BuildingIcon(building);
            if (icon != null) GUI.DrawTexture(new Rect(basePos.x + 30, y, 72, 72), icon, ScaleMode.ScaleToFit);
            for (int l = 0; l < levels.Count; l++)
            {
                var level = levels[l];
                float x = basePos.x + 110 + l * 70;
                bool has = E.Upgrades.Contains(level.Key);
                bool can = !has && next == level && S.Buildings.Trees.CanBuy(E, level);
                if (has && boughtBg != null) GUI.DrawTexture(new Rect(x + 30, y, 72, 72), boughtBg);
                if (has && l > 0 && E.Upgrades.Contains(levels[l - 1].Key) && connector != null) GUI.DrawTexture(new Rect(x, y + 24, 50, 20), connector);
                var state = has ? bought : (next == level ? buyable : locked);
                var r = new Rect(x + 40, y + 10, 50, 50);
                if (state != null) GUI.DrawTexture(r, state); else Gui.Fill(r, has ? Gui.Gold : new Color(0.25f, 0.22f, 0.18f));
                if (!has)
                {
                    var main = level.Cost.FirstOrDefault(c => c.Type != Currency.Gold) ?? level.Cost.FirstOrDefault();
                    if (main != null)
                    {
                        var cur = Art.Dd1("shared", "estate", $"currency.{main.Type}.icon.png");
                        if (cur != null) GUI.DrawTexture(new Rect(x + 40, y + 64, 22, 22), cur);
                        Gui.Text(new Rect(x + 62, y + 62, 40, 26), main.Amount.ToString(), 16, can ? Gui.Dd1Text : Gui.Dim, TextAnchor.MiddleLeft);
                    }
                }
                if (r.Contains(Event.current.mousePosition))
                {
                    if (highlight != null) GUI.DrawTexture(r, highlight);
                    string desc = lore?.Text("upgrade_tree_tooltip_description_" + tree);
                    tip = $"{name}, level {l + 1}" + (desc != null ? $"\n{desc}" : "") +
                          (has ? "\nBuilt." : "\nCost: " + string.Join(", ", level.Cost.Select(c => $"{Gui.Num(c.Amount, "#,0")} {(c.Type == Currency.Gold ? "gold" : Pretty(c.Type) + "s")}")) +
                                               (next == level ? (can ? "\nClick to build." : "\nNot enough heirlooms.") : "\nBuild the levels before it first."));
                }
                if (can && Gui.Hotspot(r))
                {
                    hamlet.BuyUpgrade(level.TreeId, level.Code);
                    Runtime.Dd1Audio.Play("/town/gen_building_upgrade");
                    S.Persist();
                }
            }
            if (divider != null && i < trees.Count - 1) GUI.DrawTexture(new Rect(basePos.x, y + 118, 400, 6), divider);
        }
        if (tip != null)
        {
            var m = Event.current.mousePosition;
            var tr = new Rect(m.x + 18, m.y + 10, 380, 30 + 24 * (tip.Count(c => c == '\n') + 1));
            Gui.Fill(tr, new Color(0.03f, 0.025f, 0.02f, 0.95f));
            Gui.Text(new Rect(tr.x + 10, tr.y + 6, tr.width - 20, tr.height - 10), tip, 17, Gui.Dd1Text);
        }
    }

    private static Texture2D UpgradeArt(string file) => Art.Dd1("campaign", "town", "buildings", "upgrade", file);

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
