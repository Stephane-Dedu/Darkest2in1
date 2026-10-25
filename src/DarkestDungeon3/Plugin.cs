using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DarkestDungeon3.Runtime;
using DarkestDungeon3.Ui;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "piral.darkestdungeon3";
    public const string Name = "Darkest Dungeon 3";
    public const string Version = "0.2.0";

    internal static ManualLogSource Log;
    internal static GamePathSetting Dd1Path;
    internal static GamePathSetting NativeRoomSceneryPath;
    internal static GamePathSetting ExpeditionArtPath;
    internal static GamePathSetting HamletArtPath;
    internal static ConfigEntry<bool> HamletCameraMotion;
    internal static ConfigEntry<bool> DebugKeysEnabled;
    internal static GamePathSetting ExportedBossModel;
    internal static ConfigEntry<Runtime.Art.LargeArt> HeroArt;
    internal static ConfigEntry<bool> HeroModels;
    internal static ConfigEntry<float> HeroModelScale;
    internal static ConfigEntry<bool> SkipDd2Results;
    internal static ConfigEntry<bool> Dd1MonsterArt;
    internal static ConfigEntry<bool> Dd1MonsterSkills;
    internal static ConfigEntry<bool> Dd1AudioOn;
    internal static ConfigEntry<float> Dd1MusicVolume, Dd1SoundVolume;
    internal static ConfigEntry<bool> Dd1BackdropOn;
    internal static ConfigEntry<bool> FightInPlace;
    internal static ConfigEntry<float> HeroModelBrightness;
    internal static ConfigEntry<string> HeroModelPose;
    internal static ConfigEntry<float> Dd1MonsterScale;
    internal static ConfigEntry<bool> Shieldbreaker3D;
    internal static GamePathSetting HeroModelPath;
    internal static ConfigEntry<bool> VilhelmHero;

    private void Awake()
    {
        Log = Logger;
        // Every Paths setting may be relative to the game folder, so a zipped copy runs wherever it is unpacked.
        Dd1Path = new GamePathSetting(Config.Bind("Paths", "DarkestDungeon1Folder", "",
            "Your Darkest Dungeon 1 install folder (\".\" when DD1's files sit in the game folder). Leave empty to find it in your Steam libraries."));
        NativeRoomSceneryPath = new GamePathSetting(Config.Bind("Paths", "NativeRoomSceneryFolder", "PrivateScenery",
            "Optional private DD2 scenery pack: dd2_city-arena-01.png for rooms, dd2_city-corridor-01.png for halls; up to 12 of each per region. Empty disables it. Missing/invalid art retains existing scenery."));
        ExpeditionArtPath = new GamePathSetting(Config.Bind("Paths", "ExpeditionArtFolder", "PrivateExpeditionArt",
            "Private expedition selector artwork. The standard3D view uses map3d.png, map3d.json and optional vfx/. Missing or invalid3D inputs use the painted recovery map."));
        HamletArtPath = new GamePathSetting(Config.Bind("Paths", "HamletArtFolder", "PrivateHamletArt",
            "Optional painted recovery scene and building masks, prepared with tools/prepare_hamlet_art.py. The standard Hamlet uses installed DD2 scenery and NativeRoomSceneryFolder/HamletModels."));
        HamletCameraMotion = Config.Bind("Look", "HamletCameraMotion", true,
            "Allow gentle camera focus after selecting a Hamlet service and chimney smoke. Off keeps a still overview; explicit pan/zoom/reset remain available. Change with the town's Camera motion control.");
        HeroModelPath = new GamePathSetting(Config.Bind("Paths", "HeroModelFolder", "PrivateHeroModels",
            "Private locally converted hero models. Build Shieldbreaker with tools/shieldbreaker/build_model.py; never distribute the derived game assets."));
        VilhelmHero = Config.Bind("Experimental", "VilhelmHero", false,
            "Allow inviting Sir Vilhelm at the Stagecoach when the owner's private animated model is installed. Local test content; no Souls zone is enabled.");
        Shieldbreaker3D = Config.Bind("Look", "Shieldbreaker3D", true,
            "Use the experimental rigged Shieldbreaker model in corridors and fights. Missing or incompatible files retain her DD1 art. Off restores DD1 combat sprites.");
        ExportedBossModel = new GamePathSetting(Config.Bind("Paths", "ExportedBossModel", Path.Combine("PrivateBossModels", "soul_of_cinder", "soul_of_cinder.gltf"),
            "Testing: a private glTF export (with its .bin and textures beside it) that stands in for a Lost Battalion knight in the F1 test fight. Never shipped."));
        DebugKeysEnabled = Config.Bind("Debug", "DebugKeys", true, "F8 dumps state, F9 test fight from the road, F10 wins a fight, F11 starts a fight in the dungeon.");
        HeroModels = Config.Bind("Look", "Dd2HeroModelsInDungeon", true, "DD2's animated hero models in the DD1 dungeon (falls back to DD2's flat hero art by itself if they render black).");
        Dd1AudioOn = Config.Bind("Sound", "Dd1MusicAndSounds", true, "DD1's own music, ambience and sounds (from your DD1 install) on our screens; DD2's music is turned down while they play.");
        Dd1MusicVolume = Config.Bind("Sound", "Dd1MusicVolume", 0.7f, "Volume of DD1's music (0-1).");
        Dd1SoundVolume = Config.Bind("Sound", "Dd1SoundVolume", 0.8f, "Volume of DD1's sounds and ambience (0-1).");
        HeroModelPose = Config.Bind("Look", "Dd2HeroModelPose", "combat", "Pose of the DD2 hero models in the dungeon: \"combat\" (DD2's fight stance, facing the way the party walks) or \"neutral\" (DD2's road/inn idle).");
        HeroModelBrightness = Config.Bind("Look", "HeroModelBrightness", 2.6f, "How much the DD2 hero models are brightened in the dungeon (their off-screen stage lacks DD2's arena lighting).");
        Dd1BackdropOn = Config.Bind("Look", "Dd1BackdropInFights", true, "Fights happen in front of the DD1 room or hallway the party is in (DD2's arena scenery hidden) instead of a DD2 arena.");
        FightInPlace = Config.Bind("Look", "FightStartsInPlace", true, "DD1's battle start: a fight begins in the corridor or room the party stands in (DD2 regions too), without DD2's fade, loading or battle intro. Off: DD2's arenas and transition.");
        Dd1MonsterSkills = Config.Bind("Gameplay", "Dd1MonsterSkills", true, "DD1 monsters fight with their DD1 skills: DD1's ranks, targets, damage (scaled to the DD2 enemy standing in), crit and effects, built as DD2 skills on the stand-in. Off: the stand-in's own DD2 skills, shown under DD1's names.");
        Dd1MonsterArt = Config.Bind("Look", "Dd1MonstersInFights", true, "Draw DD1's own animated monsters over the DD2 enemies standing in for them (DD2 still runs the fight).");
        Dd1MonsterScale = Config.Bind("Look", "Dd1MonsterScale", 1f, "Size of the DD1 monsters in fights, relative to the DD2 model they replace.");
        SkipDd2Results = Config.Bind("Look", "SkipDd2ResultsView", true, "After a fight, go straight back to the dungeon (DD1's spoils scroll) instead of DD2's stagecoach results view.");
        HeroModelScale = Config.Bind("Look", "HeroModelScale", 1f, "Size of the DD2 hero models in the dungeon.");
        HeroArt = Config.Bind("Look", "HeroArtInDungeon", Runtime.Art.LargeArt.Story, "Which DD2 hero picture stands in the DD1 dungeon (Altar, HeroStory, Story).");

        // Consume orphaned entries before removing them so older false values cannot keep either view in2D.
        foreach (string key in new[] { "Hamlet3D", "ExpeditionMap3D" })
            Config.Remove(Config.Bind("Look",key,true).Definition);
        Config.Save();

        // Each patch class on its own: one that no longer fits the game is logged and skipped, the others still apply
        // (PatchAll stops at the first failure).
        var harmony = new Harmony(Guid);
        int patched = 0;
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            try
            {
                var applied = harmony.CreateClassProcessor(type).Patch();
                if (applied != null && applied.Count > 0) patched++;
            }
            catch (Exception e) { Log.LogWarning($"[patch] {type.Name} not applied: {e.InnerException?.Message ?? e.Message}"); }
        }
        Log.LogInfo($"[patch] {patched} patch classes applied");

        // BepInEx's own manager object can be destroyed by scene loads in some games; keep ours separate.
        var host = new GameObject("DarkestDungeon3");
        DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<Driver>();
        host.AddComponent<Dd2.HeroStage>();
        host.AddComponent<Dd2.ExportedHeroes>();
        host.AddComponent<Dd2.HamletStage>();
        host.AddComponent<UiRoot>();
        if (DebugKeysEnabled.Value)
        {
            host.AddComponent<DebugKeys>();
            host.AddComponent<Dd2.ExportedBoss>();   // F1: the exported-model test fight
        }

        Session.BeginLoad(Dd1Path.Value, Path.GetDirectoryName(Info.Location), Application.streamingAssetsPath, HamletArtPath.Value);
        Log.LogInfo($"{Name} {Version} loaded");
    }
}

/// <summary>A folder or file setting. A relative value is read inside the game folder, never the working directory.</summary>
internal sealed class GamePathSetting
{
    private readonly ConfigEntry<string> _entry;

    internal GamePathSetting(ConfigEntry<string> entry) => _entry = entry;

    internal string Value
    {
        get
        {
            string value = _entry.Value?.Trim();
            if (string.IsNullOrEmpty(value) || Path.IsPathRooted(value)) return value;
            return Path.GetFullPath(Path.Combine(Paths.GameRootPath, value));
        }
    }
}
