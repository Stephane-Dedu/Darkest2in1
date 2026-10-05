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
    private sealed class Conversion { public string Name, Source, Output, Temporary, Encoder; }
    private static Conversion _active;
    private static readonly Queue<Conversion> Pending = new();
    private static readonly HashSet<string> ConversionFiles = new(StringComparer.OrdinalIgnoreCase);
    internal static bool HasPendingPictures => _active != null || Pending.Count > 0;
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
        bool busy = _active != null && string.Equals(_active.Output, file, StringComparison.OrdinalIgnoreCase);
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
        PreparePictures(dd1, movies, ffmpeg, directory);
    }

    internal static void PreparePictures(Dd1Install dd1, IEnumerable<string> names, string encoder, string directory)
    {
        foreach (string name in names)
        {
            string output = Path.Combine(directory, "dd1_" + name + ".webm");
            if (File.Exists(output) || !ConversionFiles.Add(output)) continue;
            Pending.Enqueue(new Conversion
            {
                Name = name, Source = Dd1Cinematic.VideoPath(dd1, name), Output = output, Encoder = encoder,
                Temporary = output + "." + Guid.NewGuid().ToString("N") + ".part.webm",
            });
        }
        Pump();
    }

    public static void Update()
    {
        while (Completed.TryDequeue(out var finish)) finish();
        foreach (var job in Voices.Values)
            if (job.Work.IsCompleted && !job.Reported) Observe(job);
    }

    private static void Pump()
    {
        if (_active != null) return;
        while (Pending.Count > 0)
        {
            var job = Pending.Dequeue();
            if (!File.Exists(job.Source)) { ConversionFiles.Remove(job.Output); continue; }
            Process process = null;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo(job.Encoder, $"-y -loglevel error -i \"{job.Source}\" -an -c:v libvpx -b:v 3M -deadline realtime -cpu-used 8 \"{job.Temporary}\"")
                    {
                        UseShellExecute = false, CreateNoWindow = true,
                    },
                    EnableRaisingEvents = true,
                };
                var started = process;
                process.Exited += (_, _) => Completed.Enqueue(() => Finish(job, started));
                _active = job;
                _converting = process;
                if (!process.Start()) throw new InvalidOperationException("encoder did not start");
                Plugin.Log.LogInfo($"[cinematic] {job.Name}: converting the picture for Unity (ffmpeg, in the background)");
                return;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[cinematic] {job.Name}: ffmpeg failed to start: {e.Message}");
                _active = null;
                _converting = null;
                ConversionFiles.Remove(job.Output);
                process?.Dispose();
            }
        }
    }

    private static void Finish(Conversion job, Process process)
    {
        if (!ReferenceEquals(_active, job) || !ReferenceEquals(_converting, process)) { process.Dispose(); return; }
        try
        {
            if (process.ExitCode == 0 && File.Exists(job.Temporary) && !File.Exists(job.Output))
                File.Move(job.Temporary, job.Output);
            Plugin.Log.LogInfo($"[cinematic] {job.Name}: picture {(File.Exists(job.Output) ? "ready" : "failed (ffmpeg " + process.ExitCode + ")")}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {job.Name}: {e.Message}"); }
        finally
        {
            if (File.Exists(job.Temporary))
                try { File.Delete(job.Temporary); } catch (Exception e) { Plugin.Log.LogWarning($"[cinematic] {job.Name} temporary picture: {e.Message}"); }
            _active = null;
            _converting = null;
            ConversionFiles.Remove(job.Output);
            process.Dispose();
            Pump();
        }
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
