using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>
/// A skinned glTF 2.0 model (.gltf with external .bin and PNG images), read into plain arrays in Unity's
/// coordinates: glTF is right-handed and Unity left-handed, so X is mirrored (positions, normals, rotations,
/// bind matrices), triangles are rewound, and V is flipped (glTF's texture origin is the top left).
/// Used to try exported characters from other games in DD2's fights; the files stay private.
/// </summary>
public sealed class GltfModel
{
    public const long MaxBytes = 256L * 1024 * 1024;

    public sealed class Node
    {
        public string Name;
        public int Parent = -1;
        public float[] Translation = { 0, 0, 0 }, Rotation = { 0, 0, 0, 1 }, Scale = { 1, 1, 1 };
    }

    public sealed class Primitive
    {
        public string Mesh;
        public int Material = -1, Node = -1;
        public float[] Positions, Normals, Uv, Weights;
        public int[] Joints, Triangles;
        public int VertexCount => Positions.Length / 3;
    }

    public sealed class MaterialInfo
    {
        public string Name, BaseColor, Emissive;
        public bool Mask, DoubleSided;
        public float Cutoff = 0.5f;
    }

    public string Directory { get; private set; }
    public List<Node> Nodes { get; } = new();
    public List<Primitive> Primitives { get; } = new();
    public List<MaterialInfo> Materials { get; } = new();
    /// <summary>The skin's joints (node indices) and their inverse bind matrices (16 floats, column-major).</summary>
    public int[] Joints { get; private set; } = new int[0];
    public float[][] InverseBind { get; private set; } = new float[0][];

    public int TriangleCount => Primitives.Sum(p => p.Triangles.Length / 3);

