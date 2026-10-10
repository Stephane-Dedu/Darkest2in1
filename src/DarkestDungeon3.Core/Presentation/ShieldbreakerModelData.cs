using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Private, locally converted model data. Contains no campaign state or Unity objects.</summary>
public sealed class ShieldbreakerModelData
{
    public const int MaxBytes = 32 * 1024 * 1024;
    public int Version;
    public string Donor;
    public string[] Bones;
    public float[][] Bindposes, Vertices, Normals, Uv, Weights;
    public int[][] Indices;
    public int[] Triangles;
    public Dictionary<string, ModelClip> Clips;
    public ModelPart[] Parts;
    [JsonIgnore] public float BodyHeight { get; private set; }

    public static ShieldbreakerModelData Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxBytes) throw new FormatException("Missing or oversized Shieldbreaker model");
        var model = JsonConvert.DeserializeObject<ShieldbreakerModelData>(File.ReadAllText(path),
            new JsonSerializerSettings { MaxDepth = 16 });
        if (model == null) throw new FormatException("Empty Shieldbreaker model");
        model.Validate();
        return model;
    }

    public void Validate()
    {
        bool Rows(float[][] a, int count, int width) => a != null && a.Length == count
            && a.All(r => r != null && r.Length == width && r.All(Finite));
        int n = Vertices?.Length ?? 0, b = Bones?.Length ?? 0;
        if (Version != 1 || Donor != "hellion" || n < 3 || n > 150000 || b < 1 || b > 256
            || Bones.Any(string.IsNullOrEmpty) || Bones.Distinct(StringComparer.Ordinal).Count() != b
            || !Rows(Vertices, n, 3) || !Rows(Normals, n, 3) || !Rows(Uv, n, 2)
            || !Rows(Bindposes, b, 16) || !Rows(Weights, n, 4)
            || Indices == null || Indices.Length != n
            || Indices.Any(r => r == null || r.Length != 4 || r.Any(i => i < 0 || i >= b))
            || Weights.Any(r => r.Any(w => w < 0 || w > 1) || Math.Abs(r.Sum() - 1) > .002f)
            || Triangles == null || Triangles.Length < 3 || Triangles.Length > 450000 || Triangles.Length % 3 != 0
            || Triangles.Any(i => i < 0 || i >= n))
            throw new FormatException("Invalid Shieldbreaker geometry or skin weights");
        if (Clips == null || Clips.Count > 16 || new[] { "idle", "attack", "defend" }.Any(k => !Clips.ContainsKey(k)))
            throw new FormatException("Shieldbreaker idle, attack and defend clips are required");
        foreach (var clip in Clips.Values) clip?.Validate();
        if (Clips.Values.Any(c => c == null)) throw new FormatException("Null Shieldbreaker clip");
        if (Parts == null || Parts.Length < 1 || Parts.Length > 512) throw new FormatException("Missing Shieldbreaker mesh parts");
        int end = 0;
        var body = new List<float>();
        foreach (var part in Parts)
        {
            if (part == null || string.IsNullOrEmpty(part.Name) || part.Start != end || part.Count < 0 || part.Count > n - end)
                throw new FormatException("Invalid Shieldbreaker mesh part range");
            if (!part.Name.StartsWith("spear_", StringComparison.Ordinal) && !part.Name.StartsWith("shield_", StringComparison.Ordinal))
                for (int i = part.Start; i < part.Start + part.Count; i++) body.Add(Vertices[i][1]);
            end += part.Count;
        }
        if (end != n || body.Count == 0) throw new FormatException("Incomplete Shieldbreaker mesh parts");
        BodyHeight = body.Max() - body.Min();
    }

    internal static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    public static bool ValidTexturePng(byte[] d)
    {
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (d == null || d.Length < 33 || d.Length > 16 * 1024 * 1024) return false;
        for (int i = 0; i < 8; i++) if (d[i] != signature[i]) return false;
        if (d[8] != 0 || d[9] != 0 || d[10] != 0 || d[11] != 13
            || d[12] != 'I' || d[13] != 'H' || d[14] != 'D' || d[15] != 'R') return false;
        uint w = (uint)d[16] << 24 | (uint)d[17] << 16 | (uint)d[18] << 8 | d[19];
        uint h = (uint)d[20] << 24 | (uint)d[21] << 16 | (uint)d[22] << 8 | d[23];
        return w == 4096 && h == 2048 && d[24] == 8 && (d[25] == 2 || d[25] == 6);
    }
}

public sealed class ModelPart
{
    public string Name;
    public int Start, Count;
}

public sealed class ModelClip
{
    public float Duration;
    public bool Loop;
    public ModelPoseKey[] Keys;

    public void Validate()
    {
        if (!ShieldbreakerModelData.Finite(Duration) || Duration <= 0 || Duration > 30 || Keys == null
            || Keys.Length < 2 || Keys.Length > 256 || Keys.Any(k => k == null)
            || Keys[0].T != 0 || Math.Abs(Keys[Keys.Length - 1].T - Duration) > .0001f)
            throw new FormatException("Invalid Shieldbreaker clip timing");
        float previous = -1;
        foreach (var k in Keys)
        {
            bool Vec(float[] a) => a != null && a.Length == 3 && a.All(ShieldbreakerModelData.Finite);
            if (!ShieldbreakerModelData.Finite(k.T) || k.T <= previous || k.T > Duration
                || !Vec(k.Right) || !Vec(k.Left) || !Vec(k.Spear) || k.Spear.Sum(x => x * x) < .0001f)
                throw new FormatException("Invalid Shieldbreaker pose key");
            previous = k.T;
        }
        if (Loop && (!Keys[0].Right.SequenceEqual(Keys[Keys.Length - 1].Right)
            || !Keys[0].Left.SequenceEqual(Keys[Keys.Length - 1].Left)
            || !Keys[0].Spear.SequenceEqual(Keys[Keys.Length - 1].Spear)))
            throw new FormatException("Looping Shieldbreaker clip must close without a jump");
    }

    public ModelPose Sample(float elapsed)
    {
        float t = ShieldbreakerModelData.Finite(elapsed) ? Math.Max(0, elapsed) : 0;
        t = Loop ? t % Duration : Math.Min(t, Duration);
        if (t >= Duration)
        {
            var last = Keys[Keys.Length - 1];
            return new ModelPose(ModelPoint.Lerp(last.Right, last.Right, 0), ModelPoint.Lerp(last.Left, last.Left, 0), ModelPoint.Lerp(last.Spear, last.Spear, 0));
        }
        int next = 1;
        while (next < Keys.Length - 1 && Keys[next].T < t) next++;
        var a = Keys[next - 1]; var b = Keys[next];
        float u = (t - a.T) / (b.T - a.T);
        u = u * u * (3 - 2 * u);
        return new ModelPose(ModelPoint.Lerp(a.Right, b.Right, u), ModelPoint.Lerp(a.Left, b.Left, u), ModelPoint.Lerp(a.Spear, b.Spear, u));
    }
}

public sealed class ModelPoseKey
{
    public float T;
    public float[] Right, Left, Spear;
}

public readonly struct ModelPoint
{
    public readonly float X, Y, Z;
    public ModelPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
    internal static ModelPoint Lerp(float[] a, float[] b, float t) => new(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t);
}

public readonly struct ModelPose
{
    public readonly ModelPoint Right, Left, Spear;
    public ModelPose(ModelPoint right, ModelPoint left, ModelPoint spear) { Right = right; Left = left; Spear = spear; }
}
