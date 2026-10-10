using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Combat;
using Assets.Code.Combat.BattleConfiguration;
using Assets.Code.Game;
using Assets.Code.Library;
using Assets.Code.UI.Screens;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Expedition;
using HarmonyLib;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// Starts DD2 fights for the crawl and tells it when they're over. Uses the same entry point as DD2's own road
/// fights (see TriggerCombatBhv): a CombatScenarioData, a switch to COMBAT, the scenario installed once the
/// previous mode has exited.
/// </summary>
internal static class Dd2Combat
{
    /// <summary>Raised when one of our fights is over and DD2 has left combat. Arg: was the party wiped.</summary>
    public static event Action<bool> Finished;

    public static bool InFight { get; private set; }

    // ---- DD1 retreat: a roll each round (70%, +5% per failed try, shared/rules.json), then DD2's own retreat ----

    /// <summary>The fight ended because the party fled.</summary>
    public static bool Retreated { get; private set; }
    private static int _retreatTries, _round, _triedRound = -1;
    private static readonly System.Random RetreatRng = new();

    public static float RetreatChance(Core.Expedition.CrawlRules rules) =>
        UnityEngine.Mathf.Clamp01((rules?.RetreatChance ?? 0.7f) + (rules?.RetreatBonusPerAttempt ?? 0.05f) * _retreatTries);

    /// <summary>Why the party can't try to flee right now, or null.</summary>
    public static string WhyNoRetreat()
    {
        if (!InFight || Retreated) return "Not now";
        if (GameModeMgr.CurrentMode != GameModeType.COMBAT) return "Not now";
        var combat = SingletonMonoBehaviour<CombatBhv>.Instance;
        if (combat == null || combat.IsRetreatInvalid) return "No escape from this fight";
        if (_triedRound == _round) return "Already tried this round";
        return null;
    }

    /// <summary>Roll DD1's retreat. On success DD2 ends the battle as a retreat (its own penalties: 2 stress each).</summary>
    public static bool TryRetreat(Core.Expedition.CrawlRules rules)
    {
        if (WhyNoRetreat() != null) return false;
        _triedRound = _round;
        if (RetreatRng.NextDouble() >= RetreatChance(rules))
        {
            _retreatTries++;
            Assets.Code.Combat.Events.EventBattleRetreatFailed.Trigger();
            Plugin.Log.LogInfo("[combat] retreat failed");
            return false;
        }
        Retreated = true;
        var scenario = Singleton<GameTypeMgr>.Instance.CombatScenarioData;
        Assets.Code.Combat.Events.EventBattleRetreat.Trigger(scenario != null && scenario.BattleConfigurations.Count > 1);
        Plugin.Log.LogInfo("[combat] the party retreats");
        return true;
    }

    private static void OnRound(Assets.Code.Combat.Events.EventBattleStartRound e) => _round = e.m_Round;
    public static string LastBattleId { get; private set; }

    private static HashSet<string> _knownScenes;

    /// <summary>Is there an addressable scene with this name? Unknown arenas would fail to load mid-transition.</summary>
    public static bool ArenaExists(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        if (_knownScenes == null)
        {
            _knownScenes = new HashSet<string>();
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                    foreach (var key in locator.Keys)
                        if (key is string s && s.StartsWith("combat_arena_", StringComparison.Ordinal)) _knownScenes.Add(s);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not list addressable arenas: " + e.Message); }
            Plugin.Log.LogInfo($"[combat] {_knownScenes.Count} combat arenas available");
        }
        return _knownScenes.Count == 0 || _knownScenes.Contains(scene);
    }

    private const string LineUpBattleId = "dd3_dd1_encounter";

    /// <summary>How many ranks a DD2 enemy takes (1 if DD2 doesn't know it).</summary>
    public static int EnemySize(string actorClass)
    {
        var lib = SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance;
        return lib != null && lib.GetHasLibraryKey(actorClass) ? lib.GetLibraryElement(actorClass).m_Size : 1;
    }

