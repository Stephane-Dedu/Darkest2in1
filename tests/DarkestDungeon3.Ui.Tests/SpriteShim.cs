// A command-recording shim, not a GPU rasterizer. Native shader/color/clipping still need an in-game check.
namespace UnityEngine;

public enum SpritePackingMode { Tight, Rectangle }
public enum SpritePackingRotation { None, FlipHorizontal, FlipVertical, Rotate180, Any }
public sealed class Sprite
{
    public string name = "test sprite";
    public Texture2D texture;
    public bool packed;
    public SpritePackingMode packingMode;
    public SpritePackingRotation packingRotation;
    public Vector2[] vertices, uv;
    public ushort[] triangles;
    public float pixelsPerUnit = 100;
    public Rect Region;
    public int RectReads;
    public Rect textureRect
    {
        get
        {
            RectReads++;
            if (packed && packingMode == SpritePackingMode.Tight) throw new InvalidOperationException("Tightly packed sprite");
            return Region;
        }
    }
}
public readonly struct Vector3(float x, float y, float z)
{
    public readonly float x = x, y = y, z = z;
}
// Only the 2D affine operations used by the linked road renderer; this is not a Unity rasterizer.
public struct Matrix4x4
{
    public float m00, m01, m03, m10, m11, m13, m22, m33;
    public static Matrix4x4 identity => new() { m00 = 1, m11 = 1, m22 = 1, m33 = 1 };
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new()
    {
        m00 = a.m00 * b.m00 + a.m01 * b.m10,
        m01 = a.m00 * b.m01 + a.m01 * b.m11,
        m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m03,
        m10 = a.m10 * b.m00 + a.m11 * b.m10,
        m11 = a.m10 * b.m01 + a.m11 * b.m11,
        m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m13,
        m22 = a.m22 * b.m22, m33 = a.m33 * b.m33,
    };
    public Vector3 MultiplyPoint3x4(Vector3 p) => new(m00 * p.x + m01 * p.y + m03,
        m10 * p.x + m11 * p.y + m13, m22 * p.z);
}
public static class Mathf
{
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);
    public static int CeilToInt(float a) => (int)MathF.Ceiling(a);
    public static float Clamp01(float x) => Math.Clamp(x, 0, 1);
    public static float Clamp(float x, float min, float max) => Math.Clamp(x, min, max);
    public static float SmoothStep(float from, float to, float x) => from + (to-from)*x*x*(3-2*x);
}
public sealed class Shader
{
    public bool isSupported = true;
    public static Shader Find(string name) => new();
}
public sealed class Material(Shader shader)
{
    public HideFlags hideFlags;
    public Texture mainTexture;
    public bool HasProperty(string name) => true;
    public void SetColor(string name, Color color) { }
    public bool SetPass(int pass) => true;
}
public sealed class Mesh
{
    public HideFlags hideFlags;
    public Vector3[] vertices;
    public Vector2[] uv;
    public int[] triangles;
}
public enum RenderTextureFormat { ARGB32 }
public enum RenderTextureReadWrite { sRGB }
public sealed class RenderTexture : Texture
{
    public static RenderTexture active;
    public static readonly List<RenderTexture> Created = new();
    public HideFlags hideFlags;
    public FilterMode filterMode;
    public TextureWrapMode wrapMode;
    private bool _created;
    public RenderTexture(int w, int h, int depth, RenderTextureFormat format, RenderTextureReadWrite rw)
    { width = w; height = h; Created.Add(this); }
    public RenderTexture(int w, int h, int depth) : this(w, h, depth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { }
    public bool Create() { _created = true; return true; }
    public bool IsCreated() => _created;
    public void Release() => _created = false;
}
public static class GL
{
    public static bool sRGBWrite;
    public static int MatrixDepth;
    public static void Clear(bool depth, bool color, Color value) { }
    public static void PushMatrix() => MatrixDepth++;
    public static void PopMatrix() => MatrixDepth--;
    public static void LoadPixelMatrix(float left, float right, float bottom, float top) { }
}
public static class Graphics
{
    public static Mesh LastMesh;
    public static int Draws;
    public static bool FailDraw;
    public static void DrawMeshNow(Mesh mesh, Matrix4x4 transform)
    {
        Draws++;
        if (FailDraw) throw new InvalidOperationException("Synthetic GPU failure");
        LastMesh = mesh;
    }
}
public enum ColorSpace { Gamma, Linear }
public static class QualitySettings { public static ColorSpace activeColorSpace = ColorSpace.Linear; }
