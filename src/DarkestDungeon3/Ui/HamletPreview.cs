using System;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Explicit unsaved native Hamlet QA. Never selects an estate or enters gameplay.</summary>
[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class HamletPreview
{
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("-dd3-hamlet-preview");
    private static HamletUi _ui;
    private static EmbarkUi _embark;
    private static SaveFile _save;
    private static bool _closed;
    private static readonly bool CheckRequested = Environment.GetCommandLineArgs().Contains("-dd3-hamlet-check");
    private static int _checkStep;
    private static float _nextCheck;
    private static bool _checked;
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save, _save);

    private static bool Prefix()
    {
        if (!Requested || !Plugin.DebugKeysEnabled.Value || _closed || Driver.Instance?.Phase != Phase.Off
            || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU) return true;
        var session = Session.Current;
        if (session == null) return true;
        if (_ui == null)
        {
            if (session.Save != null || session.SavePath != null) return true;
            var estate = new Estate { Name = "Hillside Estate", Seed = 1101, Week = 12, QuestsCompleted = 20,
                RegionLayoutVersion = CampaignRegions.LayoutVersion };
            estate.Add(Currency.Gold, 12000);
            foreach (var currency in new[] { Currency.Bust, Currency.Crest, Currency.Deed, Currency.Portrait }) estate.Add(currency, 25);
            string[] classes = { "highwayman", "leper", "jester", "vestal", "flagellant", "hellion", "plague_doctor", "man_at_arms", "occultist", "grave_robber" };
            string[] names = { "Dismas", "Baldwin", "Sarmenti", "Junia", "Damian", "Boudica", "Paracelsus", "Barristan", "Alhazred", "Audrey" };
            for (int i = 0; i < classes.Length; i++) estate.Roster.Add(new HeroRecord
            { Id = "hamlet-preview-" + i, ClassId = classes[i], Name = names[i], ResolveLevel = 1, Stress = i % 5 });
            estate.Upgrades.Add("stage_coach.rostersize:a");
            foreach (string zone in CampaignRegions.Legacy) estate.Toggles["zone." + zone] = true;
            _save = new SaveFile { Estate = estate };
            session.Save = _save;
            estate.Quests = QuestBoard.Generate(estate, session.Campaign, CampaignRegions.Open(estate, session.Campaign));
            _ui = new HamletUi();
            CampaignAssets.Begin(estate);
            Plugin.Log.LogInfo("[hamlet preview] transient campaign; no estate path selected");
        }
        Gui.Begin(); Drag.Begin(); GUI.depth = -1000; Gui.BlockInput(true);
        var originalEvent = Event.current;
        var checkInput = CheckInput();
        if (checkInput != null) Event.current = checkInput;
        try
        {
            if (_embark != null)
            {
                _embark.Draw();
                if (_embark.WantsBack) _embark = null;
            }
            else
            {
                _ui.Draw();
                if (_ui.WantsEmbark) { _ui.WantsEmbark = false; _embark = EmbarkUi.ForPreview(_save.Estate); }
            }
            // A separate close control avoids touching the ordinary campaign picker on exit.
            if (ExpeditionSkin.Button(new Rect(1230, 1018, 270, 45), "Close preview", size: 20))
            {
                session.Save = null; _save = null; _ui = null; _embark = null; _closed = true;
                Gui.BlockInput(false);
            }
            if (checkInput != null) CheckResult();
        }
        catch (Exception e) { _checked = true; Plugin.Log.LogError("[hamlet preview] " + e); }
        finally { Event.current = originalEvent; }
        Drag.Overlay(); Gui.DrawTip();
        return false;
    }

    // Native QA dispatches IMGUI events inside this unsaved overlay. It never focuses the desktop or sends OS keys.
    private static Event CheckInput()
    {
        if (!CheckRequested || _checked || Event.current.type != EventType.Repaint || !HamletArt.Ready
            || !CampaignAssets.Status.Ready || Time.unscaledTime < _nextCheck) return null;
        _nextCheck = Time.unscaledTime + 1.2f;
        var buildings = HamletArt.Pack.Buildings;
        int serviceStep = buildings.Count * 2;
        if (_checkStep < serviceStep)
        {
            if (_checkStep % 2 == 1) return new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
            var building = buildings[_checkStep / 2];
            // Find a real visible pixel near its centre, away from the quick-nav chrome.
            for (int y = building.Y + building.Height / 2; y < building.Y + building.Height; y++)
                for (int x = building.X + building.Width / 2; x < building.X + building.Width; x++)
                    if (x > 110 && x < RosterColumn.X && y < 958 && HamletArt.Pack.Hit(x, y)?.Id == building.Id)
                        return new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(x, y) };
            _checked = true;
            Plugin.Log.LogError("[hamlet check] No visible QA point for " + building.Id);
            return null;
        }
        return new Event { type = _checkStep == serviceStep ? EventType.MouseDown : EventType.MouseUp,
            button = 0, mousePosition = new Vector2(990, 1016) };
    }

    private static void CheckResult()
    {
        var buildings = HamletArt.Pack.Buildings;
        int serviceStep = buildings.Count * 2;
        if (_checkStep < serviceStep)
        {
            string expected = _checkStep % 2 == 0 ? buildings[_checkStep / 2].Id : null;
            if (_ui.PreviewBuilding != expected) throw new InvalidOperationException("Visible-mask click/close did not select " + expected);
            if (expected != null) Plugin.Log.LogInfo("[hamlet check] visible-mask click opened " + expected);
        }
        else if (_checkStep > serviceStep)
        {
            if (_embark == null) throw new InvalidOperationException("Footer Embark did not open the quest selector.");
            _checked = true;
            Plugin.Log.LogInfo("[hamlet check] all 10 service clicks/close controls and footer Embark passed; unsaved quest selector open");
        }
        _checkStep++;
    }
}

[HarmonyPatch(typeof(Session), nameof(Session.Persist))]
internal static class HamletPreviewNoSave
{
    private static bool Prefix(ref bool __result)
    {
        if (!HamletPreview.OwnsSession) return true;
        __result = true;
        return false;
    }
}