    /// <summary>A DD2 battle with exactly these enemies (front rank first), registered in DD2's battle library under
    /// one id that each translated DD1 encounter replaces. Null if DD2 doesn't know one of them.</summary>
    private static string RegisterLineUp(List<string> enemies)
    {
        var actors = SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance;
        var unknown = enemies.Where(e => actors == null || !actors.GetHasLibraryKey(e)).ToList();
        if (unknown.Count > 0) { Plugin.Log.LogWarning($"[combat] DD2 has no enemy {string.Join(", ", unknown)}; using the zone table"); return null; }
        try
        {
            var def = new BattleConfigurationDefinition(LineUpBattleId, "m_Chance,1,\nm_EnemyActors," + string.Join(",", enemies) + ",\n");
            def.Init();
            def.PostInit();
            SingletonMonoBehaviour<Library<string, BattleConfigurationDefinition>>.Instance.AddLibraryElement(def, overrideCSV: true);
            return LineUpBattleId;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[combat] could not build the DD1 line-up battle: " + e);
            return null;
        }
    }

    private static float _revealedAt = -1f, _revealStart = -1f;
    private static float _requestedAt;
    private static bool _nativePresentation;

    /// <summary>The current fight started DD1's way, where the party stands (Look.FightStartsInPlace).</summary>
    public static bool InPlace { get; private set; }

    private static float _returnStart = -1f;
    private static bool _returnPending;
    private static float Now => UnityEngine.Time.unscaledTime;

    // The transition's own clock: real time, except that one frame never moves it on by more than 1/30 s. While DD2
    // loads, the game can stall for a fifth of a second; the heroes' turn and glide then pause instead of jumping.
    private static float _clock, _readyClock = -1f, _returnClock = -1f;
    private static int _clockFrame = -1;

