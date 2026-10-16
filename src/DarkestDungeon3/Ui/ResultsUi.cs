using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's expedition results (raid_results/*): the quest's outcome on its background in the middle of the screen,
/// a page of heroes (resolve gained, new quirks, the fallen) and a page of what came home, then back to the Hamlet.
/// </summary>
internal sealed class ResultsUi
{
    private int _page;
    private HomecomingReport _shown;
    private Vector2 _trinketScroll;

    private static Texture2D Rr(string file) => Art.Dd1("raid_results", file);
    private static Session S => Session.Current;
    private const float Left = 510, Width = 899;   // completion background centred on the screen

    public void Draw(Driver d, bool canLeave)
    {
        using var skin = CampaignPanelSkin.Scope();
        var report = d.LastReport;
        if (report != _shown) { _shown = report; _page = 0; _trinketScroll = Vector2.zero; }
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), CampaignPanelSkin.Coal);
        if (report == null)
        {
            Gui.Text(new Rect(560, 480, 800, 60), "The party returns to the Hamlet", 40, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            if (Gui.DdButton(new Rect(1650, 980, 240, 66), canLeave ? "Continue" : "Returning...", canLeave, 26)) d.BackToHamlet();
            return;
        }

        var bg = Rr(report.Result switch
        {
            "complete" => "raid_results.quest_completed_background.png",
            "defeat" => "raid_results.quest_not_completed_defeat_background.png",
            _ => "raid_results.quest_not_completed_escape_background.png",
        });
        if (bg != null)
        {
            var tint = GUI.color; GUI.color = new Color(.76f, .73f, .66f, .34f);
            GUI.DrawTexture(new Rect(Left, 0, Width, 1080), bg);
            GUI.color = tint;
        }
        CampaignPanelSkin.Frame(new Rect(330, 86, 1260, 856));
        ExpeditionSkin.Banner(new Rect(440, 108, 1040, 100), new Color(.60f, .49f, .31f));
        string result = report.Result switch { "complete" => "Quest Complete", "defeat" => "Defeat", _ => "Retreat" };
        // On DD1's banner (quest_result_pos 960,150).
        Gui.Text(new Rect(Left, 128, Width, 50), result, 40, report.Result == "complete" ? Gui.Gold : new Color(0.95f, 0.75f, 0.55f), TextAnchor.MiddleCenter, heading: true);
        var q = report.Quest;
        if (q != null)
            Gui.Text(new Rect(410, 206, 1100, 38), $"{S.Zones.ZoneName(q.Dungeon)}  ·  {q.DifficultyName} {q.Size} {HamletUi.Pretty(q.Type)}", 24, Gui.Dd1Class, TextAnchor.MiddleCenter);

        if (_page == 0) DrawHeroes(report); else DrawLoot(report);
        Gui.Text(new Rect(680, 962, 560, 44), _page == 0 ? "1 / 2 · The returning party" : "2 / 2 · Recovered spoils", 22, Gui.Dd1Class, TextAnchor.MiddleCenter);

        if (_page == 1 && Gui.DdButton(new Rect(50, 980, 240, 66), "Back", true, 26)) _page = 0;
        if (_page == 0)
        {
            if (Gui.DdButton(new Rect(1630, 980, 260, 66), "Next", true, 28)) _page = 1;
        }
        else if (Gui.DdButton(new Rect(1590, 980, 300, 66), canLeave ? "Return to the Hamlet" : "Returning...", canLeave, 24)) d.BackToHamlet();
    }

    private static void DrawHeroes(HomecomingReport report)
    {
        const float fx = 390, fy = 270;
        var dead = Rr("deadhero_portrait.png");
        for (int i = 0; i < report.Heroes.Count && i < 4; i++)
        {
            var h = report.Heroes[i];
            float x = fx, y = fy + i * 160;
            CampaignPanelSkin.Section(new Rect(x, y, 1140, 148));
            var portrait = new Rect(x + 14, y + 25, 94, 94);
            var sprite = Art.HeroIcon(h.ClassId);
            var old = GUI.color;
            if (h.Died) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (sprite != null) Art.DrawSprite(portrait, sprite);
            GUI.color = old;
            if (h.Died && dead != null) GUI.DrawTexture(portrait, dead);
            Gui.Text(new Rect(x + 130, y + 12, 350, 42), h.Name, 30, h.Died ? Gui.Dim : Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
            Gui.Text(new Rect(x + 130, y + 56, 350, 30), HamletUi.Pretty(h.ClassId), 22, Gui.Dd1Class, TextAnchor.MiddleLeft);

            if (h.Died)
            {
                Gui.Text(new Rect(x + 510, y + 32, 580, 92), $"Did not return: {h.Cause}.", 24, Gui.Blood, TextAnchor.MiddleLeft);
                continue;
            }
            // Resolve on the right, as DD1 shows it.
            // DD1's resolve box sits in the frame's right column (campaign_status_offset 556,3).
            float rx = fx + 1010;
            Gui.Text(new Rect(rx, y + 14, 110, 48), h.ResolveAfter.ToString(), 42, h.ResolveAfter > h.ResolveBefore ? Gui.Gold : Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
            Gui.Text(new Rect(rx, y + 66, 110, 26), "resolve", 20, Gui.Dd1Class, TextAnchor.MiddleCenter);
            if (h.XpGained > 0) Gui.Text(new Rect(rx, y + 98, 110, 26), $"+{h.XpGained} xp", 20, Gui.Dd1Class, TextAnchor.MiddleCenter);
            if (h.ResolveAfter > h.ResolveBefore) Gui.Text(new Rect(x + 300, y + 100, 180, 28), "Resolve up!", 22, Gui.Gold, TextAnchor.MiddleRight, heading: true);

            Gui.Text(new Rect(x + 130, y + 96, 170, 30), $"Stress {h.Stress}/10", 22, h.Stress >= 7 ? Gui.Blood : Gui.Dd1Text, TextAnchor.MiddleLeft);
            float qy = y + 18;
            foreach (var quirk in h.NewQuirks.Take(2))
            {
                bool good = S.Catalog.IsPositive(quirk);
                Gui.Text(new Rect(x + 510, qy, 470, 36), (good ? "New quirk: " : "New negative quirk: ") + HamletUi.QuirkName(quirk), 22, good ? Gui.Gold : Gui.Blood, TextAnchor.MiddleLeft);
                qy += 38;
            }
            foreach (var quirk in h.LostQuirks.Take(1))
            {
                Gui.Text(new Rect(x + 510, qy, 470, 36), "Lost: " + HamletUi.QuirkName(quirk), 22, Gui.Dd1Class, TextAnchor.MiddleLeft);
                qy += 38;
            }
        }
    }

    private void DrawLoot(HomecomingReport report)
    {
        const float fx = 400, fy = 270;
        CampaignPanelSkin.Section(new Rect(fx, fy, 1120, 232));
        CampaignPanelSkin.Section(new Rect(fx, fy + 250, 520, 374));
        CampaignPanelSkin.Section(new Rect(fx + 540, fy + 250, 580, 374));

        // Quest rewards (only when it was completed).
        Gui.Text(new Rect(fx + 24, fy + 12, 1072, 38), report.Rewards.Count > 0 ? "Quest rewards" : "No quest rewards", 28, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        for (int i = 0; i < report.Rewards.Count && i < 6; i++)
        {
            var r = report.Rewards[i];
            var cell = new Rect(fx + 560 - System.Math.Min(6, report.Rewards.Count) * 56 + i * 112 + 4, fy + 64, 80, 144);
            ExpeditionSkin.Slot(cell);
            var icon = r.Type == "trinket" ? null : Art.InventoryIcon(r.Type, r.Amount, r.Type == "gold" ? 1750 : 99);
            if (r.Type == "trinket" && r.Id != null)
            {
                HeroSheet.TrinketIcon(cell, r.Id);
                if (cell.Contains(Event.current.mousePosition)) HeroSheet.TrinketTip(r.Id);
            }
            else if (icon != null) GUI.DrawTexture(cell, icon);
            else Gui.Text(cell, HamletUi.Pretty(r.Id ?? r.Type), 19, Gui.Dd1Text, TextAnchor.MiddleCenter);
            Gui.Text(new Rect(cell.x, cell.yMax - 28, cell.width - 4, 26), r.Type == "trinket" ? "" : r.Amount.ToString(), 22, Color.white, TextAnchor.LowerRight);
        }

        // What the party carried out: gold (and gems sold for gold), heirlooms, trinkets.
        int gold = (report.Loot.TryGetValue(Currency.Gold, out var g) ? g : 0) + report.GemGold;
        Gui.Text(new Rect(fx + 24, fy + 270, 250, 38), "Gold recovered", 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(fx + 274, fy + 270, 220, 38), Gui.Num(gold, "#,0"), 30, Gui.Gold, TextAnchor.MiddleRight, heading: true);
        if (report.GemGold > 0)
            Gui.Text(new Rect(fx + 24, fy + 318, 470, 60), $"Includes {Gui.Num(report.GemGold, "#,0")} gold from {report.Gems.Values.Sum()} gems", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);

        Gui.Text(new Rect(fx + 24, fy + 410, 470, 38), "Heirlooms", 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        var heirlooms = report.Loot.Where(kv => Currency.Heirlooms.Contains(kv.Key)).ToList();
        for (int i = 0; i < heirlooms.Count; i++)
        {
            var (key, amount) = (heirlooms[i].Key, heirlooms[i].Value);
            var tex = Art.Dd1("shared", "estate", $"currency.{key}.icon.png");
            var cell = new Rect(fx + 28 + i * 120, fy + 470, 40, 40);
            if (tex != null) GUI.DrawTexture(cell, tex);
            Gui.Text(new Rect(cell.xMax + 6, cell.y, 70, 40), amount.ToString(), 26, Gui.Dd1Text, TextAnchor.MiddleLeft, heading: true);
        }
        if (heirlooms.Count == 0) Gui.Text(new Rect(fx + 28, fy + 470, 450, 40), "None", 22, Gui.Dim, TextAnchor.MiddleLeft);
        Gui.Text(new Rect(fx + 564, fy + 270, 530, 38), $"Trinkets recovered · {report.Trinkets.Count}", 28, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        if (report.Trinkets.Count == 0)
            Gui.Text(new Rect(fx + 564, fy + 334, 530, 80), "No trinkets recovered", 24, Gui.Dim, TextAnchor.MiddleCenter);
        else
        {
            const int columns = 5;
            int rows = (report.Trinkets.Count + columns - 1) / columns;
            var view = new Rect(fx + 560, fy + 320, 540, 280);
            _trinketScroll = GUI.BeginScrollView(view, _trinketScroll, new Rect(0, 0, 520, Mathf.Max(280, rows * 160)));
            for (int i = 0; i < report.Trinkets.Count; i++)
            {
                var cell = new Rect(i % columns * 104 + 12, i / columns * 160 + 4, 80, 144);
                ExpeditionSkin.Slot(cell);
                HeroSheet.TrinketIcon(cell, report.Trinkets[i]);
                if (cell.Contains(Event.current.mousePosition)) HeroSheet.TrinketTip(report.Trinkets[i]);
            }
            GUI.EndScrollView();
        }
    }
}
