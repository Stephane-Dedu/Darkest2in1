using System.Linq;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>The DD1 dungeon: a side view of the current square, the party, the torch, the map and the actions.</summary>
internal sealed class CrawlUi
{
    private string _curioHero;
    private string _campHero, _campTarget;
    private Vector2 _logScroll;

    private static Session S => Session.Current;
    private static Driver D => Driver.Instance;

    public void Draw()
    {
        var crawl = D.Crawl;
        var exp = D.Expedition;
        if (crawl == null || exp == null) return;
        string zone = exp.Quest.Dungeon;

        DrawScene(new Rect(0, 0, Gui.W, 620), crawl, zone);
        DrawParty(new Rect(40, 380, 1100, 220));
        DrawTorch(new Rect(560, 20, 800, 40), exp.Light);
        DrawMap(new Rect(1260, 630, 650, 440), exp.Map, exp);
        DrawActions(new Rect(10, 630, 640, 440), crawl, exp);
        DrawLog(new Rect(660, 630, 590, 440));
        if (exp.Camp != null) DrawCamp(new Rect(360, 90, 1200, 520), crawl, exp);
    }

    private static void DrawScene(Rect r, Crawl crawl, string zone)
    {
        Gui.Fill(r, Color.black);
        Gui.Image(r, Art.CorridorBackground(zone));
        Texture wall = crawl.State.InRoom
            ? (crawl.State.RoomId == crawl.State.Map.EntranceRoomId ? Art.EntranceWall(zone) : Art.RoomWall(zone, RoomKind(crawl.State.RoomId)))
            : Art.CorridorWall(zone, crawl.State.TileIndex + crawl.State.CorridorId);
        Gui.Image(r, wall);
        // DD1's darkness: the lower the torch, the darker the scene.
        float dark = Mathf.Clamp01((60f - crawl.State.Light) / 120f);
        if (dark > 0) Gui.Fill(r, new Color(0, 0, 0, dark));

        string where = crawl.State.InRoom
            ? (crawl.State.RoomId == crawl.State.Map.EntranceRoomId ? "The entrance" : "A room")
            : $"Corridor, square {crawl.State.TileIndex + 1}";
        Gui.Panel(new Rect(20, 16, 520, 48));
        Gui.Label(new Rect(34, 24, 500, 40), $"{S.Zones.ZoneName(zone)} — {where}");
        Gui.Panel(new Rect(1380, 16, 520, 48));
        Gui.Label(new Rect(1394, 24, 500, 40), $"{crawl.State.Quest}{(crawl.State.QuestComplete ? Gui.Colour("  ✔ complete", Gui.Gold) : "")}");
    }

    private static readonly string[] RoomKinds = { "altar", "arch", "barrels", "drain", "empty", "library", "torture" };
    private static string RoomKind(int roomId) => RoomKinds[(roomId * 7 + 3) % RoomKinds.Length];

    private static void DrawParty(Rect area)
    {
        var exp = D.Expedition;
        // DD1 shows the front rank on the right; so do we.
        for (int i = 0; i < exp.Party.Count; i++)
        {
            string id = exp.Party[i];
            var hero = S.Save.Estate.Hero(id);
            var actor = Dd2Api.Actor(D.Party?.Guid(id) ?? 0);
            float x = area.xMax - (i + 1) * 270;
            var r = new Rect(x, area.y, 260, area.height);
            Gui.Panel(r);
            bool dead = actor == null || Dd2Api.IsDead(D.Party.Guid(id));
            Gui.Label(new Rect(r.x + 10, r.y + 6, 240, 60), $"<b>{hero?.Name}</b>\n{Gui.Colour(HamletUi.Pretty(hero?.ClassId), Gui.Dim)}");
            if (dead) { Gui.Label(new Rect(r.x + 10, r.y + 80, 240, 40), Gui.Colour("Dead", Gui.Blood)); continue; }
            Gui.Small(new Rect(r.x + 10, r.y + 74, 240, 26), $"HP {actor.HpRounded:0}/{actor.CurrentHpMax:0}");
            Gui.Bar(new Rect(r.x + 10, r.y + 100, 240, 16), Dd2Api.HpFraction(actor), Gui.Blood);
            Gui.Small(new Rect(r.x + 10, r.y + 122, 240, 26), $"Stress {actor.Stress:0}/{actor.StressMax:0}");
            Gui.Bar(new Rect(r.x + 10, r.y + 148, 240, 16), actor.StressMax > 0 ? actor.Stress / actor.StressMax : 0, new Color(0.85f, 0.85f, 0.85f));
            var buffs = exp.PendingBuffs.TryGetValue(id, out var b) ? b.Count : 0;
            if (buffs > 0) Gui.Small(new Rect(r.x + 10, r.y + 170, 240, 26), Gui.Colour($"{buffs} camp/curio buff(s)", Gui.Gold));
        }
    }

