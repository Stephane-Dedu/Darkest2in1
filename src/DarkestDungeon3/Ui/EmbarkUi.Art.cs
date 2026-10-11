using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class EmbarkUi
{
    private bool _expeditionStyle;
    private bool ProvisionBackButton() => ExpeditionSkin.Button(new Rect(30, 1000, 300, 60), "Back to quests", size: 26);

    private static readonly Dictionary<string, Vector2> PaintedMapSpots = new()
    {
        ["crypts"] = new(730, 230),
        ["warrens"] = new(610, 450),
        ["weald"] = new(900, 635),
        ["cove"] = new(1180, 470),
        [QuestBoard.DarkestDungeon] = new(1110, 126)
    };

    private string AreaName(string zone) => _expeditionStyle && zone == QuestBoard.DarkestDungeon
        ? "The Mountain" : S.Zones.ZoneName(zone);

    private void DrawPaintedMap(Texture2D background)
    {
        var canvas = new Rect(0, 0, Gui.W, Gui.H);
        GUI.DrawTexture(canvas, background);
        foreach (string location in CampaignRegions.Legacy)
            if (_mapAreas.TryGetValue(location, out string area) && ExpeditionMapArt.Overlay(area) is { } overlay)
                GUI.DrawTexture(canvas, overlay);
    }

    private static bool ExpeditionButton(Rect r, string text, bool enabled = true, int size = 28)
    {
        return ExpeditionSkin.Button(r, text, enabled, size);
    }

    private void DrawPaintedQuestScroll(QuestOffer q)
    {
        DrawDossierBackdrop(q.Dungeon);
        ExpeditionSkin.Text(new Rect(145, 270, 360, 46), AreaName(q.Dungeon), 36, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter, heading: true);
        ExpeditionSkin.Text(new Rect(145, 322, 360, 48), $"{q.DifficultyName} · {Cap(q.Size)} · {HamletUi.Pretty(q.Type)}", 23, ExpeditionSkin.Brass, TextAnchor.MiddleCenter);

        string restriction = Homecoming.MinResolveFor(q.Difficulty) > 0
            ? $"Only heroes of resolve {Homecoming.MinResolveFor(q.Difficulty)} or higher dare go."
            : $"Heroes of resolve {Homecoming.MaxResolveFor(q.Difficulty)} or lower will join; the experienced scorn easy work.";
        ExpeditionSkin.Text(new Rect(159, 388, 332, 94), (q.IsPlot ? "A quest of consequence.\n" : "") + restriction, 22, ExpeditionSkin.Ivory);

        string camp = q.Length >= 3 ? "Two camps allowed" : q.Length == 2 ? "One camp allowed" : "No camping";
        ExpeditionSkin.Text(new Rect(183, 510, 320, 28), camp, 23, ExpeditionSkin.Muted);
        ExpeditionSkin.Text(new Rect(159, 546, 332, 36), "Objective", 29, ExpeditionSkin.Ivory, heading: true);
        string goal = GoalLine(q);
        ExpeditionSkin.TextFit(new Rect(165, 586, 320, 64), goal, 24, ExpeditionSkin.Ivory);
        ExpeditionSkin.Tip(new Rect(165, 586, 320, 64), goal);
        ExpeditionSkin.Text(new Rect(145, 676, 360, 36), "Rewards", 29, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter, heading: true);
        for (int i = 0; i < q.Rewards.Count && i < 8; i++)
        {
            var rw = q.Rewards[i];
            var r = new Rect(167 + (i % 4) * 80, 720 + (i / 4) * 124, 72, 118);
            var icon = rw.Type == "trinket" ? Art.Dd1("panels", "icons_equip", "trinket", "inv_trinket+_unknown.png")
                : Art.InventoryIcon(rw.Type, rw.Amount, rw.Type == "gold" ? 1750 : 99);
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
            ExpeditionSkin.Text(new Rect(r.x - 2, r.yMax - 26, r.width + 4, 25), rw.Type == "trinket" ? "Trinket" : rw.Amount.ToString(), 22, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter);
            ExpeditionSkin.Tip(r, rw.Type == "trinket" ? HamletUi.Pretty(rw.Id ?? "trinket") : $"{rw.Amount} {HamletUi.Pretty(rw.Type)}");
        }
    }

    private void DrawEmptyQuestPanel()
    {
        DrawDossierBackdrop(_focusedZone ?? "dd2_city");
        ExpeditionSkin.Text(new Rect(145, 270, 360, 46), "Choose your path", 34, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter, heading: true);
        ExpeditionSkin.Text(new Rect(159, 388, 332, 94), "Select a quest beneath a region to inspect its objective, rewards and resolve requirements.", 24, ExpeditionSkin.Ivory);
        ExpeditionSkin.Text(new Rect(159, 592, 332, 112), "Click heroes in the roster or drag them into the party. Right-click a hero to inspect their equipment.", 24, ExpeditionSkin.Muted);
    }

    private static void DrawDossierBackdrop(string zone)
    {
        // DD1's inset scroll and red name strip, without the tall boxed-card frame.
        var r = new Rect(125, 132, 400, 843);
        var scroll = Qs("quest_select.questverbose_bg.png");
        if (scroll != null) GUI.DrawTexture(r, scroll); else Gui.Fill(r, ExpeditionSkin.Ink);
        var preview = ExpeditionMapArt.Preview(zone);
        if (preview != null) GUI.DrawTexture(new Rect(145, 140, 360, 120), preview, ScaleMode.ScaleAndCrop);
    }

    private void DrawQuestMarker(Rect r, QuestOffer q, bool hover)
    {
        bool selected = q == _quest;
        if (selected && Qs("quest_select_selected.png") is { } selection)
            GUI.DrawTexture(new Rect(r.x - 16, r.y - 16, r.width + 32, r.height + 32), selection);
        var ring = Qs($"quest_select_length_{(q.IsPlot ? "plot" : "generated")}_{Mathf.Clamp(q.Length, 0, 4)}.png")
            ?? Qs($"quest_select_length_generated_{Mathf.Clamp(q.Length, 0, 5)}.png");
        if (ring != null) GUI.DrawTexture(hover ? new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6) : r, ring);
        var danger = q.Difficulty >= 5 ? ExpeditionSkin.Red : q.Difficulty >= 3 ? ExpeditionSkin.Brass : ExpeditionSkin.Ivory;
        ExpeditionSkin.QuestIcon(new Rect(r.x + 17, r.y + 17, 38, 38), q.Type, danger);
        ExpeditionSkin.Tip(r, $"{q.DifficultyName} · {Cap(q.Size)} · {HamletUi.Pretty(q.Type)}" + (q.IsPlot ? "\nPlot quest" : ""));
    }
}
