using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using StudioSystem = FMOD.Studio.System;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1's own music, ambience and sounds, from the user's DD1 install (audio/master_banks + secondary_banks), played
/// through DD2's FMOD Studio system. Event names are DD1's ("/music/mus_exploration", "/general/party/hero_step"...,
/// the ones its executable uses). While our screens are up, DD2's music and sound VCAs are turned down (by GUID, so
/// DD1's own buses are untouched); during a fight DD2's sounds come back under DD1's battle music.
/// </summary>
internal static class Dd1Audio
{
    private static bool _tried, _ok;
    private static readonly List<Bank> Banks = new();
    private static readonly HashSet<string> Missing = new();
    private static EventInstance _music, _ambience;
    private static string _musicPath, _ambiencePath;

    private static VCA _dd2Music, _dd2Sfx;
    private static float _dd2MusicVolume = -1f, _dd2SfxVolume = -1f;

    // DD1's banks: everything but its heroes' voice banks (the heroes are DD2's) and the arena (PvP) bits.
    private static readonly string[] Secondary =
    {
        "general", "music", "ambience", "ui_dungeon", "raid_screen", "town", "title_screen",
        "props_shared", "props_crypts", "props_weald", "props_warrens", "props_cove", "props_darkestdungeon",
        "en_shared", "en_crypts", "en_weald", "en_warrens", "en_cove", "en_darkestdungeon",
    };

    public static bool Ready => Ensure();

    private static bool Ensure()
    {
        if (_tried) return _ok;
        if (!Plugin.Dd1AudioOn.Value || Session.Current == null) return false;
        StudioSystem sys;
        try
        {
            sys = RuntimeManager.StudioSystem;
            if (!sys.isValid()) return false;   // DD2's FMOD isn't up yet: try again later
        }
        catch (Exception) { return false; }
        _tried = true;
        try
        {
            // DD2's VCAs first, by GUID, before DD1's strings bank adds its own paths.
            _dd2Music = Vca(sys, "vca:/Music");
            _dd2Sfx = Vca(sys, "vca:/SFX");

            string dir = Session.Current.Dd1.PathOf("audio");
            if (!Load(sys, Path.Combine(dir, "master_banks", "master_bank.bank")))
            {
                Plugin.Log.LogWarning("[audio] DD1's master bank didn't load into DD2's FMOD: DD1 audio off");
                return _ok = false;
            }
            Load(sys, Path.Combine(dir, "master_banks", "master_bank.strings.bank"));
            int loaded = Secondary.Count(b => Load(sys, Path.Combine(dir, "secondary_banks", b + ".bank")));
            _ok = true;
            Plugin.Log.LogInfo($"[audio] DD1 banks loaded: master + {loaded}/{Secondary.Length}");
            DumpEvents();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[audio] DD1 audio unavailable: " + e.Message);
            _ok = false;
        }
        return _ok;
    }

    private static VCA Vca(StudioSystem sys, string path)
    {
        if (sys.lookupID(path, out GUID id) == RESULT.OK && sys.getVCAByID(id, out VCA vca) == RESULT.OK) return vca;
        Plugin.Log.LogInfo($"[audio] DD2 has no {path}");
        return default;
    }

    private static bool Load(StudioSystem sys, string path)
    {
        if (!File.Exists(path)) { Plugin.Log.LogInfo("[audio] missing " + path); return false; }
        var r = sys.loadBankFile(path, LOAD_BANK_FLAGS.NORMAL, out Bank bank);
        if (r != RESULT.OK && r != RESULT.ERR_EVENT_ALREADY_LOADED) { Plugin.Log.LogWarning($"[audio] {Path.GetFileName(path)}: {r}"); return false; }
        Banks.Add(bank);
        return true;
    }

