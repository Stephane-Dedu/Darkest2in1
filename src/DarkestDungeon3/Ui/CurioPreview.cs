using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Native PNG/input QA on the real curio renderer. No host run, estate file or native actor is used.</summary>
[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class CurioPreview
{
    private static readonly bool Checking = Environment.GetCommandLineArgs().Contains("-dd3-curio-check");
    private static readonly bool Regional = Environment.GetCommandLineArgs().Contains("-dd3-curio-regions");
    private static readonly string[] Regions = { "dd2_city", "dd2_forest", "dd2_farm", "dd2_coast" };
    private static readonly bool Requested = Checking;
    private static readonly string Shots = Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-dd3-curio-shots=",StringComparison.Ordinal))?.Substring("-dd3-curio-shots=".Length);
    private static readonly CrawlUi Ui = new();
    private static readonly System.Reflection.FieldInfo Panel = AccessTools.Field(typeof(CrawlUi), "_curioPanel");
    private static SaveFile _save;
    private static Crawl _previousCrawl;
    private static string _previousHero;
    private static bool _closed, _done, _previousBackground, _usedCaptured;
    private static NativeCurioDefinition[] _definitions;
    private static int _index, _step, _counter, _holy, _inputFrame = -1, _captureFrame = -1;
    private static float _until, _started;
    private static NativeCurioPainting _painting;
    private static Texture2D _closedTexture;
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save,_save) && Session.Current.SavePath == null;
    internal static bool Active => Requested && !_closed && OwnsSession && Driver.Instance?.Phase == Phase.Off
        && GameModeMgr.CurrentMode == GameModeType.MAIN_MENU;
    internal static ExpeditionState Expedition => Active ? _save.Expedition : null;

    private sealed class PreviewParty : IParty
    {
        public IReadOnlyList<string> Alive { get; } = new[] { "curio-preview-hero" };
        public float HpFraction(string id) => 1;
        public void Damage(string id,float amount,string cause) { }
        public void Heal(string id,float amount) { }
        public void AddStress(string id,float amount,string cause) { }
        public string AddDd1Quirk(string id,string quirk) => quirk;
        public string PurgeNegative(string id) => null;
        public string CureDisease(string id) => null;
    }

    private static bool Prefix()
    {
        if (!Requested || _closed || !Plugin.DebugKeysEnabled.Value || Driver.Instance?.Phase != Phase.Off
            || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Session.Current == null) return true;
        var session=Session.Current;
        if (_save == null)
        {
            if (session.Save != null || session.SavePath != null) return true;
            var estate=new Estate{Name="Curio art preview",Seed=7342,OpeningRaidPending=false};
            estate.Roster.Add(new HeroRecord{Id="curio-preview-hero",ClassId="highwayman",Name="Curio preview",ResolveLevel=1});
            var map=new DungeonMap();
            map.Rooms.Add(new Room{Id=0,Content=RoomContent.Curio,Visited=true,Scouted=true});
            _save=new SaveFile{Estate=estate,Expedition=new ExpeditionState{Map=map,RoomId=0,Seed=7342,Started=true,
                Quest=new QuestOffer{Id="curio-preview",Dungeon="dd2_city",Type="cleanse",Difficulty=1},Party={"curio-preview-hero"}}};
            session.Save=_save;
            _previousCrawl=Driver.Instance.Crawl;_previousHero=Driver.Instance.SelectedHeroId;
            AccessTools.Property(typeof(Driver),nameof(Driver.Crawl)).SetValue(Driver.Instance,new Crawl(_save.Expedition,session.Rules,new PreviewParty(),session.Content),null);
            Driver.Instance.SelectedHeroId="curio-preview-hero";
            _previousBackground=Application.runInBackground;Application.runInBackground=true;
            _started=Time.unscaledTime;
            Plugin.Log.LogInfo("[curio check] transient main-menu renderer fixture; stand-in Core party; no native host, estate path or save");
        }
        var original=Event.current;
        try
        {
            Gui.Begin();GUI.depth=-1000;Gui.BlockInput(true);
            NativeCurioArt.Prepare(Expedition);RegionSceneryArt.Prepare(Expedition.Quest.Dungeon);
            if (original.type==EventType.Repaint) Tick();
            var plan=RegionalScenery.For(Expedition.Quest.Dungeon);
            var choice=RegionSceneryArt.CorridorChoice(plan,7342,0);
            var background=RegionSceneryArt.RoomTextureFor(choice?.FileName);
            Gui.Fill(new Rect(0,0,1920,1080),Color.black);
            if(background!=null)CorridorPanoramaUi.Draw(background,.45f,1,tileCount:6);
            Event injected=original.type==EventType.Repaint && _inputFrame!=Time.frameCount && _captureFrame!=Time.frameCount ? Input() : null;
            if(injected!=null){Event.current=injected;_inputFrame=Time.frameCount;}
            Drag.Begin();
            if(injected!=null && (_step==12 || _step==14))
                Drag.Source(new Rect(650,810,100,100),new PackStack(Expedition.Pack,0,Supply.HolyWater),_=>{});
            Ui.DrawCurioPreview(Driver.Instance.Crawl,Expedition);
            Drag.Overlay();
            if(injected!=null)VerifyInput();
            Event.current=original;
            string currentId = _save.Expedition.Map.Rooms[0].CurioId;
            ExpeditionSkin.Text(new Rect(60,40,1750,55),"Curio preview / "+(NativeCurioArt.DisplayName(currentId)??currentId??"Preparing pack")+" / "+Expedition.Quest.Dungeon,30,ExpeditionSkin.Ivory);
            ExpeditionSkin.Text(new Rect(60,780,1750,55),"Unsaved native rendering and input / Core stand-in party / no host run",23,ExpeditionSkin.Muted);
            ExpeditionSkin.Text(new Rect(60,845,1500,45),_done ? "Check finished. See log for PASS or FAIL." : "Checking exact art, alpha picking, states and supply drops",22,ExpeditionSkin.Muted);
            if(ExpeditionSkin.Button(new Rect(1530,945,310,65),"Close preview",size:25))Close();
        }
        catch(Exception error){_done=true;Plugin.Log.LogError("[curio check] FAIL: "+error);}
        finally{Event.current=original;}
        return false;
    }

    private static void Tick()
    {
        if(_done || !Checking)return;
        if(Time.unscaledTime-_started>240)throw new TimeoutException("Curio preview stage "+_step);
        if(_definitions==null)
        {
            if(!NativeCurioArt.Ready)return;
            _definitions=NativeCurioArt.Definitions.OrderBy(d=>d.Id).ToArray();
            if (Regional) _definitions = _definitions.Where(d=>d.Id=="locked_display_cabinet").SelectMany(d=>Regions.Select(_=>d)).ToArray();
            Require(_definitions.Length>0,"No exact curio paintings loaded");
        }
        var exp=Expedition;var room=exp.Map.Rooms[0];
        if(_step==0)
        {
            if(_index>=_definitions.Length){Set("decorative_urn");exp.Pack.Add(Supply.HolyWater,2);_holy=exp.Pack.Count(Supply.HolyWater);_step=11;return;}
            Set(_definitions[_index].Id);_step=1;return;
        }
        bool sceneryReady=RegionSceneryArt.RoomAlpha(RegionSceneryArt.CorridorChoice(RegionalScenery.For(exp.Quest.Dungeon),7342,0)?.FileName)>=.999f;
        if(_step==1 && sceneryReady && Time.unscaledTime>=_until && NativeCurioArt.Ready && NativeCurioArt.TryGet(room.CurioId,false,out _closedTexture,out _painting))
        {
            Plugin.Log.LogInfo("[curio check] FRAME "+room.CurioId+" closed; region="+exp.Quest.Dungeon+"; pairs="+NativeCurioArt.ResidentPairs);
            Capture(room.CurioId,"closed");
            _step=2;
        }
        if(_step==6 && !_usedCaptured){Capture(room.CurioId,"used");_usedCaptured=true;}
        if(_step==6 && Time.unscaledTime>=_until)
        {
            Require(NativeCurioArt.TryGet(room.CurioId,true,out var used,out var same),"Used painting missing");
            Require(!ReferenceEquals(used,_closedTexture) && ReferenceEquals(same,_painting),"Used state replaced the identity or body registration");
            Require(exp.RandomCounter==_counter,"Rendering or clicks consumed expedition RNG");
            Plugin.Log.LogInfo("[curio check] FRAME "+room.CurioId+" used; opaque click/padding miss PASS; foot=690");
            _index++;_step=0;
        }
        if(_step==11 && sceneryReady && Time.unscaledTime>=_until && NativeCurioArt.Ready && NativeCurioArt.TryGet(room.CurioId,false,out _closedTexture,out _painting))_step=12;
        if(_step==16 && Time.unscaledTime>=_until)
        {
            _done=true;
            Plugin.Log.LogInfo("[curio check] PASS: "+(Regional ? "4 regional cabinet cases" : _definitions.Length+" exact IDs")+" closed/used; actual DrawProp click and padding rejection; actual Drag.Source/Drop and ordinary urn supply resolution; no estate/native host/save access");
        }
    }
    private static void Set(string id)
    {
        var exp=Expedition;exp.Map.Rooms[0].CurioId=id;exp.Map.Rooms[0].CurioTaken=false;exp.PendingCurio=null;
        exp.Quest.Dungeon=id.Contains("oyster")||id.Contains("coral")||id.Contains("fish") ? "dd2_coast" :
            id.Contains("tent") ? "dd2_forest" : id.Contains("bones") ? "dd2_farm" : "dd2_city";
        if (Regional && id=="locked_display_cabinet") exp.Quest.Dungeon=Regions[_index];
        Panel.SetValue(Ui,null);_counter=exp.RandomCounter;_until=Time.unscaledTime+1.6f;_usedCaptured=false;
        NativeCurioArt.Clear();NativeCurioArt.Prepare(exp);
    }
    private static Event Input()
    {
        if(_step!=2&&_step!=3&&_step!=4&&_step!=5&&_step!=12&&_step!=13&&_step!=14&&_step!=15)return null;
        var at=_painting.At(1040,690);
        bool source=_step==12||_step==14;
        bool empty=_step==2||_step==3||_step==13;
        Vector2 point=source?new Vector2(700,860):empty?new Vector2(at.Left+1,at.Top+1):Opaque(at);
        bool down=_step==2||_step==4||source;
        return new Event{type=down?EventType.MouseDown:EventType.MouseUp,button=0,mousePosition=point};
    }
    private static Vector2 Opaque(NativeCurioPlacement at)
    {
        for(int y=_painting.MaskHeight-1;y>=0;y--)
            for(int dx=0;dx<_painting.MaskWidth;dx++)
            {
                int x=(_painting.MaskWidth/2+dx)%_painting.MaskWidth;
                float u=(x+.5f)*_painting.MaskCellSize/_painting.PixelWidth,v=(y+.5f)*_painting.MaskCellSize/_painting.PixelHeight;
                if(_painting.Hit(u,v))return new Vector2(at.Left+u*at.Width,at.Top+v*at.Height);
            }
        throw new InvalidOperationException("No painted alpha target");
    }
    private static void VerifyInput()
    {
        var exp=Expedition;
        if(_step==3)Require(Panel.GetValue(Ui)==null,"Transparent padding opened a curio");
        if(_step==5)
        {
            Require(Panel.GetValue(Ui)!=null,"Opaque paint did not open the curio");
            Panel.SetValue(Ui,null);exp.Map.Rooms[0].CurioTaken=true;_until=Time.unscaledTime+1.6f;
        }
        if(_step==13)Require(exp.Pack.Count(Supply.HolyWater)==_holy&&!exp.Map.Rooms[0].CurioTaken&&exp.RandomCounter==_counter,"A padding drop consumed a supply or resolved the curio");
        if(_step==15)
        {
            Require(exp.Pack.Count(Supply.HolyWater)==_holy-1&&exp.Map.Rooms[0].CurioTaken&&Driver.Instance.Crawl.LastCurio!=null,"Opaque supply drop did not run ordinary curio resolution exactly once");
            _until=Time.unscaledTime+1.6f;
        }
        _step++;
    }
    private static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    private static void Capture(string id,string state)
    {
        if (Regional && id=="locked_display_cabinet") id += "-"+Expedition.Quest.Dungeon;
        if(string.IsNullOrWhiteSpace(Shots))return;
        _captureFrame=Time.frameCount;
        if(!System.IO.Path.IsPathRooted(Shots))throw new InvalidOperationException("Curio screenshot directory must be absolute");
        System.IO.Directory.CreateDirectory(Shots);
        // Resolve the optional Unity capture module only for this opt-in QA fixture.
        var type=Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule",throwOnError:true);
        AccessTools.Method(type,"CaptureScreenshot",new[]{typeof(string),typeof(int)})
            .Invoke(null,new object[]{System.IO.Path.Combine(Shots,id+"-"+state+".png"),1});
    }
    private static void Close()
    {
        _closed=true;Drag.Cancel();NativeCurioArt.Clear();RegionSceneryArt.Clear();
        if(OwnsSession){Session.Current.Save=null;AccessTools.Property(typeof(Driver),nameof(Driver.Crawl)).SetValue(Driver.Instance,_previousCrawl,null);Driver.Instance.SelectedHeroId=_previousHero;}
        _save=null;Application.runInBackground=_previousBackground;Gui.BlockInput(false);
    }
}

[HarmonyPatch(typeof(Session),nameof(Session.Persist))]
internal static class CurioPreviewNoSave
{
    private static bool Prefix(ref bool __result){if(!CurioPreview.OwnsSession)return true;__result=true;return false;}
}
[HarmonyPatch(typeof(NativeCurioArt),nameof(NativeCurioArt.Prepare))]
internal static class CurioPreviewLoader
{
    private static void Prefix(ref ExpeditionState expedition){if(CurioPreview.Expedition is {} preview)expedition=preview;}
}
[HarmonyPatch(typeof(RegionSceneryArt),nameof(RegionSceneryArt.Prepare))]
internal static class CurioPreviewScenery
{
    private static void Prefix(ref string region){if(CurioPreview.Expedition is {} preview)region=preview.Quest.Dungeon;}
}
