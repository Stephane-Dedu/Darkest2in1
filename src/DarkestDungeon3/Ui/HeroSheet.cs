using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's character sheet (shared/character, character.layout.darkest) for a DD2 hero: resolve, quirks, base stats,
/// equipment with two trinket slots, combat and camping skills, resistances, diseases. DD1's Trinket Inventory
/// (RealmInventory, opened from the estate bar) sits over its right side; trinkets are dragged between the two.
/// </summary>
internal static class HeroSheet
{
    private static readonly Vector2 O = new(144, 132);          // the sheet sits where building windows do
    /// <summary>DD1's Trinket Inventory is open (from the estate bar); it stays open from hero to hero.</summary>
    public static bool RealmOpen;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;
    private static Texture2D Ch(string f) => Art.Dd1("shared", "character", f);
    private static Rect At(float x, float y, float w, float h) => new(O.x + x, O.y + y, w, h);

    /// <summary>
    /// Draw the sheet. <paramref name="show"/> switches hero (previous/next among <paramref name="cycle"/>, the roster
    /// by default), <paramref name="close"/> closes it. <paramref name="readOnly"/> (in the dungeon): look only, as
    /// DD1 doesn't reach the town's stash mid-expedition.
    /// </summary>
    public static void Draw(HeroRecord h, Action<string> show, Action close, bool readOnly = false, IReadOnlyList<string> cycle = null)
    {
        _readOnly = readOnly;
        // Right-click outside closes it, as in DD1.
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1) { Event.current.Use(); close(); return; }
        var win = At(0, 0, 1395, 776);
        var bg = Ch("characterpanel_bg.png");
        if (bg != null) GUI.DrawTexture(win, bg); else Gui.Fill(win, new Color(0.04f, 0.035f, 0.03f, 0.97f));
        var frames = Ch("characterpanel_frames.png");
        if (frames != null) GUI.DrawTexture(At(10, 10, 1395, 776), frames);
        // The DD1 frame contains an opaque stained-glass background in the hero's entire area.
        // Its character picture belongs above that art, otherwise the frame erases the picture.
        var figure = Art.HeroFigure(h.ClassId);
        if (figure != null) Art.DrawSprite(At(18, 250, 220, 450), figure);   // hero_pos 98,700

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
        bool equipmentEnabled = GUI.enabled;
        GUI.enabled = equipmentEnabled && (readOnly || (h.IsAvailable && E.Roster.Contains(h)));
        try { DrawEquipment(h); }
        finally { GUI.enabled = equipmentEnabled; }
        DrawSkills(h);
        DrawResistances(h);
        DrawDiseases(h);

        // Previous / next hero, dismiss, close.
        var ids = cycle ?? E.Roster.Select(x => x.Id).ToList();
        int at = Math.Max(0, ids.ToList().IndexOf(h.Id));
        var prev = At(1162, 772 - 50, 64, 48);
        var next = At(1246, 772 - 50, 64, 48);
        if (Ch("previous_hero.png") is { } p) GUI.DrawTexture(prev, p, ScaleMode.ScaleToFit);
        if (Ch("next_hero.png") is { } n) GUI.DrawTexture(next, n, ScaleMode.ScaleToFit);
        if (ids.Count > 1 && Gui.Hotspot(prev)) show(ids[(at - 1 + ids.Count) % ids.Count]);
        if (ids.Count > 1 && Gui.Hotspot(next)) show(ids[(at + 1) % ids.Count]);