    private static void AdvanceClock()
    {
        if (_clockFrame == UnityEngine.Time.frameCount) return;
        _clockFrame = UnityEngine.Time.frameCount;
        _clock += UnityEngine.Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 1f / 30f);
    }

    /// <summary>DD2's side of the fight is set up (combat entered, backdrop in place or given up; in place, the battle
    /// itself running too). The first time it is, the moment is kept: the reveal is timed from it.</summary>
    private static bool Ready()
    {
        if (_revealedAt < 0 || Dd1Backdrop.Pending) return false;
        if (_revealStart < 0)
        {
            if (InPlace && SingletonMonoBehaviour<CombatBhv>.Instance is not { IsBattleRunning: true }) return false;
            _revealStart = Now;
            _readyClock = _clock;
            string presentation = Dd1Backdrop.Ready ? _nativePresentation ? "in place" : "DD1" : _nativePresentation ? "native" : "fallback";
            Plugin.Log.LogInfo($"[combat timing] reveal ready after {(_revealStart - _requestedAt) * 1000f:0} ms, presentation {presentation}");
        }
        return true;
    }

    /// <summary>How opaque the party's DD1 scene is over the fight: whole while DD2 sets the fight up, then the fight
    /// shows through (see FightTransition); and back over it when a fight that started in place ends.</summary>
    public static float CoverAlpha
    {
        get
        {
            if (!InFight) return 0f;
            if (_returnClock >= 0) return FightTransition.CoverAtReturn(_clock - _returnClock);
            if (!Ready()) return 1f;
            return InPlace ? FightTransition.CoverAtStart(_clock - _readyClock)
                : 1f - UnityEngine.Mathf.Clamp01((_clock - _readyClock) / 0.5f);
        }
    }

    /// <summary>In place: how far heroes and scene have moved from the corridor layout to the fight's (0..1).</summary>
    public static float LayoutBlend =>
        !InFight || !InPlace ? 0f
        : _returnClock >= 0 ? FightTransition.MoveAtReturn(_clock - _returnClock)
        : Ready() ? FightTransition.MoveAtStart(_clock - _readyClock) : 0f;

    /// <summary>In place: how far the corridor heroes have turned from the camera to the enemy (0..1).</summary>
    public static float TurnBlend =>
        !InFight || !InPlace ? 0f
        : _returnClock >= 0 ? FightTransition.MoveAtReturn(_clock - _returnClock)
        : FightTransition.TurnAtStart(_clock);

    /// <summary>A fight that started in place has ended and is handing the party's scene back.</summary>
    public static bool Returning => InFight && _returnClock >= 0;

    /// <summary>The fight is over: cover it with the party's own scene, glide back to the corridor layout, then go
    /// back to the road (see Tick).</summary>
    internal static void ReturnInPlace()
    {
        Dd1Backdrop.RefreshHeroTargets();
        _returnStart = Now;
        _returnClock = _clock;
        _returnPending = true;
    }

    /// <summary>Every frame of a fight: the transition clock moves on, and once the party's scene and layout are back,
    /// DD2 returns to the road under them, without its fade.</summary>
    public static void Tick()
    {
        AdvanceClock();
        Ready();
        if (!_returnPending || !FightTransition.ReturnDone(_clock - _returnClock)) return;
        _returnPending = false;
        Dd2Api.Modes.SetMode(GameModeType.DRIVING, isLoad: false, Assets.Code.UI.Transitions.SceneTransition.SKIP, showTransitionThrobberOverride: false);
    }

    public static string RollBattle(FightPlan plan)
    {
        if (plan.Enemies is { Count: > 0 } && RegisterLineUp(plan.Enemies) is { } lineUp) return lineUp;
        if (!plan.IsTable) return plan.BattleId;
        var result = new List<string>();
        if (LibraryBattleConfigurationTables.RollBattleConfiguration(plan.BattleId, RandomIdentifier.COMBAT, result) && result.Count > 0)
            return result[0];
        Plugin.Log.LogError($"[combat] table {plan.BattleId} rolled nothing");
        return null;
    }

    private static List<uint> _buffed = new();

    public static bool Start(FightPlan plan, IReadOnlyList<uint> party, float torch, bool heroesSurprised,
                             IReadOnlyList<(uint Guid, Core.Dd1.Dd1Buff Buff)> buffs = null, bool monstersSurprised = false,
                             Func<string, string, bool> onPrepared = null)
    {
        var modes = Dd2Api.Modes;
        if (modes == null || modes.IsChangingState()) { Plugin.Log.LogWarning("[combat] mode change in progress"); return false; }
        if (party == null || party.Count == 0) { Plugin.Log.LogError("[combat] no party"); return false; }
        _requestedAt = UnityEngine.Time.unscaledTime;
        var setupWatch = System.Diagnostics.Stopwatch.StartNew();

        string battle = RollBattle(plan);
        if (battle == null || !SingletonMonoBehaviour<Library<string, BattleConfigurationDefinition>>.Instance.GetHasLibraryKey(battle))
        {
            Plugin.Log.LogError($"[combat] unknown battle '{battle}' from {plan}");
            return false;
        }
        string arena = plan.Arenas.FirstOrDefault(ArenaExists);

        // Not CAMP_AMBUSH: after its results DD2 goes to the Inn or the Embark screen instead of back to the road.
        var source = plan.Kind == FightKind.CampAmbush || heroesSurprised ? CombatSource.AMBUSH : CombatSource.DUNGEON;
        var scenario = new CombatScenarioData(battle, arena, source, party);
        long scenarioMs = setupWatch.ElapsedMilliseconds;
        // Save the actual rolled configuration before native combat can change either team.
        if (onPrepared != null && !onPrepared(battle, scenario.BackgroundSceneName)) return false;

        Dd2Api.Torch = torch;
        LastBattleId = battle;
        InFight = true;
        Retreated = false;
        _retreatTries = 0;
        _round = 0;
        _triedRound = -1;
        Assets.Code.Events.EventManager.RemoveListener<Assets.Code.Combat.Events.EventBattleStartRound>(OnRound);
        Assets.Code.Events.EventManager.AddListener<Assets.Code.Combat.Events.EventBattleStartRound>(OnRound);
        Plugin.Log.LogInfo($"[combat] {plan.Kind} fight {battle} in {scenario.BackgroundSceneName ?? "(default arena)"}, source {source.GetName()}, torch {torch}");

        SingletonMonoBehaviour<ScreenStackBhv>.Instance.Clear();
        modes.OnNextGameModeExitComplete(_ =>
        {
            Plugin.Log.LogInfo($"[combat timing] road exited after {(UnityEngine.Time.unscaledTime - _requestedAt) * 1000f:0} ms");
            Singleton<GameTypeMgr>.Instance.SetCombatScenario(scenario, isLoad: true);
        });
        // DD1 starts a fight where the party stands: no DD2 stagecoach loading screen. The DD1 scene covers DD2's
        // switch until the fight is ready (see CoverAlpha). In place, DD2 neither fades nor plays its battle intro,
        // every fight happens in front of the corridor or room the crawl was showing, and the heroes turn to the
        // enemy and step to their battle places before the fight shows (FightTransition).
        InPlace = Plugin.FightInPlace.Value;
        _returnStart = -1f;
        _returnPending = false;
        _clock = 0f;
        _readyClock = -1f;
        _returnClock = -1f;
        _clockFrame = UnityEngine.Time.frameCount;
        _revealedAt = -1f;
        _revealStart = -1f;
        _nativePresentation = plan.NativePresentation;
        Dd1Backdrop.Reset(plan.NativePresentation, InPlace);
        modes.OnNextGameModeEnterComplete(_ =>
        {
            _revealedAt = UnityEngine.Time.unscaledTime;
            Plugin.Log.LogInfo($"[combat timing] combat entered after {(_revealedAt - _requestedAt) * 1000f:0} ms, arena {scenario.BackgroundSceneName}");
        });
        var transition = InPlace ? Assets.Code.UI.Transitions.SceneTransition.SKIP : Assets.Code.UI.Transitions.SceneTransition.FADE_IN_AND_OUT;
        modes.SetMode(GameModeType.COMBAT, isLoad: false, transition, showTransitionThrobberOverride: false);
        Plugin.Log.LogInfo($"[combat timing] synchronous setup {setupWatch.ElapsedMilliseconds} ms, roll/scenario {scenarioMs} ms, native arena {plan.NativePresentation}, in place {InPlace}");
        // The party's DD1 buffs go on once DD2 has entered the fight, and come off when it ends.
        _buffed = party.ToList();
        if (buffs != null && buffs.Count > 0)
            modes.OnNextGameModeEnterComplete(_ => FightBuffs.Apply(buffs));
        // DD1 surprise: whoever got the drop acts twice in the first round (DD2's own one-round initiative buff).
        if (heroesSurprised || monstersSurprised)
            modes.OnNextGameModeEnterComplete(_ => FightBuffs.Surprise(party, heroesActFirst: monstersSurprised));
        return true;
    }

    /// <summary>Called when DD2 is back out of combat (results done) or the party was wiped.</summary>
    internal static void OnFightOver(bool partyWiped)
    {
        if (!InFight) return;
        InFight = false;
        Assets.Code.Events.EventManager.RemoveListener<Assets.Code.Combat.Events.EventBattleStartRound>(OnRound);
        FightBuffs.Remove(_buffed);
        Dd1MonsterView.Clear();
        Dd1Backdrop.End();
        if (_returnStart >= 0) Plugin.Log.LogInfo($"[combat timing] back in the dungeon {(UnityEngine.Time.unscaledTime - _returnStart) * 1000f:0} ms after the fight ended");
        Plugin.Log.LogInfo($"[combat] fight over, party wiped: {partyWiped}");
        Finished?.Invoke(partyWiped);
    }
}

