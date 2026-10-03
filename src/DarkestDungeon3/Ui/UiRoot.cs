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
                    Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
                    Gui.Text(new Rect(460, 480, 1000, 70), "The party sets out...", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
                    _embarking = false;
                    break;
                case Phase.Crawling:
                    _crawl.Draw();
                    break;
                case Phase.Fighting:
                    DrawRetreatButton();
                    Gui.DrawAnnouncement();
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
        Drag.Overlay();
    }

    // Estate summaries for the picker, read once each time it opens.
    private readonly Core.Campaign.SaveFile[] _slots = new Core.Campaign.SaveFile[4];

    private void DrawMainMenuEntry(Driver d)
    {
        if (!_slotPicker)
        {
            string label = Session.LoadError != null ? "DD1 Hamlet unavailable"
                : Session.Current == null ? "Loading Darkest Dungeon 1..."
                : "The Hamlet";
            if (Gui.DdButton(new Rect(40, 860, 380, 90), label, Session.Current != null, 36))
            {
                _slotPicker = true;
                for (int slot = 1; slot <= 3; slot++)
                {
                    try { _slots[slot] = Core.Campaign.SaveFile.Load(Session.SlotPath(slot)); }
                    catch (System.Exception e) { _slots[slot] = null; Plugin.Log.LogWarning($"[session] slot {slot}: {e.Message}"); }
                }
            }
            if (Session.Current != null) Gui.Text(new Rect(40, 950, 380, 30), "Darkest Dungeon 1 campaign", 18, Gui.Dd1Class, TextAnchor.MiddleCenter);
            if (Session.LoadError != null) Gui.Text(new Rect(40, 986, 900, 60), Session.LoadError, 18, Gui.Blood);
            return;
        }

        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0, 0, 0, 0.85f));
        Gui.Text(new Rect(460, 150, 1000, 70), "Choose an estate", 52, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var plate = Art.Dd1("campaign", "town", "estate_title", "estate_nameplate.png");
        for (int slot = 1; slot <= 3; slot++)
        {
            var save = _slots[slot];
            var r = new Rect(960 - 446, 230 + (slot - 1) * 230, 893, 220);
            bool hover = r.Contains(Event.current.mousePosition);
            var old = GUI.color;
            if (!hover) GUI.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            if (plate != null) GUI.DrawTexture(new Rect(r.x, r.y - 30, 893, 281), plate);
            else Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.95f));
            GUI.color = old;
            float tx = r.x + 286;
            if (save?.Estate != null)
            {
                var e = save.Estate;
                Gui.Text(new Rect(tx, r.y + 30, 560, 52), e.Name, 40, hover ? Color.white : Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
                Gui.Text(new Rect(tx, r.y + 84, 560, 32), $"Week {e.Week + 1}  ·  {e.Roster.Count} heroes  ·  {Gui.Num(e.Get(Core.Campaign.Currency.Gold), "#,0")} gold  ·  {e.QuestsCompleted - 1} quests", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);
                if (save.Expedition != null) Gui.Text(new Rect(tx, r.y + 118, 560, 28), "A party is still out there.", 19, Gui.Blood, TextAnchor.MiddleLeft);
            }
            else
            {
                Gui.Text(new Rect(tx, r.y + 30, 560, 52), "A new estate", 40, hover ? Color.white : Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
                Gui.Text(new Rect(tx, r.y + 84, 560, 32), "Return to the Hamlet and begin the campaign.", 22, Gui.Dd1Class, TextAnchor.MiddleLeft);
            }
            if (Gui.Hotspot(r))
            {
                _slotPicker = false;
                d.EnterHamlet(slot);
            }
        }
        if (Gui.DdButton(new Rect(860, 940, 200, 56), "Cancel", true, 24)
            || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)) _slotPicker = false;
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
        if (why == null && Gui.Hotspot(r) && !DarkestDungeon3.Dd2.Dd2Combat.TryRetreat(rules)) Gui.Announce("The retreat fails!");
    }

    private readonly ResultsUi _results = new();

    private void DrawHomecoming(Driver d)
    {
        bool backInMenu = GameModeMgr.CurrentMode == GameModeType.MAIN_MENU && !DarkestDungeon3.Dd2.Dd2Api.Modes.IsChangingState();
        _results.Draw(d, backInMenu);
    }
}
