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

    private static float _revealedAt = -1f;

    /// <summary>0 while DD2 is still setting up the fight (the DD1 scene stays on screen), then 0..1 over half a
    /// second as the fight shows through.</summary>
    public static float RevealProgress => _revealedAt < 0 ? 0f : UnityEngine.Mathf.Clamp01((UnityEngine.Time.unscaledTime - _revealedAt) / 0.5f);

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
                             IReadOnlyList<(uint Guid, Core.Dd1.Dd1Buff Buff)> buffs = null, bool monstersSurprised = false)
    {
        var modes = Dd2Api.Modes;
        if (modes == null || modes.IsChangingState()) { Plugin.Log.LogWarning("[combat] mode change in progress"); return false; }
        if (party == null || party.Count == 0) { Plugin.Log.LogError("[combat] no party"); return false; }

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
        modes.OnNextGameModeExitComplete(_ => Singleton<GameTypeMgr>.Instance.SetCombatScenario(scenario, isLoad: true));
        // DD1 starts a fight where the party stands: no DD2 stagecoach loading screen, just a fade we cover with
        // the DD1 scene until the fight is ready (see RevealProgress).
        _revealedAt = -1f;
        modes.OnNextGameModeEnterComplete(_ => _revealedAt = UnityEngine.Time.unscaledTime);
        modes.SetMode(GameModeType.COMBAT, isLoad: false, Assets.Code.UI.Transitions.SceneTransition.FADE_IN_AND_OUT, showTransitionThrobberOverride: false);
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
        Plugin.Log.LogInfo("[combat] straight back to the dungeon (DD2 results view skipped)");
        Dd2Api.Modes.SetMode(GameModeType.DRIVING, isLoad: false, Assets.Code.UI.Transitions.SceneTransition.FADE_IN_AND_OUT, showTransitionThrobberOverride: false);
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
