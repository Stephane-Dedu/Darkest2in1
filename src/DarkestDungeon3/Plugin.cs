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
    internal static ConfigEntry<string> Dd1Path;
    internal static ConfigEntry<string> NativeRoomSceneryPath;
    internal static ConfigEntry<string> ExpeditionArtPath;
    internal static ConfigEntry<bool> DebugKeysEnabled;
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
    internal static ConfigEntry<bool> Dd2DestinationMenu;

    private void Awake()
    {
        Log = Logger;
        Dd1Path = Config.Bind("Paths", "DarkestDungeon1Folder", "",
            "Your Darkest Dungeon 1 install folder. Leave empty to find it in your Steam libraries.");
        NativeRoomSceneryPath = Config.Bind("Paths", "NativeRoomSceneryFolder", Path.Combine(Paths.GameRootPath, "PrivateScenery"),
            "Optional private DD2 scenery pack: dd2_city-arena-01.png for rooms, dd2_city-corridor-01.png for halls; up to 12 of each per region. Empty disables it. Missing/invalid art retains existing scenery.");
        ExpeditionArtPath = Config.Bind("Paths", "ExpeditionArtFolder", Path.Combine(Paths.GameRootPath, "PrivateExpeditionArt"),
            "Optional private expedition selector artwork. Contains map.png, regional overlays and preview images. Empty or missing map.png retains the DD1 selector.");
        Dd2DestinationMenu = Config.Bind("Look", "Dd2DestinationMenu", false,
            "Preview: Embark opens DD2's destination cards (the innkeeper's region choice, each region's quests in place of its modifier) instead of the DD1 quest map. Also switched in the Hamlet's Regions panel and on the quest screen.");
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
        host.AddComponent<UiRoot>();
        if (DebugKeysEnabled.Value) host.AddComponent<DebugKeys>();

        Session.BeginLoad(Dd1Path.Value, Path.GetDirectoryName(Info.Location));
        Log.LogInfo($"{Name} {Version} loaded");
    }
}
