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
    internal static ConfigEntry<bool> DebugKeysEnabled;
    internal static ConfigEntry<Runtime.Art.LargeArt> HeroArt;
    internal static ConfigEntry<bool> HeroModels;
    internal static ConfigEntry<float> HeroModelScale;
    internal static ConfigEntry<bool> SkipDd2Results;
    internal static ConfigEntry<bool> Dd1MonsterArt;
    internal static ConfigEntry<float> Dd1MonsterScale;

    private void Awake()
    {
        Log = Logger;
        Dd1Path = Config.Bind("Paths", "DarkestDungeon1Folder", "",
            "Your Darkest Dungeon 1 install folder. Leave empty to find it in your Steam libraries.");
        DebugKeysEnabled = Config.Bind("Debug", "DebugKeys", true, "F8 dumps state, F9 test fight from the road, F10 wins a fight, F11 starts a fight in the dungeon.");
        HeroModels = Config.Bind("Look", "HeroModelsInDungeon", false, "Experimental: DD2's animated hero models in the DD1 dungeon. They currently render unlit (black) off-screen.");
        Dd1MonsterArt = Config.Bind("Look", "Dd1MonstersInFights", true, "Draw DD1's own animated monsters over the DD2 enemies standing in for them (DD2 still runs the fight).");
        Dd1MonsterScale = Config.Bind("Look", "Dd1MonsterScale", 1f, "Size of the DD1 monsters in fights, relative to the DD2 model they replace.");
        SkipDd2Results = Config.Bind("Look", "SkipDd2ResultsView", true, "After a fight, go straight back to the dungeon (DD1's spoils scroll) instead of DD2's stagecoach results view.");
        HeroModelScale = Config.Bind("Look", "HeroModelScale", 1f, "Size of the DD2 hero models in the dungeon.");
        HeroArt = Config.Bind("Look", "HeroArtInDungeon", Runtime.Art.LargeArt.Story, "Which DD2 hero picture stands in the DD1 dungeon (Altar, HeroStory, Story).");

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

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
