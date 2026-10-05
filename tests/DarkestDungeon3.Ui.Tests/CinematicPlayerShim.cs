using UnityEngine;

namespace UnityEngine
{
    public enum ScaleMode { ScaleAndCrop }
    public sealed class GameObject(string name)
    {
        public T AddComponent<T>() where T : new() => new T();
    }
}

namespace UnityEngine.Video
{
    public enum VideoSource { Url }
    public enum VideoRenderMode { RenderTexture }
    public enum VideoAudioOutputMode { None }
    public sealed class VideoPlayer
    {
        public static readonly List<VideoPlayer> Created = new();
        public VideoPlayer() => Created.Add(this);
        public bool playOnAwake, isLooping, isPlaying;
        public VideoSource source;
        public string url;
        public VideoRenderMode renderMode;
        public RenderTexture targetTexture;
        public VideoAudioOutputMode audioOutputMode;
        public uint width = 1920, height = 1080;
        public double length = 5, time;
        public event Action<VideoPlayer, string> errorReceived;
        public event Action<VideoPlayer> loopPointReached, prepareCompleted;
        public void Prepare() => prepareCompleted?.Invoke(this);
        public void Play() => isPlaying = true;
        public void Stop() => isPlaying = false;
    }
}

namespace DarkestDungeon3.Runtime
{
    internal static class Art { public static Texture2D Dd1(params string[] parts) => null; }
    internal static class Dd1Audio
    {
        public static bool Hush;
        public static readonly List<int> StartThreads = new();
        public static Stream Last;
        public sealed class Stream
        {
            public bool IsPlaying = true;
            public void Stop() => IsPlaying = false;
        }
        public static Stream PlayStream(string file)
        {
            StartThreads.Add(Environment.CurrentManagedThreadId);
            return Last = file != null && File.Exists(file) ? new Stream() : null;
        }
    }
}

namespace DarkestDungeon3.Ui
{
    internal static class Gui
    {
        public const float W = 1920, H = 1080;
        public static readonly List<string> Texts = new();
        public static void Fill(Rect rect, Color colour) { }
        public static void Text(Rect rect, string text, float size, Color colour, TextAnchor align) => Texts.Add(text);
    }
}