/// <summary>
/// When a results screen closes, DD2's CombatResultsPresentationBhv force-unloads "its own scene". In our host
/// run a second instance lives in the road scene (MainScene), so closing the results unloaded the road, and
/// RunBhv then waited forever for it ("Waiting on RunBhv. Blocking Itr ..."). Never let it unload the road.
/// </summary>
[HarmonyPatch(typeof(Assets.Code.Combat.Presentation.CombatResultsPresentationBhv), "UnloadScene")]
internal static class KeepRoadSceneLoaded
{
    private static bool Prefix(Assets.Code.Combat.Presentation.CombatResultsPresentationBhv __instance)
    {
        string scene = __instance.gameObject.scene.name;
        if (scene != GameModeType.DRIVING.m_sceneName) return true;
        Plugin.Log.LogWarning($"[combat] results screen object lives in {scene}; not unloading the road scene");
        return false;
    }
}

/// <summary>
/// The road scene's copy of the results presentation has no battle result in our host run, and its
/// OnGameModeEnterComplete threw a NullReferenceException on every fight (which DD2 also reports as a crash).
/// Only the combat scene's copy presents the results: the road's copy sits this one out.
/// </summary>
[HarmonyPatch(typeof(Assets.Code.Combat.Presentation.CombatResultsPresentationBhv), "OnGameModeEnterComplete")]
internal static class RoadResultsCopyStaysQuiet
{
    private static bool Prefix(Assets.Code.Combat.Presentation.CombatResultsPresentationBhv __instance, GameModeType enter)
    {
        if (enter != GameModeType.RESULTS || !Dd2Combat.InFight) return true;
        return __instance.gameObject.scene.name != GameModeType.DRIVING.m_sceneName;
    }
}

