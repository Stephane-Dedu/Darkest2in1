using Assets.Code.Game;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Draws whichever screen the current phase needs, and the way in from DD2's main menu.</summary>
internal sealed class UiRoot : MonoBehaviour
{
    private readonly HamletUi _hamlet = new();
    private readonly EmbarkUi _embark = new();
    private readonly CrawlUi _crawl = new();
    private bool _embarking, _slotPicker;

    private void OnGUI()
    {
        var d = Driver.Instance;
        if (d == null) return;
        Gui.Begin();
        GUI.depth = -1000;

        bool fullscreen = d.Phase is Phase.Hamlet or Phase.Crawling or Phase.Homecoming;
        Gui.BlockInput(fullscreen || _slotPicker);

        try
        {
            switch (d.Phase)
            {
                case Phase.Off:
                    if (GameModeMgr.CurrentMode == GameModeType.MAIN_MENU) DrawMainMenuEntry(d);
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
                case Phase.Embarking:
                    Gui.Panel(new Rect(760, 500, 400, 80));
                    Gui.Label(new Rect(780, 520, 380, 50), "The party sets out...");
                    _embarking = false;
                    break;
                case Phase.Crawling:
                    _crawl.Draw();
                    break;
                case Phase.Homecoming:
                    DrawHomecoming(d);
                    break;
            }
        }
        catch (System.Exception e)
        {
            // One bad frame must not kill the UI for good.
            Plugin.Log.LogError(e);
        }
    }

    private void DrawMainMenuEntry(Driver d)
    {
        if (!_slotPicker)
        {
            string label = Session.LoadError != null ? "DD1 Hamlet unavailable"
                : Session.Current == null ? "Loading Darkest Dungeon 1..."
                : "<b>The Hamlet</b>\nDarkest Dungeon 1 campaign";
            if (Gui.Button(new Rect(40, 860, 380, 90), label, Session.Current != null)) _slotPicker = true;
            if (Session.LoadError != null) Gui.Small(new Rect(40, 956, 700, 60), Gui.Colour(Session.LoadError, Gui.Blood));
            return;
        }

        Gui.Panel(new Rect(560, 300, 800, 480));
        Gui.Title(new Rect(600, 320, 700, 50), "Choose an estate");
        for (int slot = 1; slot <= 3; slot++)
        {
            bool exists = Session.Current.HasSave(slot);
            if (Gui.Button(new Rect(620, 320 + slot * 100, 680, 84), exists ? $"Estate {slot} — continue" : $"Estate {slot} — new campaign"))
            {
                _slotPicker = false;
                d.EnterHamlet(slot);
            }
        }
        if (Gui.Button(new Rect(620, 720, 200, 46), "Cancel")) _slotPicker = false;
    }

    private static void DrawHomecoming(Driver d)
    {
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0, 0, 0, 0.92f));
        Gui.Title(new Rect(200, 120, 1500, 60), "The party returns to the Hamlet");
        for (int i = 0; i < d.HomecomingLog.Count; i++) Gui.Label(new Rect(220, 210 + i * 36, 1500, 34), d.HomecomingLog[i]);
        bool backInMenu = GameModeMgr.CurrentMode == GameModeType.MAIN_MENU && !DarkestDungeon3.Dd2.Dd2Api.Modes.IsChangingState();
        if (Gui.Button(new Rect(800, 900, 320, 80), backInMenu ? "Continue" : "Returning...", backInMenu)) d.BackToHamlet();
    }
}
