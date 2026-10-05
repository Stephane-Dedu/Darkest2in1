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
public readonly struct Matrix4x4 { public static Matrix4x4 identity => default; }
public static class Mathf
{
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);
    public static int CeilToInt(float a) => (int)MathF.Ceiling(a);
    public static float Clamp01(float x) => Math.Clamp(x, 0, 1);
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
