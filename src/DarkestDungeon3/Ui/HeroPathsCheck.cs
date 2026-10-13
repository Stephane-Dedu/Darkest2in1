using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Game;
using Assets.Code.Library;
using Assets.Code.Utils;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in Guild input + all native Path/skill acceptance. Owns a transient save, never an estate slot.</summary>
[HarmonyPatch(typeof(Driver), "Update")]
internal static partial class HeroPathsCheck
{
    private static readonly bool VisualRequested = Environment.GetCommandLineArgs().Contains("-dd3-paths-visual-check");
    private static readonly bool ActorsOnly = VisualRequested || Environment.GetCommandLineArgs().Contains("-dd3-paths-actors-check");
    internal static readonly bool Requested = ActorsOnly || Environment.GetCommandLineArgs().Contains("-dd3-paths-check");
    private static SaveFile _save;
    private static HamletUi _ui;
    private static int _step, _check;
    private static float _next, _since;
    private static bool _done, _host;
    private static List<(string Class, string Path, bool Mastered)> _cases;
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save, _save) && Session.Current.SavePath == null;
    internal static bool ShowGuild => Requested && OwnsSession && !_host;

    private static void Postfix()
    {
        if (!Requested || !Plugin.DebugKeysEnabled.Value) return;
        if (_done) { if (VisualRequested && OwnsSession) UpdateVisual(); return; }
        var s = Session.Current;
        var d = Driver.Instance;
        if (s == null || d == null) return;
        try
        {
            if (_save == null)
            {
                if (d.Phase != Phase.Off || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Dd2Api.Modes.IsChangingState()) return;
                if (s.Save != null || s.SavePath != null) { _done = true; return; }
                if (!ActorResources.LocationsReady) { ActorResources.Has("plague_doctor"); return; }
                var classes = s.Catalog.RecruitableClasses.Where(c => !RecruitClasses.IsDd1Only(c)).ToList();
                foreach (string cls in classes) if (HeroSkills.ForClass(cls) == null) return;
                var estate = new Estate { Name = "Unsaved Hero Paths check", Seed = 234234, Week = 12,
                    QuestsCompleted = 20, OpeningRaidPending = false, RegionLayoutVersion = CampaignRegions.LayoutVersion };
                estate.Add(Currency.Gold, 50000);
                foreach (string cls in classes.OrderBy(c => c == "plague_doctor" ? 0 : 1))
                {
                    var hero = new HeroRecord { Id = "path-check-" + cls, ClassId = cls, Name = HamletUi.Pretty(cls), ResolveLevel = 2 };
                    var skills = HeroSkills.ForClass(cls);
                    hero.LearnedSkills.AddRange(skills.Where(k => !k.Starting).Select(k => k.Id));
                    hero.EquippedSkills.AddRange(HeroSkills.Loadout(hero, skills.Select(k => k.Id)));
                    if (cls == "abomination") hero.PathId = "abm_unchained";
                    if (cls == "duelist") hero.PathId = "dul_instructrice";
                    if (cls == "crusader") hero.PathId = "cru_templar";
                    hero.MasteredSkills.Add(skills[0].Id);
                    estate.Roster.Add(hero);
                }
                _save = new SaveFile { Estate = estate }; s.Save = _save;
                _ui = new HamletUi(); _ui.PathCheckShow(estate.Roster[0].Id);
                Application.runInBackground = true;
                CampaignAssets.Begin(estate);
                _since = Time.unscaledTime; _next = _since + 4;
                Plugin.Log.LogInfo("[paths check] transient Guild; no estate path selected");
                return;
            }
            Require(OwnsSession, "Transient save ownership lost");
            if (!_host) return;
            if (d.Phase == Phase.Recovery) throw new InvalidOperationException(d.RecoveryMessage);
            if (Time.unscaledTime - _since > 220) throw new TimeoutException("Native host phase " + d.Phase);
            if (d.Phase != Phase.Crawling || Dd2Api.Party.Count == 0 || Time.unscaledTime < _next) return;
            if (_cases == null)
            {
                _cases = new();
                foreach (var hero in _save.Estate.Roster)
                    foreach (var path in HeroPathBridge.Catalog.ForClass(hero.ClassId))
                        foreach (bool mastered in new[] { false, true }) _cases.Add((hero.ClassId, path.Id, mastered));
            }
            if (_check < _cases.Count)
            {
                CheckActor(_cases[_check++]);
                _next = Time.unscaledTime + .05f;
                return;
            }
            HeroPathExpedition.CheckNative();
            _done = true;
            Plugin.Log.LogInfo($"[paths check] PASS {_cases.Count} actors; {_save.Estate.Roster.Count} classes; Guild preview/{(ActorsOnly ? "rule confirmation" : "UI confirmation")}; base/mastered replacements; canonical loadouts/form HUD; expedition buff restore; no estate files accessed");
            if (VisualRequested) BeginVisual();
        }
        catch (Exception e) { _done = true; Plugin.Log.LogError("[paths check] FAIL " + e); }
    }

    internal static void DrawGuild()
    {
        Gui.Begin(); Drag.Begin(); GUI.depth = -1000; Gui.BlockInput(true);
        var original = Event.current;
        Event input = null;
        try
        {
            if (!_done && _step < 4 && Event.current.type == EventType.Repaint && CampaignAssets.Status.Ready && Time.unscaledTime >= _next)
            {
                Vector2 point = _step < 2 ? new Vector2(935, 509) : _step < 4 ? new Vector2(327, 503) : new Vector2(1086, 826);
                input = new Event { type = _step % 2 == 0 ? EventType.MouseDown : EventType.MouseUp, button = 0, mousePosition = point };
                Event.current = input;
            }
            _ui.Draw();
            if (input != null)
            {
                _next = Time.unscaledTime + .2f;
                _step++;
                if (_step == 2) Require(_ui.PathCheckOpen, "Choose Path did not open");
                if (_step == 4)
                {
                    Require(_ui.PathCheckPreview == "plg_alchemist", "Alchemist preview did not select");
                    Require(_save.Estate.Roster[0].PathId == null && _save.Estate.Get(Currency.Gold) == 50000, "Preview spent gold/changed Path");
                    Plugin.Log.LogInfo(ActorsOnly ? "[paths check] PREVIEW ready; repeating actor/HUD acceptance with rule confirmation"
                        : "[paths check] PREVIEW ready: Alchemist versus mastered Wanderer; click Follow Alchemist to continue native acceptance");
                }
            }
            // Repeat actor/HUD checks without taking OS input. The full flag still requires a real button click.
            if (!_done && _step == 4 && ActorsOnly && _save.Estate.Roster[0].PathId == null)
                Require(Session.Current.Hamlet.TrainPath(_save.Estate.Roster[0].Id, HeroPathBridge.Catalog, "plg_alchemist", false), "Rule confirmation failed");
            if (!_done && _step == 4 && _save.Estate.Roster[0].PathId == "plg_alchemist")
                {
                    Require(_save.Estate.Roster[0].PathId == "plg_alchemist" && _save.Estate.Get(Currency.Gold) == 50000 - Session.Current.Hamlet.PathTrainingCost,
                        "Guild confirmation did not train/pay exactly once");
                    if (!ActorsOnly) Require(!_ui.PathCheckOpen, "Training did not return to skills");
                    Plugin.Log.LogInfo(ActorsOnly ? "[paths check] Guild rule PASS; UI confirmation acceptance is separate"
                        : "[paths check] Guild IMGUI PASS: preview is free; confirmation trains and preserves progress");
                    _host = true; _step++; _since = Time.unscaledTime;
                    var heroes = new[] { "abomination", "duelist", "crusader", "bounty_hunter" }
                        .Where(c => _save.Estate.Roster.Any(h => h.ClassId == c))
                        .Concat(_save.Estate.Roster.Select(h => h.ClassId)).Distinct().Take(4)
                        .Select(c => _save.Estate.Roster.First(h => h.ClassId == c)).ToList();
                    var quest = new QuestOffer { Id = "paths-check", Dungeon = "dd2_forest", Type = "cleanse", Length = 1,
                        Difficulty = 1, MapSeed = 234234, SurpriseEnabled = false, ScoutingEnabled = false };
                    var pack = new Inventory(); pack.Add(Supply.Food, 12); pack.Add(Supply.Torch, 8);
                    Require(Driver.Instance.Embark(quest, heroes, pack) == null, "Native host embark rejected");
            }
        }
        catch (Exception e) { _done = true; Plugin.Log.LogError("[paths check] FAIL Guild " + e); }
        finally { Event.current = original; }
        Drag.Overlay(); Gui.DrawTip();
    }

    private static void CheckActor((string Class, string Path, bool Mastered) test)
    {
        var skills = HeroSkills.ForClass(test.Class);
        var hero = new HeroRecord { Id = "native-" + _check, ClassId = test.Class, Name = "Path QA", ResolveLevel = 2, PathId = test.Path };
        hero.LearnedSkills.AddRange(skills.Select(k => k.Id));
        if (test.Mastered) hero.MasteredSkills.AddRange(skills.Select(k => k.Id));
        hero.EquippedSkills.AddRange(HeroSkills.Loadout(hero, skills.AsEnumerable().Reverse().Select(k => k.Id)));
        uint guid = LibraryActors.LibraryActorsInstance.CreateActor(test.Class);
        var actor = Dd2Api.Actor(guid);
        Require(actor != null, "Could not create " + test.Class);
        try
        {
            Dd2Heroes.Apply(hero, actor);
            Require(actor.ActorDataPath?.Id == test.Path, "Wrong native Path " + test.Class + "/" + test.Path);
            foreach (var skill in skills)
            {
                string expected = HeroPathBridge.Catalog.SkillId(test.Class, test.Path, skill.Id, test.Mastered || skill.MasteredByDefault);
                var actual = actor.GetCombatSkillInstance(expected);
                Require(actual != null && actual.GetIsUnlocked(), $"Missing/unlearned {expected} for {test.Class}/{test.Path}/{test.Mastered}");
                Require(actor.GetCombatBaseSkillId(actual) == skill.Id, "Lost base skill identity " + expected);
            }
            var equipped = actor.GetEquippedCombatSkillIds().Select(actor.GetCombatSkillInstance)
                .Where(k => !k.m_IsAlwaysEquipped).Select(actor.GetCombatBaseSkillId).ToList();
            Require(equipped.Count == hero.EquippedSkills.Count && equipped.All(hero.EquippedSkills.Contains), "Loadout changed " + test.Class + "/" + test.Path);
            foreach (var mode in actor.ActorDataClass.Modes)
            {
                actor.SetActorMode(mode);
                var shown = HeroSkills.EquippedForCurrentMode(actor);
                Require(shown.Count <= HeroSkills.EquipLimit && shown.Count > 0, "Invalid form HUD capacity " + test.Class + "/" + mode.Id);
                foreach (string id in shown)
                    Require(skills.Any(s => s.Id == actor.GetCombatBaseSkillId(actor.GetCombatSkillInstance(id))), "Unmapped form HUD icon " + id);
            }
            Plugin.Log.LogInfo($"[paths check] actor PASS class={test.Class} path={test.Path} mastered={test.Mastered} skills={skills.Count} equipped={string.Join(",", actor.GetEquippedCombatSkillIds())}");
        }
        finally { LibraryActors.LibraryActorsInstance.RemoveLibraryElement(actor); }
    }

    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class HeroPathsCheckOverlay
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        if (!HeroPathsCheck.ShowGuild) return true;
        HeroPathsCheck.DrawGuild(); return false;
    }
}

[HarmonyPatch(typeof(Session), nameof(Session.Persist))]
internal static class HeroPathsCheckNoSave
{
    private static bool Prefix(ref bool __result)
    {
        if (!HeroPathsCheck.OwnsSession) return true;
        __result = true; return false;
    }
}

internal sealed partial class HamletUi
{
    internal bool PathCheckOpen => _guildPaths;
    internal string PathCheckPreview => _previewPath;
    internal void PathCheckShow(string hero)
    {
        _building = Buildings.Guild; _panel = Panel.Guild; _heroId = hero;
    }
}
