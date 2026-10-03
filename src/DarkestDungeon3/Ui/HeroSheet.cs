using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's character sheet (shared/character, character.layout.darkest) for a DD2 hero: resolve, quirks, base stats,
/// equipment with two trinket slots, combat and camping skills, resistances, diseases; and DD1's realm inventory
/// (campaign/town/realm_inventory) beside it. Trinkets are dragged between the two.
/// </summary>
internal static class HeroSheet
{
    private static readonly Vector2 O = new(144, 132);          // the sheet sits where building windows do
    private static readonly Vector2 Realm = new(881, 128);      // realm_inventory_pos
    public static bool RealmOpen;
    private static int _realmTop;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;
    private static Texture2D Ch(string f) => Art.Dd1("shared", "character", f);
    private static Texture2D Ri(string f) => Art.Dd1("campaign", "town", "realm_inventory", f);
    private static Rect At(float x, float y, float w, float h) => new(O.x + x, O.y + y, w, h);

    /// <summary>Draw the sheet. <paramref name="show"/> switches hero (previous/next), <paramref name="close"/> closes it.</summary>
    public static void Draw(HeroRecord h, Action<string> show, Action close)
    {
        var win = At(0, 0, 1395, 776);
        var bg = Ch("characterpanel_bg.png");
        if (bg != null) GUI.DrawTexture(win, bg); else Gui.Fill(win, new Color(0.04f, 0.035f, 0.03f, 0.97f));
        var figure = Art.HeroFigure(h.ClassId);
        if (figure != null) Art.DrawSprite(At(18, 250, 220, 450), figure);   // hero_pos 98,700
        var frames = Ch("characterpanel_frames.png");
        if (frames != null) GUI.DrawTexture(At(10, 10, 1395, 776), frames);

        // Header: name, class, resolve (campaign status).
        Gui.Text(At(76, 8, 600, 50), h.Name, 40, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(At(76, 58, 600, 34), HamletUi.Pretty(h.ClassId), 22, Gui.Dd1Class, TextAnchor.MiddleLeft);
        Gui.Text(At(67, 132, 128, 56), h.ResolveLevel.ToString(), 46, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(At(67, 186, 128, 24), "Resolve", 17, Gui.Dd1Class, TextAnchor.MiddleCenter);
        var thresholds = S.Campaign.HeroResolveThresholds;
        if (h.ResolveLevel + 1 < thresholds.Count)
        {
            float from = thresholds[h.ResolveLevel], to = thresholds[h.ResolveLevel + 1];
            Gui.Bar(At(75, 212, 112, 6), (h.ResolveXp - from) / Mathf.Max(1, to - from), Gui.Gold);
        }

        DrawQuirks(h);
        DrawStats(h);
        DrawEquipment(h);
        DrawSkills(h);
        DrawResistances(h);
        DrawDiseases(h);

        // Previous / next hero, dismiss, close.
        int at = E.Roster.FindIndex(x => x.Id == h.Id);
        var prev = At(1162, 772 - 50, 64, 48);
        var next = At(1246, 772 - 50, 64, 48);
        if (Ch("previous_hero.png") is { } p) GUI.DrawTexture(prev, p, ScaleMode.ScaleToFit);
        if (Ch("next_hero.png") is { } n) GUI.DrawTexture(next, n, ScaleMode.ScaleToFit);
        if (E.Roster.Count > 1 && Gui.Hotspot(prev)) show(E.Roster[(at - 1 + E.Roster.Count) % E.Roster.Count].Id);
        if (E.Roster.Count > 1 && Gui.Hotspot(next)) show(E.Roster[(at + 1) % E.Roster.Count].Id);

        var dismiss = At(20, 70, 32, 32);
        if (Ch("icon_dismiss.png") is { } d) GUI.DrawTexture(dismiss, d);
        if (dismiss.Contains(Event.current.mousePosition))
            Gui.Text(At(56, 70, 380, 32), _confirmDismiss == h.Id ? "Click again to dismiss for good" : "Dismiss hero", 18, Gui.Blood, TextAnchor.MiddleLeft);
        if (Gui.Hotspot(dismiss))
        {
            if (_confirmDismiss == h.Id) { S.Hamlet.Dismiss(h.Id); S.Persist(); _confirmDismiss = null; close(); return; }
            _confirmDismiss = h.Id;
        }

        var closeRect = At(1344 - 6, 18 - 6, 46, 46);
        var x = Art.Dd1("shared", "progression", "progression_close.png");
        if (x != null) GUI.DrawTexture(closeRect, x);
        if (Gui.Hotspot(closeRect) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)) { RealmOpen = false; close(); return; }

        // The realm inventory toggle (DD1 has it on the estate bar; it's handy here too).
        var toggle = At(1200, 18, 120, 40);
        if (Gui.DdButton(toggle, RealmOpen ? "Hide trinkets" : "Trinkets", true, 18)) RealmOpen = !RealmOpen;
        if (RealmOpen) DrawRealmInventory(h);
    }

    private static string _confirmDismiss;

    private static void Title(float centreX, float y, string text) =>
        Gui.Text(At(centreX - 250, y - 2, 500, 36), text, 24, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);

    private static void DrawQuirks(HeroRecord h)
    {
        Title(464, 128, "Quirks");
        var pos = h.Quirks.Where(q => S.Catalog.IsPositive(q) && !S.Catalog.IsDisease(q)).ToList();
        var neg = h.Quirks.Where(q => !S.Catalog.IsPositive(q) && !S.Catalog.IsDisease(q)).ToList();
        var lockIcon = Ch("lockquirk.png");
        for (int i = 0; i < pos.Count && i < 5; i++)
        {
            Gui.Text(At(241, 172 + i * 30, 210, 28), HamletUi.QuirkName(pos[i]), 18, Gui.Gold, TextAnchor.MiddleLeft);
            if (h.LockedQuirks.Contains(pos[i]) && lockIcon != null) GUI.DrawTexture(At(205, 172 + i * 30, 28, 28), lockIcon);
        }
        for (int i = 0; i < neg.Count && i < 5; i++)
        {
            Gui.Text(At(486, 172 + i * 30, 220, 28), HamletUi.QuirkName(neg[i]), 18, Gui.Dd1Health, TextAnchor.MiddleRight);
            if (h.LockedQuirks.Contains(neg[i]) && lockIcon != null) GUI.DrawTexture(At(710, 172 + i * 30, 28, 28), lockIcon);
        }
        if (pos.Count + neg.Count == 0) Gui.Text(At(241, 180, 465, 28), "No quirks", 18, Gui.Dim, TextAnchor.MiddleCenter);
    }

    private static void DrawStats(HeroRecord h)
    {
        Title(464, 358, "Base stats");
        var c = Dd2.Dd2Catalog.Tables.Classes.TryGetValue(h.ClassId, out var cs) ? cs : null;
        if (c == null) return;
        int armourHp = new[] { 0, 10, 20, 30, 45 }[Mathf.Clamp(h.ArmorRank, 0, 4)];
        int weaponSpd = new[] { 0, 0, 1, 1, 2 }[Mathf.Clamp(h.WeaponRank, 0, 4)];
        var lines = new List<(string, string)>
        {
            ("Max HP", Gui.Num(Mathf.Round(c.Get("health_max") * (1f + armourHp / 100f)), "0")),
            ("Speed", Gui.Num(c.Get("speed") + weaponSpd, "0")),
            ("Stress", "max " + Gui.Num(c.Get("stress_max"), "0")),
            ("Death's door", Gui.Num(c.Get("deaths_door_chance") * 100f, "0") + "% to survive"),
        };
        for (int i = 0; i < lines.Count; i++)
        {
            var (k, v) = lines[i];
            float x = 160 + (i % 2) * 230, y = 392 + (i / 2) * 30;
            Gui.Text(At(x, y, 120, 28), k, 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
            Gui.Text(At(x + 110, y, 110, 28), v, 18, Gui.Dd1Text, TextAnchor.MiddleRight);
        }
    }

    // Equipment frames painted in characterpanel_frames (equipment_pos 141,516; equipment 107,34; trinkets 321,34).
    private static Rect WeaponCell => At(281, 600, 72, 144);
    private static Rect ArmourCell => At(372, 600, 72, 144);
    private static Rect TrinketCell(int i) => At(499 + i * 90, 600, 72, 144);

    private static void DrawEquipment(HeroRecord h)
    {
        Title(464, 504, "Equipment");
        string dd1Class = S.Campaign.HeroUpgrades.Dd1Class(h.ClassId);
        if (Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_weapon_{Mathf.Clamp(h.WeaponRank, 0, 4)}.png") is { } w) GUI.DrawTexture(WeaponCell, w);
        if (Art.Dd1("heroes", dd1Class, "icons_equip", $"eqp_armour_{Mathf.Clamp(h.ArmorRank, 0, 4)}.png") is { } a) GUI.DrawTexture(ArmourCell, a);
        if (WeaponCell.Contains(Event.current.mousePosition)) Tip($"Weapon rank {h.WeaponRank + 1}: {Dd2.Dd2Heroes.EquipmentText("weapon", h.WeaponRank)}");
        if (ArmourCell.Contains(Event.current.mousePosition)) Tip($"Armour rank {h.ArmorRank + 1}: {Dd2.Dd2Heroes.EquipmentText("armour", h.ArmorRank)}");

        for (int i = 0; i < 2; i++)
        {
            var r = TrinketCell(i);
            string id = i < h.Trinkets.Count ? h.Trinkets[i] : null;
            bool hover = Drag.Hovering<TrinketDrag>(r);
            if (hover && Drag.Payload is TrinketDrag over)
                Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), S.Catalog.TrinketFits(over.TrinketId, h.ClassId) ? Gui.Gold : Gui.Blood);
            if (Drag.Drop<TrinketDrag>(r, out var dropped)) { Equip(h, dropped, i); return; }
            if (id == null) continue;
            string tid = id;
            Drag.Source(r, new TrinketDrag(tid, h.Id), rect => TrinketIcon(rect, tid));
            if (!(Drag.Payload is TrinketDrag c && c.FromHero == h.Id && c.TrinketId == tid)) TrinketIcon(r, tid);
            if (r.Contains(Event.current.mousePosition) && !Drag.Active) Tip(TrinketText(tid) + "\nClick or drag away to unequip.");
            if (Gui.Hotspot(r) && !Drag.JustDropped)
            {
                h.Trinkets.Remove(tid);
                E.Trinkets.Add(tid);
                S.Persist();
                return;
            }
        }
    }

    private static void Equip(HeroRecord h, TrinketDrag drag, int slot)
    {
        if (!S.Catalog.TrinketFits(drag.TrinketId, h.ClassId)) { Gui.Announce($"Only {Dd2.Dd2Catalog.Tables.Trinkets[drag.TrinketId].HeroClass} can wear that."); return; }
        if (drag.FromHero == h.Id) return;                       // already worn
        if (drag.FromHero == null) { if (!E.Trinkets.Remove(drag.TrinketId)) return; }
        else if (E.Hero(drag.FromHero) is { } other) other.Trinkets.Remove(drag.TrinketId);
        if (slot < h.Trinkets.Count)
        {
            E.Trinkets.Add(h.Trinkets[slot]);                    // the one it replaces goes to the stash
            h.Trinkets[slot] = drag.TrinketId;
        }
        else if (h.Trinkets.Count < 2) h.Trinkets.Add(drag.TrinketId);
        else E.Trinkets.Add(drag.TrinketId);
        S.Persist();
    }

    private static void DrawSkills(HeroRecord h)
    {
        Title(1060, 52, "Combat skills");
        var skills = Dd2.HeroSkills.ForClass(h.ClassId);
        var selected = Ch("selected_ability.png");
        var locked = Ch("lockedskill.png");
        if (skills != null)
        {
            var equipped = h.EquippedSkills.Count > 0 ? h.EquippedSkills : skills.Where(s => s.Starting).Take(Dd2.HeroSkills.EquipLimit).Select(s => s.Id).ToList();
            for (int i = 0; i < skills.Count && i < 14; i++)
            {
                var s = skills[i];
                var r = At(790 + (i % 7) * 80, 180 + (i / 7) * 80, 72, 72);
                bool known = Dd2.HeroSkills.Knows(h, s);
                var old = GUI.color;
                if (!known) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                if (s.Icon != null) Art.DrawSprite(r, s.Icon);
                GUI.color = old;
                if (!known && locked != null) GUI.DrawTexture(new Rect(r.x + 20, r.y + 20, 32, 32), locked);
                if (known && equipped.Contains(s.Id) && selected != null) GUI.DrawTexture(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), selected);
                if (h.MasteredSkills.Contains(s.Id)) Gui.Text(new Rect(r.x + 36, r.y + 48, 36, 24), "+", 22, Gui.Gold, TextAnchor.MiddleRight, heading: true);
                if (r.Contains(Event.current.mousePosition))
                    Tip($"{Dd2.HeroSkills.Name(s.Id)}{(h.MasteredSkills.Contains(s.Id) ? " (mastered)" : "")}\n{(known ? equipped.Contains(s.Id) ? "Brought on expeditions" : "Known" : "Learn it at the Guild")}");
            }
        }
        Title(1060, 286, "Camping skills");
        for (int i = 0; i < h.CampingSkills.Count && i < 7; i++)
        {
            var r = At(790 + i * 80, 340, 72, 72);
            if (Art.Dd1("raid", "camping", "skill_icons", $"camp_skill_{h.CampingSkills[i]}.png") is { } icon) GUI.DrawTexture(r, icon);
            if (r.Contains(Event.current.mousePosition))
            {
                var cs = S.Content.Camping.Get(h.CampingSkills[i]);
                Tip($"{Dd1Text.CampSkillName(h.CampingSkills[i])} ({cs?.Cost} respite)\n" + string.Join("\n", S.Content.Camping.DescribeAll(cs)));
            }
        }
    }

