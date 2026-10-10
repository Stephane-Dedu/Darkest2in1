// Input shims for actual Drag/map drawing outside Unity, with group-local pointer coordinates.
// Affine draw recording checks geometry; native GUI buttons, GPU rendering and resource loading need a game check.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
namespace UnityEngine;

public enum EventType { MouseDown, MouseDrag, MouseUp, MouseMove, Repaint, Layout, Used, KeyDown, ScrollWheel }
public enum KeyCode { None, Escape, Space, Return }
public sealed class Event
{
    public static Event current;
    public EventType type, rawType;
    public int button;
    public KeyCode keyCode;
    public Vector2 mousePosition, delta;
    // Verified in DD2's Unity runtime: consuming the event changes rawType as well.
    public void Use() => type = rawType = EventType.Used;
}
public readonly struct Vector2(float x, float y)
{
    public readonly float x = x, y = y;
    public float magnitude => MathF.Sqrt(x * x + y * y);
    public static Vector2 zero => new(0, 0);
    public Vector2 normalized => magnitude > 0 ? new(x / magnitude, y / magnitude) : zero;
    public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
}
public struct Rect(float x, float y, float width, float height)
{
    public float x = x, y = y, width = width, height = height;
    public float xMax => x + width;
    public float yMax => y + height;
    public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y) { }
    public Vector2 position => new(x, y);
    public Vector2 size => new(width, height);
    public Vector2 center => new(x + width / 2, y + height / 2);
    public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
}
public readonly struct Color(float r, float g, float b, float a = 1)
{
    public readonly float r = r, g = g, b = b, a = a;
    public static Color white => new(1, 1, 1, 1);
    public static Color clear => new(0, 0, 0, 0);
    public static Color black => new(0, 0, 0, 1);
}
public static class GUI
{
    private static readonly Stack<(Vector2 Mouse, Vector2 Clip)> Groups = new();
    public static void BeginGroup(Rect area)
    {
        Groups.Push((Event.current.mousePosition, GUIUtility.clipOffset));
        Event.current.mousePosition -= area.position;
        GUIUtility.clipOffset += area.position;
    }
    public static void EndGroup()
    {
        var prior = Groups.Pop();
        Event.current.mousePosition = prior.Mouse;
        GUIUtility.clipOffset = prior.Clip;
    }
    public static Color color;
    public static Matrix4x4 matrix = Matrix4x4.identity;
    public static bool enabled = true;
    public static void DrawTexture(Rect r, Texture2D t) { }
    public static void DrawTexture(Rect r, Texture t) { }
    public static void DrawTexture(Rect r, Texture t, ScaleMode mode) { }
    public static (Rect Rect, Texture Texture, Rect Uv) LastTextureDraw;
    public static Matrix4x4 LastTextureMatrix;
    public static bool ThrowOnTextureDraw;
    public static Action<Rect, Texture, Rect, Matrix4x4, Color> ObserveTextureDraw;
    public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv, bool alpha = true)
    {
        LastTextureDraw = (r, t, uv);
        LastTextureMatrix = matrix;
        ObserveTextureDraw?.Invoke(r, t, uv, matrix, color);
        if (ThrowOnTextureDraw) throw new InvalidOperationException("synthetic draw failure");
    }
}
public static class GUIUtility
{
    public static int hotControl;
    public static Vector2 clipOffset;
    public static Vector2 GUIToScreenPoint(Vector2 point) => point + clipOffset;
}
public static class Time { public static int frameCount; public static float unscaledTime; }
public enum TextureFormat { RGBA32 }
public enum FilterMode { Bilinear, Trilinear }
public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
public enum TextureWrapMode { Clamp }
public enum HideFlags { HideAndDontSave }
public readonly struct Color32(byte r, byte g, byte b, byte a)
{
    public readonly byte r = r, g = g, b = b, a = a;
}
public class Texture { public int width, height; }
public sealed class Texture2D : Texture
{
    private static Texture2D _whiteTexture;
    public static Texture2D whiteTexture => _whiteTexture ??= new(1, 1, TextureFormat.RGBA32, false);
    public static readonly List<int> ApiThreads = new();
    public static int RoomDecodeCount;
    public byte[] Raw;
    public FilterMode filterMode;
    public TextureWrapMode wrapMode;
    public HideFlags hideFlags;
    public string name;
    public int anisoLevel;
    public float mipMapBias;
    public Texture2D(int w, int h, TextureFormat format, bool mipChain)
    { Track(); width = w; height = h; }
    private static void Track() => ApiThreads.Add(Environment.CurrentManagedThreadId);
    // Synthetic atlas pixels, not Unity decoding. Tests validate scheduling, row orientation, caching and hits.
    public bool LoadImage(byte[] data) { Track(); width = height = 2; return true; }
    // Only validates call scheduling and allocation bounds; native Unity PNG decoding remains unverified.
    public bool LoadImage(byte[] data, bool markNonReadable)
    {
        Track(); RoomDecodeCount++;
        width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        return true;
    }
    public Color32[] GetPixels32()
    {
        Track();
        return new[] { new Color32(200, 100, 50, 255), new Color32(100, 200, 50, 255),
            new Color32(50, 100, 200, 255), new Color32(100, 50, 200, 255) };
    }
    public void LoadRawTextureData(byte[] data) { Track(); Raw = data; }
    public void SetPixels32(Color32[] pixels) => Track();
    public void Apply(bool mipMaps, bool makeNoLongerReadable) => Track();
}
public static class Object
{
    public static void Destroy(object value) => Texture2D.ApiThreads.Add(Environment.CurrentManagedThreadId);
    public static void DontDestroyOnLoad(object value) { }
}
