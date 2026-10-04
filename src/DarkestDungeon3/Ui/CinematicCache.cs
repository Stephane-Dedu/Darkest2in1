using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;

namespace DarkestDungeon3.Ui;

/// <summary>
/// Playable copies of DD1's cinematics, made on the user's machine from their own DD1 files and kept in the mod's cache
/// folder: the narration as .ogg (copied out of the .ogv), the picture as VP8 .webm (ffmpeg, if installed; a built
/// Unity game decodes VP8 itself but not Theora).
/// </summary>
internal static class CinematicCache
{
    private static Process _converting;
    private static readonly Queue<string> Pending = new();

    private static string Dir
    {
        get
        {
            string dir = Path.Combine(Session.SaveDir, "cache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Voice(string name, string ogv)
    {
        try
        {
            string file = Path.Combine(Dir, "dd1_" + name + ".ogg");
            if (!File.Exists(file)) File.WriteAllBytes(file, Dd1Cinematic.VorbisAudio(File.ReadAllBytes(ogv)));
            return file;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {name} voice: {e.Message}"); return null; }
    }

    /// <summary>The ready .webm picture, or null (still converting, or no ffmpeg).</summary>
    public static string Picture(string name)
    {
        string file = Path.Combine(Dir, "dd1_" + name + ".webm");
        bool busy = _converting != null && !_converting.HasExited && Equals(_converting.StartInfo.Arguments.Contains(name), true);
        return File.Exists(file) && !busy ? file : null;
    }

    /// <summary>Convert the cinematics not converted yet, one at a time, in the background.</summary>
    public static void Prepare(Dd1Install dd1, IEnumerable<string> names)
    {
        string ffmpeg = FindFfmpeg();
        if (ffmpeg == null) { Plugin.Log.LogInfo("[cinematic] no ffmpeg: cinematics play as narration over DD1's title art"); return; }
        foreach (var n in names)
            if (!File.Exists(Path.Combine(Dir, "dd1_" + n + ".webm")) && !Pending.Contains(n)) Pending.Enqueue(n);
        Pump(dd1, ffmpeg);
    }

    private static void Pump(Dd1Install dd1, string ffmpeg)
    {
        if ((_converting != null && !_converting.HasExited) || Pending.Count == 0) return;
        string name = Pending.Dequeue();
        string src = Dd1Cinematic.VideoPath(dd1, name), part = Path.Combine(Dir, "dd1_" + name + ".part.webm"), done = Path.Combine(Dir, "dd1_" + name + ".webm");
        if (!File.Exists(src)) { Pump(dd1, ffmpeg); return; }
        try
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo(ffmpeg, $"-y -loglevel error -i \"{src}\" -an -c:v libvpx -b:v 3M -deadline realtime -cpu-used 8 \"{part}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            p.Exited += (_, _) =>
            {
                try
                {
                    if (p.ExitCode == 0 && File.Exists(part)) { if (File.Exists(done)) File.Delete(done); File.Move(part, done); }
                    Plugin.Log.LogInfo($"[cinematic] {name}: picture {(File.Exists(done) ? "ready" : "failed (ffmpeg " + p.ExitCode + ")")}");
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {name}: {e.Message}"); }
                Pump(dd1, ffmpeg);
            };
            p.Start();
            _converting = p;
            Plugin.Log.LogInfo($"[cinematic] {name}: converting the picture for Unity (ffmpeg, in the background)");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {name}: ffmpeg failed to start: {e.Message}"); }
    }

    /// <summary>ffmpeg on the PATH, or WinGet's usual install.</summary>
    private static string FindFfmpeg()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            try { string f = Path.Combine(dir.Trim(), "ffmpeg.exe"); if (File.Exists(f)) return f; } catch (Exception) { }
        try
        {
            string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(winget))
                return Directory.GetFiles(winget, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (Exception) { }
        return null;
    }
}
