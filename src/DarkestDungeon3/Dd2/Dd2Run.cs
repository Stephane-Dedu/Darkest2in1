using System;
using Assets.Code.Game;
using Assets.Code.Run;
using Assets.Code.Utils;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// The DD2 run that hosts a DD1 expedition. DD2's combat, torch, stress and items only exist inside a run, so an
/// expedition starts one (skipping the valley, prologue and road fights) and ends it on the way home.
/// </summary>
internal static class Dd2Run
{
    public static bool Hosting { get; private set; }
    private static int _generation;
    private static float? _waitingSince;
    private static Action _waitFailed;
    internal const float RoadWaitSeconds = 180f;

    /// <summary>Bound an inactive host wait without aborting DD2's own slow scene transition.</summary>
    public static void Update()
    {
        if (_waitingSince == null || UnityEngine.Time.unscaledTime - _waitingSince.Value < RoadWaitSeconds) return;
        Plugin.Log.LogError("[run] no completed scene entry for three minutes; saved expedition can retry when ready");
        FailWaiting();
    }

    private static void FailWaiting()
    {
        var failed = _waitFailed;
        _waitingSince = null; _waitFailed = null;
        _generation++; // A late entry may finish, but it must not start gameplay behind the recovery screen.
        failed?.Invoke();
    }

    /// <summary>DD2 developer settings that keep the road out of the way while we host.</summary>
    private static void Configure(bool hosting)
    {
        TextBasedEditorPrefsBaseType.MAP_GENERATION_SKIP_VALLEY.SetValue(hosting);
        TextBasedEditorPrefsBaseType.RUN_TEST_SKIP_PROLOGUE.SetValue(hosting);
        TextBasedEditorPrefsBaseType.DRIVING_DISABLE_COMBATS.SetValue(hosting);
        TextBasedEditorPrefsBaseType.DISABLE_INTRO_CINEMATIC.SetValue(hosting);
        TextBasedEditorPrefsBaseType.DISABLE_TUTORIALS.SetValue(hosting);
    }

    /// <summary>Start a DD2 expedition run; <paramref name="onReady"/> fires once DD2 is on the road with a party.</summary>
    public static bool Start(Action onReady, Action onFailed = null)
    {
        var modes = Dd2Api.Modes;
        if (modes == null || modes.IsChangingState()) { Plugin.Log.LogWarning("[run] can't start: mode change in progress"); return false; }
        if (Hosting && GameModeMgr.CurrentMode == GameModeType.DRIVING)
        {
            try { onReady(); return true; }
            catch (Exception e) { Plugin.Log.LogError("[run] party retry failed: " + e); onFailed?.Invoke(); return false; }
        }

        Configure(true);
        Hosting = true;
        Singleton<GameTypeMgr>.Instance.SetGameType(GameType.EXPEDITION);
        if (SingletonMonoBehaviour<RunBhv>.HasInstance())
            SingletonMonoBehaviour<RunBhv>.Instance.SetNextRunStartType(RunStartType.MAIN_MENU_NEW_RUN);

        Plugin.Log.LogInfo($"[run] starting host run from {GameModeMgr.CurrentMode?.GetName()}");
        _waitingSince = UnityEngine.Time.unscaledTime;
        _waitFailed = onFailed;
        WaitForRoad(onReady, onFailed, ++_generation, attempts: 0);
        modes.SetMode(GameModeType.DRIVING, isLoad: false);
        return true;
    }

    /// <summary>DD2 may pass through other modes (hero select, cinematics) before the road; wait for DRIVING.</summary>
    private static void WaitForRoad(Action onReady, Action onFailed, int generation, int attempts)
    {
        Dd2Api.Modes.OnNextGameModeEnterComplete(mode =>
        {
            if (generation != _generation || !Hosting) return;
            Plugin.Log.LogInfo($"[run] entered {mode?.GetName()}, party {Dd2Api.Party.Count}");
            if (mode == GameModeType.DRIVING)
            {
                _waitingSince = null; _waitFailed = null;
                try { onReady(); }
                catch (Exception e) { Plugin.Log.LogError("[run] party setup failed: " + e); onFailed?.Invoke(); }
            }
            else if (attempts < 6)
            {
                _waitingSince = UnityEngine.Time.unscaledTime;
                WaitForRoad(onReady, onFailed, generation, attempts + 1);
            }
            else { Plugin.Log.LogError("[run] never reached the road"); FailWaiting(); }
        });
    }

    /// <summary>End the host run without DD2's end-of-run screens and return to the main menu.</summary>
    public static void End()
    {
        _generation++;
        _waitingSince = null; _waitFailed = null;
        Plugin.Log.LogInfo("[run] ending host run");
        if (SingletonMonoBehaviour<RunBhv>.HasInstance() && SingletonMonoBehaviour<RunBhv>.Instance.RunScoreManager is { } score
            && score.GetGameOverReason() == null)
            score.SetGameOver(GameOverReason.ABANDON);
        Configure(false);
        Hosting = false;
        Dd2Api.Modes?.SetMode(GameModeType.MAIN_MENU, isLoad: false);
    }
}