        var closeRect = At(1344 - 6, 18 - 6, 46, 46);
        var x = Art.Dd1("shared", "progression", "progression_close.png");
        if (x != null) GUI.DrawTexture(closeRect, x);
        if (Gui.Hotspot(closeRect) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)) { close(); return; }
        if (readOnly) return;

        var dismiss = At(20, 70, 32, 32);
        if (Ch("icon_dismiss.png") is { } d) GUI.DrawTexture(dismiss, d);
        if (dismiss.Contains(Event.current.mousePosition))
            Gui.Text(At(56, 70, 380, 32), _confirmDismiss == h.Id ? "Click again to dismiss for good" : "Dismiss hero", 18, Gui.Blood, TextAnchor.MiddleLeft);
        if (Gui.Hotspot(dismiss))
        {
            if (_confirmDismiss == h.Id) { S.Hamlet.Dismiss(h.Id); S.Persist(); _confirmDismiss = null; close(); return; }
            _confirmDismiss = h.Id;
        }
    }

    private static string _confirmDismiss;
    private static bool _readOnly;

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
            string id = h.TrinketAt(i);
            bool hover = Drag.Hovering<TrinketDrag>(r);
            if (hover && Drag.Payload is TrinketDrag over)
                Gui.Fill(new Rect(r.x, r.yMax + 2, r.width, 4), Core.Campaign.Town.TrinketEquipment.Refusal(E, S.Catalog, over.TrinketId, over.FromHero, over.FromSlot, h, i) == null ? Gui.Gold : Gui.Blood);
            if (!_readOnly && Drag.Drop<TrinketDrag>(r, out var dropped)) { Equip(h, dropped, i); return; }
            if (id == null) continue;
            string tid = id;
            if (_readOnly)
            {
                TrinketIcon(r, tid);
                if (r.Contains(Event.current.mousePosition)) TrinketTip(tid);
                continue;
            }
            Drag.Source(r, new TrinketDrag(tid, h.Id, i), rect => TrinketIcon(rect, tid));
            if (!(Drag.Payload is TrinketDrag c && c.FromHero == h.Id && c.TrinketId == tid && c.FromSlot == i)) TrinketIcon(r, tid);
            if (r.Contains(Event.current.mousePosition) && !Drag.Active) TrinketTip(tid, "Click, or drag it to the Trinket Inventory, to unequip.");
            if (Gui.Hotspot(r) && !Drag.JustDropped)
            {
                if (!Core.Campaign.Town.TrinketEquipment.Unequip(E, h, i)) return;
                Dd1Audio.Play("/ui/dun/trink_unqeuip");
                S.Persist();
                return;
            }
        }
    }

    private static void Equip(HeroRecord h, TrinketDrag drag, int slot)
    {
        string refusal = Core.Campaign.Town.TrinketEquipment.Refusal(E, S.Catalog, drag.TrinketId, drag.FromHero, drag.FromSlot, h, slot);
        if (refusal != null) { Gui.Announce(refusal); return; }
        if (!Core.Campaign.Town.TrinketEquipment.Transfer(E, S.Catalog, drag.TrinketId, drag.FromHero, drag.FromSlot, h, slot)) return;
        Dd1Audio.Play("/ui/dun/trink_equip");
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

    /// <summary>DD1's trinket tooltip: name, rarity, class requirement, then DD2's effects.</summary>
    public static void TrinketTip(string id, string hint = null)
    {
        var t = Dd2.Dd2Catalog.Tables.Trinkets.TryGetValue(id, out var tr) ? tr : null;
        Gui.EquipmentTip(TrinketName(id), t == null ? "Trinket" : HamletUi.Pretty(t.Rarity), RarityColour(id),
            t?.HeroClass == null ? null : $"{HamletUi.Pretty(t.HeroClass)} only", Dd2.ItemText.Effects(id), hint);
    }

    /// <summary>DD2 rarity labels use the closest DD1 palette tier; the item name has its own title colour.</summary>
    public static Color RarityColour(string id)
    {
        string rarity = Dd2.Dd2Catalog.Tables.Trinkets.TryGetValue(id, out var t) ? t.Rarity : null;
        string colour = rarity switch
        {
            "epic" => "very_rare",
            "cultist" => "harmful",
            _ => rarity ?? "common",
        };
        return Dd1Palette.Get(colour, Gui.Dd1Class);
    }

    private static void Tip(string text) => Gui.Tip(text);
}
