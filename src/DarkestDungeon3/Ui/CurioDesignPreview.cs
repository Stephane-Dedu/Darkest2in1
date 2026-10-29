using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Game;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in unsaved DLC DESIGN rendering/input QA. Never resolves a curio or claims mechanics.</summary>
[HarmonyPatch(typeof(Driver), "Update")]
internal static class CurioDesignPreview
{
    internal static readonly bool Requested = Environment.GetCommandLineArgs().Contains("-dd3-curio-design-check");
    internal static readonly string Root = Argument("-dd3-curio-design-root=");
    private static readonly string Shots = Argument("-dd3-curio-design-shots=");
    private static readonly string Id = Argument("-dd3-curio-design-id=") ?? "cc_wine_crate";
    private static bool ProvisionDesign => Id == "com_provisionbox_1" || Id == "com_provisionbox_2";
    private static bool FarmsteadDesign => Id == "com_fireplace" || Id == "com_hero_health_heal" || Id == "com_party_health_heal" || Id == "com_hero_stress_heal" || Id == "com_party_stress_heal" || ProvisionDesign;
    private static string Region => FarmsteadDesign ? "farm" : Id == "cc_food_cart" ? "dd2_farm" : "courtyard";
    private static string ContextName => FarmsteadDesign ? "Farmstead" : Id == "cc_food_cart" ? "Foetor (paired Warrens design)" : "Courtyard";
    private static bool KeyRewardChest => Id == "redkey_heirloom_chest" || Id == "bluekey_heirloom_chest" || Id == "greenkey_heirloom_chest" || Id == "yellowkey_heirloom_chest";
    private static bool NoItemDesign => KeyRewardChest || Id == "com_fireplace" || Id == "com_hero_health_heal" || Id == "com_party_health_heal" || Id == "com_hero_stress_heal" || Id == "com_party_stress_heal";
    private static string Title => Id switch
    {
        "com_party_stress_heal" => "Spade-marked glimmering mound",
        "com_hero_stress_heal" => "Glimmering stone mound",
        "com_party_health_heal" => "Harvest sign and produce tub",
        "com_hero_health_heal" => "Fresh produce tub",
        "com_provisionbox_1" => "Provision armoire",
        "com_provisionbox_2" => "Provision chest",
        "com_fireplace" => "Crystalline ruined hearth",
        "cc_food_cart" => "Tainted serving stall",
        "cc_treasure_chest" => "Treasure chest",
        "trinket_chest" => "Trinket chest",
        "redkey_heirloom_chest" => "Red key-reward chest",
        "bluekey_heirloom_chest" => "Blue key-reward chest",
        "greenkey_heirloom_chest" => "Green key-reward chest",
        "yellowkey_heirloom_chest" => "Yellow key-reward chest",
        "cc_bone_pile" => "Vampire bones",
        "cc_bloodflower" => "Bloodflowers",
        "cc_hive" => "Hive",
        _ => "Wine Crate"
    };
    private static string PreviewSupply => NoItemDesign ? null : Id switch
    {
        "com_provisionbox_1" => Supply.Key,
        "com_provisionbox_2" => Supply.Key,
        "cc_food_cart" => Supply.Herbs,
        "cc_treasure_chest" => Supply.Key,
        "trinket_chest" => Supply.Key,
        "cc_bone_pile" => Supply.Bandage,
        "cc_bloodflower" => Supply.Shovel,
        "cc_hive" => Supply.Torch,
        _ => Supply.Antivenom
    };
    private static SaveFile _save;
    private static bool _done, _failed, _used, _reload;
    private static int _step, _inputFrame = -1, _captureFrame = -1, _counter;
    private static float _started, _next;
    private static string _pack, _party;
    private static Texture2D _background;
    private static NativeCurioPainting _painting;
    private static readonly MethodInfo Prop = AccessTools.Method(typeof(CrawlUi), "DrawProp");
    private static readonly FieldInfo Click = AccessTools.Field(typeof(CrawlUi), "CurioClicked");
    private static readonly FieldInfo Drop = AccessTools.Field(typeof(CrawlUi), "CurioDrop");
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save, _save) && Session.Current.SavePath == null;
    internal static bool Active => Requested && OwnsSession && Driver.Instance?.Phase == Phase.Crawling;
    private static string Argument(string prefix) => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?.Substring(prefix.Length);
    private static void Require(bool yes, string why) { if (!yes) throw new InvalidOperationException(why); }
    private static void Fail(Exception e) { _failed = _done = true; Plugin.Log.LogError("[curio design] FAIL: " + e); }

    private static void Postfix()
    {
        if (!Requested || !Plugin.DebugKeysEnabled.Value || _done) return;
        var d = Driver.Instance; var s = Session.Current;
        if (d == null || s == null) return;
        try
        {
            if (_save == null)
            {
                if (d.Phase != Phase.Off || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU || Dd2Api.Modes.IsChangingState()) return;
                Require(s.Save == null && s.SavePath == null, "An estate is already open");
                Require(Path.IsPathRooted(Root ?? "") && Path.IsPathRooted(Shots ?? "") && Directory.Exists(Root), "Absolute private design pack and output paths required");
                Require(Path.GetFullPath(Root).StartsWith(@"D:\CodexArtifacts\DarkestDungeon3\", StringComparison.OrdinalIgnoreCase) && Path.GetFullPath(Shots).StartsWith(@"D:\CodexArtifacts\DarkestDungeon3\", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(Shots), "Use new private task outputs only");
                Require(!Environment.GetCommandLineArgs().Any(a => a == "-dd3-curio-check" || a == "-dd3-curio-actors-check"), "Run design QA in an isolated process");
                Require(Id == "cc_wine_crate" || Id == "cc_hive" || Id == "cc_bloodflower" || Id == "cc_bone_pile" || Id == "trinket_chest" || Id == "cc_treasure_chest" || Id == "cc_food_cart" || Id == "com_fireplace" || Id == "com_hero_health_heal" || Id == "com_party_health_heal" || Id == "com_hero_stress_heal" || Id == "com_party_stress_heal" || ProvisionDesign || KeyRewardChest, "Only reviewed canonical DLC design cases are supported");
                string csv = FarmsteadDesign
                    ? s.Dd1.PathOf("dlc", "735730_color_of_madness", "curios", "com_curio_type_library.csv")
                    : s.Dd1.PathOf("dlc", "580100_crimson_court", "curios", "cc_curio_type_library.csv");
                Require(File.ReadAllText(csv).Contains("," + Id + ","), "Canonical DLC identity missing");
                var estate = new Estate { Name = "Unsaved DLC design preview", Seed = 73232, Week = 12, OpeningRaidPending = false };
                foreach (string cls in new[] { "highwayman", "grave_robber", "plague_doctor", "man_at_arms" })
                    estate.Roster.Add(new HeroRecord { Id = "design-" + cls, Name = cls, ClassId = cls, ResolveLevel = 1 });
                var map = new DungeonMap { Dungeon = Region, EntranceRoomId = 0 };
                map.Rooms.Add(new Room { Id = 0, Content = RoomContent.Empty, Visited = true, Scouted = true, CurioId = Id });
                var exp = new ExpeditionState { Map = map, RoomId = 0, Seed = 73232, Started = true, Light = 100,
                    Quest = new QuestOffer { Id = "curio-design-preview", Dungeon = Region, Type = "cleanse", Difficulty = 1 },
                    Party = estate.Roster.Select(h => h.Id).ToList() };
                if (!NoItemDesign) exp.Pack.Add(PreviewSupply, 1);
                _save = new SaveFile { Estate = estate, Expedition = exp }; s.Save = _save;
                _started = Time.unscaledTime; Application.runInBackground = true;
                NativeCurioArt.Clear();
                AccessTools.Property(typeof(Driver), nameof(Driver.Phase)).SetValue(d, Phase.Embarking, null);
                Require(Dd2Run.Start(() => AccessTools.Method(typeof(Driver), "OnRoadReady").Invoke(d, null),
                    () => Fail(new InvalidOperationException("Native design host did not become ready"))), "Native host start rejected");
                Plugin.Log.LogInfo(FarmsteadDesign
                    ? "[curio design] transient canonical Farmstead visual context; no generated encounter, resolver, estate path or persistence"
                    : Id == "cc_food_cart"
                    ? "[curio design] transient paired Foetor visual context for canonical Warrens design; no generated encounter, resolver, estate path or persistence"
                    : "[curio design] transient canonical Courtyard visual context; no generated encounter, resolver, estate path or persistence");
            }
            Require(OwnsSession, "Transient design ownership lost");
            if (Time.unscaledTime - _started > 260) throw new TimeoutException("Native design stage " + _step);
        }
        catch (Exception e) { Fail(e); }
    }

    internal static bool Draw()
    {
        if (!Active) return true;
        var original = Event.current;
        try
        {
            Gui.Begin(); GUI.depth = -1000; Gui.BlockInput(true);
            var exp = _save.Expedition;
            NativeCurioArt.Prepare(exp);
            if (_background == null) _background = Art.Png(FarmsteadDesign
                ? Session.Current.Dd1.PathOf("dlc", "735730_color_of_madness", "dungeons", "farm", "farm.room_wall.windmill.png")
                : Id == "cc_food_cart"
                ? Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "data", "scenery", "dd2_farm-02.png")
                : Session.Current.Dd1.PathOf("dlc", "580100_crimson_court", "features", "crimson_court", "dungeons", Region, "courtyard.room_wall.hedge1.RMHDG.png"));
            Gui.Fill(new Rect(0, 0, 1920, 1080), Color.black);
            if (_background != null) GUI.DrawTexture(new Rect(0, 0, 1920, 720), _background);
            bool ready = _background != null && HeroStage.Lit && HeroStage.Instance?.Texture != null && NativeCurioArt.Ready
                && _save.Expedition.Party.All(id => HeroStage.Instance.HasHeroModel(Driver.Instance.Party.Guid(id)))
                && NativeCurioArt.TryGet(Id, _used, out _, out _painting);
            if (ready)
            {
                if (_step == 0) { _counter = exp.RandomCounter; _pack = JsonConvert.SerializeObject(exp.Pack); _party = JsonConvert.SerializeObject(exp.PartyStates); _step = 1; _next = Time.unscaledTime + 3; }
                Event injected = !_done && original.type == EventType.Repaint && Time.unscaledTime >= _next
                    && _inputFrame != Time.frameCount && _captureFrame != Time.frameCount ? Input() : null;
                if (injected != null) { Event.current = injected; _inputFrame = Time.frameCount; }
                Click.SetValue(null, false); Drop.SetValue(null, null); Drag.Begin();
                if (injected != null && (_step == 6 || _step == 8))
                    Drag.Source(new Rect(650, 810, 100, 100), new PackStack(exp.Pack, 0, PreviewSupply), _ => { });
                Prop.Invoke(null, new object[] { "curios", Id, _used, 1040f, true });
                Drag.Overlay();
                if (injected != null) Verify();
                Event.current = original;
                if (injected != null)
                {
                    Event.current = new Event(original) { mousePosition = injected.mousePosition };
                    Prop.Invoke(null, new object[] { "curios", Id, _used, 1040f, true });
                    Event.current = original;
                }
                GUI.DrawTexture(new Rect(0, 0, 1920, 720), HeroStage.Instance.Brightened ?? HeroStage.Instance.Texture);
                if (original.type == EventType.Repaint && !_done && Time.unscaledTime >= _next && _captureFrame != Time.frameCount)
                {
                    if (_step == 1) { Capture("closed"); _step = 2; _next = Time.unscaledTime + 1; }
                    else if (_step == 12) { Capture("used"); _step = 13; _reload = true; _next = Time.unscaledTime + 2; }
                    else if (_step == 13 && _reload) { NativeCurioArt.Clear(); _reload = false; _next = Time.unscaledTime + 2; }
                    else if (_step == 13 && !_reload) { if (!NoItemDesign) Capture("revisit"); _done = true; CheckUnchanged(); Plugin.Log.LogInfo("[curio design] PASS " + Id + "/" + Region + "; native four-hero scale, actual DrawProp " + (NoItemDesign ? "click/padding; no item route exercised" : "alpha picking/padding/drop dispatch") + ", inactive used state and reload; NO canonical resolution or gameplay claim"); }
                }
            }
            ExpeditionSkin.Text(new Rect(45, 25, 1800, 55), Title + " / " + ContextName + " / DESIGN visual and input check", 28, ExpeditionSkin.Ivory);
            ExpeditionSkin.Text(new Rect(45, 775, 1800, 55), "Unsaved native actors. Closed/used art states only. No supply consumption, loot or canonical outcome.", 22, ExpeditionSkin.Muted);
            ExpeditionSkin.Text(new Rect(45, 840, 1800, 55), _failed ? "FAILED. See retained log." : _done ? "Design check complete. Canonical DLC mechanics remain pending." : "Checking fixed body, picking, drop dispatch and used-state reload.", 22, ExpeditionSkin.Muted);
        }
        catch (Exception e) { Fail(e); }
        finally { Event.current = original; }
        return false;
    }

    private static Event Input()
    {
        if (_step < 2 || _step > 11) return null;
        var at = _painting.At(1040, 690); var point = Opaque(at);
        if (_step == 2 || _step == 3 || _step == 7) point = new Vector2(at.Left + 1, at.Top + 1);
        bool source = _step == 6 || _step == 8;
        if (source) point = new Vector2(700, 860);
        bool down = _step == 2 || _step == 4 || source || _step == 10;
        return new Event { type = down ? EventType.MouseDown : EventType.MouseUp, button = 0, mousePosition = point };
    }
    private static Vector2 Opaque(NativeCurioPlacement at)
    {
        for (int y = _painting.MaskHeight - 1; y >= 0; y--)
            for (int x = 0; x < _painting.MaskWidth; x++)
            {
                float u = (x + .5f) * _painting.MaskCellSize / _painting.PixelWidth, v = (y + .5f) * _painting.MaskCellSize / _painting.PixelHeight;
                if (_painting.Hit(u, v)) return new Vector2(at.Left + u * at.Width, at.Top + v * at.Height);
            }
        throw new InvalidOperationException("No alpha picking target");
    }
    private static void Verify()
    {
        bool clicked = (bool)Click.GetValue(null); string dropped = (string)Drop.GetValue(null);
        if (_step == 3) Require(!clicked && dropped == null, "Padding activated the design");
        if (_step == 5)
        {
            Require(clicked, "Opaque paint was not clickable");
            if (NoItemDesign)
            {
                _used = true;
                CheckUnchanged(); _step = 10; _next = Time.unscaledTime + .35f;
                return;
            }
            Capture("picked");
        }
        if (_step == 7) Require(dropped == null, "Padding accepted a drop");
        if (_step == 9) { Require(dropped == PreviewSupply, "Opaque drop dispatch missing"); Capture("drop"); _used = true; }
        if (_step == 11) Require(!clicked && dropped == null, "Used design remained interactive");
        CheckUnchanged(); _step++; _next = Time.unscaledTime + .35f;
    }
    private static void CheckUnchanged()
    {
        var exp = _save.Expedition;
        Require(exp.RandomCounter == _counter && JsonConvert.SerializeObject(exp.Pack) == _pack
            && JsonConvert.SerializeObject(exp.PartyStates) == _party && Driver.Instance.Crawl.LastCurio == null
            && !exp.Map.Rooms[0].CurioTaken && exp.GoalProgress == 0 && !exp.QuestComplete, "Design input changed gameplay state");
    }
    private static void Capture(string state)
    {
        Directory.CreateDirectory(Shots); _captureFrame = Time.frameCount;
        var type = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", true);
        AccessTools.Method(type, "CaptureScreenshot", new[] { typeof(string), typeof(int) }).Invoke(null,
            new object[] { Path.Combine(Shots, Id + "-" + Region + "-design-" + state + ".png"), 1 });
        Plugin.Log.LogInfo("[curio design] FRAME " + state + "; nativeActors=" + HeroStage.Lit + "; no resolver or persistence");
    }
}
[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class CurioDesignDraw { private static bool Prefix() => CurioDesignPreview.Draw(); }
[HarmonyPatch(typeof(Session), nameof(Session.Persist))]
internal static class CurioDesignNoSave { private static bool Prefix(ref bool __result) { if (!CurioDesignPreview.OwnsSession) return true; __result = true; return false; } }
[HarmonyPatch(typeof(Driver), nameof(Driver.Investigate))]
internal static class CurioDesignNoResolution { private static bool Prefix() => !CurioDesignPreview.OwnsSession; }
[HarmonyPatch(typeof(NativeCurioArt), "ReadManifest")]
internal static class CurioDesignManifest { private static void Prefix(ref string root) { if (CurioDesignPreview.OwnsSession) root = CurioDesignPreview.Root; } }
[HarmonyPatch(typeof(NativeCurioArt), "ReadPair")]
internal static class CurioDesignPair { private static void Prefix(ref string root) { if (CurioDesignPreview.OwnsSession) root = CurioDesignPreview.Root; } }
