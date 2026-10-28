using System;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in native travel/battle acceptance using a transient campaign and the ordinary Driver APIs.</summary>
[HarmonyPatch(typeof(Driver), "Update")]
internal static class CrawlSceneryCheck
{
    private static readonly bool MotionChecking = Environment.GetCommandLineArgs().Contains("-dd3-crawl-motion-check");
    private static readonly bool VilhelmChecking = Environment.GetCommandLineArgs().Contains("-dd3-vilhelm-check");
    private static readonly bool CurioChecking = Environment.GetCommandLineArgs().Contains("-dd3-curio-actors-check");
    private static readonly bool QuestCurioChecking = Environment.GetCommandLineArgs().Contains("-dd3-curio-quest-check");
    private static readonly bool QuestActivationChecking = Environment.GetCommandLineArgs().Contains("-dd3-curio-quest-activate-check");
    private static readonly string[][] CurioCases = Environment.GetCommandLineArgs()
        .Where(a=>a.StartsWith("-dd3-curio-actor-case=",StringComparison.Ordinal))
        .Select(a=>a.Substring("-dd3-curio-actor-case=".Length).Split('@')).ToArray();
    private static readonly string CurioShots = Environment.GetCommandLineArgs()
        .FirstOrDefault(a=>a.StartsWith("-dd3-curio-shots=",StringComparison.Ordinal))?.Substring("-dd3-curio-shots=".Length);
    private static readonly bool Requested = CurioChecking || MotionChecking || VilhelmChecking || Environment.GetCommandLineArgs().Contains("-dd3-crawl-scenery-check");
    private static readonly string[] Regions = CurioChecking ? CurioCases.Select(c=>c.Length>1?c[1]:"").ToArray()
        : new[]{ "dd2_city", "dd2_farm", "dd2_forest", "dd2_coast" };
    private static SaveFile _save;
    private static int _step, _region;
    private static float _next, _wait;
    private static bool _done;
    private static Texture _beforeFight;
    private static float _motionCamera, _motionTime, _motionMaximum;
    private static int _motionFrames;
    private static bool _motionSampled;
    private static int _curioSupplyBefore, _curioCounter;
    private static int _curioQuestBefore;
    private static string[] _curioResolveBefore;
    private static Texture _curioUsed;
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save,_save) && Session.Current.SavePath == null;

    private static void Postfix()
    {
        if (!Requested || !Plugin.DebugKeysEnabled.Value || _done) return;
        var d = Driver.Instance;
        var s = Session.Current;
        if (s == null || d == null) return;
        try
        {
            if (MotionChecking && _step >= 99 && _step <= 102 && OwnsSession
                && d.Phase == Phase.Crawling && !d.Expedition.InRoom)
                ObserveMotion(d);
            if (Time.unscaledTime < _next) return;
            if (_save == null)
            {
                if (d.Phase != Phase.Off || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Dd2Api.Modes.IsChangingState()) return;
                if (s.Save != null || s.SavePath != null) { _done = true; return; }
                if(CurioChecking)Require(CurioCases.Length>0 && CurioCases.Length<=32 && CurioCases.All(c=>c.Length==3 &&
                    new[]{"dd2_city","dd2_forest","dd2_farm","dd2_coast"}.Contains(c[1]) &&
                    c[0].All(ch=>char.IsLower(ch)||char.IsDigit(ch)||ch=='_') &&
                    (new[]{"none",Supply.HolyWater,Supply.Bandage,Supply.Antivenom,Supply.Shovel,Supply.Key,Supply.Herbs,Supply.Torch}.Contains(c[2])
                        || QuestActivationChecking && c[2]=="quest")),
                    "Invalid native curio QA cases");
                if(QuestActivationChecking)Require(QuestCurioChecking && CurioChecking,"Quest activation QA requires both quest and actor flags");
                if(QuestCurioChecking)Require(CurioChecking && CurioCases.All(c=>c[2]==(QuestActivationChecking?"quest":"none")),
                    "Quest QA requires the matching gather/activation case item");
                var estate = new Estate { Name = "Unsaved crawl scenery check", Seed = 73232, Week = 12,
                    OpeningRaidPending = false, RegionLayoutVersion = CampaignRegions.LayoutVersion };
                estate.Add(Currency.Gold,50000);
                string[] classes = { "highwayman", "grave_robber", "plague_doctor", "man_at_arms" };
                if (VilhelmChecking) classes[2] = Core.Campaign.Town.ExperimentalHeroes.Vilhelm;
                for (int i = 0; i < classes.Length; i++) estate.Roster.Add(new HeroRecord
                    { Id = "scenery-check-"+i, Name = classes[i], ClassId = classes[i], ResolveLevel = 1 });
                if (VilhelmChecking)
                {
                    var visitor = estate.Roster[2]; visitor.Name = Core.Campaign.Town.ExperimentalHeroes.VilhelmName;
                    visitor.EquippedSkills.AddRange(Core.Campaign.Town.VilhelmKit.Loadout);
                }
                _save = new SaveFile { Estate = estate };
                s.Save = _save;
                if (VilhelmChecking)
                {
                    estate.Roster.RemoveAt(2);
                    var visitor=Core.Campaign.Town.ExperimentalHeroes.InviteVilhelm(s.Hamlet,new Core.Rng(73232));
                    Require(visitor!=null && Core.Campaign.Town.ExperimentalHeroes.InviteVilhelm(s.Hamlet,new Core.Rng(73232))==null,"Visitor invitation was missing or duplicated");
                    Require(s.Hamlet.Recruit(visitor.Id),"Ordinary recruitment rejected the visitor");
                    estate.Roster.Remove(visitor); estate.Roster.Insert(2,visitor);
                    Plugin.Log.LogInfo("[vilhelm check] coach invitation and ordinary recruitment PASS; transient campaign only");
                }
                Application.runInBackground = true;
                var quest = new QuestOffer { Id = "scenery-check", Dungeon = Regions[0], Type = "cleanse", Length = 1,
                    Difficulty = 1, MapSeed = 73232, SurpriseEnabled = false, ScoutingEnabled = false };
                var pack = new Inventory();
                pack.Add(Supply.Food,12); pack.Add(Supply.Torch,8); pack.Add(Supply.Shovel,4);
                string why = d.Embark(quest,estate.Roster,pack);
                Require(why == null,why ?? "embark rejected");
                SetMap(d,s);
                Advance("host-start",d,1);
                _wait = Time.unscaledTime;
                return;
            }
            Require(OwnsSession,"Transient session ownership lost");
            if (d.Phase == Phase.Recovery) throw new InvalidOperationException(d.RecoveryMessage);
            if (Time.unscaledTime-_wait > (_step == 1 ? 200 : VilhelmChecking && _step==17 ? 300 : 90)) throw new TimeoutException("step "+_step+" phase "+d.Phase);
            var exp = d.Expedition;
            switch (_step)
            {
                case 1:
                    if (d.Phase != Phase.Crawling || !HeroStage.Lit || RegionSceneryArt.RoomSceneFiles(Regions[_region]).Count < 8) return;
                    if (VilhelmChecking && !UnityEngine.Object.FindObjectsOfType<ExportedHeroModel>().Any(m => m.Ready && HeroStage.Owns(m.Actor))) return;
                    if (VilhelmChecking) VilhelmCombatCheck.CheckRegistration();
                    if (VilhelmChecking && MotionChecking) Require(UnityEngine.Object.FindObjectsOfType<ExportedHeroModel>().Any(m => m.Ready && HeroStage.Owns(m.Actor) && m.NativeWalkReady),"Original Vilhelm walk calibration missing");
                    if(CurioChecking)
                    {
                        if(!NativeCurioArt.Ready)return;
                        Require(NativeCurioArt.TryGet(CurioCases[_region][0],false,out _,out _),"Regional candidate art missing");
                        d.Travel(1);Advance("curio-corridor-entry",d,210);break;
                    }
                    if (MotionChecking)
                    {
                        foreach (var tile in exp.Map.Corridors[0].Tiles) { tile.Content = HallContent.Empty; tile.Resolved = true; }
                        d.Travel(1);
                        // Hall entry uses its room fade; the next case exercises
                        // an actual button step before the ordinary held route.
                        _step = 98; _next = Time.unscaledTime+CorridorSceneMotion.StepSeconds+.1f; _wait = Time.unscaledTime; break;
                    }
                    Advance("entrance-native-actors",d,2,VilhelmChecking ? 15 : 1.5f); break;
                case 210:
                    d.Step(true);Require(d.Crawl.CurioHere==CurioCases[_region][0],"Native QA curio tile missing");
                    Advance("curio-actors-closed",d,211,Mathf.Max(3,d.StepAnimationSeconds+.3f));break;
                case 211:
                    Require(HeroStage.Lit && HeroStage.Instance?.Texture!=null,"Native party missing beside curio");
                    CurioCapture("closed");
                    _curioCounter=exp.RandomCounter;
                    _curioResolveBefore=exp.Party.Select(id=>Dd1Stress.Trait(Dd2Api.Actor(d.Party.Guid(id)))).ToArray();
                    _curioSupplyBefore=exp.Pack.Count(QuestActivationChecking?QuestInventoryKey(exp):CurioCases[_region][2]);
                    if(QuestCurioChecking)_curioQuestBefore=exp.Pack.Count(QuestInventoryKey(exp));
                    if(QuestActivationChecking)Require(_curioQuestBefore==3 && exp.GoalProgress==0 && !exp.QuestComplete,
                        "Activation fixture did not begin with the three canonical quest items");
                    Advance("curio-closed-captured",d,212,1);break;
                case 212:
                    if(QuestActivationChecking)
                    {
                        d.Investigate(exp.Party[0],null);CheckQuestRejection(d);
                        CurioCapture("needs-item");Advance("quest-curio-needs-item",d,225,2);break;
                    }
                    string supply=CurioCases[_region][2];
                    d.Investigate(exp.Party[0],supply=="none"?null:supply);
                    Require(d.Crawl.LastCurio!=null && d.Crawl.CurrentTile.Resolved,"Ordinary native-party curio did not resolve");
                    if(QuestCurioChecking)
                    {
                        CheckQuestResult(d,1);
                        Advance("quest-curio-result",d,213,2);break;
                    }
                    if(supply!="none")Require(exp.Pack.Count(supply)==_curioSupplyBefore-1,"Curio supply was not consumed exactly once");
                    // A first resolve break uses Dd1Stress.NextRng once in addition
                    // to Crawl's outcome and loot-selection streams. Keep an exact
                    // counter check, including only observed new native traits.
                    int resolveBreaks=exp.Party.Select((id,i)=>_curioResolveBefore[i]==null
                        && Dd1Stress.Trait(Dd2Api.Actor(d.Party.Guid(id)))!=null ? 1 : 0).Sum();
                    Require(exp.RandomCounter==_curioCounter+2+resolveBreaks,"Curio resolution did not preserve the ordinary outcome, loot-selection and observed resolve-break RNG steps");
                    Plugin.Log.LogInfo($"[regional curio actors] RNG {CurioCases[_region][0]}/{Regions[_region]}; before={_curioCounter}; after={exp.RandomCounter}; resolveBreaks={resolveBreaks}");
                    Plugin.Log.LogInfo("[regional curio actors] RESULT "+CurioCases[_region][0]+"/"+Regions[_region]+"/"+supply+"; "+d.Crawl.LastCurio.Text);
                    Advance("curio-result",d,213,2);break;
                case 225:
                    d.DismissCurio(d.Crawl.LastCurio);
                    int wrongItemBefore=exp.Pack.Count(Supply.HolyWater);
                    Require(wrongItemBefore==1,"Wrong-item rejection fixture supply missing");
                    d.Investigate(exp.Party[0],Supply.HolyWater);CheckQuestRejection(d);
                    Require(exp.Pack.Count(Supply.HolyWater)==wrongItemBefore,"Rejected ordinary supply was consumed");
                    CurioCapture("wrong-item");Advance("quest-curio-wrong-item",d,226,2);break;
                case 226:
                    d.DismissCurio(d.Crawl.LastCurio);
                    d.Investigate(exp.Party[0],QuestInventoryKey(exp));CheckQuestResult(d,1);
                    Advance("quest-curio-result",d,213,2);break;
                case 213:
                    CurioCapture("result");
                    // Unity captures at end of frame. Keep the report and map
                    // unchanged until that frame has actually been presented.
                    Advance("curio-result-captured",d,218,1);break;
                case 218:
                    var report=d.Crawl.LastCurio;
                    // Loot overflow is discarded only inside this unsaved fixture.
                    report.LeftBehind.Clear();d.DismissCurio(report);
                    Require(NativeCurioArt.TryGet(CurioCases[_region][0],true,out var searched,out _),"Searched native painting missing");
                    _curioUsed=searched;Advance("curio-actors-used",d,214,3);break;
                case 214:
                    CurioCapture("used");Advance("curio-used-captured",d,219,1);break;
                case 219:
                    d.Step(false);
                    Require(exp.TileIndex==0,"Curio return step was rejected");
                    Advance("curio-revisit-back",d,215,Mathf.Max(1.5f,d.StepAnimationSeconds+.3f));break;
                case 215:
                    d.Step(true);Require(exp.TileIndex==1,"Curio revisit step was rejected");
                    Advance("curio-revisit-forward",d,216,Mathf.Max(2,d.StepAnimationSeconds+.3f));break;
                case 216:
                    Require(d.Crawl.CurrentTile.Resolved && d.Crawl.LastCurio==null,"Curio revisit resolved or reopened the result");
                    Require(NativeCurioArt.TryGet(CurioCases[_region][0],true,out var revisited,out _) && ReferenceEquals(revisited,_curioUsed),"Revisit replaced the used painting");
                    CurioCapture("return");
                    if(QuestCurioChecking)Require(exp.Pack.Count(QuestInventoryKey(exp))==_curioQuestBefore+(QuestActivationChecking?-1:1)
                        && exp.GoalProgress==1 && !exp.QuestComplete,"Quest revisit changed inventory or progress");
                    Plugin.Log.LogInfo("[regional curio actors] PASS "+CurioCases[_region][0]+"/"+Regions[_region]+"; native party, "+
                        (QuestCurioChecking?(QuestActivationChecking?"canonical quest activation":"canonical quest collection"):"ordinary supply/outcome")+", used state and revisit; unsaved fixture");
                    Advance("curio-return-captured",d,QuestCurioChecking?220:217,1);break;
                case 220:
                    d.Step(true);Require(exp.TileIndex==2,"Second quest objective step rejected");
                    Advance("quest-curio-second",d,221,Mathf.Max(2,d.StepAnimationSeconds+.3f));break;
                case 221:
                    _curioCounter=exp.RandomCounter;d.Investigate(exp.Party[0],QuestActivationChecking?QuestInventoryKey(exp):null);CheckQuestResult(d,2);
                    CurioCapture("second-result");Advance("quest-curio-second-result",d,222,2);break;
                case 222:
                    d.DismissCurio(d.Crawl.LastCurio);d.Step(true);Require(exp.TileIndex==3,"Final quest objective step rejected");
                    Advance("quest-curio-third",d,223,Mathf.Max(2,d.StepAnimationSeconds+.3f));break;
                case 223:
                    _curioCounter=exp.RandomCounter;d.Investigate(exp.Party[0],QuestActivationChecking?QuestInventoryKey(exp):null);CheckQuestResult(d,3);
                    CurioCapture("complete-result");Advance("quest-curio-completed",d,224,2);break;
                case 224:
                    d.DismissCurio(d.Crawl.LastCurio);Require(d.Crawl.LastCurio==null,"Completed quest result did not dismiss");
                    CurioCapture("complete-used");
                    Plugin.Log.LogInfo("[regional quest curio actors] PASS "+CurioCases[_region][0]+"/"+Regions[_region]+
                        "; canonical goal "+exp.Goal.Id+"; three quest items "+(QuestActivationChecking?"consumed":"collected")+
                        ", progress3/3, completion, no duplicate revisit; unsaved fixture");
                    Advance("quest-curio-final-capture",d,217,1);break;
                case 217:
                    if(++_region<Regions.Length){SetMap(d,s);Advance("regional-curio-reload",d,1,2);}
                    else{_done=true;Plugin.Log.LogInfo("[regional curio actors] PASS ALL "+CurioCases.Length+" cases; no estate selected or persisted");}
                    break;
                case 98:
                    d.Step(true);
                    Require(exp.TileIndex == 1,"First ordinary button step was rejected");
                    d.Step(true);
                    Require(exp.TileIndex == 1,"A repeated button step skipped ahead while the ground was moving");
                    _motionSampled=false;_motionFrames=0;_motionMaximum=0;
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} button; repeated step rejected during the walking-duration pan");
                    _step = 99; _next = Time.unscaledTime+d.StepAnimationSeconds+.1f; break;
                case 99:
                    Require(_motionFrames>10,"Button pan was not observed");
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} button-finished; maximumPanSpeed={_motionMaximum:0.0}");
                    d.WalkToTile(0,5);
                    _motionSampled = false; _motionFrames = 0; _motionMaximum = 0;
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} forward; continuous ordinary map route; nativeActors={HeroStage.Lit}");
                    _step = 100; _next = Time.unscaledTime; break;
                case 100:
                    if (d.IsWalking) return;
                    // Ordinary hunger checks stop a route even when the party eats automatically.
                    // Resume through the public API; do not suppress the gameplay check.
                    if (exp.TileIndex != 5 && d.Crawl.CanNavigate && !d.Crawl.IsBlocked) { d.WalkToTile(0,5); return; }
                    Require(exp.TileIndex == 5 && _motionFrames > 40,"Forward continuous route did not finish");
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} forward-finished; frames={_motionFrames}; maximumPanSpeed={_motionMaximum:0.0}");
                    _step = 101; _next = Time.unscaledTime+2; break;
                case 101:
                    d.WalkToTile(0,0);
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} reverse; continuous ordinary map route; nativeActors={HeroStage.Lit}");
                    _step = 102; _next = Time.unscaledTime; break;
                case 102:
                    if (d.IsWalking) return;
                    if (exp.TileIndex != 0 && d.Crawl.CanNavigate && !d.Crawl.IsBlocked) { d.WalkToTile(0,0); return; }
                    Require(exp.TileIndex == 0,"Reverse continuous route did not finish");
                    Plugin.Log.LogInfo($"[crawl motion] {Regions[_region]} PASS: forward/reverse/stop; frames={_motionFrames}; maximumPanSpeed={_motionMaximum:0.0}; no camera jumps");
                    if (++_region < Regions.Length)
                    { SetMap(d,s); _motionSampled = false; _step = 1; _next = Time.unscaledTime+2; _wait = Time.unscaledTime; }
                    else { _done = true; Plugin.Log.LogInfo("[crawl motion] PASS: all four regions; no estate selected or persisted"); }
                    break;
                case 2:
                    exp.Light = 20; Advance("low-torch",d,3); break;
                case 3:
                    int torches = exp.Pack.Count(Supply.Torch);
                    d.UseTorch(); Require(exp.Light > 20 && exp.Pack.Count(Supply.Torch) == torches-1,"Torch did not consume/restore light");
                    Advance("torch-restored",d,4); break;
                case 4:
                    d.Travel(1); Require(!exp.InRoom && exp.TileIndex == 0,"Entrance did not enter corridor");
                    Advance("corridor-entry",d,5); break;
                case 5:
                    d.Step(true); Require(d.Crawl.CurioHere != null,"Curio tile missing");
                    Advance("curio-prompt",d,6); break;
                case 6:
                    d.Investigate(exp.Party[0],null); Require(d.Crawl.LastCurio != null,"Curio did not resolve");
                    Advance("curio-result",d,7); break;
                case 7:
                    Require(d.Crawl.LastCurio.LeftBehind.Count == 0,"Curio loot overflow needs manual review");
                    d.DismissCurio(d.Crawl.LastCurio); d.Step(true);
                    Require(exp.TileIndex == 2 && d.Crawl.IsBlocked,"Scouted trap did not stop travel");
                    Advance("trap-prompt",d,8); break;
                case 8:
                    d.DisarmTrap(); Require(d.Crawl.CurrentTile.Resolved,"Trap did not resolve");
                    Advance("trap-resolved",d,9); break;
                case 9:
                    d.Step(true); Require(exp.TileIndex == 3 && d.Crawl.IsBlocked,"Obstacle did not stop travel");
                    Advance("obstacle-prompt",d,10); break;
                case 10:
                    int shovels = exp.Pack.Count(Supply.Shovel);
                    d.ClearObstacle(); Require(d.Crawl.CurrentTile.Resolved && exp.Pack.Count(Supply.Shovel) == shovels-1,"Obstacle shovel use failed");
                    Advance("obstacle-resolved",d,11); break;
                case 11:
                    d.Step(false); Require(exp.TileIndex == 2,"Reverse step did not return to trap tile");
                    Advance("corridor-reverse",d,12); break;
                case 12:
                    d.Step(true); Require(exp.TileIndex == 3 && !d.Crawl.IsBlocked,"Cleared obstacle blocked revisit");
                    Advance("corridor-forward",d,13); break;
                case 13: d.Step(true); Advance("corridor-penultimate",d,14); break;
                case 14: d.Step(true); Advance("corridor-last",d,15); break;
                case 15:
                    d.Step(true); Require(!exp.InRoom,"Walking bypassed the room door");
                    Require(d.EnterDoor(1),"Room door refused its explicit interaction");
                    Advance("door-clicked",d,150,.45f);break;
                case 150:
                    Require(exp.InRoom && exp.RoomId == 1 && d.Phase == Phase.Fighting,"Room entry did not start native battle");
                    Advance("room-battle-start",d,16); break;
                case 16:
                    if (!Dd1Backdrop.Ready || Dd2Combat.CoverAlpha > .001f) return;
                    Require(Dd2Combat.InPlace && Dd1Backdrop.Snapshot != null,"Battle did not retain the crawl scene");
                    var plan = RegionalScenery.For(Regions[_region]);
                    var choice = RegionSceneryArt.RoomChoice(plan,exp.Seed,d.Crawl.CurrentRoom);
                    _beforeFight = RegionSceneryArt.RoomTextureFor(choice.FileName);
                    Require(_beforeFight != null && DungeonAtmosphereUi.LastDrawCount > 0,"Battle environment/effects missing");
                    Advance("native-battle-atmosphere",d,17,8); break;
                case 17:
                    if (VilhelmChecking && _region==0 && !VilhelmCombatCheck.Tick()) return;
                    // This fixture validates rendering/lifecycle, not combat balance or skill kills.
                    AccessTools.Method(typeof(DebugKeys),"WinFight").Invoke(null,new object[] { false });
                    Advance("debug-victory-requested",d,18); break;
                case 18:
                    if (d.Phase != Phase.Crawling || Dd2Combat.InFight)
                    {
                        // DEBUG damage is resolved when native combat advances. If it is waiting
                        // for hero input, pass that turn through the ordinary native event.
                        var combat = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Combat.CombatBhv>.Instance;
                        // Death's Door enemies (notably the Pit Fighter) can survive both DEBUG
                        // hits at zero HP. Finish them with the existing native debug-kill helper.
                        var stage = UnityEngine.Object.FindObjectOfType<Assets.Code.Combat.Presentation.CombatPresentationBhv>();
                        if (stage != null && stage.AllActors.Select(a => a?.ActorInstance).Any(a => a != null
                            && !d.Party.Guids.Contains(a.ActorGuid) && !(a.ActorDataId ?? "").EndsWith("_corpse")))
                            AccessTools.Method(typeof(DebugKeys),"KillFront").Invoke(null,null);
                        _next = Time.unscaledTime+1;
                        if (combat != null && combat.CurrentBattleState == Assets.Code.Combat.BattleState.IN_TURN_SELECT
                            && d.Party.Guids.Contains(combat.CurrentActorGuid))
                        {
                            Assets.Code.Combat.Events.EventBattlePass.Trigger(combat.CurrentActorGuid);
                            _next = Time.unscaledTime+1;
                        }
                        return;
                    }
                    Require(exp.InRoom && exp.RoomId == 1 && d.Crawl.CurrentRoom.Cleared,"Battle did not return to cleared room");
                    var after = RegionSceneryArt.RoomChoice(RegionalScenery.For(Regions[_region]),exp.Seed,d.Crawl.CurrentRoom);
                    Require(ReferenceEquals(_beforeFight,RegionSceneryArt.RoomTextureFor(after.FileName)),"Room backdrop swapped after battle");
                    Advance("battle-return-same-scene",d,19); break;
                case 19:
                    if (d.Crawl.LastSpoils is { } spoils)
                    { Require(spoils.LeftBehind.Count == 0,"Battle loot overflow needs manual review"); d.DismissSpoils(spoils); }
                    if (++_region < Regions.Length)
                    { SetMap(d,s); Advance("region-reload",d,1); }
                    else
                    {
                        _done = true;
                        Plugin.Log.LogInfo("[crawl scenery check] PASS: four native regions, real actor travel/torch/curio/trap/obstacle/reversal/room fight and return; debug victories; no estate selected or persisted");
                    }
                    break;
            }
        }
        catch (Exception e) { _done = true; Plugin.Log.LogError("[crawl scenery check] FAIL: "+e); }
    }

    private static void ObserveMotion(Driver d)
    {
        var exp = d.Expedition;
        var corridor = d.Crawl.CurrentCorridor;
        var choice = RegionSceneryArt.CorridorChoice(RegionalScenery.For(exp.Quest.Dungeon),exp.Seed,corridor.Id);
        var texture = RegionSceneryArt.RoomTextureFor(choice?.FileName);
        if (texture == null || !HeroStage.Lit) return;
        Require(HeroStage.WalkContactErrorPixels <= 6,"A planted native foot missed the moving ground target");
        if (VilhelmChecking)
            foreach (var model in UnityEngine.Object.FindObjectsOfType<ExportedHeroModel>().Where(m => m.Ready && HeroStage.Owns(m.Actor)))
                Require(model.NativeWalkReady && Time.frameCount-model.SampledFrame<=1,"Imported Vilhelm pose stopped updating");
        float progress = CorridorPanoramaLayout.ViewProgress(exp.TileIndex,corridor.Tiles.Count,
            exp.HeadingRoomId != corridor.RoomB,d.SceneSlide);
        var crop = CorridorPanoramaLayout.Crop(texture.width,texture.height,progress,corridor.Tiles.Count);
        float camera = 1920*crop.X/crop.Width, now = Time.unscaledTime;
        if (_motionSampled && now > _motionTime)
        {
            float dt = now-_motionTime, distance = Mathf.Abs(camera-_motionCamera);
            // Button steps and held routes share one physical walking limit,
            // across hall lengths and crop zoom.
            float maximum = HeroStage.Instance?.WalkingSpeedLimit ?? CorridorSceneMotion.WalkingPixelsPerSecond;
            Require(distance <= maximum*dt+3,"Panorama jumped at a tile boundary or reversal");
            _motionMaximum = Mathf.Max(_motionMaximum,distance/dt); _motionFrames++;
        }
        _motionCamera = camera; _motionTime = now; _motionSampled = true;
    }

    private static void SetMap(Driver d, Session s)
    {
        var exp = d.Expedition;
        exp.Quest.Dungeon = Regions[_region]; exp.Seed = 73232+_region;
        // Each curio case is an independent deterministic synthetic map. This
        // seed keeps ordinary return-content rolls clear; no rules/probabilities
        // or persisted expedition state are changed for the fixture.
        if(CurioChecking){exp.Seed=73281;exp.RandomCounter=0;}
        var props = s.Campaign.Props(exp.Quest.Dungeon);
        var rng = new Rng(73232);
        var map = new DungeonMap { Dungeon = exp.Quest.Dungeon, EntranceRoomId = 0, Seed = exp.Seed };
        map.Rooms.Add(new Room { Id = 0, Content = RoomContent.Entrance, X = 0, Y = 0, Scouted = true, CorridorIds = { 0 } });
        map.Rooms.Add(new Room { Id = 1, Content = RoomContent.Battle, X = 7, Y = 0, Scouted = true, CorridorIds = { 0 } });
        var corridor = new Corridor { Id = 0, RoomA = 0, RoomB = 1 };
        for (int i = 0; i < 6; i++) corridor.Tiles.Add(new HallTile { Index = i, X = i+1, Y = 0, Scouted = true });
        corridor.Tiles[1].Content = HallContent.Curio;
        corridor.Tiles[1].ContentId = props.Pick(ZoneProps.HallCurios,rng);
        corridor.Tiles[2].Content = HallContent.Trap;
        corridor.Tiles[2].ContentId = props.Pick(ZoneProps.Traps,rng);
        corridor.Tiles[3].Content = HallContent.Obstacle;
        corridor.Tiles[3].ContentId = props.Pick(ZoneProps.Obstacles,rng);
        if(CurioChecking)
        {
            foreach(var tile in corridor.Tiles){tile.Content=HallContent.Empty;tile.ContentId=null;}
            corridor.Tiles[1].Content=HallContent.Curio;corridor.Tiles[1].ContentId=CurioCases[_region][0];
            map.Rooms[1].Content=RoomContent.Empty;
            if(QuestCurioChecking)
            {
                string questType=QuestActivationChecking?"inventory_activate":"gather";
                var goal=s.Campaign.Goals.For(questType,exp.Quest.Dungeon);
                Require(goal!=null && goal.Type==(QuestActivationChecking?"activate":"gather") && goal.CurioName==CurioCases[_region][0]
                    && goal.Amount==3 && goal.NeedsItem==QuestActivationChecking
                    && (QuestActivationChecking?goal.StartingItems.Count==1 && goal.StartingItems[0].Amount==3:!string.IsNullOrEmpty(goal.QuestItem)),
                    "No matching canonical three-item quest goal for this curio/context");
                exp.Quest.Type=questType;exp.Quest.GoalId=goal.Id;exp.Goal=goal;exp.GoalProgress=0;
                for(int i=1;i<=goal.Amount;i++)
                {
                    corridor.Tiles[i].Content=HallContent.Curio;corridor.Tiles[i].ContentId=goal.CurioName;corridor.Tiles[i].IsQuestGoal=true;
                }
            }
        }
        map.Corridors.Add(corridor);
        exp.Map = map; exp.RoomId = 0; exp.CorridorId = -1; exp.TileIndex = -1;
        exp.HeadingRoomId = -1; exp.PendingEncounter = null; exp.FightCheckpoint = null;
        exp.PendingCurio = null; exp.PendingSpoils = null; exp.QuestComplete = false; exp.Light = 100;
        exp.Pack.Items.Clear();
        exp.Pack.Add(Supply.Food,MotionChecking ? 32 : 8); exp.Pack.Add(Supply.Torch,2); exp.Pack.Add(Supply.Shovel,1);
        if(QuestActivationChecking)
        {
            foreach(var item in exp.Goal.StartingItems)exp.Pack.Add(ItemCatalog.QuestKey(item.Id),item.Amount);
            // An ordinary supply is deliberately distinct from quest_item+holy_water.
            // This inventory is transient and tests rejection without altering the goal.
            exp.Pack.Add(Supply.HolyWater,1);
        }
        else if(CurioChecking && CurioCases[_region][2]!="none")exp.Pack.Add(CurioCases[_region][2],2);
    }

    private static string QuestInventoryKey(ExpeditionState exp) =>
        ItemCatalog.QuestKey(QuestActivationChecking?exp.Goal.StartingItems[0].Id:exp.Goal.QuestItem);

    private static void CheckQuestRejection(Driver d)
    {
        var exp=d.Expedition;var report=d.Crawl.LastCurio;
        Require(report!=null && report.OutcomeType=="NeedsItem" && report.ItemUsed==null
            && report.CurioId==exp.Goal.CurioName && report.Loot.Count==0 && report.LeftBehind.Count==0,
            "Missing/wrong quest item was not rejected by the ordinary quest route");
        Require(!d.Crawl.CurrentTile.Resolved && d.Crawl.CurioHere==exp.Goal.CurioName
            && exp.GoalProgress==0 && !exp.QuestComplete && exp.Pack.Count(QuestInventoryKey(exp))==_curioQuestBefore
            && exp.RandomCounter==_curioCounter,"Rejected quest interaction changed state/inventory/RNG");
        Plugin.Log.LogInfo("[regional quest curio actors] REJECTION "+exp.Goal.Id+"; inventory/progress/RNG unchanged; "+report.Text);
    }

    private static void CheckQuestResult(Driver d,int count)
    {
        var exp=d.Expedition;var report=d.Crawl.LastCurio;
        Require(report!=null && report.OutcomeType=="Quest" && report.CurioId==exp.Goal.CurioName && report.LeftBehind.Count==0,
            "Native quest result was not the canonical quest objective");
        if(QuestActivationChecking)Require(report.ItemUsed==QuestInventoryKey(exp) && report.Loot.Count==0,
            "Native activation did not consume the canonical quest item exactly once");
        else Require(report.ItemUsed==null && report.Loot.Count==1 && report.Loot[0].Type=="quest_item"
            && report.Loot[0].Id==exp.Goal.QuestItem && report.Loot[0].Amount==1,
            "Native gather result was not the canonical quest item");
        Require(d.Crawl.CurrentTile.Resolved && exp.GoalProgress==count
            && exp.Pack.Count(QuestInventoryKey(exp))==_curioQuestBefore+(QuestActivationChecking?-count:count)
            && exp.QuestComplete==(count==exp.Goal.Amount),"Quest item/progress/completion mismatch");
        Require(exp.RandomCounter==_curioCounter,"Quest resolution unexpectedly consumed RNG");
        Plugin.Log.LogInfo("[regional quest curio actors] RESULT "+exp.Goal.CurioName+"/"+exp.Quest.Dungeon+
            "; goal="+exp.Goal.Id+"; item="+QuestInventoryKey(exp)+"; progress="+count+"/"+exp.Goal.Amount+
            "; RNG="+_curioCounter+"->"+exp.RandomCounter+"; "+report.Text);
    }

    private static void CurioCapture(string state)
    {
        if(string.IsNullOrWhiteSpace(CurioShots))return;
        Require(System.IO.Path.IsPathRooted(CurioShots),"Curio screenshot directory must be absolute");
        System.IO.Directory.CreateDirectory(CurioShots);
        var type=Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule",throwOnError:true);
        AccessTools.Method(type,"CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{
            System.IO.Path.Combine(CurioShots,CurioCases[_region][0]+"-"+Regions[_region]+"-actors-"+state+".png"),1});
    }

    private static void Advance(string label, Driver d, int next, float hold = 1.5f)
    {
        Plugin.Log.LogInfo($"[crawl scenery sample] {Regions[_region]} {label}; phase={d.Phase}; room={d.Expedition?.RoomId}; tile={d.Expedition?.TileIndex}; effects={DungeonAtmosphereUi.LastDrawCount}; nativeActors={HeroStage.Lit}");
        _step = next; _next = Time.unscaledTime+hold; _wait = Time.unscaledTime;
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

[HarmonyPatch(typeof(Session),nameof(Session.Persist))]
internal static class CrawlSceneryCheckNoSave
{
    private static bool Prefix(ref bool __result)
    {
        if (!CrawlSceneryCheck.OwnsSession) return true;
        __result = true; return false;
    }
}

[HarmonyPatch(typeof(Dd2Combat),nameof(Dd2Combat.RollBattle))]
internal static class CrawlSceneryCheckBattle
{
    private static void Postfix(ref string __result)
    {
        // Pin the first encounter to cover the zero-HP survivor that previously stalled QA.
        if (CrawlSceneryCheck.OwnsSession && Driver.Instance?.Expedition?.Quest.Dungeon == "dd2_city")
            __result = "fanatic_mash_211";
    }
}

// The fixture renders in a real window, but user key presses and held mouse
// buttons must not change its scripted route. Ordinary campaigns are unaffected.
[HarmonyPatch(typeof(Driver),"Walk")]
internal static class CrawlSceneryCheckKeyboard
{
    private static void Prefix(ref UnityEngine.InputSystem.Keyboard kb) { if (CrawlSceneryCheck.OwnsSession) kb=null; }
}
[HarmonyPatch(typeof(Driver),"MouseWalk")]
internal static class CrawlSceneryCheckMouse
{
    private static bool Prefix(ref int __result)
    {
        if (!CrawlSceneryCheck.OwnsSession) return true;
        __result=0; return false;
    }
}
[HarmonyPatch(typeof(DebugKeys),"Update")]
internal static class CrawlSceneryCheckKeys
{
    private static bool Prefix() => !CrawlSceneryCheck.OwnsSession;
}
