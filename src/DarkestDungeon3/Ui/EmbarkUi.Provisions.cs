using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class EmbarkUi
{
    private void DrawProvisionBackdrop(int length)
    {
        _expeditionStyle = true;
        ExpeditionSkin.Prepare();
        var bg = Prov("provision.background.png");
        if (bg != null) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), bg); else Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), ExpeditionSkin.Ink);
        var window = Prov("provision.character_background.png");
        if (window != null) GUI.DrawTexture(Window, window);
        var keeper = Prov("provision.character.png");
        if (keeper != null)
        {
            float k = Mathf.Min(1f, (Window.height - 4) / keeper.height);
            GUI.DrawTexture(new Rect(Window.x + 2, Window.yMax - keeper.height * k - 2, keeper.width * k, keeper.height * k), keeper);
        }
        ExpeditionSkin.Text(new Rect(Window.x + 40, Window.y + 20, 520, 60), "Provisioner", 46, ExpeditionSkin.Ivory, heading: true);
        var q = _quest;
        if (q != null) ExpeditionSkin.Text(new Rect(1000, 60, 860, 44), $"{S.Zones.ZoneName(q.Dungeon)} · {q.DifficultyName} · {Cap(q.Size)}", 27, ExpeditionSkin.Ivory, TextAnchor.MiddleRight, heading: true);
        ExpeditionSkin.Text(new Rect(814, 108, 680, 28), $"Recommended food: {S.Provisioner.MinimumFood(length)} · Charged on embark", 22, ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
    }

    private static void DrawProvisionStack(Rect r, string key, int count, bool dim = false)
    {
        ExpeditionSkin.Slot(r);
        var icon = Art.InventoryIcon(key, Mathf.Max(1, count), Mathf.Max(1, S.Content.Items.StackLimit(key)));
        var old = GUI.color;
        if (dim) GUI.color = new Color(.38f, .36f, .34f);
        if (icon != null) GUI.DrawTexture(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 22), icon, ScaleMode.ScaleToFit);
        GUI.color = old;
        ExpeditionSkin.Text(new Rect(r.x + 4, r.yMax - 29, r.width - 8, 26), count.ToString(), 23,
            dim ? ExpeditionSkin.Muted : ExpeditionSkin.Ivory, TextAnchor.MiddleRight);
        ExpeditionSkin.Tip(r, HamletUi.Pretty(key) + $"\n{count} available\nClick to buy or return · Shift+click for a stack");
    }
}
