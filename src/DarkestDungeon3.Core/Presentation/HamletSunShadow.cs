using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Immutable, optional world-space sunlight visibility. No Unity/GPU state.</summary>
public sealed class HamletSunShadow
{
    private readonly byte[] _gray;
    private readonly float _minX, _maxX, _minZ, _maxZ;
    public readonly int Size;
    public readonly string Identity;
    private static readonly float[] Linear = Table(false), Gamma = Table(true);

    public HamletSunShadow(byte[] gray, int size, float minX, float maxX, float minZ, float maxZ, string source = "")
    {
        if (size < 2 || size > 4096 || gray == null || gray.Length != size * size)
            throw new ArgumentException("A shadow field must contain a complete bounded square of bytes.");
        if (!Finite(minX) || !Finite(maxX) || !Finite(minZ) || !Finite(maxZ) || minX >= maxX || minZ >= maxZ)
            throw new ArgumentException("Invalid shadow world bounds.");
        if (gray.Any(v => v < 89)) throw new ArgumentException("Shadow visibility must retain ambient light.");
        _gray = (byte[])gray.Clone(); Size = size;
        _minX = minX; _maxX = maxX; _minZ = minZ; _maxZ = maxZ;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(size); writer.Write(minX); writer.Write(maxX); writer.Write(minZ); writer.Write(maxZ);
            Text(writer, source); writer.Write(_gray);
        }
        Identity = Hash(stream.ToArray());
    }

    public float At(float x, float z)
    {
        if (!Finite(x) || !Finite(z) || x < _minX || x > _maxX || z < _minZ || z > _maxZ) return 1;
        float fx = Math.Max(0, Math.Min(Size - 1, (x - _minX) / (_maxX - _minX) * Size - .5f));
        float fy = Math.Max(0, Math.Min(Size - 1, (z - _minZ) / (_maxZ - _minZ) * Size - .5f));
        int ix = (int)fx, iy = (int)fy, nx = Math.Min(Size - 1, ix + 1), ny = Math.Min(Size - 1, iy + 1);
        float Lerp(float a, float b, float t) => a + (b - a) * t;
        return Lerp(Lerp(_gray[iy * Size + ix], _gray[iy * Size + nx], fx - ix),
            Lerp(_gray[ny * Size + ix], _gray[ny * Size + nx], fx - ix), fy - iy) / 255f;
    }

    /// <summary>Shade only daylight in linear space; callers add local lamp light afterwards.</summary>
    public static float Daylight(float encoded, float visibility)
    {
        if (visibility >= 1) return encoded; // Preserve the exact existing paint for white/no mask.
        int i = (int)(Math.Max(0, Math.Min(1, encoded)) * 8191 + .5f);
        float linear = Linear[i] * Math.Max(0, Math.Min(1, visibility));
        return Math.Min(encoded, Gamma[(int)(linear * 8191 + .5f)]);
    }

    // Canonical little-endian arrays and site transforms; also emitted by the offline baker.
    public static string SceneDigest(HamletTerrain terrain) => Hash(SceneReference(terrain));

    public static byte[] SceneReference(HamletTerrain terrain)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            Text(writer, "DD3-shadow-scene-v1");
            foreach (float v in new[] { HamletLayout.MinX, HamletLayout.MaxX, HamletLayout.MinZ, HamletLayout.MaxZ }) writer.Write(v);
            writer.Write(HamletLayout.Sites.Count);
            foreach (var s in HamletLayout.Sites)
            {
                Text(writer, s.Id);
                foreach (float v in new[] { s.X, s.Y, s.Z, s.Width, s.Yaw, s.FootprintWidth, s.FootprintDepth, s.GroundBlend }) writer.Write(v);
            }
            void Floats(float[] values) { writer.Write(values.Length); foreach (float v in values) writer.Write(v); }
            void Ints(int[] values) { writer.Write(values.Length); foreach (int v in values) writer.Write(v); }
            Floats(terrain.Positions); Ints(terrain.Ground); Ints(terrain.Rock);
            foreach (var part in new[] { terrain.Paving, terrain.Masonry, terrain.Stairs })
            { Floats(part.Positions); Ints(part.Indices); }
        }
        return stream.ToArray();
    }

    private static bool EquivalentScene(byte[] expected, byte[] actual, out float maximumError, out string reason)
    {
        // Mono and the offline CLR can round procedural vertices differently.
        // Compare the complete topology/site contract, with a 0.1mm allowance
        // for floats rather than quantizing hashes near rounding boundaries.
        float maximum = 0; string detail = "geometry structure";
        using var first = new BinaryReader(new MemoryStream(expected));
        using var second = new BinaryReader(new MemoryStream(actual));
        bool FloatValue()
        {
            float a = first.ReadSingle(), b = second.ReadSingle();
            if (!Finite(a) || !Finite(b)) return false;
            maximum = Math.Max(maximum, Math.Abs(a - b));
            if (Math.Abs(a - b) <= .0001f) return true;
            detail = "vertex/site difference " + maximum.ToString("G5"); return false;
        }
        bool TextValue()
        {
            int n = first.ReadInt32();
            if (n < 0 || n > 4096 || n != second.ReadInt32()) return false;
            return first.ReadBytes(n).SequenceEqual(second.ReadBytes(n));
        }
        bool Floats()
        {
            int count = first.ReadInt32(), other = second.ReadInt32();
            if (count < 0 || count != other) { detail = "vertex array length " + count + "/" + other; return false; }
            for (int i = 0; i < count; i++) if (!FloatValue()) return false;
            return true;
        }
        bool Ints()
        {
            int count = first.ReadInt32(), other = second.ReadInt32();
            if (count < 0 || count != other) { detail = "index array length " + count + "/" + other; return false; }
            for (int i = 0; i < count; i++) if (first.ReadInt32() != second.ReadInt32())
                { detail = "triangle topology"; return false; }
            return true;
        }
        bool Compare()
        {
            if (!TextValue()) return false;
            for (int i = 0; i < 4; i++) if (!FloatValue()) return false;
            int sites = first.ReadInt32();if (sites != second.ReadInt32() || sites != HamletLayout.Sites.Count) return false;
            for (int i = 0; i < sites; i++)
            {
                if (!TextValue()) return false;
                for (int j = 0; j < 8; j++) if (!FloatValue()) return false;
            }
            if (!Floats() || !Ints() || !Ints()) return false;
            for (int i = 0; i < 3; i++) if (!Floats() || !Ints()) return false;
            return first.BaseStream.Position == expected.Length && second.BaseStream.Position == actual.Length;
        }
        bool result = expected.Length == actual.Length && Compare();
        maximumError = maximum; reason = detail;return result;
    }

    public static HamletSunShadow ReadOptional(string folder, HamletTerrain terrain,
        IReadOnlyDictionary<string, string> models, out string status)
    {
        string path = Path.Combine(folder, "manifest.json");
        status = "no optional field";
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > 131072) throw new FormatException("oversized manifest");
            var manifest = JObject.Parse(File.ReadAllText(path));
            int size = (int?)manifest["size"] ?? 0;
            if ((int?)manifest["version"] != 1 || size < 64 || size > 4096) throw new FormatException("unsupported field");
            var actualScene = SceneReference(terrain);
            string scene = (string)manifest["scene_sha256"];
            float rounding = 0;
            byte[] reference = null;
            if (manifest["reference_file"] != null)
            {
                if ((string)manifest["reference_file"] != "scene-reference.bin") throw new FormatException("unexpected scene reference");
                string referencePath = Path.Combine(folder, "scene-reference.bin");
                if (new FileInfo(referencePath).Length != actualScene.Length) throw new FormatException("stale terrain topology");
                reference = File.ReadAllBytes(referencePath);
                if (Hash(reference) != scene) throw new FormatException("damaged scene reference");
            }
            if (Hash(actualScene) != scene)
            {
                if (reference == null) throw new FormatException("stale terrain/layout");
                if (!EquivalentScene(reference, actualScene, out rounding, out var reason)) throw new FormatException("stale terrain/layout: " + reason);
            }
            var sources = manifest["source_glb_hashes"] as JObject;
            if (sources == null || sources.Count != HamletLayout.Sites.Count) throw new FormatException("incomplete model sources");
            var source = new StringBuilder(scene);
            foreach (var site in HamletLayout.Sites)
            {
                if (!models.TryGetValue(site.Id, out var hash) || hash == null || hash.Length != 64
                    || (string)sources[site.Id] != hash) throw new FormatException("stale model " + site.Id);
                source.Append(site.Id).Append(hash);
            }
            var b = manifest["bounds"] as JObject;
            if (b == null || (float?)b["MinX"] != HamletLayout.MinX || (float?)b["MaxX"] != HamletLayout.MaxX
                || (float?)b["MinZ"] != HamletLayout.MinZ || (float?)b["MaxZ"] != HamletLayout.MaxZ)
                throw new FormatException("stale bounds");
            if ((string)manifest["raw_file"] != "ground-shadow.gray") throw new FormatException("unexpected data file");
            string dataPath = Path.Combine(folder, "ground-shadow.gray");
            if (new FileInfo(dataPath).Length != size * size) throw new FormatException("incomplete field data");
            var bytes = File.ReadAllBytes(dataPath);
            if (Hash(bytes) != (string)manifest["raw_sha256"]) throw new FormatException("damaged field data");
            var field = new HamletSunShadow(bytes, size, HamletLayout.MinX, HamletLayout.MaxX,
                HamletLayout.MinZ, HamletLayout.MaxZ, source.ToString());
            status = size + "px field " + field.Identity.Substring(0, 12) + "; scene float error " + rounding.ToString("G4") + "m";
            return field;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
            || ex is FormatException || ex is Newtonsoft.Json.JsonException || ex is OverflowException || ex is InvalidCastException)
        {
            status = "ignored optional field: " + ex.Message;
            return null;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    private static void Text(BinaryWriter writer, string text)
    { var bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
    private static float[] Table(bool gamma)
    {
        var result = new float[8192];
        for (int i = 0; i < result.Length; i++)
        {
            double v = i / 8191d;
            result[i] = (float)(gamma ? v <= .0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - .055
                : v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4));
        }
        return result;
    }
}
