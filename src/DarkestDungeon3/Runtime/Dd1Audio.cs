using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DarkestDungeon3.Core.Dd1;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using CoreSystem = FMOD.System;
using StudioSystem = FMOD.Studio.System;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1's own music, ambience and sounds from the user's DD1 install. DD2's FMOD Studio can't load DD1's (older) banks,
/// so the sample banks (FSB5) inside them are played with FMOD's core API, straight from the bank files: music and
/// ambience stream, sounds are kept as compressed samples. While our screens are up, DD2's music and sound VCAs are
/// turned down; during a fight DD2's sounds come back under DD1's battle music.
/// </summary>
internal static class Dd1Audio
{
    private static bool _tried, _ok;
    private static Fsb5Index _index;
    private static readonly Dictionary<Fsb5Index.Chunk, Sound> Samples = new();
    private static readonly System.Random Rng = new();
    private static readonly HashSet<string> Missing = new();

    private sealed class Loop
    {
        public string Name;
        public Sound Parent;
        public Channel Channel;
        public float Volume, Target;
    }

    private static Loop _music, _ambience;
    private static readonly List<Loop> Fading = new();

    private static VCA _dd2Music, _dd2Sfx;
    private static float _dd2MusicVolume = -1f, _dd2SfxVolume = -1f;

    private static readonly string[] Banks =
    {
        "music", "ambience", "general", "ui_dungeon", "ui_shared", "ui_town", "raid_screen", "town",
        "props_shared", "props_crypts", "props_weald", "props_warrens", "props_cove", "props_darkestdungeon",
    };

    public static bool Ready => Ensure();

    private static bool Ensure()
    {
        if (_tried) return _ok;
        if (!Plugin.Dd1AudioOn.Value || Session.Current == null) return false;
        try { if (!RuntimeManager.CoreSystem.hasHandle()) return false; }
        catch (Exception) { return false; }
        _tried = true;
        try
        {
            var studio = RuntimeManager.StudioSystem;
            _dd2Music = Vca(studio, "vca:/Music");
            _dd2Sfx = Vca(studio, "vca:/SFX");
            string dir = Session.Current.Dd1.PathOf("audio", "secondary_banks");
            _index = Fsb5Index.Load(Banks.Select(b => Path.Combine(dir, b + ".bank")));
            _ok = _index.Count > 0;
            Plugin.Log.LogInfo($"[audio] DD1 sounds: {_index.Count} samples in {_index.Chunks.Count} sample banks");
            try
            {
                string file = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "dd1_audio_samples.txt");
                File.WriteAllLines(file, _index.Names.OrderBy(n => n));
            }
            catch (Exception) { }
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
        return default;
    }

