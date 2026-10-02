using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "piral.darkestdungeon3";
    public const string Name = "Darkest Dungeon 3";
    public const string Version = "0.1.0";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

        // BepInEx's own manager object can be destroyed by scene loads in some games; keep ours separate.
        var host = new GameObject("DarkestDungeon3");
        DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<DebugKeys>();

        Log.LogInfo($"{Name} {Version} loaded");
    }
}