    public static GltfModel Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxBytes) throw new FileNotFoundException("Missing or oversized glTF", path);
        var gltf = JObject.Parse(File.ReadAllText(path));
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        var model = new GltfModel { Directory = dir };

        var buffers = new List<byte[]>();
        foreach (var b in (JArray)gltf["buffers"] ?? new JArray())
        {
            string uri = (string)b["uri"];
            if (uri == null || uri.StartsWith("data:", StringComparison.Ordinal)) throw new FormatException("Only external .bin buffers are read");
            buffers.Add(File.ReadAllBytes(Inside(dir, uri)));
        }
        var views = ((JArray)gltf["bufferViews"] ?? new JArray()).Cast<JObject>().ToList();
        var accessors = ((JArray)gltf["accessors"] ?? new JArray()).Cast<JObject>().ToList();

        float[] Floats(int accessor) => ReadFloats(accessors[accessor], views, buffers);
        int[] Ints(int accessor) => ReadInts(accessors[accessor], views, buffers);

        var nodes = ((JArray)gltf["nodes"] ?? new JArray()).Cast<JObject>().ToList();
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var node = new Node { Name = (string)n["name"] ?? "node" + i };
            if (n["translation"] is JArray t) node.Translation = new[] { -(float)t[0], (float)t[1], (float)t[2] };
            if (n["rotation"] is JArray r) node.Rotation = new[] { (float)r[0], -(float)r[1], -(float)r[2], (float)r[3] };
            if (n["scale"] is JArray s) node.Scale = new[] { (float)s[0], (float)s[1], (float)s[2] };
            if (n["matrix"] != null) throw new FormatException("Node matrices are not read; export TRS");
            model.Nodes.Add(node);
        }
        for (int i = 0; i < nodes.Count; i++)
            foreach (var child in (JArray)nodes[i]["children"] ?? new JArray())
                model.Nodes[(int)child].Parent = i;

        if (gltf["skins"] is JArray skins && skins.Count > 0)
        {
            var skin = (JObject)skins[0];
            model.Joints = ((JArray)skin["joints"]).Select(j => (int)j).ToArray();
            var ibm = skin["inverseBindMatrices"] != null ? Floats((int)skin["inverseBindMatrices"]) : null;
            model.InverseBind = new float[model.Joints.Length][];
            for (int j = 0; j < model.Joints.Length; j++)
            {
                var m = ibm != null ? ibm.Skip(j * 16).Take(16).ToArray() : Identity();
                model.InverseBind[j] = Mirror(m);
            }
        }

        var images = ((JArray)gltf["images"] ?? new JArray()).Select(i => (string)i["uri"]).ToList();
        var textures = ((JArray)gltf["textures"] ?? new JArray()).Select(t => t["source"] != null ? (int)t["source"] : -1).ToList();
        string Image(JToken texture)
        {
            if (texture?["index"] == null) return null;
            int t = (int)texture["index"];
            int source = t >= 0 && t < textures.Count ? textures[t] : -1;
            return source >= 0 && source < images.Count && images[source] != null ? Inside(dir, images[source]) : null;
        }
        foreach (var m in ((JArray)gltf["materials"] ?? new JArray()).Cast<JObject>())
            model.Materials.Add(new MaterialInfo
            {
                Name = (string)m["name"],
                BaseColor = Image(m["pbrMetallicRoughness"]?["baseColorTexture"]),
                Emissive = Image(m["emissiveTexture"]),
                Mask = (string)m["alphaMode"] == "MASK",
                Cutoff = m["alphaCutoff"] != null ? (float)m["alphaCutoff"] : 0.5f,
                DoubleSided = m["doubleSided"] != null && (bool)m["doubleSided"],
            });

        var meshNodes = new Dictionary<int, int>();
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i]["mesh"] != null) meshNodes[(int)nodes[i]["mesh"]] = i;
        var meshes = ((JArray)gltf["meshes"] ?? new JArray()).Cast<JObject>().ToList();
        for (int mi = 0; mi < meshes.Count; mi++)
            foreach (var p in ((JArray)meshes[mi]["primitives"]).Cast<JObject>())
            {
                if (p["mode"] != null && (int)p["mode"] != 4) continue;   // triangles only
                var a = (JObject)p["attributes"];
                if (a["POSITION"] == null || p["indices"] == null) continue;
                var prim = new Primitive
                {
                    Mesh = (string)meshes[mi]["name"] ?? "mesh" + mi,
                    Material = p["material"] != null ? (int)p["material"] : -1,
                    Node = meshNodes.TryGetValue(mi, out int node) ? node : -1,
                    Positions = MirrorX(Floats((int)a["POSITION"]), 3),
                    Normals = a["NORMAL"] != null ? MirrorX(Floats((int)a["NORMAL"]), 3) : null,
                    Uv = a["TEXCOORD_0"] != null ? FlipV(Floats((int)a["TEXCOORD_0"])) : null,
                    Joints = a["JOINTS_0"] != null ? Ints((int)a["JOINTS_0"]) : null,
                    Weights = a["WEIGHTS_0"] != null ? Floats((int)a["WEIGHTS_0"]) : null,
                    Triangles = Rewind(Ints((int)p["indices"])),
                };
                model.Primitives.Add(prim);
            }
        return model;
    }

    /// <summary>A file the model refers to, kept inside its own folder.</summary>
    private static string Inside(string dir, string uri)
    {
        string full = Path.GetFullPath(Path.Combine(dir, Uri.UnescapeDataString(uri)));
        if (!full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new FormatException("glTF file outside its folder: " + uri);
        return full;
    }

    private static readonly int[] ComponentSizes = { 1, 1, 2, 2, 0, 4, 4 };   // 5120..5126

    private static int Components(string type) => type switch
    {
        "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16,
        _ => throw new FormatException("Unsupported accessor type " + type),
    };

    private static (byte[] Data, int Offset, int Stride, int Count, int Width, int ComponentType) Layout(JObject accessor, List<JObject> views, List<byte[]> buffers)
    {
        if (accessor["sparse"] != null) throw new FormatException("Sparse accessors are not read");
        var view = views[(int)accessor["bufferView"]];
        int type = (int)accessor["componentType"];
        int size = type >= 5120 && type <= 5126 ? ComponentSizes[type - 5120] : 0;
        if (size == 0) throw new FormatException("Unsupported component type " + type);
        int width = Components((string)accessor["type"]);
        int offset = (view["byteOffset"] != null ? (int)view["byteOffset"] : 0) + (accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0);
        int stride = view["byteStride"] != null ? (int)view["byteStride"] : size * width;
        int count = (int)accessor["count"];
        var data = buffers[(int)view["buffer"]];
        if (count < 0 || offset < 0 || (count > 0 && offset + (long)(count - 1) * stride + size * width > data.Length))
            throw new FormatException("Accessor outside its buffer");
        return (data, offset, stride, count, width, type);
    }

    private static float[] ReadFloats(JObject accessor, List<JObject> views, List<byte[]> buffers)
    {
        var (data, offset, stride, count, width, type) = Layout(accessor, views, buffers);
        bool normalized = accessor["normalized"] != null && (bool)accessor["normalized"];
        var result = new float[count * width];
        for (int i = 0; i < count; i++)
            for (int c = 0; c < width; c++)
            {
                int at = offset + i * stride;
                result[i * width + c] = type switch
                {
                    5126 => BitConverter.ToSingle(data, at + c * 4),
                    5123 => BitConverter.ToUInt16(data, at + c * 2) / (normalized ? 65535f : 1f),
                    5121 => data[at + c] / (normalized ? 255f : 1f),
                    _ => throw new FormatException("Unsupported float component type " + type),
                };
            }
        return result;
    }

    private static int[] ReadInts(JObject accessor, List<JObject> views, List<byte[]> buffers)
    {
        var (data, offset, stride, count, width, type) = Layout(accessor, views, buffers);
        var result = new int[count * width];
        for (int i = 0; i < count; i++)
            for (int c = 0; c < width; c++)
            {
                int at = offset + i * stride;
                result[i * width + c] = type switch
                {
                    5125 => (int)BitConverter.ToUInt32(data, at + c * 4),
                    5123 => BitConverter.ToUInt16(data, at + c * 2),
                    5121 => data[at + c],
                    _ => throw new FormatException("Unsupported index component type " + type),
                };
            }
        return result;
    }

    private static float[] MirrorX(float[] values, int width)
    {
        for (int i = 0; i < values.Length; i += width) values[i] = -values[i];
        return values;
    }

    private static float[] FlipV(float[] uv)
    {
        for (int i = 1; i < uv.Length; i += 2) uv[i] = 1f - uv[i];
        return uv;
    }

    private static int[] Rewind(int[] triangles)
    {
        for (int i = 0; i + 2 < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        return triangles;
    }

    private static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

    /// <summary>F·M·F with F = diag(-1, 1, 1, 1): a column-major matrix in mirrored-X space.</summary>
    public static float[] Mirror(float[] columnMajor)
    {
        var m = (float[])columnMajor.Clone();
        for (int c = 0; c < 4; c++)
            for (int r = 0; r < 4; r++)
                if ((r == 0) != (c == 0)) m[c * 4 + r] = -m[c * 4 + r];
        return m;
    }

    /// <summary>The visible height of the given primitives (model space, rest pose), and their lowest point.</summary>
    public (float Height, float Bottom) Extent(Func<Primitive, bool> include = null)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var p in Primitives.Where(p => include == null || include(p)))
            for (int i = 1; i < p.Positions.Length; i += 3) { lo = Math.Min(lo, p.Positions[i]); hi = Math.Max(hi, p.Positions[i]); }
        return hi >= lo ? (hi - lo, lo) : (0, 0);
    }
}
