using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// Embarking, DD1 style, in DD1's two steps. Quest select: the estate map (campaign/town/quest_select) with each
/// dungeon's quests at its spot, the quest scroll on the left, the party slots at the bottom and the roster on the
/// right. Provisions: the provisioner's window, the store and the party's pack, then off to the dungeon.
/// </summary>
internal sealed class EmbarkUi
{
    private QuestOffer _quest;
    private readonly List<string> _party = new();      // front rank first
    private readonly Inventory _cart = new();
    private string _error;
    private bool _confirmLow, _provisioning;
    private readonly Dictionary<string, string> _mapAreas = new();
    private string _focusedZone;
    private Vector2 _questListScroll;

    public bool WantsBack;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;

    private static Texture2D Qs(string file) => Art.Dd1("campaign", "town", "quest_select", file);
    private static Texture2D Prov(string file) => Art.Dd1("campaign", "town", "provision", file);

    public void Draw()
    {
        _party.RemoveAll(id => E.Hero(id) == null);
        // While a hero sheet is open, the screen under it is only painted: clicks belong to the sheet.
        if (_sheetHeroId == null || Event.current.type == EventType.Repaint)
        {
            if (_provisioning) DrawProvisioner(); else DrawQuestSelect();
        }
        DrawSheet();
        if (_error != null) Gui.Text(new Rect(560, 1040, 900, 36), _error, 22, Gui.Blood, TextAnchor.MiddleCenter);
    }

    // ================================================================ quest select

    private static Dictionary<string, Vector2> _mapSpots;

