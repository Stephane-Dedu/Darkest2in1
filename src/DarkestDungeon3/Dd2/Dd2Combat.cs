using System;
using System.Collections.Generic;
using System.Linq;
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

    public static string RollBattle(FightPlan plan)
    {
        if (!plan.IsTable) return plan.BattleId;
        var result = new List<string>();
        if (LibraryBattleConfigurationTables.RollBattleConfiguration(plan.BattleId, RandomIdentifier.COMBAT, result) && result.Count > 0)
            return result[0];
        Plugin.Log.LogError($"[combat] table {plan.BattleId} rolled nothing");
        return null;
    }

    public static bool Start(FightPlan plan, IReadOnlyList<uint> party, float torch, bool heroesSurprised)
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
        Plugin.Log.LogInfo($"[combat] {plan.Kind} fight {battle} in {scenario.BackgroundSceneName ?? "(default arena)"}, source {source.GetName()}, torch {torch}");

        SingletonMonoBehaviour<ScreenStackBhv>.Instance.Clear();
        modes.OnNextGameModeExitComplete(_ => Singleton<GameTypeMgr>.Instance.SetCombatScenario(scenario, isLoad: true));
        modes.SetMode(GameModeType.COMBAT, isLoad: false);
        return true;
    }

    /// <summary>Called when DD2 is back out of combat (results done) or the party was wiped.</summary>
    internal static void OnFightOver(bool partyWiped)
    {
        if (!InFight) return;
        InFight = false;
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