    private static readonly (string Key, string Name)[] Resist =
        { ("stun", "Stun"), ("blight", "Blight"), ("bleed", "Bleed"), ("burn", "Burn"), ("disease", "Disease"), ("move", "Move"), ("debuff", "Debuff"), ("death", "Deathblow") };

    private static void DrawResistances(HeroRecord h)
    {
        Title(1060, 450, "Resistances");
        if (!Dd2.Dd2Catalog.Tables.Classes.TryGetValue(h.ClassId, out var c)) return;
        for (int i = 0; i < Resist.Length; i++)
        {
            float x = 800 + (i % 2) * 260, y = 494 + (i / 2) * 28;
            Gui.Text(At(x, y, 140, 26), Resist[i].Name, 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
            float v = c.Resistances.TryGetValue(Resist[i].Key, out var r) ? r : 0f;
            Gui.Text(At(x + 140, y, 90, 26), Gui.Num(v * 100f, "0") + "%", 18, Gui.Dd1Text, TextAnchor.MiddleRight);
        }
    }

    private static void DrawDiseases(HeroRecord h)
    {
        Title(1060, 614, "Diseases");
        var diseases = h.Quirks.Where(S.Catalog.IsDisease).ToList();
        if (diseases.Count == 0) Gui.Text(At(830, 660, 460, 28), "None", 18, Gui.Dim, TextAnchor.MiddleCenter);
        for (int i = 0; i < diseases.Count && i < 3; i++)
            Gui.Text(At(830, 660 + i * 28, 460, 26), HamletUi.QuirkName(diseases[i]), 18, Gui.Dd1Health, TextAnchor.MiddleCenter);
    }

    // ---- the realm inventory: the estate's unworn trinkets ----

    private static Rect RealmCell(int i) => new(Realm.x + 30 + (i % 7) * 80, Realm.y + 195 + (i / 7) * 160, 72, 144);

    private static void DrawRealmInventory(HeroRecord h)
    {
        var panel = new Rect(Realm.x, Realm.y, 667, 780);
        if (Ri("realminv_bg.png") is { } bg) GUI.DrawTexture(panel, bg); else Gui.Fill(panel, new Color(0.03f, 0.025f, 0.02f, 0.96f));
        Gui.Text(new Rect(Realm.x + 40, Realm.y + 20, 500, 46), "Trinkets", 34, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(Realm.x + 40, Realm.y + 66, 580, 50), "Drag a trinket onto a hero's slot. Drag a worn trinket here to take it off.", 17, Gui.Dd1Class);

        var trinkets = E.Trinkets;
        int rows = Mathf.Max(1, (trinkets.Count + 6) / 7);
        var area = new Rect(Realm.x + 30, Realm.y + 195, 560, 525);
        if (Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _realmTop = Mathf.Clamp(_realmTop + (Event.current.delta.y > 0 ? 1 : -1), 0, Mathf.Max(0, rows - 3));
            Event.current.Use();
        }
        if (Drag.Hovering<TrinketDrag>(panel) && Drag.Payload is TrinketDrag t && t.FromHero != null) Gui.Fill(new Rect(panel.x, panel.yMax - 8, panel.width, 4), Gui.Gold);
        if (Drag.Drop<TrinketDrag>(panel, out var back) && back.FromHero != null && E.Hero(back.FromHero) is { } wearer && wearer.Trinkets.Remove(back.TrinketId))
        {
            E.Trinkets.Add(back.TrinketId);
            S.Persist();
            return;
        }
        string hovered = null;
        for (int i = 0; i < 21 && _realmTop * 7 + i < trinkets.Count; i++)
        {
            string id = trinkets[_realmTop * 7 + i];
            var r = RealmCell(i);
            bool fits = S.Catalog.TrinketFits(id, h.ClassId);
            Drag.Source(r, new TrinketDrag(id), rect => TrinketIcon(rect, id));
            var old = GUI.color;
            if (!fits) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            TrinketIcon(r, id);
            GUI.color = old;
            if (r.Contains(Event.current.mousePosition)) hovered = id;
        }
        if (trinkets.Count == 0) Gui.Text(new Rect(Realm.x + 40, Realm.y + 300, 580, 40), "No trinkets yet. The Nomad Wagon sells them.", 20, Gui.Dd1Class, TextAnchor.MiddleCenter);
        if (hovered != null && !Drag.Active)
            Gui.Text(new Rect(Realm.x + 40, Realm.y + 120, 580, 70), TrinketText(hovered), 18, Gui.Dd1Text);
    }

    // ---- trinket art and words ----

    public static void TrinketIcon(Rect r, string id)
    {
        var sprite = Dd2.ItemIcons.Get(id);
        if (sprite != null) Art.DrawSprite(r, sprite);
        else
        {
            Gui.Fill(r, new Color(0.1f, 0.08f, 0.06f, 0.9f));
            Gui.Text(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), TrinketName(id), 14, Gui.Dd1Text, TextAnchor.MiddleCenter);
        }
    }