/// <summary>
/// DD1's battle starts without DD2's intro (its camera sweep and battle-start stamp): a fight that starts in place
/// ends the intro timeline at once, exactly as DD2's own battle_skip_intro preference does, and keeps the battle
/// modifier icon the intro would have set.
/// </summary>
[HarmonyPatch(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "RunIntroTimeline")]
internal static class NoDd2BattleIntroInPlace
{
    private static readonly System.Reflection.FieldInfo Director = AccessTools.Field(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "m_PlayableDirector");

    private static bool Prefix(Assets.Code.Combat.Presentation.CombatPresentationBhv __instance, ref System.Collections.IEnumerator __result)
    {
        if (!Dd2Combat.InFight || !Dd2Combat.InPlace || Director == null) return true;
        __result = SkipIntro(__instance);
        return false;
    }

    private static System.Collections.IEnumerator SkipIntro(Assets.Code.Combat.Presentation.CombatPresentationBhv presentation)
    {
        try { SingletonMonoBehaviour<Assets.Code.UI.Managers.CombatUiBhv>.Instance?.SetBattleModifierIcon(); }
        catch (Exception e) { Plugin.Log.LogWarning("[combat] battle modifier icon: " + e.Message); }
        // PlayableDirector lives in UnityEngine.DirectorModule, which the build doesn't reference: by reflection.
        var director = Director.GetValue(presentation);
        var type = director?.GetType();
        var asset = type?.GetProperty("playableAsset");
        var time = type?.GetProperty("time");
        try
        {
            if (asset?.GetValue(director) != null && time != null)
            {
                time.SetValue(director, type.GetProperty("duration").GetValue(director));
                type.GetMethod("Evaluate", Type.EmptyTypes).Invoke(director, null);
                asset.SetValue(director, null);
                time.SetValue(director, 0.0);
            }
            Plugin.Log.LogInfo("[combat] DD2 battle intro skipped (the fight starts in place)");
        }
        catch (Exception e) { Plugin.Log.LogWarning("[combat] battle intro skip: " + e.Message); }
        yield break;
    }
}

/// <summary>
/// DD2 runs Resources.UnloadUnusedAssets and a full GC.Collect on every mode change (GameModeMgr.MemoryCleanup), a
/// visible part of a fight's start. A fight that starts in place skips it on the way in; the way back to the road,
/// hidden under the party's scene, still runs it, so nothing piles up between fights.
/// </summary>
[HarmonyPatch(typeof(GameModeMgr), nameof(GameModeMgr.MemoryCleanup))]
internal static class NoMemoryCleanupIntoAFightInPlace
{
    private static bool Prefix(ref System.Collections.IEnumerator __result)
    {
        if (!Dd2Combat.InFight || !Dd2Combat.InPlace || GameModeMgr.CurrentMode != GameModeType.COMBAT) return true;
        __result = Nothing();
        Plugin.Log.LogInfo("[combat timing] memory cleanup left to the return");
        return false;
    }

