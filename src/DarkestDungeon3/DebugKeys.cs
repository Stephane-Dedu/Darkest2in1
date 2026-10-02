using System;
using System.Linq;
using Assets.Code.Combat;
using Assets.Code.Combat.BattleConfiguration;
using Assets.Code.Game;
using Assets.Code.Library;
using Assets.Code.Roster;
using Assets.Code.UI.Screens;
using Assets.Code.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkestDungeon3;

/// <summary>Developer hotkeys used to verify each slice in the running game.</summary>
public class DebugKeys : MonoBehaviour
{
    // Slice 1 test fight: a catacombs encounter in a catacombs arena, standing in for DD1's Ruins.
    private const string TestArena = "combat_arena_catacombs_cultist";

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        try
        {
            if (kb.f8Key.wasPressedThisFrame) DumpState();
            if (kb.f9Key.wasPressedThisFrame) StartTestCombat();
            if (kb.f10Key.wasPressedThisFrame) WinFight();
            if (kb.f5Key.wasPressedThisFrame)
            {
                // Make camp here: hand the party firewood and some food first (testing).
                var d = Runtime.Driver.Instance;
                if (d?.Expedition != null)
                {
                    d.Expedition.Pack.Add(Core.Expedition.Supply.Firewood, 1);
                    if (d.Expedition.Pack.Count(Core.Expedition.Supply.Food) < 8) d.Expedition.Pack.Add(Core.Expedition.Supply.Food, 8);
                    d.MakeCamp();
                    Plugin.Log.LogInfo("[F5] camp: " + (d.Expedition.Camp != null ? "made" : "refused (needs a safe room)"));
                }
            }
            if (kb.f6Key.wasPressedThisFrame)
            {
                // Cycle through DD1's curios (Shift+F6 clears the preview).
                var dir = Runtime.Session.Current?.Dd1.PathOf("props", "shared", "curios");
                var all = dir != null && System.IO.Directory.Exists(dir) ? System.IO.Directory.GetDirectories(dir).Select(System.IO.Path.GetFileName).OrderBy(n => n).ToList() : new System.Collections.Generic.List<string>();
                if (kb.shiftKey.isPressed || all.Count == 0) Ui.CrawlUi.PreviewCurio = null;
                else Ui.CrawlUi.PreviewCurio = all[(all.IndexOf(Ui.CrawlUi.PreviewCurio ?? "") + 1) % all.Count];
                Plugin.Log.LogInfo("[F6] curio preview: " + (Ui.CrawlUi.PreviewCurio ?? "off"));
            }
            if (kb.f7Key.wasPressedThisFrame)
                Plugin.Log.LogInfo("[F7] " + (DarkestDungeon3.Dd2.HeroStage.Instance?.Dump(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "DarkestDungeon3", "herostage.png")) ?? "no stage"));
            if (kb.f11Key.wasPressedThisFrame) DarkestDungeon3.Runtime.Driver.Instance?.DebugFight();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
        }
    }

    private static void DumpState()
    {
        foreach (var cls in new[] { "highwayman", "plague_doctor", "man_at_arms", "vestal" })
            foreach (DarkestDungeon3.Runtime.Art.LargeArt k in System.Enum.GetValues(typeof(DarkestDungeon3.Runtime.Art.LargeArt)))
                DarkestDungeon3.Runtime.Art.LargePortrait(cls, k);   // sizes are logged as they load
        Plugin.Log.LogInfo($"[F8] mode={GameModeMgr.CurrentMode?.GetName()} changing={Singleton<GameModeMgr>.Instance.IsChangingState()}");
        var roster = Singleton<GameTypeMgr>.Instance?.RosterManager;
        if (roster != null)
            Plugin.Log.LogInfo($"[F8] party guids: {string.Join(",", roster.GetActorGuids(RosterStatusType.PARTY))}");

        var lib = SingletonMonoBehaviour<Library<string, BattleConfigurationDefinition>>.Instance;
        if (lib == null) { Plugin.Log.LogInfo("[F8] battle configuration library not loaded"); return; }
        var ids = lib.GetLibraryElementKeys();
        Plugin.Log.LogInfo($"[F8] {ids.Count} battle configurations; catacombs ones: " +
                           string.Join(", ", ids.Where(i => i.Contains("catacomb")).Take(40)));
    }

    /// <summary>F10 (testing): every enemy in the current fight takes lethal damage, twice to finish corpses.</summary>
    private static void WinFight()
    {
        var combat = UnityEngine.Object.FindObjectOfType<Assets.Code.Combat.Presentation.CombatPresentationBhv>();
        if (combat == null) { Plugin.Log.LogWarning("[F10] not in combat"); return; }
        var party = new System.Collections.Generic.HashSet<uint>(Singleton<GameTypeMgr>.Instance.RosterManager.GetActorGuids(RosterStatusType.PARTY));
        int hit = 0;
        for (int pass = 0; pass < 2; pass++)
            foreach (var a in combat.AllActors.ToList())
            {
                var actor = a?.ActorInstance;
                if (actor == null || party.Contains(actor.ActorGuid)) continue;
                actor.ApplyHealthDamage(9999f, isCrit: false, isRiposte: false, actor, Assets.Code.Actor.DeathType.DEBUG,
                                        Assets.Code.Source.SourceType.DEBUG, "F10", hasDisplayed: false);
                hit++;
            }
        Plugin.Log.LogInfo($"[F10] struck {hit} enemy actors");
    }

    private static void StartTestCombat()
    {
        var modes = Singleton<GameModeMgr>.Instance;
        if (modes.IsChangingState()) { Plugin.Log.LogWarning("[F9] mode change in progress, ignoring"); return; }

        var party = Singleton<GameTypeMgr>.Instance?.RosterManager?.GetActorGuids(RosterStatusType.PARTY);
        if (party == null || party.Count == 0) { Plugin.Log.LogWarning("[F9] no party (start a run first)"); return; }

        var lib = SingletonMonoBehaviour<Library<string, BattleConfigurationDefinition>>.Instance;
        var configId = lib.GetLibraryElementKeys().FirstOrDefault(i => i.Contains("catacomb"))
                       ?? lib.GetLibraryElementKeys().First();

        var scenario = new CombatScenarioData(configId, TestArena, CombatSource.DUNGEON, party);
        Plugin.Log.LogInfo($"[F9] starting combat config={configId} arena={scenario.BackgroundSceneName} party={party.Count} from mode={GameModeMgr.CurrentMode.GetName()}");

        // Same order as TriggerCombatBhv: clear screens, switch mode, install the scenario once the old mode is gone.
        SingletonMonoBehaviour<ScreenStackBhv>.Instance.Clear();
        modes.OnNextGameModeExitComplete(_ => Singleton<GameTypeMgr>.Instance.SetCombatScenario(scenario, isLoad: true));
        modes.SetMode(GameModeType.COMBAT, isLoad: false);
    }
}

/// <summary>Log every game-mode switch: the cheapest way to learn DD2's flow.</summary>
[HarmonyPatch(typeof(GameModeMgr), nameof(GameModeMgr.SetMode))]
internal static class LogModeChanges
{
    private static void Prefix(GameModeType mode, bool isLoad) =>
        Plugin.Log.LogInfo($"[mode] {GameModeMgr.CurrentMode?.GetName()} -> {mode?.GetName()} (isLoad={isLoad})");
}