    private static CREATESOUNDEXINFO Info(Fsb5Index.Chunk chunk, int subsound)
    {
        var info = new CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
            fileoffset = (uint)chunk.Offset,
            length = (uint)chunk.Length,
        };
        if (subsound >= 0) info.initialsubsound = subsound;
        return info;
    }

    // ---- one-shot sounds ----

    /// <summary>
    /// Play a DD1 sound once. <paramref name="what"/> is a DD1 event name ("/general/party/hero_step"), turned into its
    /// samples ("gen_party_foot_stone_03"...; one is picked), or a sample name.
    /// </summary>
    public static void Play(string what, string zone = null)
    {
        if (!Ensure() || what == null) return;
        var names = Candidates(what, zone ?? CurrentZone);
        if (names.Count == 0) { if (Missing.Add(what)) Plugin.Log.LogInfo($"[audio] no DD1 sample for {what}"); return; }
        string name = names[Rng.Next(names.Count)];
        if (!_index.TryGet(name, out var chunk, out int i)) return;
        try
        {
            var core = RuntimeManager.CoreSystem;
            if (!Samples.TryGetValue(chunk, out var parent))
            {
                var info = Info(chunk, -1);
                var r = core.createSound(chunk.File, MODE.CREATECOMPRESSEDSAMPLE | MODE._2D | MODE.LOOP_OFF, ref info, out parent);
                if (r != RESULT.OK) { Plugin.Log.LogWarning($"[audio] {Path.GetFileName(chunk.File)}: {r}"); parent = default; }
                Samples[chunk] = parent;
            }
            if (!parent.hasHandle() || parent.getSubSound(i, out Sound sound) != RESULT.OK) return;
            core.getMasterChannelGroup(out ChannelGroup master);
            float level = what == "/general/party/hero_step" ? 0.45f : 1f;   // footsteps sit under everything
            if (core.playSound(sound, master, false, out Channel ch) == RESULT.OK) ch.setVolume(Plugin.Dd1SoundVolume.Value * SfxGain() * level);
        }
        catch (Exception e) { if (Missing.Add("!" + what)) Plugin.Log.LogWarning($"[audio] {what}: {e.Message}"); }
    }

    public static string CurrentZone;

    private static readonly Dictionary<string, string> Footing = new()
    {
        ["crypts"] = "stone", ["weald"] = "dirt", ["warrens"] = "stone", ["cove"] = "dirt", ["darkestdungeon"] = "stone",
    };

    /// <summary>The DD1 samples for one of DD1's event names (or a sample name / prefix).</summary>
    private static List<string> Candidates(string what, string zone)
    {
        string prefix = what switch
        {
            "/general/party/hero_step" => "gen_party_foot_" + (zone != null && Footing.TryGetValue(zone, out var f) ? f : "stone"),
            "/general/map/room_transition" => zone != null && _index.Has("gen_map_door_open_" + zone) ? "gen_map_door_open_" + zone : "gen_map_door_open",
            "/general/map/camp_start" => "gen_map_campstart",
            "/general/map/camp_end" => "gen_map_campend",
            "/general/map/torch_out" => "gen_map_torchout",
            "/general/combat/ambush" => "gen_combat_ambush",
            "/general/combat/retreat" => "gen_combat_retreat",
            "/general/combat/victory" => "Combat_Level2_Victory",
            "/general/combat/start" => "gen_map_com_mix",
            "/ui/shared/button_click" => _index.Has("ui_town_button_click") ? "ui_town_button_click" : "ui_shared_button_click",
            _ => what.Trim('/').Replace('/', '_'),
        };
        if (_index.Has(prefix)) return new List<string> { prefix };
        return _index.Names.Where(n => n.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)
                                       && n.Length > prefix.Length + 1 && char.IsDigit(n[prefix.Length + 1])).ToList();
    }

    // ---- music and ambience ----

    private static void SetLoop(ref Loop current, string name, float volume)
    {
        if (current?.Name == name) { if (current != null) current.Target = volume; return; }
        if (current != null) { current.Target = 0f; Fading.Add(current); current = null; }
        if (name == null || !_index.TryGet(name, out var chunk, out int i)) return;
        try
        {
            var core = RuntimeManager.CoreSystem;
            var info = Info(chunk, i);
            if (core.createSound(chunk.File, MODE.CREATESTREAM | MODE._2D | MODE.LOOP_NORMAL, ref info, out Sound parent) != RESULT.OK) return;
            if (parent.getSubSound(i, out Sound sound) != RESULT.OK) { parent.release(); return; }
            sound.setMode(MODE.LOOP_NORMAL);
            sound.setLoopCount(-1);
            core.getMasterChannelGroup(out ChannelGroup master);
            if (core.playSound(sound, master, true, out Channel ch) != RESULT.OK) { parent.release(); return; }
            ch.setVolume(0f);
            ch.setPaused(false);
            current = new Loop { Name = name, Parent = parent, Channel = ch, Volume = 0f, Target = volume };
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[audio] {name}: {e.Message}"); }
    }

    /// <summary>Fade loops toward their targets (1.5 s), releasing the ones faded out.</summary>
    private static void Fade()
    {
        float step = Time.unscaledDeltaTime / 1.5f;
        foreach (var l in new[] { _music, _ambience }.Where(l => l != null).Concat(Fading))
        {
            l.Volume = Mathf.MoveTowards(l.Volume, l.Target, step);
            l.Channel.setVolume(l.Volume);
        }
        for (int i = Fading.Count - 1; i >= 0; i--)
        {
            if (Fading[i].Volume > 0f) continue;
            Fading[i].Channel.stop();
            Fading[i].Parent.release();
            Fading.RemoveAt(i);
        }
    }

    private static string Pick(params string[] names) => names.FirstOrDefault(n => n != null && _index.Has(n));

    /// <summary>DD1 has a sample of this name.</summary>
    public static bool Has(string sample) => Ensure() && _index.Has(sample);

    /// <summary>DD1's exploration music for the zone, darker as the torch burns down.</summary>
    private static string Exploration(string zone, float light)
    {
        if (zone == "weald") return Pick(light > 50 ? "Mournweald_LEVEL1_LOOP1_V11" : "Mournweald_LEVEL2_LOOP1_V13b", "Mournweald_LEVEL1_LOOP1_V11");
        int level = light > 75 ? 1 : light > 50 ? 2 : light > 25 ? 3 : 4;
        return Pick($"Explore_Vaults_Level_{level}_Loop", "Explore_Vaults_Level_1_Loop");
    }

    private static string Battle(string zone, bool hall) => zone switch
    {
        "weald" => Pick("mus_combat_weald_hallway", "Combat_Level1_Loop1"),
        "warrens" => Pick(hall ? "mus_combat_warrens_hallway" : "WARRENS_Combat_LOOP1_LEVEL1_V06b", "WARRENS_Combat_LOOP1_LEVEL1_V06b"),
        "cove" => Pick(hall ? "mus_combat_cove_hallway_a" : "mus_combat_cove_lvl1_loop1", "mus_combat_cove_lvl1_loop1"),
        "darkestdungeon" => Pick(hall ? "mus_combat_dd_hallway_a" : "mus_combat_dd_lvl1_loop1", "mus_combat_dd_lvl1_loop1"),
        _ => Pick(hall ? "mus_combat_hallway_part_a" : "Combat_Level1_Loop1", "Combat_Level1_Loop1"),
    };

    private static string Ambience(string zone, float light)
    {
        string z = zone == "crypts" ? "ruins" : zone == "darkestdungeon" ? "darkest_1" : zone;
        return Pick(light < 25 ? $"amb_dun_{z}_dark" : $"amb_dun_{z}_base", $"amb_dun_{z}_base", "amb_dun_ruins_base");
    }

    /// <summary>
    /// What should be playing for where the player is: DD1's town music and ambience in the Hamlet, the zone's
    /// exploration music and ambience in the dungeon (camp music at camp), its battle music in a fight; nothing (and
    /// DD2's own audio back) outside our screens.
    /// </summary>
    public static void Update(Phase phase, string zone, bool camping, float light = 100f, bool hallFight = false)
    {
        CurrentZone = zone;
        if (!Plugin.Dd1AudioOn.Value || !Ensure())
        {
            if (_ok) { SetLoop(ref _music, null, 0); SetLoop(ref _ambience, null, 0); Fade(); }
            RestoreDd2();
            return;
        }
        // Under DD2's own volume settings (its Master/Music/SFX sliders), and mixed below the narrator.
        float mv = Plugin.Dd1MusicVolume.Value * 0.45f * MusicGain(), av = Plugin.Dd1SoundVolume.Value * 0.35f * SfxGain();
        switch (phase)
        {
            case Phase.Hamlet:
            case Phase.Homecoming:
                SetLoop(ref _music, Pick("Town_Stereo_Mix_LOOP_1"), mv);
                SetLoop(ref _ambience, Pick("amb_town_gen_base", "amb_town2_gen_base"), av);
                QuietDd2(sfx: true);
                break;
            case Phase.Embarking:
            case Phase.Crawling:
                SetLoop(ref _music, camping ? Pick("Camping_Stereo_Mix_LOOP_1") : Exploration(zone, light), mv);
                SetLoop(ref _ambience, camping ? Pick("amb_local_campfire") : Ambience(zone, light), av);
                QuietDd2(sfx: true);
                break;
            case Phase.Fighting:
                SetLoop(ref _music, Battle(zone, hallFight), mv);
                SetLoop(ref _ambience, Ambience(zone, light), av * 0.6f);
                QuietDd2(sfx: false);   // DD2 runs the fight: its sounds stay
                break;
            default:
                SetLoop(ref _music, null, 0);
                SetLoop(ref _ambience, null, 0);
                RestoreDd2();
                break;
        }
        Fade();
    }

    private static VCA _dd2Master;
    private static float _dd2MasterVolume = -1f;

    private static float MasterGain()
    {
        if (_dd2MasterVolume < 0 && _ok)
        {
            _dd2Master = Vca(RuntimeManager.StudioSystem, "vca:/Master");
            _dd2MasterVolume = _dd2Master.isValid() && _dd2Master.getVolume(out float v) == RESULT.OK ? v : 1f;
        }
        return _dd2MasterVolume < 0 ? 1f : _dd2MasterVolume;
    }

    /// <summary>DD2's own music volume setting (as it was before we turned DD2's music down) times its master.</summary>
    private static float MusicGain()
    {
        float music = _dd2MusicVolume >= 0 ? _dd2MusicVolume : _dd2Music.isValid() && _dd2Music.getVolume(out float v) == RESULT.OK ? v : 1f;
        return music * MasterGain();
    }

    private static float SfxGain()
    {
        float sfx = _dd2SfxVolume >= 0 ? _dd2SfxVolume : _dd2Sfx.isValid() && _dd2Sfx.getVolume(out float v) == RESULT.OK ? v : 1f;
        return 0.8f * sfx * MasterGain();
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
