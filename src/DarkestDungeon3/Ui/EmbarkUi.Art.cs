using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class EmbarkUi
{
    private bool _expeditionStyle;

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

    private static void ExpeditionFrame(Rect r, float opacity = 0.9f, bool highlighted = false)
    {
        Gui.Fill(r, new Color(0.025f, 0.028f, 0.03f, opacity));
        var line = highlighted ? Gui.Gold : new Color(0.39f, 0.34f, 0.23f, 0.8f);
        Gui.Fill(new Rect(r.x, r.y, r.width, 1), line);
        Gui.Fill(new Rect(r.x, r.yMax - 1, r.width, 1), line);
        Gui.Fill(new Rect(r.x, r.y, 1, r.height), line);
        Gui.Fill(new Rect(r.xMax - 1, r.y, 1, r.height), line);
    }

    private static bool ExpeditionButton(Rect r, string text, bool enabled = true, int size = 28)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        ExpeditionFrame(r, hover ? 0.96f : 0.9f, hover);
        Gui.Text(r, text, size, enabled ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleCenter, heading: true);
        return enabled && Gui.Hotspot(r);
    }

    private void DrawPaintedQuestScroll(QuestOffer q)
    {
        var panel = new Rect(30, 112, 400, 752);
        ExpeditionFrame(panel, 0.95f);
        var preview = ExpeditionMapArt.Preview(q.Dungeon);
        if (preview != null) GUI.DrawTexture(new Rect(34, 116, 392, 188), preview, ScaleMode.ScaleAndCrop);
        Gui.Fill(new Rect(34, 306, 392, 76), new Color(0.22f, 0.035f, 0.027f, 0.9f));
        Gui.Text(new Rect(44, 310, 372, 38), AreaName(q.Dungeon), 32, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        Gui.Text(new Rect(44, 348, 372, 30), $"{q.DifficultyName} · {Cap(q.Size)} · {HamletUi.Pretty(q.Type)}{(q.IsPlot ? " · Plot" : "")}", 18, Gui.Dd1Class, TextAnchor.MiddleCenter);

        string restriction = Homecoming.MinResolveFor(q.Difficulty) > 0
            ? $"Only heroes of resolve {Homecoming.MinResolveFor(q.Difficulty)} or higher dare go."
            : $"Heroes of resolve {Homecoming.MaxResolveFor(q.Difficulty)} or lower will join; the experienced scorn easy work.";
        Gui.Text(new Rect(56, 393, 348, 82), (q.IsPlot ? "A quest of consequence.\n" : "") + restriction, 17, Gui.Dd1Text);

        Gui.Fill(new Rect(52, 486, 356, 1), new Color(0.38f, 0.35f, 0.27f, 0.7f));
        string camp = q.Length >= 3 ? "Two camps allowed" : q.Length == 2 ? "One camp allowed" : "No camping";
        Gui.Text(new Rect(56, 491, 348, 28), camp, 18, Gui.Dd1Class, TextAnchor.MiddleLeft);
        Gui.Text(new Rect(56, 531, 348, 30), "Goal", 26, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(62, 565, 336, 60), GoalLine(q), 18, Gui.Dd1Text);
        Gui.Fill(new Rect(52, 632, 356, 1), new Color(0.38f, 0.35f, 0.27f, 0.7f));
        Gui.Text(new Rect(44, 636, 372, 32), "Rewards", 26, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        for (int i = 0; i < q.Rewards.Count && i < 8; i++)
        {
            var rw = q.Rewards[i];
            var r = new Rect(68 + (i % 4) * 86, 674 + (i / 4) * 92, 72, 88);
            var icon = rw.Type == "trinket" ? Art.Dd1("panels", "icons_equip", "trinket", "inv_trinket+_unknown.png")
                : Art.InventoryIcon(rw.Type, rw.Amount, rw.Type == "gold" ? 1750 : 99);
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
            Gui.Text(new Rect(r.x - 6, r.yMax - 25, r.width + 12, 25), rw.Type == "trinket" ? HamletUi.Pretty(rw.Id ?? "trinket") : rw.Amount.ToString(), rw.Type == "trinket" ? 14 : 21, Color.white, TextAnchor.LowerCenter);
            if (r.Contains(Event.current.mousePosition)) Gui.Tip(rw.Type == "trinket" ? HamletUi.Pretty(rw.Id ?? "trinket") : $"{rw.Amount} {HamletUi.Pretty(rw.Type)}");
        }
    }
}
