using System;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Game;
using Assets.Code.Source;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal static partial class HeroPathsCheck
{
    private static int _visualStep;
    private static uint _visualActor;
    private static float _visualSince;

    private static void BeginVisual()
    {
        var hero = _save.Estate.Roster.FirstOrDefault(h => h.ClassId == "abomination");
        Require(hero != null, "Visual check requires owned Abomination");
        _visualActor = Driver.Instance.Party.Guid(hero.Id);
        Driver.Instance.SelectedHeroId = hero.Id;
        _visualSince = Time.unscaledTime;
        _visualStep = 1;
        VisualReady("human");
    }

    private static void VisualReady(string stage)
    {
        _next = Time.unscaledTime + 15;
        Plugin.Log.LogInfo("[paths visual] READY " + stage + "; holds 15 seconds for a capture");
    }

    private static void UpdateVisual()
    {
        if (_visualStep == 0 || Time.unscaledTime < _next) return;
        try
        {
            Require(Time.unscaledTime - _visualSince < 240, "Visual check timed out");
            var actor = Dd2Api.Actor(_visualActor);
            var d = Driver.Instance;
            switch (_visualStep)
            {
                case 1:
                    CheckVisualMode(actor, "human");
                    ChangeVisualMode(actor, "beast");
                    _visualStep = 2; VisualReady("beast"); break;
                case 2:
                    CheckVisualMode(actor, "beast");
                    ChangeVisualMode(actor, "human");
                    _visualStep = 3; VisualReady("human-restored"); break;
                case 3:
                    CheckVisualMode(actor, "human");
                    d.Leave();
                    Require(d.Phase == Phase.Homecoming, "Homecoming failed");
                    _visualStep = 4; _next = Time.unscaledTime + 2; break;
                case 4:
                    if (GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Dd2Api.Modes.IsChangingState()) return;
                    Require(HeroStage.Instance?.Texture == null, "Hero stage retained models after homecoming");
                    d.BackToHamlet();
                    Require(d.Phase == Phase.Hamlet && _save.Expedition == null, "Hamlet return retained expedition");
                    _visualStep = 5; VisualReady("hamlet-return"); break;
                case 5:
                    _visualStep = 0;
                    Plugin.Log.LogInfo("[paths visual] PASS human/beast/HUD portrait/native model/restore/homecoming; requesting native application quit");
                    Application.Quit(); break;
            }
        }
        catch (Exception e) { _visualStep = 0; Plugin.Log.LogError("[paths visual] FAIL " + e); }
    }

    private static void ChangeVisualMode(ActorInstance actor, string form)
    {
        var mode = actor.ActorDataClass.Modes.Single(m => m.Id.EndsWith("_" + form, StringComparison.Ordinal));
        // The native event updates actor data and ActorBhv's model together. A direct setter only changes data.
        var type = typeof(ActorInstance).Assembly.GetType("Assets.Code.Actor.Events.EventActorChangeMode", true);
        AccessTools.Method(type, "Trigger").Invoke(null, new object[]
            { actor.ActorGuid, -1, actor.ActorDataMode.Id, mode.Id, SourceType.DEBUG, "dd3_paths_visual_check" });
        Require(actor.ActorDataMode == mode, "Native mode event did not change form");
    }

    private static void CheckVisualMode(ActorInstance actor, string form)
    {
        Require(actor != null && actor.ActorDataMode.Id.EndsWith("_" + form, StringComparison.Ordinal), "Wrong actor form");
        Require(HeroSkills.EquippedForCurrentMode(actor).Count == 5, "Wrong form HUD skill count");
        var resource = ActorResources.Get(actor.ActorDataId);
        var native = resource.GetPortraitIconByType(ResourceActor.PortraitIconType.CombatBar, actor.ActorDataMode.Id);
        if (native == null || native == resource.GetPortraitIconByType(ResourceActor.PortraitIconType.CombatBar))
            native = resource.GetPortraitIconByType(ResourceActor.PortraitIconType.Color, actor.ActorDataMode.Id);
        Require(native != null && Art.HeroIcon(actor.ActorDataId, actor.ActorDataMode.Id) == native, "HUD did not use native form portrait");
        if (form == "beast") Require(native != Art.HeroIcon(actor.ActorDataId), "Beast HUD retained the human portrait");
        var model = UnityEngine.Object.FindObjectsOfType<ActorBhv>().Single(a => a.GetActorGuid() == actor.ActorGuid && a.transform.position.x > 19000);
        var bindings = model.GetComponentsInChildren<ActorModesBhv>(true);
        Require(bindings.Any(b => b.TryGetRootForMode(actor.ActorDataMode.Id, out var root) && root.activeInHierarchy), "Native form model is hidden");
        Plugin.Log.LogInfo("[paths visual] mode PASS " + form + "; " + string.Join(",", HeroSkills.EquippedForCurrentMode(actor)));
    }
}