    /// <summary>Where DD1 draws each dungeon on the estate map (quest_select.layout.darkest), nudged left so the
    /// rightmost quests clear the roster.</summary>
    private static Dictionary<string, Vector2> MapSpots()
    {
        if (_mapSpots != null) return _mapSpots;
        _mapSpots = new Dictionary<string, Vector2>();
        try
        {
            foreach (var r in DarkestFile.Load(S.Dd1.PathOf("campaign", "town", "quest_select", "quest_select.layout.darkest")))
            {
                const string prefix = "quest_select_dungeon_layout_";
                if (!r.Type.StartsWith(prefix) || !r.Has("quest_map_pos")) continue;
                _mapSpots[r.Type.Substring(prefix.Length)] = new Vector2(r.Float("quest_map_pos", 0) - 120, r.Float("quest_map_pos", 1));
            }
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[embark] quest map layout: " + e.Message); }
        return _mapSpots;
    }

    private string _sheetHeroId;   // DD1: right-click a hero for their sheet (trinkets can be changed here)

    /// <summary>The hero sheet over the quest select, if open. True while it is.</summary>
    private bool DrawSheet()
    {
        if (_sheetHeroId == null || E.Hero(_sheetHeroId) is not { } h) { _sheetHeroId = null; return false; }
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0, 0, 0, 0.55f));
        HeroSheet.Draw(h, id => _sheetHeroId = id, () => _sheetHeroId = null);
        return true;
    }

    private void DrawQuestSelect()
    {
        var bg = Qs("quest_select.background.png");
        if (bg != null) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), bg); else Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0.05f, 0.04f, 0.05f));
        Gui.Text(new Rect(560, 24, 900, 60), "Choose a quest", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);

        if (_quest != null && !E.Quests.Contains(_quest)) _quest = null;
        if (_focusedZone == null || (_focusedZone != QuestBoard.DarkestDungeon && !CampaignRegions.Enabled(E, _focusedZone)))
            FocusArea(_quest?.Dungeon ?? E.Quests.FirstOrDefault()?.Dungeon);

        // Each pair shares the original estate-map position. Only the focused area's contracts are drawn below
        // the map, so enabling optional regions never puts their controls under the quest-detail scroll.
        var spots = MapSpots();
        foreach (var location in CampaignRegions.Legacy)
        {
            var areas = CampaignRegions.AtLocation(E, location);
            if (areas.Count == 0 || !spots.TryGetValue(location, out var pos)) continue;
            if (!_mapAreas.TryGetValue(location, out var zone) || !areas.Contains(zone))
                _mapAreas[location] = zone = areas[0];
            DrawDungeon(zone, pos, areas);
        }
        if (E.Quests.Any(q => q.Dungeon == QuestBoard.DarkestDungeon) && spots.TryGetValue(QuestBoard.DarkestDungeon, out var darkestPos))
            DrawDungeon(QuestBoard.DarkestDungeon, darkestPos, new[] { QuestBoard.DarkestDungeon });

        DrawAreaQuests();
        if (_quest != null) DrawQuestScroll(_quest);

        DrawPartySlots(new Vector2(754, 900));

        var clicked = RosterColumn.Draw(E, S.Buildings.RosterSize(E), h =>
        {
            bool inParty = _party.Contains(h.Id);
            string note = inParty ? "In the party"
                : h.MissingWeeks > 0 ? "Missing"
                : !h.IsAvailable ? "Busy this week"
                : _quest != null && !Homecoming.WillEmbark(h, _quest, S.Hamlet.AnyResolveCanEmbark) ? "Refuses this quest"
                : null;
            return new RosterColumn.Look(dim: inParty || note != null, note: inParty ? null : note, highlight: inParty);
        }, draggable: true);
        if (clicked != null) Toggle(clicked);
        if (RosterColumn.RightClickedHero != null) _sheetHeroId = RosterColumn.RightClickedHero.Id;
        // A hero dragged out of the party back onto the roster leaves it.
        if (Drag.Drop<HeroDrag>(RosterColumn.Area, out var back) && back.FromSlot >= 0) _party.Remove(back.HeroId);

        if (Gui.DdButton(new Rect(30, 1000, 300, 60), "Back to the Hamlet", size: 24)) WantsBack = true;
        var heroes = Heroes();
        string why = Embark.WhyCantEmbark(E, _quest, heroes, S.Hamlet.AnyResolveCanEmbark);
        if (Gui.DdButton(new Rect(1190, 905, 330, 90), why == null ? "Provision" : why, why == null, why == null ? 40 : 18))
        {
            _provisioning = true;
            _error = null;
        }
    }

    /// <summary>A hero dropped on a rank: from the roster it joins (whoever held the rank goes back), from another
    /// rank the two swap. Ranks stay packed from the front, as the party list has no gaps.</summary>
    private void DropHero(HeroDrag drag, int rank)
    {
        var h = E.Hero(drag.HeroId);
        if (h == null) return;
        if (drag.FromSlot >= 0)
        {
            int from = _party.IndexOf(h.Id);
            if (from < 0) return;
            int to = Mathf.Min(rank, _party.Count - 1);
            (_party[from], _party[to]) = (_party[to], _party[from]);
            return;
        }
        if (_party.Contains(h.Id) || !h.IsAvailable || h.MissingWeeks > 0) return;
        if (_quest != null && !Homecoming.WillEmbark(h, _quest, S.Hamlet.AnyResolveCanEmbark)) { _error = $"{h.Name} refuses this quest."; return; }
        if (rank < _party.Count) _party[rank] = h.Id;          // replaces whoever stood there
        else if (_party.Count < 4) _party.Add(h.Id);
        _error = null;
    }

    private void Toggle(HeroRecord h)
    {
        if (_party.Remove(h.Id)) return;
        if (_party.Count >= 4 || !h.IsAvailable || h.MissingWeeks > 0) return;
        if (_quest != null && !Homecoming.WillEmbark(h, _quest, S.Hamlet.AnyResolveCanEmbark)) return;
        _party.Add(h.Id);
    }

    private List<HeroRecord> Heroes() => _party.Select(E.Hero).Where(h => h != null).ToList();

    private void FocusArea(string zone)
    {
        if (_focusedZone != zone) _questListScroll = Vector2.zero;
        _focusedZone = zone;
        if (zone != null) _mapAreas[ZoneBase.Of(zone)] = zone;
        if (_quest?.Dungeon != zone || !E.Quests.Contains(_quest))
            SelectQuest(E.Quests.FirstOrDefault(q => q.Dungeon == zone));
    }

    private void SelectQuest(QuestOffer quest)
    {
        _quest = quest;
        if (quest != null) _party.RemoveAll(id => !Homecoming.WillEmbark(E.Hero(id), quest, S.Hamlet.AnyResolveCanEmbark));
        _error = null;
    }

    private void DrawDungeon(string zone, Vector2 pos, IReadOnlyList<string> areas)
    {
        var plate = Qs("dungeon_progressionbar.png");
        if (plate != null) GUI.DrawTexture(new Rect(pos.x - 5, pos.y - 10, 282, 84), plate);
        else Gui.Fill(new Rect(pos.x - 5, pos.y - 10, 282, 84), new Color(0, 0, 0, 0.8f));
        Gui.Text(new Rect(pos.x + 4, pos.y - 6, 190, 30), S.Zones.ZoneName(zone), 24, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);

        E.ZoneXp.TryGetValue(zone, out int xp);
        int level = S.Campaign.ZoneLevel(xp);
        var thresholds = S.Campaign.ZoneLevelThresholds;
        Gui.Text(new Rect(pos.x + 199, pos.y + 2, 40, 36), level.ToString(), 26, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
        if (thresholds.Count > level + 1)
        {
            float from = thresholds[level], to = thresholds[level + 1];
            Gui.Fill(new Rect(pos.x + 14, pos.y + 32, 194 * Mathf.Clamp01((xp - from) / Mathf.Max(1, to - from)), 6), new Color(0.75f, 0.62f, 0.3f));
        }
        bool unlocked = zone == QuestBoard.DarkestDungeon || CampaignRegions.Unlocked(E, S.Campaign, zone);
        int count = E.Quests.Count(q => q.Dungeon == zone);
        string status = unlocked ? $"{count} quest{(count == 1 ? "" : "s")}" : $"Opens after {CampaignRegions.UnlockAfter(S.Campaign, zone)} quests";
        Gui.Text(new Rect(pos.x + 8, pos.y + 44, 258, 26), status, 16, unlocked ? Gui.Dd1Text : Gui.Dim, TextAnchor.MiddleLeft);
        if (_focusedZone == zone) Gui.Fill(new Rect(pos.x + 8, pos.y + 72, 258, 2), Gui.Gold);
        if (Gui.Hotspot(new Rect(pos.x - 5, pos.y - 10, 282, 84))) FocusArea(zone);

        if (areas.Count > 1)
        {
            int at = areas.ToList().IndexOf(zone);
            var prev = new Rect(pos.x - 44, pos.y + 8, 32, 40);
            var next = new Rect(pos.x + 284, pos.y + 8, 32, 40);
            Gui.Image(prev, Art.Dd1("shared", "character", "previous_hero.png"), ScaleMode.ScaleToFit);
            Gui.Image(next, Art.Dd1("shared", "character", "next_hero.png"), ScaleMode.ScaleToFit);
            if (prev.Contains(Event.current.mousePosition)) Gui.Tip("Previous area");
            if (next.Contains(Event.current.mousePosition)) Gui.Tip("Next area");
            if (Gui.Hotspot(prev)) FocusArea(areas[(at - 1 + areas.Count) % areas.Count]);
            if (Gui.Hotspot(next)) FocusArea(areas[(at + 1) % areas.Count]);
        }
    }

    private void DrawAreaQuests()
    {
        var strip = new Rect(570, 650, 980, 180);
        Gui.Fill(strip, new Color(0.025f, 0.02f, 0.025f, 0.9f));
        Gui.Fill(new Rect(strip.x, strip.y, strip.width, 2), Gui.Dd1Name);
        string title = _focusedZone == null ? "Select an area on the map" : S.Zones.ZoneName(_focusedZone);
        Gui.Text(new Rect(strip.x + 18, strip.y + 8, strip.width - 36, 34), title, 25, Gui.Dd1Name, heading: true);
        var quests = E.Quests.Where(q => q.Dungeon == _focusedZone).ToList();
        if (quests.Count == 0)
        {
            string why = _focusedZone != null && !CampaignRegions.Unlocked(E, S.Campaign, _focusedZone)
                ? $"Complete {CampaignRegions.UnlockAfter(S.Campaign, _focusedZone)} expeditions to open this area."
                : "No contracts available here this week.";
            Gui.Text(new Rect(strip.x + 24, strip.y + 60, strip.width - 48, 56), why, 22, Gui.Dd1Class);
            return;
        }
        var view = new Rect(strip.x + 8, strip.y + 48, strip.width - 16, 124);
        _questListScroll = GUI.BeginScrollView(view, _questListScroll,
            new Rect(0, 0, view.width - 20, Mathf.Max(view.height, ((quests.Count + 4) / 5) * 114)));
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            var cell = new Rect(4 + (i % 5) * 188, 4 + (i / 5) * 114, 180, 106);
            if (q == _quest) Gui.Fill(cell, new Color(0.35f, 0.28f, 0.12f, 0.4f));
            var c = new Vector2(cell.x + 40, cell.y + 48);
            var r = new Rect(c.x - 36, c.y - 36, 72, 72);
            bool hover = cell.Contains(Event.current.mousePosition);
            var ring = Qs($"quest_select_length_{(q.IsPlot ? "plot" : "generated")}_{Mathf.Clamp(q.Length, 0, 4)}.png") ?? Qs($"quest_select_length_generated_{Mathf.Clamp(q.Length, 0, 5)}.png");
            if (ring != null) GUI.DrawTexture(hover ? new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8) : r, ring);
            int tier = q.Difficulty >= 6 ? 6 : q.Difficulty >= 5 ? 5 : q.Difficulty >= 3 ? 3 : 1;
            var badge = Qs($"quest_select_{q.Type}_{tier}.png") ?? Qs($"quest_select_explore_{tier}.png");
            if (badge != null) GUI.DrawTexture(new Rect(c.x - 20, c.y - 20, 40, 40), badge);
            Gui.Text(new Rect(cell.x + 82, cell.y + 7, 94, 91), $"{q.DifficultyName}\n{Cap(q.Size)}\n{HamletUi.Pretty(q.Type)}", 15, Gui.Dd1Text);
            if (Gui.Hotspot(cell)) SelectQuest(q);
        }
        GUI.EndScrollView();
    }

    private static string GoalLine(QuestOffer q)
    {
        S.Campaign.Goals.Goals.TryGetValue(q.GoalId ?? "", out var goal);
        return q.Type switch
        {
            "explore" => "Explore 90% of the rooms.",
            "cleanse" => "Clear every room battle.",
            "kill_boss" => $"Find and slay the {HamletUi.Pretty(ZoneEncounters.BossKey(q.BossId))}.",
            "gather" when goal != null => $"Gather {goal.Amount} {HamletUi.Pretty(goal.QuestItem ?? goal.CurioName)} from the {HamletUi.Pretty(goal.CurioName)}s.",
            "activate" or "inventory_activate" when goal != null =>
                $"Find {goal.Amount} {HamletUi.Pretty(goal.CurioName)}" + (goal.NeedsItem ? $" and use the {HamletUi.Pretty(goal.StartingItems[0].Id)} you carry on them." : " and activate them."),
            _ => HamletUi.Pretty(q.Type),
        };
    }

    private void DrawQuestScroll(QuestOffer q)
    {
        var at = new Vector2(125, 132);
        var scroll = Qs("quest_select.questverbose_bg.png");
        if (scroll != null) GUI.DrawTexture(new Rect(at.x, at.y, 400, 843), scroll);
        else Gui.Fill(new Rect(at.x, at.y, 400, 843), new Color(0, 0, 0, 0.85f));

        Gui.Text(new Rect(at.x + 20, at.y + 120, 360, 44), S.Zones.ZoneName(q.Dungeon), 34, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(at.x + 20, at.y + 160, 360, 30), $"{q.DifficultyName} · {Cap(q.Size)} · {HamletUi.Pretty(q.Type)}{(q.IsPlot ? " · Plot" : "")}", 20, Gui.Dd1Class, TextAnchor.MiddleCenter);
        Gui.Text(new Rect(at.x + 34, at.y + 200, 340, 160),
            (q.IsPlot ? "A quest of consequence. Its rewards are great, and so is its danger.\n\n" : "") +
            (Homecoming.MinResolveFor(q.Difficulty) > 0
                ? $"Only heroes of resolve {Homecoming.MinResolveFor(q.Difficulty)} or higher dare go."
                : $"Heroes of resolve {Homecoming.MaxResolveFor(q.Difficulty)} or lower will join; the experienced scorn easy work."), 18, Gui.Dd1Text);

        string camp = q.Length >= 3 ? "Two camps allowed" : q.Length == 2 ? "One camp allowed" : "No camping";
        Gui.Text(new Rect(at.x + 58, at.y + 376, 320, 28), camp, 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
        Gui.Text(new Rect(at.x + 34, at.y + 420, 340, 32), "Goal", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(at.x + 40, at.y + 452, 330, 80), GoalLine(q), 19, Gui.Dd1Text);

        Gui.Text(new Rect(at.x + 20, at.y + 520, 360, 32), "Rewards", 26, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        for (int i = 0; i < q.Rewards.Count && i < 8; i++)
        {
            var rw = q.Rewards[i];
            var r = new Rect(at.x + 42 + (i % 4) * 80, at.y + 562 + (i / 4) * 150, 72, 144);
            var icon = rw.Type == "trinket" ? Art.Dd1("panels", "icons_equip", "trinket", "inv_trinket+_unknown.png") ?? null : Art.InventoryIcon(rw.Type, rw.Amount, rw.Type == "gold" ? 1750 : 99);
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
            else Gui.Fill(r, new Color(0.1f, 0.08f, 0.06f, 0.9f));
            Gui.Text(new Rect(r.x - 6, r.yMax - 30, r.width + 12, 28), rw.Type == "trinket" ? HamletUi.Pretty(rw.Id ?? "trinket") : rw.Amount.ToString(), rw.Type == "trinket" ? 14 : 22, Color.white, TextAnchor.LowerCenter);
        }
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private void DrawPartySlots(Vector2 at)
    {
        var plate = Art.Dd1("campaign", "town", "embark_party", "embark_party.background.png");
        if (plate != null) GUI.DrawTexture(new Rect(at.x, at.y, 412, 113), plate); else Gui.Fill(new Rect(at.x, at.y, 412, 113), new Color(0, 0, 0, 0.8f));
        Gui.Text(new Rect(at.x, at.y - 40, 412, 36), "The party", 28, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var slotBg = Art.Dd1("campaign", "town", "hero_slot", "hero_slot.background.png");
        // DD1 lines the party up facing right: rank 4 on the left, rank 1 on the right.
        for (int s = 0; s < 4; s++)
        {
            int rank = 3 - s;
            var r = new Rect(at.x + 22 + s * 93, at.y + 16, 80, 80);
            if (s < _party.Count && CrawlUi.RightClicked(r)) _sheetHeroId = _party[s];
            if (slotBg != null) GUI.DrawTexture(r, slotBg);
            if (Drag.Hovering<HeroDrag>(r)) Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), Gui.Gold);
            if (Drag.Drop<HeroDrag>(r, out var dropped)) { DropHero(dropped, rank); break; }
            if (rank >= _party.Count) continue;
            var h = E.Hero(_party[rank]);
            string cls = h.ClassId;
            Drag.Source(r, new HeroDrag(h.Id, rank), rect =>
            {
                var icon = Art.HeroIcon(cls);
                if (icon != null) Art.DrawSprite(new Rect(rect.x + 4, rect.y + 4, 72, 72), icon);
            });
            bool carried = Drag.Payload is HeroDrag hd && hd.FromSlot == rank;
            var sprite = Art.HeroIcon(h.ClassId);
            if (sprite != null && !carried) Art.DrawSprite(new Rect(r.x + 4, r.y + 4, 72, 72), sprite);
            if (r.Contains(Event.current.mousePosition) && !Drag.Active)
                Gui.Text(new Rect(r.x - 70, r.yMax + 2, r.width + 140, 26), $"{h.Name}: drag to move, click to remove", 17, Gui.Dd1Text, TextAnchor.MiddleCenter);
            if (Gui.Hotspot(r) && !Drag.JustDropped) { _party.RemoveAt(rank); break; }
        }
    }

    // ================================================================ provisioner

    private static readonly Rect Window = new(144, 132, 1395, 776);

    private void DrawProvisioner()
    {
        var bg = Prov("provision.background.png");
        if (bg != null) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), bg); else Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0.04f, 0.03f, 0.03f));
        var window = Prov("provision.character_background.png");
        if (window != null) GUI.DrawTexture(Window, window);
        var keeper = Prov("provision.character.png");
        if (keeper != null)
        {
            float k = Mathf.Min(1f, (Window.height - 4) / keeper.height);
            GUI.DrawTexture(new Rect(Window.x + 2, Window.yMax - keeper.height * k - 2, keeper.width * k, keeper.height * k), keeper);
        }
        Gui.Text(new Rect(Window.x + 40, Window.y + 20, 520, 60), "Provisioner", 46, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);

        var q = _quest;
        int length = q?.Length ?? 1;
        if (q != null)
            Gui.Text(new Rect(1000, 60, 860, 40), $"{S.Zones.ZoneName(q.Dungeon)}: {q.DifficultyName} {Cap(q.Size)} {HamletUi.Pretty(q.Type)}  ·  recommended food {S.Provisioner.MinimumFood(length)}", 22, Gui.Dd1Class, TextAnchor.MiddleRight);

        DrawStore(length);
        DrawPack();

        int cost = CartCost(), gold = E.Get(Currency.Gold);
        var goldIcon = Art.Dd1("shared", "estate", "currency.gold.large_icon.png");
        if (goldIcon != null) GUI.DrawTexture(new Rect(820, 920, 72, 72), goldIcon);
        Gui.Text(new Rect(900, 924, 600, 64), $"{Gui.Num(gold, "#,0")}   -{Gui.Num(cost, "#,0")}", 34, cost > gold ? Gui.Blood : Gui.Gold, TextAnchor.MiddleLeft, heading: true);

        if (Gui.DdButton(new Rect(30, 1000, 300, 60), "Back to the quests", size: 24)) { _provisioning = false; _confirmLow = false; }

        var heroes = Heroes();
        string why = Embark.WhyCantEmbark(E, _quest, heroes, S.Hamlet.AnyResolveCanEmbark);
        int minFood = S.Provisioner.MinimumFood(length);
        bool lowFood = _cart.Count(Supply.Food) < minFood, noTorch = _cart.Count(Supply.Torch) == 0;
        bool fewTrinkets = Embark.TrinketWarning(S.Campaign, _quest, heroes);   // DD1: under half the trinket slots filled
        if (why == null && (lowFood || noTorch || fewTrinkets))
        {
            // DD1's own questions (localization: town_provision_*).
            string warn = lowFood ? (S.Lore?.Text("town_provision_not_enough_food_confirm_format")?.Replace("%d", minFood.ToString()) ?? $"Less than {minFood} food: the party may starve.")
                : noTorch ? "No torches: the dark will press in."
                : S.Lore?.Text("town_provision_no_trinkets_equipped") ?? "Your party is not fully outfitted with trinkets. Really embark?";
            Gui.Text(new Rect(1080, 990, 480, 70), warn + (_confirmLow ? "\nEmbark again to go anyway." : ""), 18, Gui.Blood, TextAnchor.MiddleRight);
        }
        if (Gui.DdButton(new Rect(1580, 980, 320, 86), why ?? "Embark", why == null && cost <= gold, why == null ? 44 : 18))
        {
            if ((lowFood || noTorch || fewTrinkets) && !_confirmLow) { _confirmLow = true; return; }
            _confirmLow = false;
            var bought = new Inventory { Layout = new List<string>(_cart.Layout) };   // keep the arrangement
            foreach (var kv in _cart.Items) bought.Add(kv.Key, kv.Value);
            _error = Driver.Instance.Embark(_quest, heroes, bought);
            if (_error == null) { _party.Clear(); _cart.Items.Clear(); _quest = null; _provisioning = false; }
        }
    }

    // DD1's provisioner layout (campaign/town/provision/provision.layout.darkest).
    private static readonly Vector2 StorePos = new(814, 144), PackPos = new(800, 532);
    private static Rect StoreCell(int i) => new(StorePos.x + 120 + (i % 7) * 80, StorePos.y + 20 + (i / 7) * 170, 72, 144);
    private static Rect PackCell(int i) => new(PackPos.x + 60 + (i % 8) * 80, PackPos.y + 28 + (i / 8) * 160, 72, 144);
    private static readonly Rect StoreArea = new(814, 144, 680, 360), PackArea = new(800, 532, 720, 360);

    private void DrawStore(int length)
    {
        var grid = Prov("inventory_grid_background_store.png");
        if (grid != null) GUI.DrawTexture(StoreArea, grid);
        var stock = S.Hamlet.ProvisionStock(S.Provisioner, S.Content.Items, length);
        var items = S.Content.Items;
        string hovered = null;
        for (int i = 0; i < Supply.Provisioner.Length; i++)
        {
            string id = Supply.Provisioner[i];
            int max = stock.TryGetValue(id, out var m) ? m : 0, have = _cart.Count(id), left = max - have;
            var r = StoreCell(i);
            if (left > 0) Drag.Source(r, new ShelfItem(id), rect => ItemArt.Stack(rect, id, 1, 1));
            ItemArt.Stack(r, id, Mathf.Max(left, 0), Mathf.Max(1, items.StackLimit(id)), dim: left <= 0);
            if (r.Contains(Event.current.mousePosition)) hovered = id;
            var e = Event.current;
            if (r.Contains(e.mousePosition) && e.type == EventType.MouseUp && !Drag.Active && !Drag.JustDropped)
            {
                if (e.button == 0 && left > 0) _cart.Add(id, 1);
                else if (e.button == 1 && have > 0) _cart.Add(id, -1);
                e.Use();
            }
        }
        // A stack dragged back from the pack goes back on the shelf.
        if (Drag.Drop<PackStack>(StoreArea, out var back))
        {
            var stacks = back.Pack.Arrange(items);
            var stack = stacks.Find(st => st.Slot == back.Slot);
            if (stack != null) _cart.Add(stack.Key, -stack.Count);
        }
        if (Drag.Hovering<PackStack>(StoreArea)) Gui.Fill(new Rect(StoreArea.x, StoreArea.yMax - 6, StoreArea.width, 4), Gui.Gold);
        string tip = hovered != null
            ? $"{HamletUi.Pretty(hovered)}: {S.Hamlet.ProvisionPrice(S.Provisioner, items, hovered)} gold each. Click or drag to buy, right-click to put one back."
            : "Drag supplies into your pack. Drag a stack back to the shelf to return it.";
        Gui.Text(new Rect(StorePos.x, StorePos.y + 352, 680, 30), tip, 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
    }

    private void DrawPack()
    {
        var grid = Prov("inventory_grid_background_party.png");
        if (grid != null) GUI.DrawTexture(PackArea, grid);
        var items = S.Content.Items;
        var stacks = _cart.Arrange(items);
        for (int i = 0; i < Inventory.Slots; i++)
        {
            var r = PackCell(i);
            var stack = stacks.Find(st => st.Slot == i);
            if (Drag.Hovering<ShelfItem>(r) || Drag.Hovering<PackStack>(r)) Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), Gui.Gold);
            if (stack != null)
            {
                Drag.Source(r, new PackStack(_cart, i, stack.Key), rect => ItemArt.Stack(rect, stack.Key, stack.Count, items.StackLimit(stack.Key)));
                if (!(Drag.Payload is PackStack carried && carried.Pack == _cart && carried.Slot == i))
                    ItemArt.Stack(r, stack.Key, stack.Count, items.StackLimit(stack.Key));
                var e = Event.current;
                if (r.Contains(e.mousePosition) && e.type == EventType.MouseUp && !Drag.Active && !Drag.JustDropped && (e.button == 0 || e.button == 1))
                {
                    _cart.Add(stack.Key, -1);
                    e.Use();
                }
            }
            if (Drag.Drop<PackStack>(r, out var moved) && moved.Pack == _cart) _cart.Move(moved.Slot, i);
            if (Drag.Drop<ShelfItem>(r, out var bought) && _cart.HasRoomFor(bought.Id, 1, items))
            {
                _cart.Add(bought.Id, 1);
                // Put a brand new stack where it was dropped.
                var placed = _cart.Arrange(items).FindLast(st => st.Key == bought.Id);
                if (placed != null && placed.Slot != i && _cart.At(i) == null) _cart.Move(placed.Slot, i);
            }
        }
        if (stacks.Count == 0)
            Gui.Text(new Rect(PackArea.x, PackArea.y + 150, PackArea.width, 40), "The pack is empty. Drag food and torches into it.", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);
        if (stacks.Count > Inventory.Slots)
            Gui.Text(new Rect(PackArea.x, PackArea.yMax - 30, PackArea.width, 30), "The pack holds 16 stacks: the rest stays behind.", 18, Gui.Blood, TextAnchor.MiddleCenter);
    }

    private int CartCost() { var hamlet = S.Hamlet; return _cart.Items.Sum(kv => hamlet.ProvisionPrice(S.Provisioner, S.Content.Items, kv.Key) * kv.Value); }
}
