using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// The dungeon, laid out like DD1's raid screen (positions from DD1's scripts/layout/screen.raid.darkest):
/// scene and party on top, torch above, banner + hero panel bottom-left, map/inventory bottom-right.
/// </summary>
internal sealed class CrawlUi
{
    private static readonly float[] HeroX = { 788, 620, 452, 284 };   // rank 1..4 (DD1 overlays.hero_start_pos/spacing)
    private static readonly float[] CampX = { 1250, 1450, 670, 470 }; // at camp: two each side of the fire
    private const float Feet = 680;
    private const float MapUnit = 40;                                  // map pixels per fine grid unit

    private bool _inventoryTab;
    private bool _confirmRetreat;
    private string _sheetHeroId;   // the hero whose DD1 sheet is open (right-click)

    /// <summary>A right-click on this rect (used up, so nothing else sees it).</summary>
    internal static bool RightClicked(Rect r)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 1 || !r.Contains(e.mousePosition)) return false;
        e.Use();
        return true;
    }
    private string _campSkillPending, _campSkillUser;   // a camp skill waiting for its target

    private static Session S => Session.Current;
    private static Driver D => Driver.Instance;

    public void Draw()
    {
        var crawl = D.Crawl;
        var exp = D.Expedition;
        if (crawl == null || exp == null) return;
        string zone = Core.Dungeon.ZoneBase.Of(exp.Quest.Dungeon);   // DD2 regions use their DD1 zone's art
        if (D.SelectedHeroId == null || !D.Party.Alive.Contains(D.SelectedHeroId)) D.SelectedHeroId = D.Party.Alive.FirstOrDefault();
        // While a hero sheet is open, the dungeon under it is only painted: clicks belong to the sheet.
        if (_sheetHeroId != null && Event.current.type != EventType.Repaint && S.Save.Estate.Hero(_sheetHeroId) is { } open)
        {
            UiRoot.ModalOpen = true;
            HeroSheet.Draw(open, id => _sheetHeroId = id, () => _sheetHeroId = null, readOnly: true, cycle: exp.Party);
            return;
        }

        DrawScene(crawl, exp, zone);
        if (exp.Camp != null) DrawCampfire();
        DrawHeroes(exp);
        float fade = D.FadeAlpha;
        if (fade > 0.001f) Gui.Fill(new Rect(0, 0, Gui.W, 720), new Color(0, 0, 0, fade));
        if (exp.Camp != null) DrawRespite(exp.Camp); else DrawTorch(exp.Light);
        DrawQuestInfo(crawl, exp);
        try { DrawHud(exp); }
        catch (NullReferenceException) { }   // DD2 tearing its actors down (leaving the dungeon)
        if (_inventoryTab || LootWaiting(crawl)) DrawInventory(crawl, exp); else DrawMap(exp);
        UiRoot.ModalOpen = false;
        if (_sheetHeroId != null && S.Save.Estate.Hero(_sheetHeroId) is { } sheetHero)
        {
            UiRoot.ModalOpen = true;
            Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0, 0, 0, 0.55f));
            HeroSheet.Draw(sheetHero, id => _sheetHeroId = id, () => _sheetHeroId = null, readOnly: true, cycle: exp.Party);
            Gui.DrawAnnouncement();
            return;
        }
        if (exp.Camp != null) DrawCamp(crawl, exp);
        else if (DrawSpoils(crawl) || DrawCurioResult(exp)) UiRoot.ModalOpen = true;   // a scroll to read first
        else DrawPrompt(crawl, exp);
        Gui.DrawAnnouncement();
    }

    /// <summary>The dungeon scene and the party only (painted while DD2 sets up a fight), at this opacity.</summary>
    public void DrawBackdrop(float alpha)
    {
        var crawl = D.Crawl;
        var exp = D.Expedition;
        if (crawl == null || exp == null) return;
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
        DrawScene(crawl, exp, Core.Dungeon.ZoneBase.Of(exp.Quest.Dungeon));
        DrawHeroes(exp);
        DrawHud(exp);
        GUI.color = old;
    }

    // ---------------- scene ----------------

    private static void DrawScene(Crawl crawl, ExpeditionState exp, string zone)
    {
        Gui.Fill(new Rect(0, 0, Gui.W, 720), Color.black);
        float t = (Time.unscaledTime - D.LastStepTime) / 0.3f;
        float slide = t < 1f ? D.LastStepDir * 720f * (1f - Mathf.SmoothStep(0, 1, t)) : 0f;
        slide -= D.WalkProgress * 720f;   // held-key walking scrolls the hallway continuously

        if (exp.InRoom)
        {
            var tex = exp.RoomId == exp.Map.EntranceRoomId ? Art.EntranceWall(zone) : Art.RoomWall(zone, exp.RoomId);
            if (tex != null) GUI.DrawTexture(new Rect(slide, 0, 1920, 720), tex);
        }
        else
        {
            // DD1's hallway is one strip: [end wall][door][squares...][door][end wall], with its far and middle
            // layers scrolling slower behind the walls' gaps. Screen-right is the way the party is heading.
            var c = crawl.CurrentCorridor;
            int n = c.Tiles.Count;
            bool towardB = exp.HeadingRoomId == c.RoomB;
            int here = towardB ? exp.TileIndex : n - 1 - exp.TileIndex;   // the party's square, counted along the heading
            float walked = (here * 720f - slide) + (towardB ? 0 : 7 * 720f);
            Parallax(Art.CorridorBackground(zone), walked * 0.25f);
            Parallax(Art.CorridorMid(zone), walked * 0.55f);
            var top = Art.ForegroundTop(zone);
            var bottom = Art.ForegroundBottom(zone);
            for (int k = -3; k <= 3; k++)
            {
                int h = here + k;
                float x = 600 + k * 720 + slide;
                if (x > 1920 || x + 720 < 0) continue;
                Texture2D tex = null;
                bool mirror = false;
                if (h >= 0 && h < n) tex = Art.CorridorWall(zone, c.Id * 3 + (towardB ? h : n - 1 - h));
                else if (h == -1 || h == n) { tex = Art.CorridorDoor(zone); mirror = h == -1; }   // DD1's door art has its doorway on the right
                else if (h == -2 || h == n + 1) { tex = Art.EndHall(zone); mirror = h == -2; }
                if (tex != null) GUI.DrawTextureWithTexCoords(new Rect(x, 0, 720, 720), tex, mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
                if (h < -2 || h > n + 1) continue;
                if (top != null) GUI.DrawTexture(new Rect(x, 0, 720, top.height), top);
                if (bottom != null) GUI.DrawTexture(new Rect(x, 720 - bottom.height, 720, bottom.height), bottom);
            }
        }
        DrawProps(crawl, exp, slide);

        // DD1's darkness: the dimmer the torch, the heavier the shadow, strongest at the edges.
        float dark = Mathf.Clamp01((75f - exp.Light) / 110f);
        if (dark > 0)
        {
            Gui.Fill(new Rect(0, 0, Gui.W, 720), new Color(0, 0, 0, dark * 0.55f));
            Gui.Fill(new Rect(0, 0, 260, 720), new Color(0, 0, 0, dark * 0.35f));
            Gui.Fill(new Rect(1660, 0, 260, 720), new Color(0, 0, 0, dark * 0.35f));
        }
    }

    /// <summary>A 720-wide layer repeated across the screen, scrolled by <paramref name="offset"/> pixels.</summary>
    private static void Parallax(Texture2D tex, float offset)
    {
        if (tex == null) return;
        float x0 = -(((offset % 720f) + 720f) % 720f);
        for (float x = x0; x < 1920; x += 720) GUI.DrawTexture(new Rect(x, 0, 720, 720), tex);
    }

    /// <summary>
    /// What stands in front of the party: DD1's own prop art (curios closed, highlighted under the mouse, open
    /// once used; obstacles; spotted traps), drawn from its Spine skeletons. Falls back to the map icon.
    /// </summary>
    /// <summary>Debug (F6): show this curio in front of the party instead of what's really there.</summary>
    public static string PreviewCurio;

    /// <summary>The props along the hallway (the party's square and the seen squares next to it), scrolling with
    /// it; in a room, the room's curio.</summary>
    private static void DrawProps(Crawl crawl, ExpeditionState exp, float slide)
    {
        if (PreviewCurio != null) { DrawProp("curios", PreviewCurio, false, PropX, hoverable: true); return; }
        if (exp.InRoom)
        {
            var room = crawl.CurrentRoom;
            if (room?.CurioId != null) DrawProp("curios", room.CurioId, room.CurioTaken, PropX + slide, hoverable: true);
            return;
        }
        var c = crawl.CurrentCorridor;
        int dir = exp.HeadingRoomId == c.RoomB ? 1 : -1;
        for (int k = -1; k <= 1; k++)
        {
            int i = exp.TileIndex + k * dir;
            if (i < 0 || i >= c.Tiles.Count) continue;
            var tile = c.Tiles[i];
            if (k != 0 && !tile.Visited && !tile.Scouted) continue;
            string kind = tile.Content switch
            {
                HallContent.Curio => "curios",
                HallContent.Obstacle when !tile.Resolved => "obstacles",
                HallContent.Trap when tile.Scouted && !tile.Resolved => "traps",
                _ => null,
            };
            if (kind != null) DrawProp(kind, tile.ContentId, kind == "curios" && tile.Resolved, PropX + k * 720 + slide, hoverable: k == 0);
        }
    }

    private static void DrawProp(string kind, string id, bool used, float x, bool hoverable)
    {
        if (x < -400 || x > 2320) return;
        var feet = new Vector2(x, Feet + 10);
        string folder = S.Dd1.PathOf("props", "shared", kind, id ?? "");
        SpineArt.Picture pic;
        if (kind == "curios")
        {
            var closed = SpineArt.Get(folder, "closed", sl => sl.Name != "active" && sl.Name != "open");
            bool hover = hoverable && !used && closed != null && closed.Hit(feet, 1f, Event.current.mousePosition);
            if (hoverable && !used && closed != null)
            {
                var area = closed.RectAt(feet);
                if (hover && Gui.Hotspot(area)) CurioClicked = true;
                if (Drag.Drop<PackStack>(area, out var dropped)) CurioDrop = dropped.Key;
            }
            pic = used ? SpineArt.Get(folder, "open", sl => sl.Name != "active" && sl.Name != "closed")
                : hover ? SpineArt.Get(folder, "active", sl => sl.Name != "closed" && sl.Name != "open") ?? closed
                : closed;
        }
        else
        {
            pic = SpineArt.Get(folder, "idle", sl => sl.Name != "active" && !sl.Name.StartsWith("dust") && sl.Name != "splash" && sl.Name != "foam");
            // A spotted trap in the party's square: click it to disarm (DD1).
            if (kind == "traps" && hoverable && pic != null)
            {
                var area = pic.RectAt(feet);
                bool hover = pic.Hit(feet, 1f, Event.current.mousePosition);
                if (hover) Gui.Text(new Rect(area.center.x - 120, area.y - 30, 240, 26), "Click to disarm", 18, Color.white, TextAnchor.MiddleCenter);
                if (hover && Gui.Hotspot(area)) TrapClicked = true;
            }
        }

        var shadow = Art.Overlay("charactershadow_med.png");
        if (pic != null)
        {
            var r = pic.RectAt(feet);
            if (shadow != null) GUI.DrawTexture(new Rect(feet.x - r.width * 0.45f, feet.y - 30, r.width * 0.9f, 50), shadow);
            pic.Draw(feet);
            return;
        }
        if (used) return;
        string icon = kind switch { "curios" => "marker_curio", "obstacles" => "marker_obstacle", _ => "marker_trap" };
        var tex = Art.MapIcon(icon);
        if (tex == null) return;
        var ir = new Rect(x - 75, Feet - 150, 150, 150);
        Gui.Fill(new Rect(ir.x - 10, ir.yMax + 2, ir.width + 20, 14), new Color(0, 0, 0, 0.4f));
        GUI.DrawTexture(ir, tex, ScaleMode.ScaleToFit);
    }

    private const float PropX = 1040;

    // The party's curio was clicked / had an inventory item dropped on it this frame (DD1's ways to interact).
    private static bool CurioClicked, TrapClicked;
    private static string CurioDrop;
    private string _curioPanel;   // the curio whose panel is open ("where:id")

    // ---------------- party ----------------

    private void DrawHeroes(ExpeditionState exp)
    {
        var shadow = Art.Overlay("charactershadow_med.png");
        var selected = Art.Overlay("selected_1.png");
        // DD2's animated hero models, rendered off-screen at DD1's rank positions (see HeroStage).
        // DD2's hero models once the stage has found a way to light them; DD2's hero art until then.
        var models = Plugin.HeroModels.Value && Dd2.HeroStage.Lit ? Dd2.HeroStage.Instance?.Texture : null;
        if (models != null) GUI.DrawTexture(new Rect(0, 0, 1920, 720), Dd2.HeroStage.Instance.Brightened ?? models);
        for (int rank = 0; rank < exp.Party.Count && rank < 4; rank++)
        {
            string id = exp.Party[rank];
            var hero = S.Save.Estate.Hero(id);
            uint guid = D.Party.Guid(id);
            var actor = Dd2Api.Actor(guid);
            bool dead = actor == null || Dd2Api.IsDead(guid);
            bool camping = exp.Camp != null;
            float x = camping ? CampX[rank] : HeroX[rank];

            if (shadow != null) GUI.DrawTexture(new Rect(x - 85, Feet - 30, 170, 50), shadow);
            if (!dead && id == D.SelectedHeroId && selected != null) GUI.DrawTexture(new Rect(x - 87, Feet - 196, 175, 206), selected);

            var old = GUI.color;
            if (dead) GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            var sprite = models != null && !dead ? null : Art.HeroFigure(hero?.ClassId);
            bool large = sprite != null && sprite != Art.Portrait(hero?.ClassId);
            // At camp the two front ranks sit on the far side of the fire, facing back toward it.
            bool facingLeft = camping && rank < 2;
            // DD1's walk: each hero bobs a little, out of step with the others.
            float bob = D.IsMovingNow && !camping ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f + rank * 1.3f)) * -9f : 0f;
            if (sprite != null) Art.DrawSprite(large ? new Rect(x - 120, Feet - 420 + bob, 240, 430) : new Rect(x - 80, Feet - 230 + bob, 160, 220), sprite, flipX: facingLeft);
            else if (models == null) Gui.Text(new Rect(x - 80, Feet - 150, 160, 60), hero?.Name, 26, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            GUI.color = old;

            if (!dead)
            {
                // DD1: a red health bar and ten stress pips at the hero's feet.
                Gui.Bar(new Rect(x - 55, Feet + 10, 110, 9), Dd2Api.HpFraction(actor), Gui.Dd1Health);
                DrawStressPips(x - 50, Feet + 24, actor.Stress, actor.StressMax);
            }
            else Gui.Text(new Rect(x - 80, Feet + 6, 160, 30), "Dead", 22, Gui.Blood, TextAnchor.UpperCenter, heading: true);

            var hit = new Rect(x - 80, Feet - 240, 160, 290);
            if (RightClicked(hit)) _sheetHeroId = id;   // DD1: right-click a hero for their sheet
            if (!dead && Drag.Hovering<PackStack>(hit)) Gui.Fill(new Rect(x - 60, Feet + 40, 120, 4), Gui.Gold);
            if (!dead && Drag.Drop<PackStack>(hit, out var supply)) UseItemOn(id, supply.Key, D.Crawl);
            if (!dead && _campSkillPending != null && hit.Contains(Event.current.mousePosition))
                Gui.Fill(new Rect(x - 60, Feet + 40, 120, 4), Gui.Gold);   // target under the mouse
            if (!dead && Gui.Hotspot(hit))
            {
                if (_campSkillPending != null)
                {
                    D.UseCampSkill(_campSkillUser, _campSkillPending, id);
                    _campSkillPending = null;
                }
                else D.SelectedHeroId = id;
            }
        }
    }

    private static Material _brighten;

    /// <summary>The hero models' picture, brightened: off in their far-away stage DD2's per-arena lighting doesn't
    /// reach them, so they come out dark (in fights, lit by the arena, they look right).</summary>
    private static void DrawBrightened(Rect r, Texture tex)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (_brighten == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (shader != null) _brighten = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (_brighten == null) { GUI.DrawTexture(r, tex); return; }
        float k = Plugin.HeroModelBrightness.Value;
        _brighten.color = new Color(k, k, k, 1f);
        Graphics.DrawTexture(r, tex, _brighten);
    }

    private static void DrawStressPips(float x, float y, float stress, float max)
    {
        var full = Art.Overlay("stress_pip_full.png");
        var over = Art.Overlay("stress_pip_full_overstressed.png");
        var empty = Art.Overlay("stress_pip_empty.png");
        int pips = Mathf.Max(1, Mathf.RoundToInt(max));
        for (int i = 0; i < pips; i++)
        {
            var tex = i < stress ? (stress >= max ? over : full) : empty;
            if (tex != null) GUI.DrawTexture(new Rect(x + i * 10, y, 9, 11), tex);
        }
    }

    // ---------------- torch ----------------

    private static void DrawTorch(float light)
    {
        var torch = Art.Overlay("torch.png");
        Gui.At(torch, 510, 28);
        float f = Mathf.Clamp01(light / 100f);
        var warm = Color.Lerp(new Color(0.55f, 0.25f, 0.08f), new Color(1f, 0.82f, 0.4f), f);
        // DD1's gauge burns down from both ends toward the flame.
        Gui.Fill(new Rect(936 - 400 * f, 115, 400 * f, 5), warm);
        Gui.Fill(new Rect(984, 115, 400 * f, 5), warm);
        var flame = Art.Overlay("torch_flame.png");
        if (flame != null)
        {
            float s = 26 + 22 * f;
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.45f + 0.55f * f);
            GUI.DrawTexture(new Rect(960 - s / 2, 98 - s / 2, s, s), flame);
            GUI.color = old;
        }
        Gui.Text(new Rect(760, 148, 400, 30), CrawlRules.BandName(light), 24, Gui.Dd1Name, TextAnchor.UpperCenter, heading: true);
        if (Gui.Hotspot(new Rect(910, 60, 100, 90))) D.UseTorch();   // DD1: click the torch to light a new one
    }

    // ---------------- quest info ----------------

    private void DrawQuestInfo(Crawl crawl, ExpeditionState exp)
    {
        Gui.At(Art.Overlay("quest_log.png"), 12, 20);
        Gui.Text(new Rect(100, 22, 420, 36), $"{exp.Quest.DifficultyName} {HamletUi.Pretty(exp.Quest.Type)}", 30, Gui.Dd1Name, heading: true);
        Gui.Text(new Rect(100, 58, 460, 60), GoalText(exp), 21, Gui.Dd1Text);

        if (exp.QuestComplete)
        {
            var home = Art.Panel("quest_return_to_hamlet.png");
            var r = Gui.At(home, 16, 112);
            if (home == null) r = new Rect(16, 112, 220, 50);
            if (home == null ? Gui.DdButton(r, "Return home") : Gui.Hotspot(r)) D.Leave();
            Gui.Text(new Rect(r.xMax + 10, r.y + 18, 300, 30), "Return to the Hamlet", 22, Gui.Gold, heading: true);
        }
        else if (!crawl.IsBlocked && exp.Quest?.CanRetreat != false)   // DD1: some plot quests can't be abandoned
        {
            var retreat = Art.Panel("retreat_button.png");
            var r = Gui.At(retreat, 20, 112);
            if (retreat == null) r = new Rect(20, 112, 64, 64);
            if (Gui.Hotspot(r))
            {
                if (_confirmRetreat) { _confirmRetreat = false; D.Leave(); return; }
                _confirmRetreat = true;
                Gui.Announce(exp.Quest?.RetreatKillCount > 0
                    ? S.Lore?.Text("retreat_raid_party_kill_darkestdungeon_confirm_question") ?? "The fiends are closing in and a random hero must give their life to ensure the others will escape from the Darkest Dungeon. Really abandon quest?"
                    : S.Lore?.Text("retreat_confirm_raid_question") ?? "Are you sure you want to retreat? The heroes will suffer the stress of defeat...", 3.5f);
            }
            Gui.Text(new Rect(r.xMax + 10, r.y + 20, 300, 30), _confirmRetreat ? "Click again to retreat" : S.Lore?.Text("retreat_raid_tooltip") ?? "Abandon Quest", 22, _confirmRetreat ? Gui.Blood : Gui.Dim, heading: true);
        }
    }

    private static string GoalText(ExpeditionState exp)
    {
        var map = exp.Map;
        var goal = exp.Goal;
        switch (exp.Quest.Type)
        {
            case "explore":
                return $"Explore rooms: {map.Rooms.Count(r => r.Visited)} / {Mathf.CeilToInt(map.Rooms.Count * 0.9f)}";
            case "cleanse":
                return $"Clear room battles: {map.Rooms.Count(r => r.HasBattle && r.Cleared)} / {map.Rooms.Count(r => r.HasBattle)}";
            case "kill_boss":
                return $"Slay {HamletUi.Pretty(Core.Expedition.ZoneEncounters.BossKey(exp.Quest.BossId))}";
            default:
                return goal != null ? $"{HamletUi.Pretty(goal.CurioName)}: {exp.GoalProgress} / {goal.Amount}" : "";
        }
    }

    // ---------------- bottom HUD ----------------

    private void DrawHud(ExpeditionState exp)
    {
        Gui.Fill(new Rect(0, 720, Gui.W, 360), Color.black);
        Gui.At(Art.Panel("panel_transition.png"), 0, 710);
        var side = Art.Panel("side_decor.png");
        Gui.At(side, 0, 720);
        Gui.At(side, 1670, 720, flipX: true);
        Gui.At(Art.Panel("panel_banner.png"), 207, 720);
        Gui.At(Art.Panel("panel_hero.png"), 240, 856);
        Gui.At(Art.Panel(_inventoryTab ? "panel_inventory.png" : "panel_map.png"), 960, 720);

        // Tabs on the right edge of the map panel (DD1 panel.map tab_placement 672,252, 48 x 90): the map above,
        // the inventory bag below; each panel's art lights its own tab.
        var mapTab = new Rect(960 + 672, 720 + 162, 48, 90);
        var bagTab = new Rect(960 + 672, 720 + 252, 48, 90);
        if (Gui.Hotspot(mapTab)) _inventoryTab = false;
        if (Gui.Hotspot(bagTab)) _inventoryTab = true;
        if (mapTab.Contains(Event.current.mousePosition)) Gui.Text(new Rect(mapTab.x - 120, mapTab.y - 26, 160, 24), "Map", 17, Gui.Dd1Text, TextAnchor.MiddleRight);
        if (bagTab.Contains(Event.current.mousePosition)) Gui.Text(new Rect(bagTab.x - 160, bagTab.y - 26, 200, 24), "Inventory", 17, Gui.Dd1Text, TextAnchor.MiddleRight);

        var hero = S.Save.Estate.Hero(D.SelectedHeroId);
        var actor = Dd2Api.Actor(D.Party.Guid(D.SelectedHeroId ?? ""));
        if (hero == null || actor == null) return;

        // Banner (DD1 panel.banner.darkest): portrait, name and class, then the five skill slots above "1-5".
        var portrait = Art.HeroIcon(hero.ClassId);
        if (portrait != null) Art.DrawSprite(new Rect(262, 738, 96, 96), portrait);
        if (RightClicked(new Rect(250, 728, 270, 120))) _sheetHeroId = hero.Id;
        float nameSize = 30;
        var font = Dd1Font.Heading;
        while (font != null && nameSize > 18 && font.Measure(hero.Name, nameSize * Gui.HeadingScale).x > 146) nameSize -= 2;
        Gui.Text(new Rect(366, 742, 150, 36), hero.Name, nameSize, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(366, 780, 150, 26), HamletUi.Pretty(hero.ClassId), 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
        Gui.Text(new Rect(366, 804, 150, 24), $"Resolve {hero.ResolveLevel}", 17, Gui.Dd1Class, TextAnchor.MiddleLeft);
        DrawSkillRow(hero, actor);

        // Hero panel (DD1 panel.hero.darkest at 240,856): health at (130,11) in red, stress at (130,40) in grey,
        // then DD1's one column of stats at (60,72) in its "stat" font (ubuntu_small): grey label, white value.
        Gui.Text(new Rect(370, 862, 200, 28), $"{actor.HpRounded:0}/{actor.CurrentHpMax:0}", 22, new Color(0.75f, 0f, 0f), TextAnchor.MiddleLeft);
        Gui.Text(new Rect(370, 891, 200, 28), $"{actor.Stress:0}/{actor.StressMax:0}", 22, new Color(0.59f, 0.59f, 0.59f), TextAnchor.MiddleLeft);
        float dmg = actor.GetClampedStatValue(ActorStatType.HEALTH_DAMAGE), range = actor.GetClampedStatValue(ActorStatType.HEALTH_DAMAGE_RANGE);
        int dodge = actor.TokenContainer.GetNumberOfTokensWithId("dodge", false) + actor.TokenContainer.GetNumberOfTokensWithId("dodge_plus", false);
        int block = actor.TokenContainer.GetNumberOfTokensWithId("block", false) + actor.TokenContainer.GetNumberOfTokensWithId("block_plus", false);
        var stats = new (string Label, string Value)[]
        {
            ("CRIT", Gui.Num(actor.GetClampedStatValue(ActorStatType.CRIT_CHANCE) * 100, "0") + "%"),
            ("DMG", dmg > 0 ? (range > 0 ? $"{Gui.Num(dmg - range, "0")}-{Gui.Num(dmg + range, "0")}" : Gui.Num(dmg, "0")) : "-"),
            ("DODGE", dodge > 0 ? dodge.ToString() : "0"),
            ("PROT", block > 0 ? block.ToString() : "0"),
            ("SPD", Gui.Num(actor.GetClampedStatValue(ActorStatType.SPEED), "0")),
            ("DTH DR", Gui.Num(actor.GetClampedStatValue(ActorStatType.DEATHS_DOOR_CHANCE) * 100, "0") + "%"),
        };
        for (int i = 0; i < stats.Length; i++)
        {
            float y = 928 + i * 23;
            Gui.Text(new Rect(300, y, 90, 23), stats[i].Label, 17, new Color(0.6f, 0.6f, 0.56f), TextAnchor.MiddleLeft);
            Gui.Text(new Rect(380, y, 80, 23), stats[i].Value, 17, new Color(0.93f, 0.9f, 0.82f), TextAnchor.MiddleRight);
        }

        // Equipment (hero_equipment 238,0) and trinkets (hero_trinket 453,0), in the panel's painted frames.
        string dd1Class = S.Campaign.HeroUpgrades.Dd1Class(hero.ClassId);
        var weapon = new Rect(512, 906, 72, 144);
        var armour = new Rect(604, 906, 72, 144);
        if (Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_weapon_{Mathf.Clamp(hero.WeaponRank, 0, 4)}.png") is { } w) GUI.DrawTexture(weapon, w);
        if (Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_armour_{Mathf.Clamp(hero.ArmorRank, 0, 4)}.png") is { } a) GUI.DrawTexture(armour, a);
        if (weapon.Contains(Event.current.mousePosition)) _hudTip = $"Weapon rank {hero.WeaponRank + 1}: {Dd2.Dd2Heroes.EquipmentText("weapon", hero.WeaponRank)}";
        if (armour.Contains(Event.current.mousePosition)) _hudTip = $"Armour rank {hero.ArmorRank + 1}: {Dd2.Dd2Heroes.EquipmentText("armour", hero.ArmorRank)}";
        for (int i = 0; i < 2 && i < hero.Trinkets.Count; i++)
        {
            var r = new Rect(718 + i * 92, 906, 72, 144);
            HeroSheet.TrinketIcon(r, hero.Trinkets[i]);
            if (r.Contains(Event.current.mousePosition)) _hudTip = HeroSheet.TrinketText(hero.Trinkets[i]);
        }
        if (_hudTip != null && Event.current.type == EventType.Repaint)
        {
            var m = Event.current.mousePosition;
            var tr = new Rect(Mathf.Min(m.x + 16, 1500), m.y - 70, 400, 60);
            Gui.Fill(tr, new Color(0.03f, 0.025f, 0.02f, 0.95f));
            Gui.Text(new Rect(tr.x + 10, tr.y + 6, tr.width - 20, tr.height - 10), _hudTip, 17, Gui.Dd1Text);
        }
        if (Event.current.type == EventType.Repaint) _hudTip = null;
    }

    private string _hudTip;

    /// <summary>The selected hero's equipped DD2 combat skills in DD1's five banner slots (upgraded ones marked +).</summary>
    private void DrawSkillRow(Core.Campaign.HeroRecord hero, Assets.Code.Actor.ActorInstance actor)
    {
        var skills = Dd2.HeroSkills.ForClass(hero.ClassId);
        if (skills == null) return;
        var equipped = actor.GetEquippedCombatSkillIds();
        for (int i = 0; i < 5 && i < equipped.Count; i++)
        {
            string id = equipped[i];
            string baseId = id.EndsWith("_u") ? id.Substring(0, id.Length - 2) : id;
            var skill = skills.Find(sk => sk.Id == baseId);
            var r = new Rect(520 + i * 76, 755, 72, 72);
            if (skill?.Icon != null) Art.DrawSprite(r, skill.Icon);
            if (id.EndsWith("_u")) Gui.Text(new Rect(r.x + 40, r.y + 46, 30, 26), "+", 22, Gui.Gold, TextAnchor.MiddleRight, heading: true);
            if (r.Contains(Event.current.mousePosition)) _hudTip = Dd2.HeroSkills.Name(baseId) + (id.EndsWith("_u") ? " (mastered)" : "");
        }
    }

    // ---------------- map ----------------

    private static Vector2 Pos(int x, int y) => new(x * MapUnit, y * MapUnit);

    private Vector2 _mapPan, _mapPress;
    private bool _mapDragging, _mapPressed;
    private string _mapSpot;

    private void DrawMap(ExpeditionState exp)
    {
        var map = exp.Map;
        var area = new Rect(976, 739, 649, 321);   // DD1 map clip region inside panel_map
        Vector2 here;
        if (exp.InRoom) here = Pos(map.Room(exp.RoomId).X, map.Room(exp.RoomId).Y);
        else
        {
            var t = map.Corridor(exp.CorridorId).Tiles[exp.TileIndex];
            here = HallPos(map, map.Corridor(exp.CorridorId), t.Index);
        }
        // DD1: drag the map to look around; it comes back to the party when the party moves.
        string spot = exp.InRoom ? "r" + exp.RoomId : $"c{exp.CorridorId}:{exp.TileIndex}";
        if (spot != _mapSpot) { _mapSpot = spot; _mapPan = Vector2.zero; }
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { _mapPress = e.mousePosition; _mapDragging = false; _mapPressed = true; }
        else if (e.type == EventType.MouseDrag && _mapPressed)
        {
            if (!_mapDragging && (e.mousePosition - _mapPress).magnitude > 5) _mapDragging = true;
            if (_mapDragging) { _mapPan += e.delta; e.Use(); }
        }
        else if (e.rawType == EventType.MouseUp && _mapPressed)
        {
            _mapPressed = false;
            if (_mapDragging) { _mapDragging = false; GUIUtility.hotControl = 0; e.Use(); }   // a drag is not a click
        }
        var offset = new Vector2(area.width / 2f, area.height / 2f) - here + _mapPan;

        GUI.BeginGroup(area);
        var visitedNear = new HashSet<int>(map.Rooms.Where(r => r.Visited).SelectMany(r => map.Neighbours(r.Id)));

        foreach (var c in map.Corridors)
        {
            bool known = map.Room(c.RoomA).Visited || map.Room(c.RoomB).Visited || c.Tiles.Any(t => t.Scouted);
            if (!known) continue;
            foreach (var t in c.Tiles)
            {
                var p = HallPos(map, c, t.Index) + offset;
                var r = new Rect(p.x - 12, p.y - 12, 24, 24);
                string baseIcon = t.Visited ? "hall_clear" : t.Scouted ? "hall_dim" : "hall_dark";
                GUI.DrawTexture(r, Art.MapIcon(baseIcon) ?? Texture2D.whiteTexture);
                string marker = (t.Visited || t.Scouted) && !t.Resolved ? t.Content switch
                {
                    HallContent.Battle => "marker_battle",
                    HallContent.Curio => "marker_curio",
                    HallContent.Trap => "marker_trap",
                    HallContent.Obstacle => "marker_obstacle",
                    _ => null,
                } : null;
                if (marker != null && Art.MapIcon(marker) is { } m) GUI.DrawTexture(r, m);
                if (Gui.Hotspot(r)) D.WalkToTile(c.Id, t.Index);
            }
        }

        foreach (var room in map.Rooms)
        {
            bool known = room.Visited || room.Scouted || visitedNear.Contains(room.Id);
            if (!known) continue;
            var p = Pos(room.X, room.Y) + offset;
            var r = new Rect(p.x - 32, p.y - 32, 64, 64);
            bool seen = room.Visited || room.Scouted;
            string icon = !seen ? "room_unknown"
                : room.Id == map.EntranceRoomId ? "room_entrance"
                : room.Content == RoomContent.Boss && !room.Cleared ? "room_boss"
                : room.HasBattle && !room.Cleared ? "room_battle"
                : room.CurioId != null && !room.CurioTaken && room.Content is RoomContent.Treasure or RoomContent.GuardedTreasure ? "room_treasure"
                : room.CurioId != null && !room.CurioTaken ? "room_curio"
                : "room_empty";
            GUI.DrawTexture(r, Art.MapIcon(icon) ?? Texture2D.whiteTexture);
            if (room.Visited && room.Id != map.EntranceRoomId && Art.MapIcon("marker_room_visited") is { } v) GUI.DrawTexture(r, v);
            if (Gui.Hotspot(r)) D.WalkToRoom(room.Id);
        }

        var ind = Art.MapIcon("indicator");
        var hp = here + offset;
        if (ind != null) GUI.DrawTexture(new Rect(hp.x - 25, hp.y - (exp.InRoom ? 70 : 52), 51, 48), ind);
        GUI.EndGroup();
    }

    /// <summary>Hall squares spaced evenly between the two rooms' icon edges.</summary>
    private static Vector2 HallPos(DungeonMap map, Corridor c, int index)
    {
        var a = Pos(map.Room(c.RoomA).X, map.Room(c.RoomA).Y);
        var b = Pos(map.Room(c.RoomB).X, map.Room(c.RoomB).Y);
        var dir = (b - a).normalized;
        var start = a + dir * 32f;
        var end = b - dir * 32f;
        float step = (end - start).magnitude / c.Tiles.Count;
        return start + dir * (step * (index + 0.5f));
    }

    // ---------------- inventory ----------------

    private static Rect PackCell(int i) => new(960 + 20 + (i % 8) * 80, 720 + 28 + (i / 8) * 160, 72, 144);

    private void DrawInventory(Crawl crawl, ExpeditionState exp)
    {
        var items = S.Content.Items;
        var stacks = exp.Pack.Arrange(items);
        for (int i = 0; i < Inventory.Slots; i++)
        {
            var r = PackCell(i);
            var stack = stacks.Find(st => st.Slot == i);
            if (Drag.Hovering<PackStack>(r)) Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), Gui.Gold);
            if (Drag.Drop<PackStack>(r, out var moved) && moved.Pack == exp.Pack) { exp.Pack.Move(moved.Slot, i); continue; }
            if (stack == null) continue;
            string key = stack.Key;
            int count = stack.Count, limit = items.StackLimit(key);
            Drag.Source(r, new PackStack(exp.Pack, i, key), rect => ItemArt.Stack(rect, key, count, limit));
            bool carried = Drag.Payload is PackStack c && c.Pack == exp.Pack && c.Slot == i;
            if (!carried) ItemArt.Stack(r, key, count, limit);
            if (r.Contains(Event.current.mousePosition) && !Drag.Active)
                Gui.Text(new Rect(960, 1050, 960, 26), $"{HamletUi.Pretty(key)}: click to use on {S.Save.Estate.Hero(D.SelectedHeroId)?.Name ?? "the party"}, drag onto a hero, shift+click to drop one.", 17, Gui.Dd1Text, TextAnchor.MiddleCenter);
            bool shift = Event.current.shift;
            if (Gui.Hotspot(r) && !Drag.JustDropped)
            {
                // DD1: shift+click throws one away to make room (quest items can't be).
                if (shift) { if (crawl.Discard(key)) { Runtime.Dd1Audio.Play("/gen/item/discard"); S.Persist(); } else Gui.Announce("That can't be left behind."); }
                else UseItem(key, crawl);
            }
        }
    }

    /// <summary>A supply dropped on a hero: select them and use it on them.</summary>
    private static void UseItemOn(string heroId, string key, Crawl crawl)
    {
        D.SelectedHeroId = heroId;
        UseItem(key, crawl);
    }

    private static void UseItem(string key, Crawl crawl)
    {
        string done = crawl.UseSupply(D.SelectedHeroId, key);
        if (done != null) { Gui.Announce(done); S.Persist(); return; }
        if (key == Supply.Torch) D.UseTorch();
        else if (key == Supply.Firewood && crawl.CanCamp) D.MakeCamp();
        else if (key == Supply.Food && D.SelectedHeroId != null && crawl.State.Pack.TryUse(Supply.Food))
        {
            D.Party.Heal(D.SelectedHeroId, 0.05f);
            Gui.Announce("A meagre meal.");
        }
        else if (key is Supply.Bandage or Supply.Antivenom or Supply.Laudanum or Supply.Herbs)
            Gui.Announce("Nothing to treat now. Keep it for curios.");
    }

    // ---------------- curio / obstacle / trap prompt (DD1 "sidebar scroll" at 1348,200) ----------------

    // ---------------- battle spoils (DD1's loot scroll) ----------------

    private BattleSpoils _spoilsDismissed;

    private bool DrawSpoils(Crawl crawl)
    {
        var spoils = crawl.LastSpoils;
        if (spoils == null || spoils == _spoilsDismissed) return false;
        if (spoils.Taken.Count == 0 && spoils.LeftBehind.Count == 0) { _spoilsDismissed = spoils; return false; }

        float left = 1342 - 228, top = 140;
        var scroll = Scroll("event_scroll_loot.png");
        if (scroll != null) GUI.DrawTexture(new Rect(left, top, 456, 475), scroll);
        else Gui.Fill(new Rect(left, top, 456, 475), new Color(0.05f, 0.04f, 0.03f, 0.93f));
        Gui.Text(new Rect(left + 40, top + 26, 376, 48), "Spoils", 34, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);

        var all = spoils.Taken.Select(d => (d, taken: true)).Concat(spoils.LeftBehind.Select(d => (d, taken: false))).ToList();
        var items = S.Content.Items;
        int perRow = Mathf.Min(5, all.Count), takenCount = spoils.Taken.Count, clicked = -1;
        for (int i = 0; i < all.Count && i < 10; i++)
        {
            var (drop, taken) = all[i];
            int row = i / 5, col = i % 5, inRow = row == 0 ? perRow : Mathf.Min(5, all.Count - 5);
            var r = new Rect(left + 228 - inRow * 40 + col * 80 + 4, top + 96 + row * 150, 72, 144);
            ItemArt.Stack(r, drop.Key, drop.Amount, Mathf.Max(1, items.StackLimit(drop.Key)), dim: !taken);
            if (!taken && Gui.Hotspot(r)) clicked = i - takenCount;
        }
        if (clicked >= 0) TakeLeftBehind(crawl, spoils.LeftBehind, clicked, spoils.Taken);
        if (spoils.LeftBehind.Count > 0) NoRoomHint(left, top + 475);
        bool canLeave = Crawl.CanLeave(spoils.LeftBehind);
        if (Gui.DdButton(new Rect(1342 - 110, top + 475 - 70, 220, 50), "Continue", canLeave, 24)
            || (canLeave && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Space)))
            _spoilsDismissed = spoils;
        return true;
    }

    // ---------------- curio result (DD1's result scroll at 1342,140) ----------------

    private CurioReport _resultShown, _resultDismissed;

    /// <summary>What the last investigation turned up, on DD1's loot or basic scroll, until clicked away.</summary>
    private bool DrawCurioResult(ExpeditionState exp)
    {
        var report = D.LastCurio;
        if (report == null || report == _resultDismissed) return false;
        if (report != _resultShown) _resultShown = report;

        bool loot = report.Loot.Count > 0;
        float left = 1342 - 228, top = 140, height = loot ? 475 : 518;
        var scroll = Scroll(loot ? "event_scroll_loot.png" : "event_scroll_basic.png");
        if (scroll != null) GUI.DrawTexture(new Rect(left, top, 456, height), scroll);
        else Gui.Fill(new Rect(left, top, 456, height), new Color(0.05f, 0.04f, 0.03f, 0.93f));

        string who = S.Save.Estate.Hero(report.HeroId)?.Name ?? "";
        Gui.Text(new Rect(left + 40, top + 26, 376, 48), HamletUi.Pretty(report.CurioId), 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var lines = new System.Collections.Generic.List<string>();
        lines.Add($"{who}: {report.Text ?? HamletUi.Pretty(report.OutcomeType)}");
        if (report.ItemUsed != null) lines.Add($"Used {HamletUi.Pretty(report.ItemUsed)}.");
        lines.AddRange(report.Effects);
        if (report.QuirkGained != null) lines.Add($"Gained the quirk {HamletUi.QuirkName(report.QuirkGained)}.");
        if (report.Purged != null) lines.Add($"Rid of {HamletUi.QuirkName(report.Purged)}.");
        if (report.Scouted) lines.Add("The way ahead is revealed.");
        Gui.Text(new Rect(left + 50, top + 104, 356, 130), string.Join("\n", lines), 19, Gui.Dd1Text, TextAnchor.UpperCenter);

        var items = S.Content.Items;
        int clicked = -1;
        for (int i = 0; i < report.Loot.Count && i < 5; i++)
        {
            var drop = report.Loot[i];
            var r = new Rect(left + 228 - Mathf.Min(5, report.Loot.Count) * 40 + i * 80 + 4, top + 236, 72, 144);
            int waiting = report.LeftBehind.IndexOf(drop);
            ItemArt.Stack(r, drop.Key, drop.Amount, Mathf.Max(1, items.StackLimit(drop.Key)), dim: waiting >= 0);
            if (waiting >= 0 && Gui.Hotspot(r)) clicked = waiting;
        }
        if (clicked >= 0) TakeLeftBehind(D.Crawl, report.LeftBehind, clicked);
        if (report.LeftBehind.Count > 0) NoRoomHint(left, top + height);
        bool canLeave = Crawl.CanLeave(report.LeftBehind);
        if (Gui.DdButton(new Rect(1342 - 110, top + height - 92, 220, 50), "Continue", canLeave, 24)
            || (canLeave && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Space)))
            _resultDismissed = report;
        return true;
    }

    /// <summary>Under a loot scroll: how to make room for what the pack couldn't take.</summary>
    private static void NoRoomHint(float left, float y)
    {
        Gui.Fill(new Rect(left + 20, y + 4, 416, 50), new Color(0f, 0f, 0f, 0.75f));
        Gui.Text(new Rect(left + 20, y + 4, 416, 50), "No room: shift+click a pack item to drop it,\nthen click a greyed item to take it.", 17, Gui.Blood, TextAnchor.MiddleCenter);
    }

    /// <summary>Loot the pack had no room for is waiting on a scroll (the pack stays in view to make room).</summary>
    private bool LootWaiting(Crawl crawl) =>
        (crawl.LastSpoils is { } sp && sp != _spoilsDismissed && sp.LeftBehind.Count > 0)
        || (D.LastCurio is { } cr && cr != _resultDismissed && cr.LeftBehind.Count > 0);

    private static void TakeLeftBehind(Crawl crawl, System.Collections.Generic.List<LootDrop> left, int index, System.Collections.Generic.List<LootDrop> taken = null)
    {
        if (crawl == null) return;
        var drop = index >= 0 && index < left.Count ? left[index] : null;
        if (crawl.TakeLeftBehind(left, index, taken))
        {
            // DD1's per-kind loot sounds (ui_dun_loot_take_*).
            string kind = drop?.Type switch { "gold" => "gold", "heirloom" => "heirloom", "gem" => "jewelry", "provision" or "supply" => "provisions", _ => "all" };
            Runtime.Dd1Audio.Play("/ui/dun/loot_take_" + kind);
            S.Persist();
        }
        else { Gui.Announce("No room in the pack."); Runtime.Dd1Audio.Play("/ui/shared/button_invalid"); }
    }

    // DD1's scrolls (scrolls/*.png) and where screen.raid.darkest puts them.
    private static Texture2D Scroll(string file) => Art.Dd1("scrolls", file);
    private const float SidebarX = 1348, SidebarY = 200;   // sidebar_scroll .pos (centre x, top)
    private string _curioItem;                            // the supply in the curio's item slot

    /// <summary>A round DD1 scroll button (byhand.png, pass.png ...) with its label underneath.</summary>
    private static bool ScrollButton(float x, float y, string icon, string label, bool enabled = true)
    {
        var r = new Rect(x, y, 124, 69);
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        var tex = Scroll(icon);
        var old = GUI.color;
        if (!enabled) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        if (tex != null) GUI.DrawTexture(hover ? new Rect(r.x - 4, r.y - 3, r.width + 8, r.height + 6) : r, tex);
        else Gui.Fill(r, new Color(0.15f, 0.12f, 0.1f));
        GUI.color = old;
        Gui.Text(new Rect(r.x - 30, r.yMax, r.width + 60, 26), label, 19, hover ? Color.white : enabled ? Gui.Dd1Name : Gui.Dim, TextAnchor.UpperCenter, heading: true);
        return enabled && Gui.Hotspot(r);
    }

    /// <summary>An inventory item used on the party's curio: the right one does what DD1 says; any other does nothing.</summary>
    private void UseOnCurio(Crawl crawl, string curio, string item)
    {
        bool works = item == crawl.QuestItemNeededHere || S.Content.Curios.UsefulItems(curio).Contains(item);
        if (!works)
        {
            Gui.Announce("Nothing happens.");
            Runtime.Dd1Audio.Play("/ui/shared/button_invalid");
            return;
        }
        _curioPanel = null;
        D.Investigate(D.SelectedHeroId, item);
    }

    private void DrawPrompt(Crawl crawl, ExpeditionState exp)
    {
        if (exp.Camp != null) return;
        var tile = crawl.CurrentTile;
        string curio = crawl.CurioHere;
        bool obstacle = tile is { Content: HallContent.Obstacle, Resolved: false };
        bool trap = tile is { Content: HallContent.Trap, Resolved: false } && tile.Scouted;
        bool battleStuck = crawl.IsBlocked && (exp.InRoom || tile?.Content == HallContent.Battle);
        string here = curio == null ? null : (exp.InRoom ? "r" + exp.RoomId : $"c{exp.CorridorId}:{exp.TileIndex}") + ":" + curio;
        if (CurioDrop != null && curio != null) { string item = CurioDrop; CurioDrop = null; _curioPanel = here; UseOnCurio(crawl, curio, item); return; }
        if (CurioClicked && curio != null) _curioPanel = here;
        CurioClicked = false;
        CurioDrop = null;
        if (_curioPanel != here) curio = null;   // DD1: a curio waits until it is clicked
        if (curio == null && !obstacle && !trap && !battleStuck) { _curioItem = null; return; }

        float left = SidebarX - 228, top = SidebarY;
        var scroll = Scroll(battleStuck ? "event_scroll_basic.png" : "event_scroll_sidebar.png");
        float height = battleStuck ? 518 : 400;
        if (scroll != null) GUI.DrawTexture(new Rect(left, top, 456, height), scroll);
        else Gui.Fill(new Rect(left, top, 456, height), new Color(0.05f, 0.04f, 0.03f, 0.93f));
        var header = new Rect(left + 40, top + 20, 376, 48);
        var body = new Rect(SidebarX - 152, top + 118, 330, 110);

        if (battleStuck)
        {
            Gui.Text(header, "Enemies!", 34, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            Gui.Text(new Rect(left + 60, top + 170, 336, 90), "The way is held. There is no going on without a fight.", 20, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (Gui.DdButton(new Rect(SidebarX - 120, top + 400, 240, 56), "Fight")) D.Fight();
            return;
        }

        string who = S.Save.Estate.Hero(D.SelectedHeroId)?.Name ?? "";
        if (curio != null)
        {
            Gui.Text(header, HamletUi.Pretty(curio), 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            Gui.Text(body, $"{who} will investigate. Select another hero to send them instead, or drag an item from the inventory onto it.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (ScrollButton(SidebarX - 152, top + 240, "byhand.png", "Investigate")) { _curioPanel = null; D.Investigate(D.SelectedHeroId, null); return; }
            if (ScrollButton(SidebarX + 75, top + 240, "pass.png", "Leave it")) { _curioPanel = null; return; }

            // DD1's item slot: drag any item from the inventory into it (or onto the curio). The right item does
            // something (a key on a locked chest); any other has no effect and stays in the pack.
            var slot = new Rect(SidebarX - 34, top + 230, 80, 160);
            var slotTex = Scroll("use_inventory.png");
            if (slotTex != null) GUI.DrawTexture(new Rect(slot.x + 4, slot.y + 8, 72, 144), slotTex);
            if (Drag.Hovering<PackStack>(slot)) Gui.Fill(new Rect(slot.x, slot.yMax + 2, slot.width, 4), Gui.Gold);
            if (Drag.Drop<PackStack>(slot, out var put)) { UseOnCurio(crawl, curio, put.Key); return; }
            if (slot.Contains(Event.current.mousePosition)) Gui.Text(new Rect(left + 20, top + 400, 416, 30), "Drag an item here to use it", 19, Color.white, TextAnchor.MiddleCenter);
        }
        else if (obstacle)
        {
            Gui.Text(header, HamletUi.Pretty(tile.ContentId), 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            bool shovel = exp.Pack.Count(Supply.Shovel) > 0;
            Gui.Text(body, shovel ? "A shovel will clear the way." : "Without a shovel the party must force its way through: hurt and stressed.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (shovel)
            {
                var icon = Art.InventoryIcon(Supply.Shovel, 1, 1);
                var slot = new Rect(SidebarX - 34, top + 230, 80, 160);
                if (icon != null) GUI.DrawTexture(new Rect(slot.x + 4, slot.y + 8, 72, 144), icon);
                if (Gui.Hotspot(slot)) { D.ClearObstacle(); return; }
            }
            if (ScrollButton(SidebarX - 152, top + 240, "byhand.png", shovel ? "Dig through" : "Clear by hand")) { D.ClearObstacle(); return; }
        }
        else if (trap)
        {
            Gui.Text(header, HamletUi.Pretty(tile.ContentId), 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            // DD1: the selected hero tries, on their class's trap skill plus the bonus for having spotted it.
            int pct = Mathf.RoundToInt(crawl.TrapDisarmChance(D.SelectedHeroId, scouted: true) * 100f);
            Gui.Text(body, $"{who} has {pct}% to disarm it (select another hero to send them). Failing springs it on them.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (ScrollButton(SidebarX - 152, top + 240, "byhand.png", "Disarm") || TrapClicked) { TrapClicked = false; D.DisarmTrap(); return; }
        }
    }

    // ---------------- camp (restyled; full DD1 camp screen comes later) ----------------

    // ---------------- camp ----------------

    private static void DrawCampfire()
    {
        // Night falls on the room; the fire lights the middle.
        Gui.Fill(new Rect(0, 0, Gui.W, 720), new Color(0, 0, 0, 0.45f));
        var glow = Art.Overlay("torch_flame.png");
        float flicker = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 7f) + 0.04f * Mathf.Sin(Time.unscaledTime * 13f);
        if (glow != null)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.25f, 0.35f);
            float w = 900 * flicker, h = 420 * flicker;
            GUI.DrawTexture(new Rect(960 - w / 2, Feet - 130 - h / 2, w, h), glow);
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(960 - 40 * flicker, Feet - 150, 80 * flicker, 110 * flicker), glow);
            GUI.color = old;
        }
        var camp = Art.Dd1("props", "shared", "campfire.png");
        if (camp != null) GUI.DrawTexture(new Rect(960 - camp.width / 2f, Feet + 30 - camp.height, camp.width, camp.height), camp);
    }

    private const float CampScrollX = 732, CampScrollY = 60;   // camp_layout .respite_scroll_pos

    private static void DrawRespite(CampState camp)
    {
        if (!camp.Ate) return;   // the meal scroll takes this spot first
        var scroll = Scroll("event_scroll_campingrespite.png");
        if (scroll != null) GUI.DrawTexture(new Rect(CampScrollX, CampScrollY, 456, 237), scroll);
        Gui.Text(new Rect(CampScrollX + 40, CampScrollY + 22, 260, 44), "Respite", 32, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(CampScrollX + 284, CampScrollY + 22, 80, 44), camp.RespiteLeft.ToString(), 36, Gui.Gold, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(CampScrollX + 56, CampScrollY + 96, 344, 80), "Spend respite on camping skills. Click a hero to see theirs.", 18, Gui.Dd1Text, TextAnchor.UpperLeft);
    }


    private void DrawCamp(Crawl crawl, ExpeditionState exp)
    {
        var camp = exp.Camp;
        if (!camp.Ate)
        {
            DrawMealChoice(crawl, exp);
            return;
        }

        // The selected hero's camping skills, in a row above the camp.
        string hero = D.SelectedHeroId;
        if (hero != null && exp.CampSkills.TryGetValue(hero, out var skills) && skills.Count > 0)
        {
            Gui.Text(new Rect(560, 304, 800, 30), $"{S.Save.Estate.Hero(hero)?.Name}'s camping skills", 22, Gui.Dd1Class, TextAnchor.MiddleCenter, heading: true);
            float w = skills.Count * 92 - 12, x0 = 960 - w / 2;
            string hovered = null;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = S.Content.Camping.Get(skills[i]);
                if (skill == null) continue;
                var r = new Rect(x0 + i * 92, 338, 80, 80);
                string why = crawl.WhyCantUseCampSkill(hero, skill.Id);
                bool hover = r.Contains(Event.current.mousePosition);
                if (hover) hovered = skill.Id;
                var old = GUI.color;
                if (why != null) GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                var icon = Art.Dd1("raid", "camping", "skill_icons", $"camp_skill_{skill.Id}.png");
                if (icon != null) GUI.DrawTexture(hover ? new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6) : r, icon);
                else Gui.Fill(r, new Color(0.15f, 0.12f, 0.1f));
                GUI.color = old;
                if (_campSkillPending == skill.Id && _campSkillUser == hero) Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), Gui.Gold);
                Gui.Fill(new Rect(r.xMax - 26, r.y, 26, 26), new Color(0, 0, 0, 0.8f));
                Gui.Text(new Rect(r.xMax - 26, r.y, 26, 26), skill.Cost.ToString(), 20, why == null ? Gui.Gold : Gui.Dim, TextAnchor.MiddleCenter, heading: true);
                if (why == null && Gui.Hotspot(r))
                {
                    if (skill.NeedsTarget)
                    {
                        _campSkillPending = skill.Id;
                        _campSkillUser = hero;
                        Gui.Announce("Choose who receives it.");
                    }
                    else
                    {
                        _campSkillPending = null;
                        D.UseCampSkill(hero, skill.Id, null);
                    }
                }
            }
            if (hovered != null) DrawCampSkillTip(crawl, exp, hero, hovered);
        }
        else Gui.Text(new Rect(560, 330, 800, 40), "Click a hero to see their camping skills.", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);

        if (_campSkillPending != null && Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            _campSkillPending = null;    // right-click cancels the targeting
            Event.current.Use();
        }
        if (Gui.DdButton(new Rect(CampScrollX + 100, CampScrollY + 180, 256, 44), "Rest and break camp", true, 22))
        {
            _campSkillPending = null;
            D.BreakCamp();
        }
    }

    private static void DrawMealChoice(Crawl crawl, ExpeditionState exp)
    {
        // DD1 starts the night with a meal: how much food to share out.
        var scroll = Scroll("meal_scroll.png");
        if (scroll != null) GUI.DrawTexture(new Rect(CampScrollX, CampScrollY, 456, 333), scroll);
        else Gui.Fill(new Rect(CampScrollX, CampScrollY, 456, 333), new Color(0.03f, 0.025f, 0.02f, 0.92f));
        Gui.Text(new Rect(CampScrollX + 40, CampScrollY + 28, 376, 48), "The meal", 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        int i = 0, food = exp.Pack.Count(Supply.Food);
        Gui.Text(new Rect(CampScrollX + 40, CampScrollY + 92, 376, 30), $"Food in the pack: {food}", 19, Gui.Dd1Class, TextAnchor.MiddleCenter);
        foreach (Meal m in Enum.GetValues(typeof(Meal)))
        {
            int cost = crawl.MealCost(m);
            bool can = food >= cost;
            float x = CampScrollX + 26 + i++ * 104;
            var r = new Rect(x, CampScrollY + 148, 100, 56);
            bool hover = can && r.Contains(Event.current.mousePosition);
            var tex = Scroll(cost == 0 ? "starve.png" : "eat.png");
            var old = GUI.color;
            if (!can) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (tex != null) GUI.DrawTexture(hover ? new Rect(r.x - 4, r.y - 3, r.width + 8, r.height + 6) : r, tex);
            GUI.color = old;
            string name = cost == 0 ? "Go hungry" : m.ToString();
            Gui.Text(new Rect(x - 6, r.yMax + 4, 112, 28), name, 19, hover ? Color.white : can ? Gui.Dd1Name : Gui.Dim, TextAnchor.UpperCenter, heading: true);
            Gui.Text(new Rect(x - 6, r.yMax + 30, 112, 26), cost == 0 ? "" : $"{cost} food", 17, can ? Gui.Dd1Text : Gui.Dim, TextAnchor.UpperCenter);
            if (can && Gui.Hotspot(r)) D.EatMeal(m);
        }
    }

    private static void DrawCampSkillTip(Crawl crawl, ExpeditionState exp, string hero, string skillId)
    {
        var skill = S.Content.Camping.Get(skillId);
        string why = crawl.WhyCantUseCampSkill(hero, skillId);
        exp.Camp.Uses.TryGetValue(hero + ":" + skillId, out int used);
        var lines = S.Content.Camping.DescribeAll(skill);
        if (skill.UseLimit > 0) lines.Add($"Uses: {used}/{skill.UseLimit}");
        if (why != null) lines.Add(why);
        var tip = new Rect(660, 430, 600, 52 + 26 * lines.Count);
        Gui.Fill(tip, new Color(0.03f, 0.025f, 0.02f, 0.94f));
        Gui.Text(new Rect(tip.x + 14, tip.y + 6, tip.width - 28, 34), $"{Dd1Text.CampSkillName(skillId)}  ·  {skill.Cost} respite", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        for (int i = 0; i < lines.Count; i++)
            Gui.Text(new Rect(tip.x + 14, tip.y + 44 + i * 26, tip.width - 28, 26), lines[i], 19, lines[i].StartsWith("(") || lines[i].StartsWith("Uses") ? Gui.Dd1Class : Gui.Dd1Text, TextAnchor.MiddleLeft);
    }
}
