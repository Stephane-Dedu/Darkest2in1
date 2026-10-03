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
        else if (windowOpen)
        {
            Gui.Fill(new Rect(0, 0, 1550, 958), new Color(0, 0, 0, 0.55f));
            DrawWindow(hamlet);
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
        if (plate != null) GUI.DrawTexture(new Rect(0, 0, 893 * 0.8f, 281 * 0.8f), plate);
        Gui.Text(new Rect(286 * 0.8f, 70 * 0.8f - 26, 600, 52), E.Name, 40, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(286 * 0.8f, 70 * 0.8f + 24, 600, 32), $"Week {E.Week + 1}", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);

        if (E.TownEventId != null)
        {
            var crierIcon = EventArt("town_event.icon.png");
            var ir = new Rect(722, 26, 70, 70);
            if (crierIcon != null) GUI.DrawTexture(ir, crierIcon);
            if (ir.Contains(Event.current.mousePosition))
                Gui.Text(new Rect(ir.x - 60, ir.yMax, 200, 26), "The week's event", 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
            if (Gui.Hotspot(ir)) _eventSeenWeek = -1;
        }
        var log = Art.Dd1("campaign", "town", "estate_title", "estate_activity_log_button.png");
        var r = new Rect(640, 26, 70, 70);
        if (log != null) GUI.DrawTexture(r, log);
        if (Gui.Hotspot(r)) _panel = _panel == Panel.Log ? Panel.Town : Panel.Log;
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
        var clicked = RosterColumn.Draw(E, S.Buildings.RosterSize(E), h => new RosterColumn.Look(highlight: (_panel == Panel.Hero || service) && _heroId == h.Id));
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

    private void DrawEstateBar()
    {
        Gui.Fill(new Rect(0, BarY, 1550, Gui.H - BarY), new Color(0, 0, 0, 0.85f));
        Gui.Fill(new Rect(0, BarY, 1550, 2), new Color(0.35f, 0.29f, 0.18f));

        var gold = Art.Dd1("shared", "estate", "currency.gold.large_icon.png");
        if (gold != null) GUI.DrawTexture(new Rect(120, BarY + 14, 88, 88), gold);
        Gui.Text(new Rect(210, BarY + 30, 200, 56), Gui.Num(E.Get(Currency.Gold), "#,0"), 34, Gui.Gold, TextAnchor.MiddleLeft, heading: true);

        var heirlooms = new[] { (Currency.Bust, "bust"), (Currency.Portrait, "portrait"), (Currency.Deed, "deed"), (Currency.Crest, "crest") };
        for (int i = 0; i < heirlooms.Length; i++)
        {
            var (cur, icon) = heirlooms[i];
            float x = 420 + i * 120;
            var tex = Art.Dd1("shared", "estate", $"currency.{icon}.icon.png");
            if (tex != null) GUI.DrawTexture(new Rect(x, BarY + 38, 40, 40), tex);
            Gui.Text(new Rect(x + 46, BarY + 34, 70, 48), E.Get(cur).ToString(), 28, Gui.Dd1Text, TextAnchor.MiddleLeft, heading: true);
        }
        Gui.Text(new Rect(900, BarY + 34, 200, 48), $"Trinkets {E.Trinkets.Count}", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);

        var embark = Art.Dd1("campaign", "town", "embark_party", "embark_party.background.png");
        var er = new Rect(1100, BarY + 4, 412, 113);
        if (embark != null) GUI.DrawTexture(er, embark);
        bool hover = er.Contains(Event.current.mousePosition);
        Gui.Text(er, "Embark", 44, hover ? Color.white : Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        if (Gui.Hotspot(er)) WantsEmbark = true;

        if (Gui.DdButton(new Rect(1580, 1000, 310, 60), "Leave the Hamlet", size: 24))
            Driver.Instance.LeaveHamlet();
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

    private string _assignHero;

    private void DrawActivities(Rect area, Hamlet hamlet, string building)
    {
        Frame(area);
        var idle = E.Roster.Where(h => h.IsAvailable).ToList();
        Gui.Text(new Rect(area.x + 20, area.y + 6, area.width - 40, 30), "Pick a hero, then an activity. Heroes stay for the week.", 18, Gui.Dd1Class);
        for (int i = 0; i < idle.Count; i++)
        {
            var r = new Rect(area.x + 14 + (i % 8) * 88, area.y + 40 + (i / 8) * 88, 82, 82);
            bool chosen = _assignHero == idle[i].Id;
            Gui.Fill(r, chosen ? new Color(0.85f, 0.7f, 0.3f, 0.6f) : new Color(0, 0, 0, 0.6f));
            var sprite = Art.HeroIcon(idle[i].ClassId);
            if (sprite != null) Art.DrawSprite(new Rect(r.x + 3, r.y + 3, 76, 76), sprite);
            Gui.Text(new Rect(r.x, r.yMax - 22, r.width, 22), $"{idle[i].Stress}", 18, idle[i].Stress >= 5 ? Gui.Dd1Health : Gui.Dd1Text, TextAnchor.MiddleRight);
            if (r.Contains(Event.current.mousePosition))
                Gui.Text(new Rect(r.x - 40, r.y - 26, r.width + 80, 26), idle[i].Name, 18, Gui.Dd1Name, TextAnchor.MiddleCenter);
            if (Gui.Hotspot(r)) _assignHero = idle[i].Id;
        }

        float y = area.y + 40 + Mathf.Max(1, (idle.Count + 7) / 8) * 88 + 10;
        foreach (var a in S.Buildings.Activities.Where(a => a.Building == building))
        {
            var cost = hamlet.ActivityCost(a);
            var (lo, hi) = a.StressHeal(E);
            var r = new Rect(area.x + 10, y, area.width - 20, 112);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var icon = Art.Dd1("campaign", "town", "buildings", building, $"{building}.{a.Id}.icon.png");
            if (icon != null) GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, 100, 100), icon, ScaleMode.ScaleToFit);
            var inside = E.Roster.Where(h => h.Activity == a.Key).Select(h => h.Name);
            Gui.Text(new Rect(r.x + 118, r.y + 4, 400, 34), Pretty(a.Id), 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(r.x + 118, r.y + 36, 420, 74),
                $"{cost?.Amount} {cost?.Type}  ·  {hamlet.UsedSlots(a.Key)}/{a.Slots(E)} places  ·  relieves {Gui.Num(lo / 10f)}-{Gui.Num(hi / 10f)} stress\n{string.Join(", ", inside)}", 17, Gui.Dd1Class);
            var hero = E.Hero(_assignHero);
            string why = hero == null ? "Pick a hero" : hamlet.WhyCantDo(hero, a);
            if (Gui.DdButton(new Rect(r.xMax - 176, r.y + 30, 164, 52), why ?? "Send " + hero.Name, why == null, why == null ? 22 : 16))
            {
                hamlet.StartActivity(hero.Id, a.Key);
                _assignHero = null;
                S.Persist();
            }
            y += 120;
        }
    }

    private void DrawSanitarium(Rect area, Hamlet hamlet)
    {
        Frame(area);
        Gui.Text(new Rect(area.x + 20, area.y + 6, area.width - 40, 50), "Treat a negative quirk or disease, or lock in a positive quirk. Treatment takes the week.", 18, Gui.Dd1Class);
        _panelScroll = GUI.BeginScrollView(new Rect(area.x + 10, area.y + 60, area.width - 20, area.height - 70), _panelScroll,
            new Rect(0, 0, area.width - 50, E.Roster.Sum(h => 50 + h.Quirks.Count * 40)));
        float y = 0;
        foreach (var h in E.Roster)
        {
            Gui.Text(new Rect(0, y, 600, 40), h.Name + (h.IsAvailable ? "" : "  (busy)"), 24, h.IsAvailable ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleLeft, heading: true);
            y += 44;
            foreach (var q in h.Quirks)
            {
                var cost = hamlet.TreatmentCost(h, q);
                bool locked = h.LockedQuirks.Contains(q);
                bool positive = S.Catalog.IsPositive(q);
                Gui.Text(new Rect(20, y, 400, 36), $"{QuirkName(q)}{(locked ? " (locked)" : "")}", 19, positive ? Gui.Gold : Gui.Dd1Health, TextAnchor.MiddleLeft);
                if (h.IsAvailable && cost != null && Gui.DdButton(new Rect(430, y, 290, 36), $"{(positive ? "Lock" : "Treat")} ({cost.Amount} {cost.Type})", !(locked && positive), 18))
                {
                    hamlet.StartTreatment(h.Id, q);
                    S.Persist();
                }
                y += 40;
            }
            y += 6;
        }
        GUI.EndScrollView();
    }

    private void DrawWagon(Rect area, Hamlet hamlet)
    {
        Frame(area);
        for (int i = 0; i < E.WagonStock.Count; i++)
        {
            string t = E.WagonStock[i];
            int price = hamlet.WagonPrice(t);
            var r = new Rect(area.x + 10, area.y + 14 + i * 64, area.width - 20, 58);
            Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            Gui.Text(new Rect(r.x + 14, r.y, 440, r.height), Pretty(t), 22, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            if (Gui.DdButton(new Rect(r.xMax - 230, r.y + 8, 220, 42), $"Buy  {price} gold", E.Get(Currency.Gold) >= price, 20))
            {
                hamlet.BuyTrinket(t);
                S.Persist();
                break;
            }
        }
        Gui.Text(new Rect(area.x + 20, area.yMax - 110, area.width - 40, 100),
            $"Your trinkets: {string.Join(", ", E.Trinkets.Select(Pretty))}\nEquip them from a hero's page (click a hero in the roster).", 17, Gui.Dd1Class);
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
            foreach (var t in E.Trinkets.Distinct().Take(8).ToList())
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
        string.IsNullOrEmpty(id) ? "" : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').Replace('.', ' '));
}
