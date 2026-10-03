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

    private static Texture2D Rr(string file) => Art.Dd1("raid_results", file);
    private static Session S => Session.Current;
    private const float Left = 510, Width = 899;   // completion background centred on the screen

    public void Draw(Driver d, bool canLeave)
    {
        var report = d.LastReport;
        if (report != _shown) { _shown = report; _page = 0; }
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
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
        if (bg != null) GUI.DrawTexture(new Rect(Left, 0, Width, 1080), bg);
        string result = report.Result switch { "complete" => "Quest Complete", "defeat" => "Defeat", _ => "Retreat" };
        // On DD1's banner (quest_result_pos 960,150).
        Gui.Text(new Rect(Left, 128, Width, 50), result, 40, report.Result == "complete" ? Gui.Gold : new Color(0.95f, 0.75f, 0.55f), TextAnchor.MiddleCenter, heading: true);
        var q = report.Quest;
        if (q != null)
            Gui.Text(new Rect(Left, 190, Width, 40), $"{S.Zones.ZoneName(q.Dungeon)}  ·  {q.DifficultyName} {q.Size} {HamletUi.Pretty(q.Type)}", 24, Gui.Dd1Class, TextAnchor.MiddleCenter);

        if (_page == 0) DrawHeroes(report); else DrawLoot(report);

        if (_page == 1 && Gui.DdButton(new Rect(50, 980, 240, 66), "Back", true, 26)) _page = 0;
        if (_page == 0)
        {
            if (Gui.DdButton(new Rect(1630, 980, 260, 66), "Next", true, 28)) _page = 1;
        }
        else if (Gui.DdButton(new Rect(1590, 980, 300, 66), canLeave ? "Return to the Hamlet" : "Returning...", canLeave, 24)) d.BackToHamlet();
    }

    private static void DrawHeroes(HomecomingReport report)
    {
        var frames = Rr("raid_results.heroes_frames.png");
        float fx = Left + 450 - 340, fy = 236;
        if (frames != null) GUI.DrawTexture(new Rect(fx, fy, 680, 675), frames);
        var dead = Rr("deadhero_portrait.png");
        for (int i = 0; i < report.Heroes.Count && i < 4; i++)
        {
            var h = report.Heroes[i];
            float x = fx + 20, y = fy + i * 168;
            var portrait = new Rect(x + 5, y + 64, 85, 85);
            var sprite = Art.HeroIcon(h.ClassId);
            var old = GUI.color;
            if (h.Died) GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (sprite != null) Art.DrawSprite(portrait, sprite);
            GUI.color = old;
            if (h.Died && dead != null) GUI.DrawTexture(portrait, dead);
            Gui.Text(new Rect(x + 6, y + 10, 400, 40), h.Name, 28, h.Died ? Gui.Dim : Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);

            if (h.Died)
            {
                Gui.Text(new Rect(x + 110, y + 70, 520, 70), $"Did not return: {h.Cause}.", 21, Gui.Blood, TextAnchor.UpperLeft);
                continue;
            }
            // Resolve on the right, as DD1 shows it.
            // DD1's resolve box sits in the frame's right column (campaign_status_offset 556,3).
            float rx = fx + 556;
            Gui.Text(new Rect(rx - 30, y + 64, 124, 40), h.ResolveAfter.ToString(), 40, h.ResolveAfter > h.ResolveBefore ? Gui.Gold : Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
            Gui.Text(new Rect(rx - 30, y + 104, 124, 22), "resolve", 16, Gui.Dd1Class, TextAnchor.MiddleCenter);
            if (h.XpGained > 0) Gui.Text(new Rect(rx - 30, y + 126, 124, 22), $"+{h.XpGained} xp", 16, Gui.Dd1Class, TextAnchor.MiddleCenter);
            if (h.ResolveAfter > h.ResolveBefore) Gui.Text(new Rect(x + 300, y + 14, 200, 30), "Resolve up!", 22, Gui.Gold, TextAnchor.MiddleRight, heading: true);

            float qy = y + 70;
            Gui.Text(new Rect(x + 110, qy, 330, 24), $"Stress {h.Stress}/10", 18, h.Stress >= 7 ? Gui.Blood : Gui.Dd1Text, TextAnchor.MiddleLeft);
            qy += 24;
            foreach (var quirk in h.NewQuirks.Take(2))
            {
                bool good = S.Catalog.IsPositive(quirk);
                Gui.Text(new Rect(x + 110, qy, 330, 24), (good ? "New quirk: " : "New affliction of the mind: ") + HamletUi.QuirkName(quirk), 18, good ? Gui.Gold : Gui.Blood, TextAnchor.MiddleLeft);
                qy += 24;
            }
            foreach (var quirk in h.LostQuirks.Take(1))
            {
                Gui.Text(new Rect(x + 110, qy, 330, 24), "Lost: " + HamletUi.QuirkName(quirk), 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
                qy += 24;
            }
        }
    }

    private static void DrawLoot(HomecomingReport report)
    {
        var frames = Rr("raid_results.items_frames.png");
        float fx = Left + 450 - 266, fy = 230;
        if (frames != null) GUI.DrawTexture(new Rect(fx, fy, 533, 723), frames);
        var items = S.Content.Items;

        // Quest rewards (only when it was completed).
        Gui.Text(new Rect(fx, fy + 6, 533, 34), report.Rewards.Count > 0 ? "Quest rewards" : "No quest rewards", 26, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        for (int i = 0; i < report.Rewards.Count && i < 6; i++)
        {
            var r = report.Rewards[i];
            var cell = new Rect(fx + 266 - System.Math.Min(6, report.Rewards.Count) * 40 + i * 80 + 4, fy + 54, 72, 144);
            var icon = r.Type == "trinket" ? null : Art.InventoryIcon(r.Type, r.Amount, r.Type == "gold" ? 1750 : 99);
            if (icon != null) GUI.DrawTexture(cell, icon); else Gui.Text(cell, HamletUi.Pretty(r.Id ?? r.Type), 15, Gui.Dd1Text, TextAnchor.MiddleCenter);
            Gui.Text(new Rect(cell.x, cell.yMax - 28, cell.width - 4, 26), r.Type == "trinket" ? "" : r.Amount.ToString(), 22, Color.white, TextAnchor.LowerRight);
        }

        // What the party carried out: gold (and gems sold for gold), heirlooms, trinkets.
        int gold = (report.Loot.TryGetValue(Currency.Gold, out var g) ? g : 0) + report.GemGold;
        Gui.Text(new Rect(fx + 30, fy + 250, 300, 34), "Gold", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(fx + 300, fy + 250, 200, 34), Gui.Num(gold, "#,0"), 28, Gui.Gold, TextAnchor.MiddleRight, heading: true);
        if (report.GemGold > 0)
            Gui.Text(new Rect(fx + 30, fy + 286, 470, 26), $"including {Gui.Num(report.GemGold, "#,0")} for {report.Gems.Values.Sum()} gems", 18, Gui.Dd1Class, TextAnchor.MiddleLeft);

        Gui.Text(new Rect(fx + 30, fy + 470, 300, 34), "Heirlooms", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        var heirlooms = report.Loot.Where(kv => Currency.Heirlooms.Contains(kv.Key)).ToList();
        for (int i = 0; i < heirlooms.Count; i++)
        {
            var (key, amount) = (heirlooms[i].Key, heirlooms[i].Value);
            var tex = Art.Dd1("shared", "estate", $"currency.{key}.icon.png");
            var cell = new Rect(fx + 40 + i * 120, fy + 520, 40, 40);
            if (tex != null) GUI.DrawTexture(cell, tex);
            Gui.Text(new Rect(cell.xMax + 6, cell.y, 70, 40), amount.ToString(), 26, Gui.Dd1Text, TextAnchor.MiddleLeft, heading: true);
        }
        if (heirlooms.Count == 0) Gui.Text(new Rect(fx + 40, fy + 520, 400, 40), "None", 22, Gui.Dim, TextAnchor.MiddleLeft);
        if (report.Trinkets.Count > 0)
            Gui.Text(new Rect(fx + 30, fy + 600, 470, 100), "Trinkets: " + string.Join(", ", report.Trinkets.Select(HamletUi.Pretty)), 19, Gui.Dd1Text);
    }
}
