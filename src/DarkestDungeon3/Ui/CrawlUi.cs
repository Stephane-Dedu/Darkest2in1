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
    private string _campSkillPending, _campSkillUser;   // a camp skill waiting for its target

    private static Session S => Session.Current;
    private static Driver D => Driver.Instance;

    public void Draw()
    {
        var crawl = D.Crawl;
        var exp = D.Expedition;
        if (crawl == null || exp == null) return;
        string zone = exp.Quest.Dungeon;
        if (D.SelectedHeroId == null || !D.Party.Alive.Contains(D.SelectedHeroId)) D.SelectedHeroId = D.Party.Alive.FirstOrDefault();

        DrawScene(crawl, exp, zone);
        if (exp.Camp != null) DrawCampfire();
        DrawHeroes(exp);
        if (exp.Camp != null) DrawRespite(exp.Camp); else DrawTorch(exp.Light);
        DrawQuestInfo(crawl, exp);
        DrawHud(exp);
        if (_inventoryTab) DrawInventory(crawl, exp); else DrawMap(exp);
        if (exp.Camp != null) DrawCamp(crawl, exp); else DrawPrompt(crawl, exp);
        Gui.DrawAnnouncement();
    }

    // ---------------- scene ----------------

    private static void DrawScene(Crawl crawl, ExpeditionState exp, string zone)
    {
        Gui.Fill(new Rect(0, 0, Gui.W, 720), Color.black);
        float t = (Time.unscaledTime - D.LastStepTime) / 0.3f;
        float slide = t < 1f ? D.LastStepDir * 720f * (1f - Mathf.SmoothStep(0, 1, t)) : 0f;

        if (exp.InRoom)
        {
            var tex = exp.RoomId == exp.Map.EntranceRoomId ? Art.EntranceWall(zone) : Art.RoomWall(zone, exp.RoomId);
            if (tex != null) GUI.DrawTexture(new Rect(slide, 0, 1920, 720), tex);
        }
        else
        {
            var c = crawl.CurrentCorridor;
            int dir = exp.HeadingRoomId == c.RoomB ? 1 : -1;   // screen-right is the way the party is heading
            for (int k = -2; k <= 2; k++)
            {
                int i = exp.TileIndex + k * dir;
                float x = 600 + k * 720 + slide;
                if (x > 1920 || x + 720 < 0) continue;
                bool beyond = i < 0 || i >= c.Tiles.Count;
                if (beyond && Math.Abs(k) > 1 && (i < -1 || i > c.Tiles.Count)) continue;
                if (beyond)
                {
                    // DD1's door segment has its doorway on the right: mirror it for the door behind the party.
                    var door = Art.CorridorDoor(zone);
                    if (door != null) GUI.DrawTextureWithTexCoords(new Rect(x, 0, 720, 720), door, k > 0 ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1));
                }
                else
                {
                    var wall = Art.CorridorWall(zone, c.Id * 3 + i);
                    if (wall != null) GUI.DrawTexture(new Rect(x, 0, 720, 720), wall);
                }
                var top = Art.ForegroundTop(zone);
                var bottom = Art.ForegroundBottom(zone);
                if (top != null) GUI.DrawTexture(new Rect(x, 0, 720, top.height), top);
                if (bottom != null) GUI.DrawTexture(new Rect(x, 720 - bottom.height, 720, bottom.height), bottom);
            }
        }
        DrawProp(crawl, exp);

        // DD1's darkness: the dimmer the torch, the heavier the shadow, strongest at the edges.
        float dark = Mathf.Clamp01((75f - exp.Light) / 110f);
        if (dark > 0)
        {
            Gui.Fill(new Rect(0, 0, Gui.W, 720), new Color(0, 0, 0, dark * 0.55f));
            Gui.Fill(new Rect(0, 0, 260, 720), new Color(0, 0, 0, dark * 0.35f));
            Gui.Fill(new Rect(1660, 0, 260, 720), new Color(0, 0, 0, dark * 0.35f));
        }
    }

    /// <summary>
    /// What stands in front of the party: DD1's own prop art (curios closed, highlighted under the mouse, open
    /// once used; obstacles; spotted traps), drawn from its Spine skeletons. Falls back to the map icon.
    /// </summary>
    /// <summary>Debug (F6): show this curio in front of the party instead of what's really there.</summary>
    public static string PreviewCurio;

    private static void DrawProp(Crawl crawl, ExpeditionState exp)
    {
        string kind = null, id = null;
        bool used = false;
        if (PreviewCurio != null) { kind = "curios"; id = PreviewCurio; }
        else if (exp.InRoom)
        {
            var room = crawl.CurrentRoom;
            if (room?.CurioId != null) { kind = "curios"; id = room.CurioId; used = room.CurioTaken; }
        }
        else if (crawl.CurrentTile is { } tile)
        {
            switch (tile.Content)
            {
                case HallContent.Curio: kind = "curios"; id = tile.ContentId; used = tile.Resolved; break;
                case HallContent.Obstacle when !tile.Resolved: kind = "obstacles"; id = tile.ContentId; break;
                case HallContent.Trap when tile.Scouted && !tile.Resolved: kind = "traps"; id = tile.ContentId; break;
            }
        }
        if (kind == null) return;

        var feet = new Vector2(PropX, Feet + 10);
        string folder = S.Dd1.PathOf("props", "shared", kind, id ?? "");
        SpineArt.Picture pic;
        if (kind == "curios")
        {
            var closed = SpineArt.Get(folder, "closed", sl => sl.Name != "active" && sl.Name != "open");
            bool hover = !used && closed != null && closed.Hit(feet, 1f, Event.current.mousePosition);
            pic = used ? SpineArt.Get(folder, "open", sl => sl.Name != "active" && sl.Name != "closed")
                : hover ? SpineArt.Get(folder, "active", sl => sl.Name != "closed" && sl.Name != "open") ?? closed
                : closed;
        }
        else pic = SpineArt.Get(folder, "idle", sl => sl.Name != "active" && !sl.Name.StartsWith("dust") && sl.Name != "splash" && sl.Name != "foam");

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
        var ir = new Rect(PropX - 75, Feet - 150, 150, 150);
        Gui.Fill(new Rect(ir.x - 10, ir.yMax + 2, ir.width + 20, 14), new Color(0, 0, 0, 0.4f));
        GUI.DrawTexture(ir, tex, ScaleMode.ScaleToFit);
    }

    private const float PropX = 1130;

    // ---------------- party ----------------

    private void DrawHeroes(ExpeditionState exp)
    {
        var shadow = Art.Overlay("charactershadow_med.png");
        var selected = Art.Overlay("selected_1.png");
        // DD2's animated hero models, rendered off-screen at DD1's rank positions (see HeroStage).
        var models = Plugin.HeroModels.Value ? Dd2.HeroStage.Instance?.Texture : null;
        if (models != null) GUI.DrawTexture(new Rect(0, 0, 1920, 720), models);
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
            if (sprite != null) Art.DrawSprite(large ? new Rect(x - 120, Feet - 420, 240, 430) : new Rect(x - 80, Feet - 230, 160, 220), sprite, flipX: facingLeft);
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
        else if (!crawl.IsBlocked)
        {
            var retreat = Art.Panel("retreat_button.png");
            var r = Gui.At(retreat, 20, 112);
            if (retreat == null) r = new Rect(20, 112, 64, 64);
            if (Gui.Hotspot(r))
            {
                if (_confirmRetreat) { _confirmRetreat = false; D.Leave(); return; }
                _confirmRetreat = true;
                Gui.Announce("Retreat? Click again to abandon the quest.");
            }
            Gui.Text(new Rect(r.xMax + 10, r.y + 20, 300, 30), _confirmRetreat ? "Click again to retreat" : "Retreat", 22, _confirmRetreat ? Gui.Blood : Gui.Dim, heading: true);
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

        // Tabs on the right edge of the map panel: map, then inventory.
        if (Gui.Hotspot(new Rect(1630, 840, 50, 62))) _inventoryTab = false;
        if (Gui.Hotspot(new Rect(1630, 906, 50, 62))) _inventoryTab = true;
        Gui.Fill(new Rect(1630, _inventoryTab ? 906 : 840, 3, 62), Gui.Gold);

        var hero = S.Save.Estate.Hero(D.SelectedHeroId);
        var actor = Dd2Api.Actor(D.Party.Guid(D.SelectedHeroId ?? ""));
        if (hero == null || actor == null) return;

        // Banner: portrait, name, class (DD1 panel.banner.darkest).
        var portrait = Art.Portrait(hero.ClassId);
        if (portrait != null) Art.DrawSprite(new Rect(243, 750, 100, 100), portrait);
        Gui.Text(new Rect(479, 744, 460, 40), hero.Name, 36, Gui.Dd1Name, heading: true);
        Gui.Text(new Rect(479, 786, 460, 30), $"{HamletUi.Pretty(hero.ClassId)} — Resolve {hero.ResolveLevel}", 22, Gui.Dd1Class);

        // Hero panel (DD1 panel.hero.darkest): health, stress, then stats.
        Gui.Text(new Rect(370, 864, 300, 28), $"{actor.HpRounded:0} / {actor.CurrentHpMax:0}", 24, new Color(0.85f, 0.15f, 0.12f));
        Gui.Text(new Rect(370, 893, 300, 28), $"{actor.Stress:0} / {actor.StressMax:0}", 24, Gui.Dd1Class);
        var stats = new List<string>
        {
            $"SPD  {actor.GetClampedStatValue(ActorStatType.SPEED):0}",
            $"CRIT {actor.GetClampedStatValue(ActorStatType.CRIT_CHANCE) * 100:0}%",
            $"DD   {actor.GetClampedStatValue(ActorStatType.DEATHS_DOOR_CHANCE) * 100:0}%",
        };
        foreach (var res in new[] { "stun", "blight", "bleed", "burn", "move", "debuff", "disease" })
            stats.Add($"{HamletUi.Pretty(res).Substring(0, Math.Min(5, res.Length))} {actor.GetUnclampedStatValue(ActorStatType.RESISTANCE, res) * 100:0}%");
        for (int i = 0; i < stats.Count; i++)
            Gui.Text(new Rect(262 + (i / 5) * 105, 928 + (i % 5) * 28, 110, 28), stats[i], 18, Gui.Dd1Text);

        // Trinkets (DD1 hero_trinket at 453,0): names in the two slots.
        for (int i = 0; i < 2; i++)
            Gui.Text(new Rect(700 + i * 118, 888, 112, 160), i < hero.Trinkets.Count ? HamletUi.Pretty(hero.Trinkets[i]) : "", 17, Gui.Dd1Text, TextAnchor.MiddleCenter);
    }

    // ---------------- map ----------------

    private static Vector2 Pos(int x, int y) => new(x * MapUnit, y * MapUnit);

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
        var offset = new Vector2(area.width / 2f, area.height / 2f) - here;

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

    private void DrawInventory(Crawl crawl, ExpeditionState exp)
    {
        var items = S.Content.Items;
        var stacks = new List<(string key, int count)>();
        foreach (var kv in exp.Pack.Items.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key))
        {
            int limit = Math.Max(1, items.StackLimit(kv.Key));
            for (int left = kv.Value; left > 0; left -= limit) stacks.Add((kv.Key, Math.Min(limit, left)));
        }
        for (int i = 0; i < stacks.Count && i < 16; i++)
        {
            var (key, count) = stacks[i];
            var r = new Rect(960 + 20 + (i % 8) * 80, 720 + 28 + (i / 8) * 160, 72, 144);
            var icon = Art.InventoryIcon(key, count, items.StackLimit(key));
            if (icon != null) GUI.DrawTexture(r, icon);
            else Gui.Text(r, HamletUi.Pretty(key), 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
            Gui.Text(new Rect(r.x, r.yMax - 28, r.width - 4, 26), count.ToString(), 22, Color.white, TextAnchor.LowerRight);
            if (Gui.Hotspot(r)) UseItem(key, crawl);
        }
    }

    /// <summary>DD1 lets you use some supplies straight from the pack.</summary>
    private static void UseItem(string key, Crawl crawl)
    {
        if (key == Supply.Torch) D.UseTorch();
        else if (key == Supply.Firewood && crawl.CanCamp) D.MakeCamp();
        else if (key == Supply.Food && D.SelectedHeroId != null && crawl.State.Pack.TryUse(Supply.Food))
        {
            D.Party.Heal(D.SelectedHeroId, 0.05f);
            Gui.Announce("A meagre meal.");
        }
    }

    // ---------------- curio / obstacle / trap prompt (DD1 "sidebar scroll" at 1348,200) ----------------

    private void DrawPrompt(Crawl crawl, ExpeditionState exp)
    {
        if (exp.Camp != null) return;
        var tile = crawl.CurrentTile;
        string curio = crawl.CurioHere;
        bool obstacle = tile is { Content: HallContent.Obstacle, Resolved: false };
        bool trap = tile is { Content: HallContent.Trap, Resolved: false } && tile.Scouted;
        bool battleStuck = crawl.IsBlocked && (exp.InRoom || tile?.Content == HallContent.Battle);
        if (curio == null && !obstacle && !trap && !battleStuck) return;

        var r = new Rect(1460, 150, 420, 400);
        Gui.Fill(r, new Color(0.05f, 0.04f, 0.03f, 0.93f));
        Gui.Fill(new Rect(r.x, r.y, r.width, 3), Gui.Gold);
        Gui.Fill(new Rect(r.x, r.yMax - 3, r.width, 3), Gui.Gold);
        float y = r.y + 18;

        if (battleStuck)
        {
            Gui.Text(new Rect(r.x + 20, y, r.width - 40, 40), "Enemies!", 34, Gui.Blood, TextAnchor.UpperCenter, heading: true);
            if (Gui.DdButton(new Rect(r.x + 90, y + 80, 240, 56), "Fight")) D.Fight();
            return;
        }

        string who = S.Save.Estate.Hero(D.SelectedHeroId)?.Name ?? "";
        if (curio != null)
        {
            Gui.Text(new Rect(r.x + 20, y, r.width - 40, 40), HamletUi.Pretty(curio), 32, Gui.Dd1Name, TextAnchor.UpperCenter, heading: true);
            Gui.Text(new Rect(r.x + 24, y + 48, r.width - 48, 60), $"{who} will investigate. Select another hero to send them instead.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            string needed = crawl.QuestItemNeededHere;
            if (Gui.DdButton(new Rect(r.x + 30, y + 120, 170, 52), needed != null ? "Use item" : "Investigate"))
            { D.Investigate(D.SelectedHeroId, needed); return; }
            if (Gui.DdButton(new Rect(r.xMax - 200, y + 120, 170, 52), "Leave it")) { D.SkipCurio(); return; }

            // Supplies that do something here (DD1's item slot): their inventory art as buttons.
            int k = 0;
            foreach (var item in S.Content.Curios.UsefulItems(curio).Where(it => exp.Pack.Count(it) > 0).Take(4))
            {
                var ir = new Rect(r.x + 40 + k++ * 90, y + 196, 54, 108);
                var icon = Art.InventoryIcon(item, 1, 1);
                if (icon != null) GUI.DrawTexture(ir, icon); else Gui.Fill(ir, Gui.Dim);
                if (Gui.Hotspot(ir)) { D.Investigate(D.SelectedHeroId, item); return; }
            }
            if (k > 0) Gui.Text(new Rect(r.x + 20, r.yMax - 46, r.width - 40, 30), "Or use a supply on it", 18, Gui.Dim, TextAnchor.UpperCenter);
        }
        else if (obstacle)
        {
            Gui.Text(new Rect(r.x + 20, y, r.width - 40, 40), HamletUi.Pretty(tile.ContentId), 32, Gui.Dd1Name, TextAnchor.UpperCenter, heading: true);
            bool shovel = exp.Pack.Count(Supply.Shovel) > 0;
            Gui.Text(new Rect(r.x + 24, y + 48, r.width - 48, 70), shovel ? "A shovel will clear the way." : "Without a shovel the party must force its way through: hurt and stressed.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (Gui.DdButton(new Rect(r.x + 90, y + 130, 240, 56), shovel ? "Use a shovel" : "Clear by hand")) { D.ClearObstacle(); return; }
        }
        else if (trap)
        {
            Gui.Text(new Rect(r.x + 20, y, r.width - 40, 40), HamletUi.Pretty(tile.ContentId), 32, Gui.Dd1Name, TextAnchor.UpperCenter, heading: true);
            Gui.Text(new Rect(r.x + 24, y + 48, r.width - 48, 70), "A trap, spotted in time. The front hero can try to disarm it.", 19, Gui.Dd1Text, TextAnchor.UpperCenter);
            if (Gui.DdButton(new Rect(r.x + 90, y + 130, 240, 56), "Disarm")) { D.DisarmTrap(); return; }
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

    private static void DrawRespite(CampState camp)
    {
        Gui.Text(new Rect(660, 30, 600, 40), "Respite", 30, Gui.Dd1Class, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(660, 64, 600, 70), camp.RespiteLeft.ToString(), 64, Gui.Gold, TextAnchor.MiddleCenter, heading: true);
    }

    private static string Describe(CampEffect e)
    {
        string who = e.Selection switch { "self" => "self", "individual" => "one ally", "party" => "party", "party_other" => "the others", _ => e.Selection };
        string what = e.Type switch
        {
            "stress_heal_amount" => $"-{Gui.Num(e.Amount / 10f)} stress",
            "stress_damage_amount" => $"+{Gui.Num(e.Amount / 10f)} stress",
            "health_heal_max_health_percent" => $"heal {Gui.Num(e.Amount * 100f, "0")}% HP",
            "buff" => HamletUi.Pretty(e.SubType),
            "remove_bleeding" or "remove_bleed" => "cure bleeding",
            "remove_poison" => "cure blight",
            "remove_disease" => "cure a disease",
            "remove_deaths_door_recovery_buffs" => "shake off death's door",
            "reduce_ambush_chance" => "no ambush tonight",
            "loot" => "find supplies",
            _ => HamletUi.Pretty(e.Type),
        };
        return (e.Chance < 1f ? $"{Gui.Num(e.Chance * 100f, "0")}%: " : "") + what + $" ({who})";
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
            Gui.Text(new Rect(560, 150, 800, 34), $"{S.Save.Estate.Hero(hero)?.Name}'s camping skills", 24, Gui.Dd1Class, TextAnchor.MiddleCenter, heading: true);
            float w = skills.Count * 92 - 12, x0 = 960 - w / 2;
            string hovered = null;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = S.Content.Camping.Get(skills[i]);
                if (skill == null) continue;
                var r = new Rect(x0 + i * 92, 190, 80, 80);
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
        else Gui.Text(new Rect(560, 190, 800, 40), "Click a hero to see their camping skills.", 24, Gui.Dd1Class, TextAnchor.MiddleCenter);

        if (_campSkillPending != null && Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            _campSkillPending = null;    // right-click cancels the targeting
            Event.current.Use();
        }
        if (Gui.DdButton(new Rect(1620, 600, 270, 60), "Break camp", true, 26))
        {
            _campSkillPending = null;
            D.BreakCamp();
        }
    }

    private static void DrawMealChoice(Crawl crawl, ExpeditionState exp)
    {
        // DD1 starts the night with a meal: how much food to share out.
        var r = new Rect(560, 150, 800, 230);
        Gui.Fill(r, new Color(0.03f, 0.025f, 0.02f, 0.92f));
        Gui.Fill(new Rect(r.x, r.y, r.width, 2), new Color(0.45f, 0.38f, 0.24f));
        Gui.Fill(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(0.45f, 0.38f, 0.24f));
        Gui.Text(new Rect(r.x, r.y + 10, r.width, 44), "The meal", 36, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        int i = 0, food = exp.Pack.Count(Supply.Food);
        foreach (Meal m in Enum.GetValues(typeof(Meal)))
        {
            int cost = crawl.MealCost(m);
            var b = new Rect(r.x + 30 + i * 192, r.y + 70, 180, 140);
            i++;
            bool can = food >= cost;
            bool hover = can && b.Contains(Event.current.mousePosition);
            Gui.Fill(b, hover ? new Color(0.2f, 0.16f, 0.1f, 0.9f) : new Color(0.08f, 0.07f, 0.06f, 0.9f));
            var icon = cost > 0 ? Art.InventoryIcon(Supply.Food, Mathf.Max(1, cost), 12) : null;
            if (icon != null) GUI.DrawTexture(new Rect(b.x + 10, b.y + 6, 64, 128), icon, ScaleMode.ScaleToFit);
            string name = cost == 0 ? "Go hungry" : m.ToString();
            Gui.Text(new Rect(b.x + 74, b.y + 20, 104, 40), name, 26, can ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(b.x + 74, b.y + 62, 104, 60), cost == 0 ? "No food" : $"{cost} food", 20, can ? Gui.Dd1Text : Gui.Dim, TextAnchor.UpperLeft);
            if (can && Gui.Hotspot(b)) D.EatMeal(m);
        }
    }

    private static void DrawCampSkillTip(Crawl crawl, ExpeditionState exp, string hero, string skillId)
    {
        var skill = S.Content.Camping.Get(skillId);
        string why = crawl.WhyCantUseCampSkill(hero, skillId);
        exp.Camp.Uses.TryGetValue(hero + ":" + skillId, out int used);
        var lines = skill.Effects.Select(Describe).ToList();
        if (skill.UseLimit > 0) lines.Add($"Uses: {used}/{skill.UseLimit}");
        if (why != null) lines.Add(why);
        var tip = new Rect(660, 286, 600, 52 + 26 * lines.Count);
        Gui.Fill(tip, new Color(0.03f, 0.025f, 0.02f, 0.94f));
        Gui.Text(new Rect(tip.x + 14, tip.y + 6, tip.width - 28, 34), $"{Dd1Text.CampSkillName(skillId)}  ·  {skill.Cost} respite", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        for (int i = 0; i < lines.Count; i++)
            Gui.Text(new Rect(tip.x + 14, tip.y + 44 + i * 26, tip.width - 28, 26), lines[i], 19, i < skill.Effects.Count ? Gui.Dd1Text : Gui.Dd1Class, TextAnchor.MiddleLeft);
    }
}
