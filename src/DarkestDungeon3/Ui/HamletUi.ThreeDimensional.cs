using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class HamletUi
{
    internal static readonly Rect TownModeButton = new(1190, 42, 320, 58);
    private static readonly Rect ResetViewButton = new(1320, 112, 190, 42);

    private void DrawTownModeSwitch()
    {
        if (TownControl(TownModeButton, HamletStage.Enabled ? "Painted town" : "3D town", 27))
            HamletStage.SetEnabled(!HamletStage.Enabled);
        if (!HamletStage.Enabled) return;
        if (TownControl(ResetViewButton, "Reset view", 22)) HamletStage.Instance?.ResetView();
        ExpeditionSkin.Text(new Rect(960, 164, 550, 28), HamletStage.Instance?.Failure ?? (HamletStage.Instance?.Ready == true ? "Wheel to zoom · Middle-drag to pan" : "Preparing town scenery…"), 20,
            HamletStage.Instance?.Failure != null ? ExpeditionSkin.Red : ExpeditionSkin.Muted, TextAnchor.MiddleRight);
    }

    private static bool TownControl(Rect rect, string label, int size)
    {
        bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
        ExpeditionSkin.Banner(rect, hover ? ExpeditionSkin.Red : ExpeditionSkin.Brass);
        ExpeditionSkin.Text(new Rect(rect.x + 12, rect.y + 4, rect.width - 24, rect.height - 8), label, size, ExpeditionSkin.Ivory, TextAnchor.MiddleCenter, heading: true);
        if (!hover || Event.current.type != EventType.MouseDown || Event.current.button != 0) return false;
        Event.current.Use();
        return true;
    }

    private bool DrawThreeDimensionalTown(bool interactive)
    {
        if (!HamletStage.Enabled || HamletStage.Instance == null) return false;
        var stage = HamletStage.Instance;
        stage.Request();
        if (!stage.Ready) return false; // Keep the approved town usable while real models load or if they fail.
        GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), stage.Texture);
        var mouse = Event.current.mousePosition;
        bool overNav = mouse.x < 120 || TownModeButton.Contains(mouse) || ResetViewButton.Contains(mouse);
        bool canHit = interactive && GUI.enabled && !_exchangeOpen && !overNav && mouse.x < RosterX && mouse.y < BarY;
        stage.SetFocus(_panel == Panel.Town ? null : _building);
        foreach (var id in HamletStage.Services) stage.SetAvailable(id, IsOpen(id));
        var hovered = canHit ? stage.Pick(mouse) : null;
        stage.Hover(hovered);
        _hovered = hovered;
        if (canHit) stage.HandleViewInput(Event.current);
        if (hovered == null) return true;
        var anchor = stage.Anchor(hovered);
        float x = Mathf.Clamp(anchor.x, 300, RosterX - 200), y = Mathf.Clamp(anchor.y - 38, 190, 880);
        ExpeditionSkin.Banner(new Rect(x - 170, y - 30, 340, 60), ExpeditionSkin.Ivory);
        ExpeditionSkin.OutlinedHeading(new Rect(x - 165, y - 26, 330, 50), Name(hovered), 29,
            IsOpen(hovered) ? ExpeditionSkin.Ivory : ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
        if (!IsOpen(hovered)) ExpeditionSkin.Text(new Rect(x - 170, y + 30, 340, 30), "Not yet restored", 21, ExpeditionSkin.Muted, TextAnchor.MiddleCenter);
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Event.current.Use();
            Open(hovered);
            stage.SetFocus(hovered);
        }
        return true;
    }
}
