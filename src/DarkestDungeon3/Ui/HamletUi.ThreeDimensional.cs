using DarkestDungeon3.Dd2;
using System.Linq;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class HamletUi
{
    internal static readonly Rect ResetViewButton = new(1320, 42, 190, 42);
    internal static readonly Rect CameraMotionButton = new(1010, 42, 295, 42);
    private string _townKeyboardFocus;

    private void DrawTownControls()
    {
        bool failed=HamletStage.Instance?.Failure!=null;
        if (TownControl(ResetViewButton, failed?"Retry scenery":"Reset view", 22))
        { if (failed) HamletStage.Instance?.Retry(); else HamletStage.Instance?.ResetView(); }
        if (TownControl(CameraMotionButton,HamletStage.MotionEnabled ? "Camera motion: On" : "Camera motion: Off",22))
            HamletStage.SetMotion(!HamletStage.MotionEnabled);
        ExpeditionSkin.Text(new Rect(780, 105, 730, 28), HamletStage.Instance?.Failure ?? (HamletStage.Instance?.Ready == true ? "Tab / arrows to select · Enter to open · Wheel / middle-drag" : "Preparing town scenery…"), 20,
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
        if (HamletStage.Instance == null) return false;
        var stage = HamletStage.Instance;
        stage.Request();
        if (!stage.Ready) return false; // Keep the approved town usable while real models load or if they fail.
        GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), stage.Texture);
        if (HamletPreview.OwnsSession && HamletPreview.PaintAuditRequested)
            GUI.DrawTexture(new Rect(125,685,260,220),stage.GroundPaintTexture,ScaleMode.ScaleToFit);
        var mouse = Event.current.mousePosition;
        bool overNav = mouse.x < 120 || mouse.y<180 || ResetViewButton.Contains(mouse) || CameraMotionButton.Contains(mouse);
        bool canHit = interactive && GUI.enabled && !_exchangeOpen && !overNav && mouse.x < RosterX && mouse.y < BarY;
        stage.SetFocus(_panel == Panel.Town ? null : _building);
        foreach (var id in HamletStage.Services) stage.SetAvailable(id, IsOpen(id));
        if (Event.current.isMouse && Event.current.type!=EventType.Repaint) _townKeyboardFocus=null;
        bool keyboardAllowed=interactive && GUI.enabled && !_exchangeOpen && !HeroSheet.RealmOpen && !_regionsOpen;
        if (keyboardAllowed) NavigateTown(stage);
        var hovered = keyboardAllowed && _townKeyboardFocus!=null ? _townKeyboardFocus : canHit ? stage.Pick(mouse) : null;
        stage.Hover(hovered);
        _hovered = hovered;
        if (canHit) stage.HandleViewInput(Event.current);
        if (hovered == null) return true;
        var anchor = stage.Anchor(hovered);
        float x = Mathf.Clamp(anchor.x, 300, RosterX - 200), y = Mathf.Clamp(anchor.y - 38, 190, 880);
        Gui.Fill(new Rect(x-166,y-25,332,52),new Color(.025f,.023f,.022f,.94f));
        ExpeditionSkin.Banner(new Rect(x - 170, y - 30, 340, 60), ExpeditionSkin.Brass);
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

    private void NavigateTown(HamletStage stage)
    {
        var e=Event.current;
        if (e.type!=EventType.KeyDown) return;
        if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space)
        {
            if (_townKeyboardFocus==null) return;
            e.Use(); Open(_townKeyboardFocus); stage.SetFocus(_townKeyboardFocus); return;
        }
        if (e.keyCode==KeyCode.Escape) { _townKeyboardFocus=null; e.Use(); return; }
        if (e.keyCode is not (KeyCode.Tab or KeyCode.LeftArrow or KeyCode.RightArrow or KeyCode.UpArrow or KeyCode.DownArrow)) return;
        var choices=HamletStage.Services.Select(id=>(Id:id,Point:stage.VisiblePoint(id)))
            .Where(p=>p.Point.HasValue).OrderBy(p=>p.Point.Value.y).ThenBy(p=>p.Point.Value.x).ToArray();
        if (choices.Length==0) return;
        int index=System.Array.FindIndex(choices,p=>p.Id==_townKeyboardFocus);
        if (index<0) _townKeyboardFocus=choices[e.shift?choices.Length-1:0].Id;
        else if (e.keyCode==KeyCode.Tab) _townKeyboardFocus=choices[(index+(e.shift?choices.Length-1:1))%choices.Length].Id;
        else
        {
            var direction=e.keyCode==KeyCode.LeftArrow?Vector2.left:e.keyCode==KeyCode.RightArrow?Vector2.right:
                e.keyCode==KeyCode.UpArrow?Vector2.down:Vector2.up;
            var origin=choices[index].Point.Value;
            var next=choices.Where(p=>Vector2.Dot(p.Point.Value-origin,direction)>1)
                .OrderBy(p=>{ var d=p.Point.Value-origin; return d.magnitude+2*Mathf.Abs(d.x*direction.y-d.y*direction.x); })
                .Select(p=>p.Id).FirstOrDefault();
            if (next!=null) _townKeyboardFocus=next;
        }
        e.Use();
    }
}