    private static void DrawTorch(Rect r, float light)
    {
        Gui.Panel(new Rect(r.x - 10, r.y - 6, r.width + 20, r.height + 12));
        Gui.Bar(new Rect(r.x, r.y + 4, r.width - 220, r.height - 8), light / 100f, Gui.Gold);
        Gui.Label(new Rect(r.xMax - 210, r.y + 2, 210, r.height), $"{Core.Expedition.CrawlRules.BandName(light)} ({light:0})");
    }

    private static void DrawMap(Rect area, DungeonMap map, ExpeditionState exp)
    {
        Gui.Panel(area);
        int minX = map.Rooms.Min(r => r.X), maxX = map.Rooms.Max(r => r.X);
        int minY = map.Rooms.Min(r => r.Y), maxY = map.Rooms.Max(r => r.Y);
        float cell = Mathf.Min((area.width - 60) / (maxX - minX + 1), (area.height - 60) / (maxY - minY + 1));
        Vector2 P(int x, int y) => new(area.x + 30 + (x - minX) * cell, area.y + 30 + (y - minY) * cell);

        foreach (var c in map.Corridors)
            foreach (var t in c.Tiles)
            {
                var p = P(t.X, t.Y);
                bool here = !exp.InRoom && exp.CorridorId == c.Id && exp.TileIndex == t.Index;
                bool known = t.Visited || t.Scouted;
                var col = here ? Color.white : !known ? new Color(0.25f, 0.22f, 0.2f)
                    : t.Content switch
                    {
                        HallContent.Battle when !t.Resolved => Gui.Blood,
                        HallContent.Trap when !t.Resolved => new Color(0.8f, 0.4f, 0.1f),
                        HallContent.Obstacle when !t.Resolved => new Color(0.5f, 0.4f, 0.3f),
                        HallContent.Curio when !t.Resolved => Gui.Gold,
                        _ => t.Visited ? new Color(0.6f, 0.55f, 0.45f) : new Color(0.4f, 0.37f, 0.32f),
                    };
                Gui.Fill(new Rect(p.x + cell * 0.3f, p.y + cell * 0.3f, cell * 0.4f, cell * 0.4f), col);
            }

        foreach (var room in map.Rooms)
        {
            var p = P(room.X, room.Y);
            var r = new Rect(p.x, p.y, cell, cell);
            bool here = exp.InRoom && exp.RoomId == room.Id;
            bool neighbour = exp.InRoom && map.Neighbours(exp.RoomId).Contains(room.Id);
            bool known = room.Visited || room.Scouted || neighbour;
            Gui.Fill(r, here ? Color.white : !known ? new Color(0.2f, 0.18f, 0.16f)
                : room.Content == RoomContent.Boss && !room.Cleared ? Gui.Blood
                : room.HasBattle && !room.Cleared && room.Scouted ? new Color(0.5f, 0.15f, 0.12f)
                : room.Visited ? new Color(0.55f, 0.5f, 0.42f) : new Color(0.35f, 0.32f, 0.28f));
            if (room.IsQuestGoal && !room.CurioTaken) Gui.Fill(new Rect(r.x + cell * 0.35f, r.y + cell * 0.35f, cell * 0.3f, cell * 0.3f), Gui.Gold);
            if (neighbour && !D.Crawl.IsBlocked && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                D.Travel(room.Id);
                return;
            }
        }
        Gui.Small(new Rect(area.x + 10, area.yMax - 30, area.width - 20, 26), "Click a neighbouring room to walk there.");
    }