    private static System.Collections.IEnumerator Nothing() { yield break; }
}

/// <summary>
/// DD2's Victory screen hands out DD2 items, hero points and torch, which mean nothing in the DD1 campaign: during
/// our fights, skip the loot window (DD1 loot is rolled by the crawl instead) and let the results timeline go on.
/// </summary>
[HarmonyPatch(typeof(Assets.Code.Loot.LootManager), nameof(Assets.Code.Loot.LootManager.ShowLoot))]
internal static class NoDd2LootInOurFights
{
    private static bool Prefix(Assets.Code.Loot.LootManager __instance, UnityEngine.Events.UnityAction onFinished)
    {
        if (!Dd2Combat.InFight) return true;
        __instance.ClearShowWindowVariables();
        __instance.ClearShowToastVariables();
        Plugin.Log.LogInfo("[combat] DD2 loot window skipped (DD1 loot instead)");
        onFinished?.Invoke();
        return false;
    }
}

/// <summary>
/// DD1 goes straight back to the corridor after a fight; DD2 shows its results view (the party by the stagecoach)
/// first. During our fights, when no further wave follows, skip RESULTS: free the results scene DD2 loaded for it
/// and head back to the road (the crawl). DD2 clears the combat scenario on leaving COMBAT whatever comes next.
/// </summary>
[HarmonyPatch(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "SetNextGameMode")]
internal static class StraightBackToTheDungeon
{
    private static readonly System.Reflection.FieldInfo NextConfigs = AccessTools.Field(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "m_NextBattleConfigurations");
    private static readonly System.Reflection.FieldInfo NextIndex = AccessTools.Field(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "m_NextBattleConfigurationIndex");
    private static readonly System.Reflection.FieldInfo ResultsScene = AccessTools.Field(typeof(Assets.Code.Combat.Presentation.CombatPresentationBhv), "m_CurrentCombatResultsScene");

    private static bool Prefix(Assets.Code.Combat.Presentation.CombatPresentationBhv __instance)
    {
        if (!Dd2Combat.InFight || !Plugin.SkipDd2Results.Value) return true;
        if (NextConfigs?.GetValue(__instance) is System.Collections.ICollection next && NextIndex?.GetValue(__instance) is int i && i >= 0 && i < next.Count)
            return true;                                   // another wave of this battle follows
        if (Singleton<GameTypeMgr>.Instance.CombatScenarioData == null) return true;
        if (ResultsScene?.GetValue(__instance) is string scene)
        {
            Assets.Code.Loading.RedHookSceneManagerBhv.UnloadAdditiveSceneByForce(scene);
            ResultsScene.SetValue(__instance, null);
        }
        Plugin.Log.LogInfo("[combat] straight back to the dungeon (DD2 results view skipped)" + (Dd2Combat.InPlace ? ", in place" : ""));
        if (Dd2Combat.InPlace) Dd2Combat.ReturnInPlace();
        else Dd2Api.Modes.SetMode(GameModeType.DRIVING, isLoad: false, Assets.Code.UI.Transitions.SceneTransition.FADE_IN_AND_OUT, showTransitionThrobberOverride: false);
        return false;
    }
}

/// <summary>Our fights end when DD2 heads back to the road (DRIVING) after the results screen.</summary>
[HarmonyPatch(typeof(GameModeMgr), nameof(GameModeMgr.SetMode))]
internal static class FightOverOnLeavingCombat
{
    private static void Postfix(GameModeType mode)
    {
        if (!Dd2Combat.InFight) return;
        var from = GameModeMgr.CurrentMode;
        if (mode == GameModeType.DRIVING && (from == GameModeType.RESULTS || from == GameModeType.COMBAT))
            Dd2Api.Modes.OnNextGameModeEnterComplete(_ => Dd2Combat.OnFightOver(partyWiped: Dd2Api.Party.Count == 0));
    }
}
