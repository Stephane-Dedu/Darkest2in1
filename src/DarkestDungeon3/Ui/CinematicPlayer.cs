using System.Collections.Generic;
using System.IO;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1's cinematics full screen (video/*.ogv): the narration through FMOD (DD2 plays its sound through FMOD; the .ogv's
/// Vorbis track is copied out to a plain .ogg once) with DD1's subtitles, the picture through Unity's VideoPlayer. A
/// built Unity game can't play Theora from a file, so the picture is a VP8 .webm made once with the user's own ffmpeg
/// (in the background, from the session start); without it the narration plays over DD1's title art. A click, Space,
/// Enter or Escape skips to the next one.
/// </summary>
internal static class CinematicPlayer
{
    private static readonly Queue<string> Queue = new();
    private static GameObject _host;
    private static VideoPlayer _video;
    private static RenderTexture _picture;
    private static Dd1Audio.Stream _voice;
    private static List<(int StartMs, int EndMs, string Text)> _subtitles = new();
    private static string _playing;
    private static float _startedAt;
    private static Texture2D _titleArt;
    private static string _pendingVoiceSource;

    public static bool Active => _playing != null || Queue.Count > 0;

    public static void Play(params string[] names)
    {
        foreach (var n in names) Queue.Enqueue(n);
        if (_playing == null) Next();
    }

    /// <summary>Draw the cinematic over everything; true while one is on (the rest of the UI waits).</summary>
    public static bool Draw()
    {
        if (!Active) return false;
        var e = Event.current;
        bool skip = (e.type == EventType.MouseDown) ||
                    (e.type == EventType.KeyDown && e.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.Escape);
        if (skip) { e.Use(); Plugin.Log.LogInfo($"[cinematic] {_playing}: skipped"); Next(); return true; }
        if (_pendingVoiceSource != null) TryStartPlayback(_playing, _pendingVoiceSource, Session.Current.Dd1);
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
        if (_video == null && _titleArt != null) GUI.DrawTexture(new Rect(0, 0, Gui.W, Gui.H), _titleArt, ScaleMode.ScaleAndCrop);
        if (_picture != null && _video != null && _video.isPlaying && Event.current.type == EventType.Repaint)
        {
            // Letterboxed at the video's own aspect.
            float aspect = _video.width > 0 && _video.height > 0 ? (float)_video.width / _video.height : 16f / 9f;
            float w = Gui.W, h = w / aspect;
            if (h > Gui.H) { h = Gui.H; w = h * aspect; }
            GUI.DrawTexture(new Rect((Gui.W - w) / 2, (Gui.H - h) / 2, w, h), _picture);
        }
        double now = _pendingVoiceSource != null ? -1 : _video != null && _video.isPlaying ? _video.time : _video == null ? Time.unscaledTime - _startedAt : -1;
        if (_video == null && _voice != null && !_voice.IsPlaying && now > 1) { Next(); return true; }   // narration over the title art: done
        if (now >= 0 && Dd1Cinematic.SubtitleAt(_subtitles, now) is { } line && line.Length > 0)
            Gui.Text(new Rect(160, Gui.H - 150, Gui.W - 320, 80), line, 34, new Color(0.93f, 0.88f, 0.76f), TextAnchor.MiddleCenter);
        return true;
    }

    private static void Next()
    {
        Stop();
        while (Queue.Count > 0)
        {
            string name = Queue.Dequeue();
            var dd1 = Session.Current?.Dd1;
            string path = dd1 != null ? Dd1Cinematic.VideoPath(dd1, name) : null;
            if (path == null || !File.Exists(path)) { Plugin.Log.LogWarning($"[cinematic] {name}: no video"); continue; }
            Start(name, path, dd1);
            return;
        }
        Dd1Audio.Hush = false;
    }

    private static void Start(string name, string path, Dd1Install dd1)
    {
        _playing = name;
        Dd1Audio.Hush = true;   // the Hamlet's music and ambience wait
        _pendingVoiceSource = path;
        _titleArt ??= Art.Dd1("fe_flow", "title_bg.png");
        TryStartPlayback(name, path, dd1);
    }

    private static void TryStartPlayback(string name, string path, Dd1Install dd1)
    {
        string voice = CinematicCache.Voice(name, path, out bool complete);
        if (!complete) return;
        _pendingVoiceSource = null;
        _subtitles = Dd1Cinematic.Subtitles(dd1, name);
        string webm = CinematicCache.Picture(name);
        if (webm == null)
        {
            // No playable picture (yet): DD1's narration and subtitles over its title art.
            _titleArt ??= Art.Dd1("fe_flow", "title_bg.png");
            _voice = Dd1Audio.PlayStream(voice);
            _startedAt = Time.unscaledTime;
            Plugin.Log.LogInfo($"[cinematic] {name}: no picture yet, narration over the title art (voice {(_voice != null ? "on" : "off")})");
            if (_voice == null) Next();
            return;
        }
        if (_host == null) { _host = new GameObject("DD3Cinematic"); Object.DontDestroyOnLoad(_host); }
        _picture = new RenderTexture(1920, 1080, 0);
        _video = _host.AddComponent<VideoPlayer>();
        _video.playOnAwake = false;
        _video.source = VideoSource.Url;
        _video.url = webm;
        _video.renderMode = VideoRenderMode.RenderTexture;
        _video.targetTexture = _picture;
        _video.audioOutputMode = VideoAudioOutputMode.None;
        _video.isLooping = false;
        var player = _video;   // callbacks from a player already replaced (skipped) are ignored
        _video.errorReceived += (vp, msg) => { if (vp != player || _video != player) return; Plugin.Log.LogWarning($"[cinematic] {name}: {msg}"); Next(); };
        _video.loopPointReached += vp => { if (vp != player || _video != player) return; Plugin.Log.LogInfo($"[cinematic] {name}: ended"); Next(); };
        _video.prepareCompleted += vp =>
        {
            if (vp != player || _video != player) return;
            vp.Play();
            _voice = Dd1Audio.PlayStream(voice);
            Plugin.Log.LogInfo($"[cinematic] {name}: {vp.width}x{vp.height}, {vp.length:0.0} s, {_subtitles.Count} subtitles, voice {(_voice != null ? "on" : "off")}");
        };
        _video.Prepare();
    }

    private static void Stop()
    {
        _voice?.Stop();
        _voice = null;
        if (_video != null) { _video.Stop(); Object.Destroy(_video); _video = null; }
        if (_picture != null) { _picture.Release(); Object.Destroy(_picture); _picture = null; }
        _playing = null;
        _pendingVoiceSource = null;
    }
}
