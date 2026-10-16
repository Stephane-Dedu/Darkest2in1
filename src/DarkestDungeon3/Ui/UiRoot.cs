using Assets.Code.Game;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Draws whichever screen the current phase needs, and the way in from DD2's main menu.</summary>
internal sealed class UiRoot : MonoBehaviour
{
    /// <summary>A window or prompt is up in the dungeon (keys shouldn't walk the party).</summary>
    public static bool ModalOpen;

    private readonly HamletUi _hamlet = new();
    private readonly EmbarkUi _embark = new();
    private readonly CrawlUi _crawl = new();
    private bool _embarking;

    private void Awake() => gameObject.AddComponent<CampaignMenu>();

    private int? _openingFrame;

    /// <summary>DD1's loading screen for the opening raid (starting_save/persist.loading_screen.json: loading_screen.old_road.png).</summary>
    private static void DrawOldRoad()
    {
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
        if (Art.Dd1("loading_screen", "loading_screen.old_road.png") is { } art) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), art, ScaleMode.ScaleAndCrop);
        Gui.Text(new Rect(0, 60, Gui.W, 80), Dd1Text.Get("PSN", "dungeon_name_old_road") ?? "The Old Road", 56, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        if (Dd1Text.Get("PSN", "str_old_road_tip") is { } tip)
            Gui.Text(new Rect(260, Gui.H - 170, Gui.W - 520, 110), tip, 24, Gui.Dd1Text, TextAnchor.MiddleCenter);
    }

    private void OnGUI()
    {
        var d = Driver.Instance;
        if (d == null) return;
        Gui.Begin();
        Drag.Begin();
        GUI.depth = -1000;

        bool fullscreen = d.Phase is Phase.Hamlet or Phase.Crawling or Phase.Homecoming or Phase.Recovery;
        Gui.BlockInput(fullscreen || CinematicPlayer.Active);
        if (CinematicPlayer.Active)
        {
            Drag.Cancel();
            try { CinematicPlayer.Draw(); } catch (System.Exception e) { Plugin.Log.LogError(e); }
            return;
        }

        try
        {
            switch (d.Phase)
            {
                case Phase.Off:
                    // CampaignMenu owns the native menu entry and loading gate.
                    break;
                case Phase.Hamlet when d.OpeningDue:
                    // DD1's opening: the Old Road's loading screen, then the raid (the same embark as any quest).
                    _openingFrame ??= Time.frameCount;
                    DrawOldRoad();
                    if (Time.frameCount > _openingFrame.Value && Event.current.type == EventType.Repaint) { _openingFrame = null; d.EmbarkOpening(); }
                    break;
                case Phase.Hamlet:
                    if (_embarking)
                    {
                        _embark.Draw();
                        if (_embark.WantsBack) { _embark.WantsBack = false; _embarking = false; }
                    }
                    else
                    {
                        _hamlet.Draw();
                        if (_hamlet.WantsEmbark) { _hamlet.WantsEmbark = false; _embarking = true; }
                    }
                    break;
                case Phase.Embarking when d.Expedition?.Quest?.MapName == Core.Dungeon.PlotMap.Opening:
                    DrawOldRoad();
                    break;
                case Phase.Embarking:
                    Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
                    Gui.Text(new Rect(460, 480, 1000, 70), "The party sets out...", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
                    _embarking = false;
                    break;
                case Phase.Recovery:
                    _embarking = false;
                    Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
                    Gui.Text(new Rect(460, 350, 1000, 80), "Your expedition is saved", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
                    Gui.Text(new Rect(460, 450, 1000, 100), d.RecoveryMessage, 26, Gui.Dd1Text, TextAnchor.MiddleCenter);
                    if (d.CanResumeSaved && Gui.DdButton(new Rect(710, 620, 500, 72), "Resume expedition", true, 30)) d.ResumeExpedition();
                    if (Gui.DdButton(new Rect(710, 720, 500, 72), "Main menu", true, 30))
                    {
                        d.LeaveHamlet();
                        if (Dd2.Dd2Run.Hosting) Dd2.Dd2Run.End();
                    }
                    break;
                case Phase.Crawling:
                    _crawl.Draw();
                    break;
                case Phase.Fighting:
                    // Until DD2's fight is ready the DD1 scene stays, then it fades into the fight (DD1's battle start).
                    // A fight starting in place first takes the scene the party stands in as its backdrop, and fades
                    // back into it when it ends.
                    if (Dd2.Dd1Backdrop.WantsSnapshot && Event.current.type == EventType.Repaint)
                        Dd2.Dd1Backdrop.TakeSnapshot(_crawl.CaptureScene());
                    float cover = Dd2.Dd2Combat.CoverAlpha;
                    if (cover > 0f && Event.current.type == EventType.Repaint) _crawl.DrawBackdrop(cover, Dd2.Dd2Combat.LayoutBlend);
                    if (!Dd2.Dd2Combat.Returning)
                    {
                        Dd2.Dd1MonsterView.Draw();
                        DrawRetreatButton();
                    }
                    Gui.DrawAnnouncement();
                    break;
                case Phase.Homecoming:
                    Dd2.HamletStage.Instance?.Prewarm();
                    DrawHomecoming(d);
                    break;
            }
        }
        catch (System.Exception e)
        {
            // One bad frame must not kill the UI for good.
            Plugin.Log.LogError(e);
        }
        Drag.Overlay();
        Gui.DrawTip();
    }

    /// <summary>DD1's retreat during our fights (DD2 hides its own outside Kingdoms).</summary>
    private static void DrawRetreatButton()
    {
        string why = DarkestDungeon3.Dd2.Dd2Combat.WhyNoRetreat();
        if (why == "Not now") return;
        var r = new Rect(24, 150, 64, 64);
        var tex = Art.Panel("retreat_button.png");
        bool hover = r.Contains(Event.current.mousePosition);
        var old = GUI.color;
        if (why != null) GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        if (tex != null) GUI.DrawTexture(hover && why == null ? new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6) : r, tex);
        else Gui.Fill(r, new Color(0.2f, 0.05f, 0.05f, 0.9f));
        GUI.color = old;
        var rules = Session.Current?.Rules;
        string label = why ?? $"Retreat ({Gui.Num(DarkestDungeon3.Dd2.Dd2Combat.RetreatChance(rules) * 100f, "0")}%)";
        if (hover || why == null) Gui.Text(new Rect(r.xMax + 10, r.y + 14, 380, 36), label, 22, why == null ? Gui.Dd1Name : Gui.Dim, TextAnchor.MiddleLeft, heading: true);
        bool retreatKey = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q
            || UnityEngine.InputSystem.Keyboard.current?.qKey.wasPressedThisFrame == true;
        if (why == null && retreatKey && Event.current.type == EventType.KeyDown) Event.current.Use();
        if (why == null && (Gui.Hotspot(r) || retreatKey) && !DarkestDungeon3.Dd2.Dd2Combat.TryRetreat(rules)) Gui.Announce("The retreat fails!");
    }

    private readonly ResultsUi _results = new();

    private void DrawHomecoming(Driver d)
    {
        bool backInMenu = GameModeMgr.CurrentMode == GameModeType.MAIN_MENU && !DarkestDungeon3.Dd2.Dd2Api.Modes.IsChangingState();
        _results.Draw(d, backInMenu);
    }
}