    private void DrawActions(Rect area, Crawl crawl, ExpeditionState exp)
    {
        Gui.Panel(area);
        float x = area.x + 14, y = area.y + 14, w = 300, h = 58;
        int n = 0;
        bool Btn(string label, bool enabled = true)
        {
            var r = new Rect(x + (n % 2) * (w + 12), y + (n / 2) * (h + 8), w, h);
            n++;
            return Gui.Button(r, label, enabled);
        }

        // Every action can move the party (corridor ↔ room) or end the crawl, so after one runs this frame's
        // drawing stops: the next OnGUI pass redraws from the new state.
        var tile = crawl.CurrentTile;
        if (crawl.IsBlocked && (exp.InRoom || tile?.Content == HallContent.Battle))
        {
            if (Btn(Gui.Colour("<b>Fight!</b>", Gui.Blood))) { D.Fight(); return; }
        }
        else if (!exp.InRoom && tile != null)
        {
            if (Btn("Walk forward ▶")) { D.Step(true); return; }
            if (Btn("◀ Back up")) { D.Step(false); return; }
            if (tile.Content == HallContent.Obstacle && !tile.Resolved && Btn($"Clear obstacle ({(exp.Pack.Count(Supply.Shovel) > 0 ? "shovel" : "by hand")})")) { D.ClearObstacle(); return; }
            if (tile.Content == HallContent.Trap && !tile.Resolved && Btn("Disarm the trap")) { D.DisarmTrap(); return; }
        }

        if (Btn($"Light a torch ({exp.Pack.Count(Supply.Torch)})", exp.Pack.Count(Supply.Torch) > 0 && exp.Light < 100)) { D.UseTorch(); return; }
        if (crawl.CanCamp && Btn($"Make camp ({exp.Pack.Count(Supply.Firewood)} firewood)")) { D.MakeCamp(); return; }
        if (Btn(exp.QuestComplete ? Gui.Colour("<b>Return to the Hamlet</b>", Gui.Gold) : "Retreat", exp.InRoom || exp.QuestComplete)) { D.Leave(); return; }

        // Curio here: pick who touches it, and optionally an item.
        string curio = crawl.CurioHere;
        if (curio != null)
        {
            float cy = area.y + 290;
            Gui.Label(new Rect(x, cy, 600, 30), $"A curio: <b>{HamletUi.Pretty(curio)}</b>");
            var alive = D.Party.Alive;
            _curioHero ??= alive.FirstOrDefault();
            for (int i = 0; i < alive.Count; i++)
                if (Gui.Button(new Rect(x + i * 152, cy + 34, 146, 40), (alive[i] == _curioHero ? "▶" : "") + S.Save.Estate.Hero(alive[i])?.Name)) _curioHero = alive[i];
            string needed = crawl.QuestItemNeededHere;
            if (Gui.Button(new Rect(x, cy + 80, 200, 46), needed != null ? $"Use {HamletUi.Pretty(needed)}" : "Investigate")) { D.Investigate(_curioHero, needed); return; }
            int k = 0;
            foreach (var item in S.Content.Curios.UsefulItems(curio).Where(it => exp.Pack.Count(it) > 0).Take(2))
                if (Gui.Button(new Rect(x + 210 + k++ * 200, cy + 80, 194, 46), "Use " + HamletUi.Pretty(item))) { D.Investigate(_curioHero, item); return; }
            if (Gui.Button(new Rect(x + 420, cy + 80, 190, 46), "Leave it")) { D.SkipCurio(); return; }
        }

        Gui.Small(new Rect(x, area.yMax - 34, 620, 30),
            "Pack: " + string.Join(", ", exp.Pack.Items.Where(kv => kv.Value > 0).Select(kv => $"{HamletUi.Pretty(kv.Key)} {kv.Value}")));
    }

    private void DrawLog(Rect area)
    {
        Gui.Panel(area);
        var log = D.Log;
        _logScroll = GUI.BeginScrollView(new Rect(area.x + 10, area.y + 10, area.width - 20, area.height - 20), _logScroll,
            new Rect(0, 0, area.width - 50, log.Count * 30));
        for (int i = 0; i < log.Count; i++) Gui.Small(new Rect(0, i * 30, area.width - 50, 28), log[i]);
        GUI.EndScrollView();
        if (Event.current.type == EventType.Repaint) _logScroll.y = log.Count * 30;
    }

    private void DrawCamp(Rect area, Crawl crawl, ExpeditionState exp)
    {
        Gui.Panel(area);
        var camp = exp.Camp;
        Gui.Title(new Rect(area.x + 20, area.y + 10, 800, 50), $"Camp — respite {camp.RespiteLeft}");
        if (!camp.Ate)
        {
            int i = 0;
            foreach (Meal m in System.Enum.GetValues(typeof(Meal)))
            {
                int cost = crawl.MealCost(m);
                if (Gui.Button(new Rect(area.x + 20 + i++ * 220, area.y + 70, 210, 50), $"{m} meal ({cost} food)", exp.Pack.Count(Supply.Food) >= cost))
                    D.EatMeal(m);
            }
        }
        var alive = D.Party.Alive;
        for (int i = 0; i < alive.Count; i++)
            if (Gui.Button(new Rect(area.x + 20 + i * 230, area.y + 140, 220, 44), (_campHero == alive[i] ? "▶ " : "") + S.Save.Estate.Hero(alive[i])?.Name))
                _campHero = alive[i];
        if (_campHero != null && exp.CampSkills.TryGetValue(_campHero, out var skills))
        {
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = S.Content.Camping.Get(skills[i]);
                if (skill == null) continue;
                string why = crawl.WhyCantUseCampSkill(_campHero, skill.Id);
                if (Gui.Button(new Rect(area.x + 20 + (i % 2) * 400, area.y + 200 + (i / 2) * 56, 390, 50),
                        $"{HamletUi.Pretty(skill.Id)} ({skill.Cost}){(why != null ? " — " + why : "")}", why == null))
                {
                    if (skill.NeedsTarget && _campTarget == null) D.Say("Choose a target first (below).");
                    else D.UseCampSkill(_campHero, skill.Id, _campTarget);
                }
            }
            Gui.Small(new Rect(area.x + 20, area.y + 380, 300, 30), "Target:");
            for (int i = 0; i < alive.Count; i++)
                if (Gui.Button(new Rect(area.x + 110 + i * 200, area.y + 376, 190, 40), (_campTarget == alive[i] ? "▶ " : "") + S.Save.Estate.Hero(alive[i])?.Name))
                    _campTarget = alive[i];
        }
        if (Gui.Button(new Rect(area.xMax - 300, area.yMax - 70, 280, 56), "Break camp")) D.BreakCamp();
    }
}
