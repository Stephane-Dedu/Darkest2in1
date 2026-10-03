using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
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

    public bool WantsBack;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;

    private static Texture2D Qs(string file) => Art.Dd1("campaign", "town", "quest_select", file);
    private static Texture2D Prov(string file) => Art.Dd1("campaign", "town", "provision", file);

    public void Draw()
    {
        _party.RemoveAll(id => E.Hero(id) == null);
        if (_provisioning) DrawProvisioner(); else DrawQuestSelect();
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

    private void DrawQuestSelect()
    {
        var bg = Qs("quest_select.background.png");
        if (bg != null) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), bg); else Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0.05f, 0.04f, 0.05f));
        Gui.Text(new Rect(560, 24, 900, 60), "Choose a quest", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);

        // Dungeons with their quests. Toggled-in DD2 zones have no spot on DD1's map: they line up at the bottom.
        var spots = MapSpots();
        var zones = E.Quests.Select(q => q.Dungeon).Distinct().ToList();
        int extra = 0;
        foreach (var zone in zones)
        {
            if (!spots.TryGetValue(zone, out var pos))
                pos = new Vector2(600 + (extra++ % 2) * 460, 640 + (extra - 1) / 2 * 150);
            DrawDungeon(zone, pos, E.Quests.Where(q => q.Dungeon == zone).ToList());
        }

        if (_quest != null && !E.Quests.Contains(_quest)) _quest = null;
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
        });
        if (clicked != null) Toggle(clicked);

        if (Gui.DdButton(new Rect(30, 1000, 300, 60), "Back to the Hamlet", size: 24)) WantsBack = true;
        var heroes = Heroes();
        string why = Embark.WhyCantEmbark(E, _quest, heroes, S.Hamlet.AnyResolveCanEmbark);
        if (Gui.DdButton(new Rect(1190, 905, 330, 90), why == null ? "Provision" : why, why == null, why == null ? 40 : 18))
        {
            _provisioning = true;
            _error = null;
        }
    }

    private void Toggle(HeroRecord h)
    {
        if (_party.Remove(h.Id)) return;
        if (_party.Count >= 4 || !h.IsAvailable || h.MissingWeeks > 0) return;
        if (_quest != null && !Homecoming.WillEmbark(h, _quest, S.Hamlet.AnyResolveCanEmbark)) return;
        _party.Add(h.Id);
    }

    private List<HeroRecord> Heroes() => _party.Select(E.Hero).Where(h => h != null).ToList();

    private void DrawDungeon(string zone, Vector2 pos, List<QuestOffer> quests)
    {
        var plate = Qs("dungeon_progressionbar.png");
        if (plate != null) GUI.DrawTexture(new Rect(pos.x - 5, pos.y - 10, 282, 84), plate);
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

        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            var c = new Vector2(pos.x + 160 + (i % 4) * 76, pos.y + 92 + (i / 4) * 80);
            var r = new Rect(c.x - 36, c.y - 36, 72, 72);
            bool hover = r.Contains(Event.current.mousePosition);
            if (q == _quest)
            {
                var sel = Qs("quest_select_selected.png");
                if (sel != null) GUI.DrawTexture(new Rect(c.x - 72, c.y - 72, 144, 144), sel);
            }
            var ring = Qs($"quest_select_length_{(q.IsPlot ? "plot" : "generated")}_{Mathf.Clamp(q.Length, 0, 4)}.png") ?? Qs($"quest_select_length_generated_{Mathf.Clamp(q.Length, 0, 5)}.png");
            if (ring != null) GUI.DrawTexture(hover ? new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8) : r, ring);
            int tier = q.Difficulty >= 6 ? 6 : q.Difficulty >= 5 ? 5 : q.Difficulty >= 3 ? 3 : 1;
            var badge = Qs($"quest_select_{q.Type}_{tier}.png") ?? Qs($"quest_select_explore_{tier}.png");
            if (badge != null) GUI.DrawTexture(new Rect(c.x - 20, c.y - 20, 40, 40), badge);
            if (Gui.Hotspot(r))
            {
                _quest = q;
                _party.RemoveAll(id => !Homecoming.WillEmbark(E.Hero(id), q, S.Hamlet.AnyResolveCanEmbark));
                _error = null;
            }
        }
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
            if (slotBg != null) GUI.DrawTexture(r, slotBg);
            if (rank >= _party.Count) continue;
            var h = E.Hero(_party[rank]);
            var sprite = Art.HeroIcon(h.ClassId);
            if (sprite != null) Art.DrawSprite(new Rect(r.x + 4, r.y + 4, 72, 72), sprite);
            if (r.Contains(Event.current.mousePosition))
                Gui.Text(new Rect(r.x - 60, r.yMax + 2, r.width + 120, 26), $"{h.Name} — click to remove", 17, Gui.Dd1Text, TextAnchor.MiddleCenter);
            if (Gui.Hotspot(r)) { _party.RemoveAt(rank); break; }
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
        if (why == null && (lowFood || noTorch))
        {
            string warn = lowFood ? $"Less than {minFood} food: the party may starve." : "No torches: the dark will press in.";
            Gui.Text(new Rect(1080, 990, 480, 70), warn + (_confirmLow ? "\nEmbark again to go anyway." : ""), 18, Gui.Blood, TextAnchor.MiddleRight);
        }
        if (Gui.DdButton(new Rect(1580, 980, 320, 86), why ?? "Embark", why == null && cost <= gold, why == null ? 44 : 18))
        {
            if ((lowFood || noTorch) && !_confirmLow) { _confirmLow = true; return; }
            _confirmLow = false;
            var bought = new Inventory();
            foreach (var kv in _cart.Items) bought.Add(kv.Key, kv.Value);
            _error = Driver.Instance.Embark(_quest, heroes, bought);
            if (_error == null) { _party.Clear(); _cart.Items.Clear(); _quest = null; _provisioning = false; }
        }
    }

    private void DrawStore(int length)
    {
        var at = new Vector2(814, 144);
        var grid = Prov("inventory_grid_background_store.png");
        if (grid != null) GUI.DrawTexture(new Rect(at.x, at.y, 680, 360), grid);
        var stock = S.Hamlet.ProvisionStock(S.Provisioner, S.Content.Items, length);
        var items = S.Content.Items;
        int i = 0;
        foreach (var id in Supply.Provisioner)
        {
            int max = stock.TryGetValue(id, out var m) ? m : 0, have = _cart.Count(id), left = max - have, price = S.Hamlet.ProvisionPrice(S.Provisioner, S.Content.Items, id);
            var r = new Rect(at.x + 120 + (i % 7) * 80 - 60, at.y + 20 + (i / 7) * 170, 72, 144);
            i++;
            var old = GUI.color;
            if (left <= 0) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            var icon = Art.InventoryIcon(id, Mathf.Max(1, left), Mathf.Max(1, items.StackLimit(id)));
            if (icon != null) GUI.DrawTexture(r, icon); else Gui.Text(r, HamletUi.Pretty(id), 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
            GUI.color = old;
            Gui.Text(new Rect(r.x, r.yMax - 28, r.width - 4, 26), left.ToString(), 22, Color.white, TextAnchor.LowerRight);
            if (r.Contains(Event.current.mousePosition))
                Gui.Text(new Rect(at.x, at.y + 340, 680, 30), $"{HamletUi.Pretty(id)}: {price} gold each. Click to buy, right-click to put one back.", 18, Gui.Dd1Text, TextAnchor.MiddleCenter);
            if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 0 && left > 0) _cart.Add(id, 1);
                else if (Event.current.button == 1 && have > 0) _cart.Add(id, -1);
                Event.current.Use();
            }
        }
    }

    private void DrawPack()
    {
        var at = new Vector2(800, 532);
        var grid = Prov("inventory_grid_background_party.png");
        if (grid != null) GUI.DrawTexture(new Rect(at.x, at.y, 720, 360), grid);
        var items = S.Content.Items;
        var stacks = new List<(string id, int count)>();
        foreach (var id in Supply.Provisioner)
        {
            int limit = Mathf.Max(1, items.StackLimit(id));
            for (int left = _cart.Count(id); left > 0; left -= limit) stacks.Add((id, Mathf.Min(limit, left)));
        }
        for (int i = 0; i < stacks.Count && i < 16; i++)
        {
            var (id, count) = stacks[i];
            var r = new Rect(at.x + 60 + (i % 8) * 80 - 40, at.y + 28 + (i / 8) * 160, 72, 144);
            var icon = Art.InventoryIcon(id, count, Mathf.Max(1, items.StackLimit(id)));
            if (icon != null) GUI.DrawTexture(r, icon); else Gui.Text(r, HamletUi.Pretty(id), 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
            Gui.Text(new Rect(r.x, r.yMax - 28, r.width - 4, 26), count.ToString(), 22, Color.white, TextAnchor.LowerRight);
            if (Gui.Hotspot(r)) _cart.Add(id, -1);
        }
        if (stacks.Count == 0)
            Gui.Text(new Rect(at.x, at.y + 150, 720, 40), "The pack is empty. Buy food and torches above.", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);
        if (stacks.Count > 16)
            Gui.Text(new Rect(at.x, at.y + 330, 720, 30), "The pack holds 16 stacks: the rest stays behind.", 18, Gui.Blood, TextAnchor.MiddleCenter);
    }

    private int CartCost() { var hamlet = S.Hamlet; return _cart.Items.Sum(kv => hamlet.ProvisionPrice(S.Provisioner, S.Content.Items, kv.Key) * kv.Value); }
}
