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

    private void Awake()
    {
        Log = Logger;
        Dd1Path = Config.Bind("Paths", "DarkestDungeon1Folder", "",
            "Your Darkest Dungeon 1 install folder. Leave empty to find it in your Steam libraries.");
        DebugKeysEnabled = Config.Bind("Debug", "DebugKeys", true, "F8 dumps game state to the log, F9 starts a test fight.");

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

        // BepInEx's own manager object can be destroyed by scene loads in some games; keep ours separate.
        var host = new GameObject("DarkestDungeon3");
        DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<Driver>();
        host.AddComponent<UiRoot>();
        if (DebugKeysEnabled.Value) host.AddComponent<DebugKeys>();

        Session.BeginLoad(Dd1Path.Value, Path.GetDirectoryName(Info.Location));
        Log.LogInfo($"{Name} {Version} loaded");
    }
}
