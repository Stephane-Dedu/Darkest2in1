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
    internal static readonly bool PaintAuditRequested=Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-paint-audit");
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-preview") || Environment.GetCommandLineArgs().Contains("-dd3-hamlet-preview");
    private static HamletUi _ui;
    private static EmbarkUi _embark;
    private static SaveFile _save;
    private static bool _closed;
    private static readonly bool Check3D = Requested && (Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-check") || Environment.GetCommandLineArgs().Contains("-dd3-hamlet-check"));
    private static int _checkStep;
    private static float _nextCheck;
    private static bool _checked;
    private static Vector2Int _checkResolution;
    private static int _checkWidth;
    private static float _resizeDeadline;
    private static Vector3 _stillCamera;
    private static int _isolatedLiveInputs;
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
            if (Check3D) Dd2.HamletStage.SetMotion(true); // Fixture-only baseline; the owner's preference is retained.
            CampaignAssets.Begin(estate);
            Plugin.Log.LogInfo("[hamlet preview] transient campaign; no estate path selected");
        }
        Gui.Begin(); Drag.Begin(); GUI.depth = -1000; Gui.BlockInput(true);
        var originalEvent = Event.current;
        Event checkInput = null;
        try
        {
            checkInput = CheckInput();
            if (checkInput != null) Event.current = checkInput;
            else if (Check3D && !_checked && (originalEvent.isMouse || originalEvent.isKey || originalEvent.type==EventType.ScrollWheel))
            {
                // Desktop movement between synthetic Tab and Enter must not clear the fixture's keyboard focus.
                _isolatedLiveInputs++;
                originalEvent.Use();
            }
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
                Dd2.HamletStage.EndPreview();
                Dd2.HamletStage.Instance?.Clear();
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
    private static Event CheckInput() => Check3D ? Check3DInput() : null;

    private static void CheckResult() => Check3DResult();

    private static Event Check3DInput()
    {
        if (_checked || Event.current.type != EventType.Repaint || !CampaignAssets.Status.Ready || Time.unscaledTime < _nextCheck) return null;
        var stage = Dd2.HamletStage.Instance;
        if (_checkStep < 25 || _checkStep is 27 or 28 or 29 or 30 || _checkStep>=35)
        {
            if (stage?.Failure != null) throw new InvalidOperationException(stage.Failure);
            if (stage?.Ready != true) return null;
        }
        if (_checkStep is 36 or 37)
        {
            int width=_checkStep==36?_checkWidth:_checkResolution.x;
            if (Screen.width!=width || stage.Texture.width!=Screen.width || stage.Texture.height!=Screen.height)
            {
                if (Time.unscaledTime>_resizeDeadline) throw new InvalidOperationException("Hamlet render target did not follow the window resize.");
                return null;
            }
        }
        _nextCheck = Time.unscaledTime + 1.2f;
        if (_checkStep is >=24 and <=27) return new Event { type=EventType.Repaint };
        if (_checkStep is 38 or 43) return new Event { type=EventType.KeyDown,keyCode=KeyCode.Tab };
        if (_checkStep is 39 or 44) return new Event { type=EventType.KeyDown,keyCode=KeyCode.Return };
        if (_checkStep is 40 or 45) return new Event { type=EventType.KeyDown,keyCode=KeyCode.Escape };
        if (_checkStep is 41 or 42 or 46 or 47)
            return new Event { type=_checkStep is 41 or 46?EventType.MouseDown:EventType.MouseUp,button=0,mousePosition=HamletUi.CameraMotionButton.center };
        if (_checkStep < 20 || _checkStep is 28 or 29)
        {
            if (_checkStep % 2 == 1) return new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
            string id = Dd2.HamletStage.Services[_checkStep == 28 ? 0 : _checkStep / 2];
            var point = stage.VisiblePoint(id);
            if (!point.HasValue) throw new InvalidOperationException("No visible 3D click point for " + id);
            return new Event { type = EventType.MouseDown, button = 0, mousePosition = point.Value };
        }
        if (_checkStep == 20) return new Event { type = EventType.ScrollWheel, delta = new Vector2(0, -3), mousePosition = new Vector2(800, 750) };
        if (_checkStep == 21) return new Event { type = EventType.MouseDrag, button = 2, delta = new Vector2(30, 20), mousePosition = new Vector2(800, 750) };
        if (_checkStep is 33 or 34) return new Event { type = _checkStep == 33 ? EventType.MouseDown : EventType.MouseUp, button = 0, mousePosition = new Vector2(175, 1030) };
        if (_checkStep >= 35) return new Event { type = EventType.Repaint, mousePosition = new Vector2(150, 930) };
        Vector2 position = _checkStep < 24 ? HamletUi.ResetViewButton.center : new Vector2(990, 1016);
        return new Event { type = _checkStep % 2 == 0 ? EventType.MouseDown : EventType.MouseUp, button = 0, mousePosition = position };
    }

    private static void Check3DResult()
    {
        var stage = Dd2.HamletStage.Instance;
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException("[hamlet 3d check] " + message); }
        if (_checkStep==0) Require(stage.SurfaceShaderName=="Universal Render Pipeline/Unlit","Terrain did not bind the installed paint shader.");
        if (_checkStep==0) Require(!stage.OpaqueHiddenBackings,"Grading made native backing geometry opaque.");
        if (_checkStep < 20 || _checkStep is 28 or 29)
        {
            string expected = _checkStep % 2 == 0 ? Dd2.HamletStage.Services[_checkStep == 28 ? 0 : _checkStep / 2] : null;
            Require(_ui.PreviewBuilding == expected, "World click/Escape expected " + expected);
            if (expected != null) Plugin.Log.LogInfo("[hamlet 3d check] world click opened " + expected);
        }
        else if (_checkStep == 20) Require(stage.Zoom < 1, "Wheel zoom did not move the view.");
        else if (_checkStep == 21) Require(stage.Pan != Vector2.zero, "Middle-drag did not pan the view.");
        else if (_checkStep == 23) Require(stage.Pan == Vector2.zero && Mathf.Approximately(stage.Zoom, 1), "Reset view did not restore the overview.");
        else if (_checkStep == 24)
        {
            stage.Clear();
            Require(!stage.HasResources, "Scene release retained native resources.");
            Plugin.Log.LogInfo("[hamlet 3d check] pan, zoom, reset and full scene release passed; standard3D reload follows");
        }
        else if (_checkStep == 27) Require(stage.Ready, "Standard3D town did not reload.");
        else if (_checkStep == 31) Require(_embark != null, "Footer did not open the quest selector.");
        else if (_checkStep == 32)
        {
            Require(!stage.HasResources && _embark != null, "Leaving town retained its 3D camera/assets.");
            Plugin.Log.LogInfo("[hamlet 3d check] Embark and stage cleanup passed");
        }
        else if (_checkStep == 34) Require(_embark == null, "Back to Hamlet did not return to town.");
        else if (_checkStep == 35)
        {
            Require(stage.Ready && _embark == null, "Returned town did not load.");
            _checkResolution=new Vector2Int(Screen.width,Screen.height);
            _checkWidth=Screen.width==1440?1600:1440;
            _resizeDeadline=Time.unscaledTime+10;
            Screen.SetResolution(_checkWidth,Screen.height,false);
        }
        else if (_checkStep is 36 or 37)
        {
            Require(stage.Texture.width==Screen.width && stage.Texture.height==Screen.height,"Target does not match the window.");
            foreach (string id in Dd2.HamletStage.Services) Require(stage.VisiblePoint(id).HasValue,"No clickable point after resize: "+id);
            if (_checkStep==36)
            {
                Plugin.Log.LogInfo($"[hamlet 3d check] resized to {Screen.width}x{Screen.height}; all ten service bounds remain clickable");
                _resizeDeadline=Time.unscaledTime+10;
                Screen.SetResolution(_checkResolution.x,_checkResolution.y,false);
            }
            else
            {
                Plugin.Log.LogInfo("[hamlet 3d check] live resize/restore passed; checking keyboard and still-camera controls");
            }
        }
        else if (_checkStep is 39 or 44)
        {
            Require(_ui.PreviewBuilding!=null,"Keyboard Enter did not open the focused service.");
            if (_checkStep==44) Require((stage.CameraPosition-_stillCamera).sqrMagnitude<.0001f,"Still camera moved when a service opened.");
        }
        else if (_checkStep is 40 or 45)
        {
            Require(_ui.PreviewBuilding==null,"Keyboard service Escape did not return to town.");
            if (_checkStep==45) Require((stage.CameraPosition-_stillCamera).sqrMagnitude<.0001f,"Still camera drifted during the service view.");
        }
        else if (_checkStep==42)
        {
            Require(!Dd2.HamletStage.MotionEnabled && !stage.SmokePlaying,"Motion off did not stop camera focus and smoke.");
            _stillCamera=stage.CameraPosition;
        }
        else if (_checkStep==47)
            Require(Dd2.HamletStage.MotionEnabled && stage.SmokePlaying,"Motion on did not restore ambient smoke.");
        else if (_checkStep==48)
        {
            _checked=true;
            Plugin.Log.LogInfo($"[hamlet 3d check] isolated {_isolatedLiveInputs} live input events during the synthetic fixture");
            Plugin.Log.LogInfo("[hamlet 3d check] PASS: 10 world services, Escape, pan/zoom/reset, full resource release, reload, re-entry click, Embark, cleanup, return, live resize/restore, keyboard focus/Enter/back and still camera/smoke controls; no estate selected or saved");
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