    /// <summary>Every DD1 event path, once, next to the plugin (handy for mapping more sounds).</summary>
    private static void DumpEvents()
    {
        try
        {
            var paths = new List<string>();
            foreach (var b in Banks)
            {
                if (!b.isValid() || b.getEventList(out EventDescription[] events) != RESULT.OK) continue;
                foreach (var e in events)
                    if (e.getPath(out string p) == RESULT.OK && !string.IsNullOrEmpty(p)) paths.Add(p);
            }
            string file = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "dd1_audio_events.txt");
            File.WriteAllLines(file, paths.Distinct().OrderBy(p => p));
            Plugin.Log.LogInfo($"[audio] {paths.Count} DD1 events listed in {file}");
        }
        catch (Exception e) { Plugin.Log.LogInfo("[audio] event list: " + e.Message); }
    }

    private static bool Description(string path, out EventDescription desc)
    {
        desc = default;
        if (!Ensure() || string.IsNullOrEmpty(path)) return false;
        var r = RuntimeManager.StudioSystem.getEvent("event:" + path, out desc);
        if (r == RESULT.OK) return true;
        if (Missing.Add(path)) Plugin.Log.LogInfo($"[audio] no DD1 event {path} ({r})");
        return false;
    }

    /// <summary>Play a DD1 sound once (fire and forget).</summary>
    public static void Play(string path)
    {
        if (!Description(path, out var desc)) return;
        if (desc.createInstance(out EventInstance inst) != RESULT.OK) return;
        inst.start();
        inst.release();
    }

    /// <summary>The first of these events DD1 has, or null.</summary>
    public static string First(params string[] paths) => paths.FirstOrDefault(p => p != null && Description(p, out _));

    private static void Loop(ref EventInstance current, ref string currentPath, string path)
    {
        if (path == currentPath) return;
        if (current.isValid())
        {
            current.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            current.release();
            current = default;
        }
        currentPath = path;
        if (path == null || !Description(path, out var desc) || desc.createInstance(out EventInstance inst) != RESULT.OK) return;
        inst.start();
        current = inst;
    }

    /// <summary>
    /// What should be playing for where the player is: DD1's town music and ambience in the Hamlet, the zone's
    /// exploration music and ambience in the dungeon (camp music at camp), its battle music in a fight; nothing (and
    /// DD2's own audio back) outside our screens.
    /// </summary>
    public static void Update(Phase phase, string zone, bool camping)
    {
        if (!Plugin.Dd1AudioOn.Value || !Ensure())
        {
            if (_ok) { Loop(ref _music, ref _musicPath, null); Loop(ref _ambience, ref _ambiencePath, null); }
            RestoreDd2();
            return;
        }
        switch (phase)
        {
            case Phase.Hamlet:
            case Phase.Homecoming:
                Loop(ref _music, ref _musicPath, "/music/mus_town");
                Loop(ref _ambience, ref _ambiencePath, "/ambience/town/general");
                QuietDd2(sfx: true);
                break;
            case Phase.Embarking:
            case Phase.Crawling:
                Loop(ref _music, ref _musicPath, camping ? "/music/mus_camp" : "/music/mus_exploration");
                Loop(ref _ambience, ref _ambiencePath, camping ? "/ambience/local/campfire" : First("/ambience/dungeon/" + zone, "/ambience/dungeon/crypts"));
                QuietDd2(sfx: true);
                break;
            case Phase.Fighting:
                Loop(ref _music, ref _musicPath, First("/music/mus_battle_" + zone, "/music/mus_battle_crypts", "/music/mus_battle"));
                Loop(ref _ambience, ref _ambiencePath, First("/ambience/dungeon/" + zone));
                QuietDd2(sfx: false);   // DD2 runs the fight: its sounds stay
                break;
            default:
                Loop(ref _music, ref _musicPath, null);
                Loop(ref _ambience, ref _ambiencePath, null);
                RestoreDd2();
                break;
        }
    }

    private static void QuietDd2(bool sfx)
    {
        if (_dd2Music.isValid())
        {
            if (_dd2MusicVolume < 0 && _dd2Music.getVolume(out float v) == RESULT.OK) _dd2MusicVolume = v;
            _dd2Music.setVolume(0f);
        }
        if (_dd2Sfx.isValid())
        {
            if (_dd2SfxVolume < 0 && _dd2Sfx.getVolume(out float v) == RESULT.OK) _dd2SfxVolume = v;
            _dd2Sfx.setVolume(sfx ? 0f : Mathf.Max(0f, _dd2SfxVolume));
        }
    }

    private static void RestoreDd2()
    {
        if (_dd2MusicVolume >= 0 && _dd2Music.isValid()) _dd2Music.setVolume(_dd2MusicVolume);
        if (_dd2SfxVolume >= 0 && _dd2Sfx.isValid()) _dd2Sfx.setVolume(_dd2SfxVolume);
        _dd2MusicVolume = _dd2SfxVolume = -1f;
    }
}