    public static string TrinketName(string id)
    {
        try
        {
            var loc = Assets.Code.Utils.Singleton<Assets.Code.Locale.Localization>.Instance;
            string name = loc?.TryGetString("item_name_" + id);
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("item_name_")) return name;
        }
        catch (Exception) { }
        string s = id.StartsWith("trinket_") ? id.Substring(8) : id;
        return HamletUi.Pretty(s.Replace("tiered_", ""));
    }

    public static string TrinketText(string id)
    {
        var t = Dd2.Dd2Catalog.Tables.Trinkets.TryGetValue(id, out var tr) ? tr : null;
        string rarity = t == null ? "" : HamletUi.Pretty(t.Rarity);
        string forClass = t?.HeroClass != null ? $"  ·  {HamletUi.Pretty(t.HeroClass)} only" : "";
        return $"{TrinketName(id)}\n{rarity}{forClass}";
    }

    private static void Tip(string text)
    {
        var m = Event.current.mousePosition;
        var size = new Vector2(380, 30 + 24 * (text.Count(c => c == '\n') + 1));
        var r = new Rect(Mathf.Min(m.x + 18, 1900 - size.x), Mathf.Min(m.y + 18, 1060 - size.y), size.x, size.y);
        if (Event.current.type != EventType.Repaint) return;
        Gui.Fill(r, new Color(0.03f, 0.025f, 0.02f, 0.95f));
        Gui.Fill(new Rect(r.x, r.y, r.width, 2), new Color(0.45f, 0.38f, 0.24f));
        Gui.Text(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 12), text, 18, Gui.Dd1Text);
    }
}
