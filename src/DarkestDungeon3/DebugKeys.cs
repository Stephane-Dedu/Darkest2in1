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
            if (kb.f10Key.wasPressedThisFrame && kb.ctrlKey.isPressed) KillFront();
            else if (kb.f10Key.wasPressedThisFrame) WinFight(onlyOne: kb.shiftKey.isPressed);
            if (kb.f3Key.wasPressedThisFrame)
            {
                // Testing: walk to the nearest room with a battle still to fight (handles traps/curios on the way: press again).
                var d = Runtime.Driver.Instance;
                var exp = d?.Expedition;
                if (exp != null)
                {
                    var here = exp.Map.Rooms.FirstOrDefault(r => r.Id == (exp.InRoom ? exp.RoomId : exp.HeadingRoomId));
                    var target = exp.Map.Rooms.Where(r => r.HasBattle && !r.Cleared && r != here)
                        .OrderBy(r => r.Content == Core.Dungeon.RoomContent.Boss ? 0 : 1)   // the boss first
                        .ThenBy(r => here == null ? 0 : System.Math.Abs(r.X - here.X) + System.Math.Abs(r.Y - here.Y)).FirstOrDefault();
                    if (target != null) d.WalkToRoom(target.Id);
                    Plugin.Log.LogInfo("[F3] walking to battle room " + (target?.Id.ToString() ?? "none"));
                }
            }
            if (kb.f4Key.wasPressedThisFrame && kb.ctrlKey.isPressed)
            {
                // Ctrl+F4, test estate only: one recruit of each class added on 2026-10-10 waits at the stagecoach.
                var session = Runtime.Session.Current;
                if (session?.SavePath != null && System.IO.Path.GetFileName(session.SavePath) == "estate_2.json")
                {
                    var e = session.Save.Estate;
                    var rng = new Core.Rng(e.Week * 7919 + e.Recruits.Count);
                    var added = session.Catalog.RecruitableClasses.Where(c => c == Core.Campaign.Town.RecruitClasses.BountyHunter || c == Core.Campaign.Town.RecruitClasses.Crusader
                            || c == Core.Campaign.Town.RecruitClasses.Shieldbreaker)
                        .Where(c => e.Recruits.All(r => r.ClassId != c)).ToList();
                    foreach (var cls in added) e.Recruits.Add(session.Hamlet.MakeHero(cls, rng, level: 0));
                    session.Persist();
                    Plugin.Log.LogInfo($"[F4] stagecoach recruits added: {(added.Count > 0 ? string.Join(", ", added) : "none")}");
                }
                else Plugin.Log.LogInfo("[F4] only works on the test estate (slot 2)");
            }
            else if (kb.f4Key.wasPressedThisFrame && kb.shiftKey.isPressed)
            {
                // Shift+F4, test estate only: open the Darkest Dungeon (a zone at level 6, the roster at resolve 5).
                var session = Runtime.Session.Current;
                if (session?.SavePath != null && System.IO.Path.GetFileName(session.SavePath) == "estate_2.json")
                {
                    var e = session.Save.Estate;
                    e.ZoneXp["crypts"] = System.Math.Max(e.ZoneXp.TryGetValue("crypts", out var zx) ? zx : 0, 100000);
                    foreach (var h in e.Roster) h.ResolveLevel = System.Math.Max(h.ResolveLevel, 5);
                    e.Quests = Core.Campaign.QuestBoard.Generate(e, session.Campaign, session.Hamlet.ToggledZones());
                    session.Persist();
                    var dd = e.Quests.FirstOrDefault(q => q.Dungeon == "darkestdungeon");
                    Plugin.Log.LogInfo($"[F4] Darkest Dungeon open: {dd?.PlotId ?? "no offer"} map {dd?.MapName ?? "-"}");
                }
                else Plugin.Log.LogInfo("[F4] only works on the test estate (slot 2)");
            }
            else if (kb.f4Key.wasPressedThisFrame)
            {
                // Test estate only (slot 2): open the service buildings, add gold and the first Blacksmith upgrades.
                var session = Runtime.Session.Current;
                if (session?.SavePath != null && System.IO.Path.GetFileName(session.SavePath) == "estate_2.json")
                {
                    var e = session.Save.Estate;
                    e.QuestsCompleted = System.Math.Max(e.QuestsCompleted, 5);
                    e.Add(Core.Campaign.Currency.Gold, 5000);
                    foreach (var u in new[] { "blacksmith.weapon:a", "blacksmith.armour:a", "guild.skill_levels:a" }) e.Upgrades.Add(u);
                    foreach (var h in e.Roster) h.ResolveLevel = System.Math.Max(h.ResolveLevel, 1);
                    e.TownEventId ??= "free_abbey";
                    // Ruins to zone level 2 so DD1's first boss quest (the Necromancer) is offered.
                    if (!e.ZoneXp.TryGetValue("crypts", out var xp) || xp < 6) e.ZoneXp["crypts"] = 6;
                    e.Quests = Core.Campaign.QuestBoard.Generate(e, session.Campaign, session.Hamlet.ToggledZones());
                    session.Persist();
                    Plugin.Log.LogInfo("[F4] test estate: buildings open, +5000 gold, first smith/guild upgrades, resolve 1, a town event");
                }
                else Plugin.Log.LogInfo("[F4] only works on the test estate (slot 2)");
            }
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
                if (kb.ctrlKey.isPressed) { Runtime.Driver.Instance?.DebugMemory(); return; }
                // Cycle through DD1's curios (Shift+F6 clears the preview).
                var dir = Runtime.Session.Current?.Dd1.PathOf("props", "shared", "curios");
                var all = dir != null && System.IO.Directory.Exists(dir) ? System.IO.Directory.GetDirectories(dir).Select(System.IO.Path.GetFileName).OrderBy(n => n).ToList() : new System.Collections.Generic.List<string>();
                if (kb.shiftKey.isPressed || all.Count == 0) Ui.CrawlUi.PreviewCurio = null;
                else Ui.CrawlUi.PreviewCurio = all[(all.IndexOf(Ui.CrawlUi.PreviewCurio ?? "") + 1) % all.Count];
                Plugin.Log.LogInfo("[F6] curio preview: " + (Ui.CrawlUi.PreviewCurio ?? "off"));
            }
            if (kb.f7Key.wasPressedThisFrame)
                Plugin.Log.LogInfo("[F7] " + (DarkestDungeon3.Dd2.HeroStage.Instance?.Dump(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "DarkestDungeon3", "herostage.png")) ?? "no stage"));
            if (kb.f11Key.wasPressedThisFrame)
            {
                // Shift+F11 (testing): fill the pack with torches first, so the spoils don't fit (DD1's full-pack loot scroll).
                var d = DarkestDungeon3.Runtime.Driver.Instance;
                var items = Runtime.Session.Current?.Content?.Items;
                if (kb.shiftKey.isPressed && d?.Expedition != null && items != null)
                {
                    while (d.Expedition.Pack.HasRoomFor(Core.Expedition.Supply.Torch, 1, items)) d.Expedition.Pack.Add(Core.Expedition.Supply.Torch, 1);
                    Plugin.Log.LogInfo($"[F11] pack filled: {d.Expedition.Pack.SlotsUsed(items)}/{Core.Expedition.Inventory.Slots} slots");
                }
                d?.DebugFight();
            }
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
    /// <summary>F10 strikes down every enemy; Shift+F10 brings the first one to 1 HP for a hero to finish (corpse checks).</summary>
    private static void WinFight(bool onlyOne = false)
    {
        var combat = UnityEngine.Object.FindObjectOfType<Assets.Code.Combat.Presentation.CombatPresentationBhv>();
        if (combat == null) { Plugin.Log.LogWarning("[F10] not in combat"); return; }
        var party = new System.Collections.Generic.HashSet<uint>(Singleton<GameTypeMgr>.Instance.RosterManager.GetActorGuids(RosterStatusType.PARTY));
        int hit = 0;
        if (onlyOne)
        {
            // The enemy in front (lowest team position): the one a hero's melee blow reaches.
            var front = combat.AllActors.Select(a => a?.ActorInstance)
                .Where(x => x != null && !party.Contains(x.ActorGuid) && !(x.ActorDataId ?? "").EndsWith("_corpse"))
                .OrderBy(x => x.TeamPosition).FirstOrDefault();
            if (front == null) { Plugin.Log.LogInfo("[F10] no living enemy"); return; }
            front.ApplyHealthDamage(System.Math.Max(0f, front.HpRaw - 1f), isCrit: false, isRiposte: false, front, Assets.Code.Actor.DeathType.DEBUG,
                                    Assets.Code.Source.SourceType.DEBUG, "F10", hasDisplayed: false);
            Plugin.Log.LogInfo($"[F10] {front.ActorDataId} at position {front.TeamPosition} left at 1 HP");
            return;
        }
        for (int pass = 0; pass < 2; pass++)
            foreach (var a in combat.AllActors.ToList())
            {
                var actor = a?.ActorInstance;
                if (actor == null || party.Contains(actor.ActorGuid) || (onlyOne && (actor.ActorDataId ?? "").EndsWith("_corpse"))) continue;
                // Shift+F10 leaves it at 1 HP: a hero's blow then kills it through DD2's own flow (a debug kill
                // outside a skill isn't resolved until combat moves on).
                actor.ApplyHealthDamage(onlyOne ? System.Math.Max(0f, actor.HpRaw - 1f) : 9999f, isCrit: false, isRiposte: false, actor, Assets.Code.Actor.DeathType.DEBUG,
                                        Assets.Code.Source.SourceType.DEBUG, "F10", hasDisplayed: false);
                hit++;
                if (onlyOne) { Plugin.Log.LogInfo($"[F10] struck {actor.ActorDataId} (death class {actor.DeathActorDataId})"); break; }
            }
        Plugin.Log.LogInfo($"[F10] struck {hit} enemy actors");
    }

    /// <summary>Ctrl+F10: the front enemy dies as from a skill (DD2's Kill with DeathType.SKILL), so its death class
    /// (corpse) is applied at once — for checking how corpses are drawn.</summary>
    private static void KillFront()
    {
        var combat = UnityEngine.Object.FindObjectOfType<Assets.Code.Combat.Presentation.CombatPresentationBhv>();
        if (combat == null) { Plugin.Log.LogWarning("[F10] not in combat"); return; }
        var party = new System.Collections.Generic.HashSet<uint>(Singleton<GameTypeMgr>.Instance.RosterManager.GetActorGuids(RosterStatusType.PARTY));
        var front = combat.AllActors.Select(a => a?.ActorInstance)
            .Where(x => x != null && !party.Contains(x.ActorGuid) && !(x.ActorDataId ?? "").EndsWith("_corpse"))
            .OrderBy(x => x.TeamPosition).FirstOrDefault();
        if (front == null) { Plugin.Log.LogInfo("[F10] no living enemy"); return; }
        string before = front.ActorDataId;
        front.Kill(Assets.Code.Actor.DeathType.SKILL, Assets.Code.Source.SourceType.SKILL, new System.Collections.Generic.List<string>(), 0f,
                   party.Take(1).ToList());
        Plugin.Log.LogInfo($"[F10] killed {before} at position {front.TeamPosition}: now {front.ActorDataId}");
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
