using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private static readonly ConcurrentQueue<Action> Completed = new();
    private sealed class VoiceResult { public string File, Error; }
    private sealed class VoiceJob { public string Name; public Task<VoiceResult> Work; public bool Reported; }
    private static readonly Dictionary<string, VoiceJob> Voices = new(StringComparer.OrdinalIgnoreCase);

    private static string Dir
    {
        get
        {
            string dir = Path.Combine(Session.SaveDir, "cache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Voice(string name, string ogv) => Voice(name, ogv, out _);

    /// <summary>Main-thread request: ready audio, or null while plain-file work runs in the background.</summary>
    public static string Voice(string name, string ogv, out bool complete)
    {
        complete = false;
        string file;
        try { file = Path.Combine(Dir, "dd1_" + name + ".ogg"); }
        catch (Exception e)
        {
            complete = true;
            Plugin.Log.LogWarning($"[cinematic] {name} voice: {e.Message}");
            return null;
        }
        if (File.Exists(file)) { complete = true; return file; }
        if (!Voices.TryGetValue(file, out var job))
        {
            Voices[file] = new VoiceJob { Name = name, Work = Task.Run(() => WriteVoice(ogv, file)) };
            return null;
        }
        if (!job.Work.IsCompleted) return null;
        complete = true;
        return Observe(job).File;
    }

    private static VoiceResult Observe(VoiceJob job)
    {
        var result = job.Work.GetAwaiter().GetResult();
        if (!job.Reported)
        {
            job.Reported = true;
            if (result.Error != null) Plugin.Log.LogWarning($"[cinematic] {job.Name} voice: {result.Error}");
        }
        return result;
    }

    // Worker code never reads Session/Application or creates FMOD/Unity objects.
    private static VoiceResult WriteVoice(string ogv, string file)
    {
        string temporary = null;
        var result = new VoiceResult();
        try
        {
            if (!File.Exists(file))
            {
                temporary = file + "." + Guid.NewGuid().ToString("N") + ".part";
                using (var input = File.OpenRead(ogv))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    if (Dd1Cinematic.VorbisAudio(input, output) == 0) throw new InvalidDataException("no Vorbis audio pages");
                try { File.Move(temporary, file); }
                catch (IOException) when (File.Exists(file)) { } // another completed publisher won
            }
            result.File = file;
        }
        catch (Exception e) { result.Error = e.Message; }
        finally
        {
            if (temporary != null && File.Exists(temporary))
                try { File.Delete(temporary); }
                catch (Exception e) { result.Error = (result.Error == null ? "" : result.Error + "; ") + "temporary audio: " + e.Message; }
        }
        return result;
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
        var movies = names.ToArray();
        string directory = Dir;
        foreach (string name in movies) Voice(name, Dd1Cinematic.VideoPath(dd1, name), out _);
        string ffmpeg = FindFfmpeg();
        if (ffmpeg == null) { Plugin.Log.LogInfo("[cinematic] no ffmpeg: cinematics play as narration over DD1's title art"); return; }
        foreach (var n in movies)
            if (!File.Exists(Path.Combine(directory, "dd1_" + n + ".webm")) && !Pending.Contains(n)) Pending.Enqueue(n);
        Pump(dd1, ffmpeg, directory);
    }

    public static void Update()
    {
        while (Completed.TryDequeue(out var finish)) finish();
        foreach (var job in Voices.Values)
            if (job.Work.IsCompleted && !job.Reported) Observe(job);
    }

    private static void Pump(Dd1Install dd1, string ffmpeg, string directory)
    {
        if ((_converting != null && !_converting.HasExited) || Pending.Count == 0) return;
        string name = Pending.Dequeue();
        string src = Dd1Cinematic.VideoPath(dd1, name), part = Path.Combine(directory, "dd1_" + name + ".part.webm"), done = Path.Combine(directory, "dd1_" + name + ".webm");
        if (!File.Exists(src)) { Pump(dd1, ffmpeg, directory); return; }
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
            p.Exited += (_, _) => Completed.Enqueue(() =>
            {
                try
                {
                    if (p.ExitCode == 0 && File.Exists(part)) { if (File.Exists(done)) File.Delete(done); File.Move(part, done); }
                    Plugin.Log.LogInfo($"[cinematic] {name}: picture {(File.Exists(done) ? "ready" : "failed (ffmpeg " + p.ExitCode + ")")}");
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {name}: {e.Message}"); }
                if (ReferenceEquals(_converting, p)) _converting = null;
                p.Dispose();
                Pump(dd1, ffmpeg, directory);
            });
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
