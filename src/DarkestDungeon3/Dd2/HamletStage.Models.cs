using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    // Disk parsing uses plain arrays on a worker; Unity uploads and mesh creation stay on the main thread.
    private sealed class ModelPack
    {
        public readonly Dictionary<string, GltfModel> Models = new();
        public readonly Dictionary<string, byte[]> Paint = new();
        public HamletTerrain Terrain;
    }
    private Task<ModelPack> _modelTask;
    private ModelPack _modelPack;
    private KeyValuePair<string, byte[]>[] _paintQueue;
    private readonly Dictionary<string, Texture2D> _paint = new();
    private int _paintIndex, _modelIndex;

    private void BeginModels()
    {
        string folder = Path.Combine(Plugin.NativeRoomSceneryPath.Value, "HamletModels");
        _modelTask = Task.Run(() => ReadModels(folder));
    }

    private static ModelPack ReadModels(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Private HamletModels package is missing.");
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        if ((int?)manifest["version"] != 1) throw new FormatException("Unsupported Hamlet model package.");
        var pack = new ModelPack();
        // Validate the complete package before submitting any texture or geometry to Unity.
        using (var sha = SHA256.Create())
            foreach (var file in ((JObject)manifest["files"]).Properties())
            {
                if (Path.GetFileName(file.Name) != file.Name) throw new FormatException("Hamlet package paths must be flat.");
                var bytes = File.ReadAllBytes(Path.Combine(folder, file.Name));
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (hash != (string)file.Value) throw new FormatException("Damaged Hamlet asset: " + file.Name);
            }
        foreach (var site in HamletLayout.Sites)
        {
            var entry = manifest["models"]?[site.Id];
            if (entry == null || (string)entry["file"] != site.Id + ".gltf") throw new FormatException("Missing Hamlet service: " + site.Id);
            var model = GltfModel.Load(Path.Combine(folder, site.Id + ".gltf"));
            if (model.TriangleCount != (int)entry["triangles"]) throw new FormatException("Incomplete Hamlet geometry: " + site.Id);
            pack.Models.Add(site.Id, model);
            foreach (var material in model.Materials)
                foreach (string path in new[] { material.BaseColor, material.Emissive }.Where(p => p != null))
                    if (!pack.Paint.ContainsKey(path)) pack.Paint.Add(path, File.ReadAllBytes(path));
        }
        pack.Terrain = HamletTerrain.Build();
        return pack;
    }

    private bool AdvanceModels()
    {
        if (Time.unscaledTime - _started > 45) throw new TimeoutException("Hamlet models did not finish loading.");
        if (_modelPack == null)
        {
            if (!_modelTask.IsCompleted) return false;
            _modelPack = _modelTask.GetAwaiter().GetResult();
            _paintQueue = _modelPack.Paint.ToArray();
        }
        if (_paintIndex < _paintQueue.Length)
        {
            var entry = _paintQueue[_paintIndex++];
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            { name = "Hamlet " + Path.GetFileName(entry.Key), wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            _details.Add(texture);
            if (!texture.LoadImage(entry.Value, true)) throw new FormatException("Cannot upload Hamlet texture: " + entry.Key);
            _paint.Add(entry.Key, texture);
            // Drop the compressed bytes once uploaded; rollback destroys all the owned GPU textures.
            _modelPack.Paint.Remove(entry.Key);
            _paintQueue[_paintIndex - 1] = default;
            return false;
        }
        var templateHandle = _assets[PaintTemplate];
        if (!templateHandle.IsDone) return false;
        var template = templateHandle.Result?.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.HasProperty("_BaseMap_texture"));
        if (template == null) throw new InvalidOperationException("Native painted environment material is unavailable.");
        if (_modelIndex < HamletLayout.Sites.Count)
        {
            var site = HamletLayout.Sites[_modelIndex++];
            BuildModel(site, _modelPack.Models[site.Id], TownMaterial(template), templateHandle.Result.layer);
            return false;
        }
        return true;
    }

    private void BuildModel(HamletLayout.Site site, GltfModel model, Material template, int layer)
    {
        var matrices = new Matrix4x4[model.Nodes.Count];
        var resolved = new bool[matrices.Length];
        Matrix4x4 NodeMatrix(int index)
        {
            if (index < 0) return Matrix4x4.identity;
            if (resolved[index]) return matrices[index];
            var node = model.Nodes[index];
            var t = node.Translation; var r = node.Rotation; var s = node.Scale;
            matrices[index] = NodeMatrix(node.Parent) * Matrix4x4.TRS(new Vector3(t[0], t[1], t[2]),
                new Quaternion(r[0], r[1], r[2], r[3]), new Vector3(s[0], s[1], s[2]));
            resolved[index] = true; return matrices[index];
        }
        var group = new GameObject("Hamlet v02 " + site.Id).transform;
        group.SetParent(_root, false);
        var renderers = new List<Renderer>();
        var hitBounds = new List<Bounds>();
        var chimneys = new List<Bounds>();
        foreach (var batch in model.Primitives.GroupBy(p => p.Material))
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var info = batch.Key >= 0 ? model.Materials[batch.Key] : new GltfModel.MaterialInfo();
            foreach (var primitive in batch)
            {
                int start = vertices.Count;
                var matrix = NodeMatrix(primitive.Node); var normalMatrix = matrix.inverse.transpose;
                var bounds = new Bounds();
                for (int v = 0; v < primitive.VertexCount; v++)
                {
                    var p = primitive.Positions;
                    var point = matrix.MultiplyPoint3x4(new Vector3(p[v * 3], p[v * 3 + 1], p[v * 3 + 2]));
                    vertices.Add(point);
                    if (v == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
                    var n = primitive.Normals;
                    normals.Add(n == null ? Vector3.up : normalMatrix.MultiplyVector(new Vector3(n[v * 3], n[v * 3 + 1], n[v * 3 + 2])).normalized);
                    uv.Add(primitive.Uv == null ? Vector2.zero : new Vector2(primitive.Uv[v * 2], primitive.Uv[v * 2 + 1]));
                }
                hitBounds.Add(bounds);
                if (model.Nodes[primitive.Node].Name?.StartsWith("Chimney cap", StringComparison.Ordinal) == true)
                    chimneys.Add(bounds);
                bool mirror = matrix.determinant < 0;
                for (int i = 0; i < primitive.Triangles.Length; i += 3)
                {
                    int a = start + primitive.Triangles[i], b = start + primitive.Triangles[i + (mirror ? 2 : 1)], c = start + primitive.Triangles[i + (mirror ? 1 : 2)];
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    // DD2's environment shader variants do not all expose culling. Keep torn canvas/foliage two-sided.
                    if (info.DoubleSided) { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
                }
            }
            var mesh = new Mesh { name = site.Id + " " + info.Name, indexFormat = IndexFormat.UInt32 };
            _details.Add(mesh);
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            mesh.colors = Enumerable.Repeat(Color.white, vertices.Count).ToArray();
            var obj = new GameObject(mesh.name) { layer = layer };
            obj.transform.SetParent(group, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = PaintMaterial(info, template);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            _baseTints[renderer] = renderer.sharedMaterial.HasProperty(Tint) ? renderer.sharedMaterial.GetColor(Tint) : Color.white;
            renderers.Add(renderer);
        }
        var combined = hitBounds[0];
        foreach (var bounds in hitBounds.Skip(1)) combined.Encapsulate(bounds);
        var shift = new Vector3(combined.center.x, combined.min.y, combined.center.z);
        foreach (Transform child in group) child.localPosition = -shift;
        float scale = site.Width / Mathf.Max(combined.size.x, combined.size.z, .1f);
        group.localScale = Vector3.one * scale;
        // Authoring Unity -> Blender -> glTF -> mirrored-X importer reverses X and Z. Restore the authored front.
        group.localRotation = Quaternion.Euler(0, 180 + site.Yaw, 0);
        group.localPosition = new Vector3(site.X, site.Y, site.Z);
        foreach (var chimney in chimneys)
            AddChimneySmoke(group,new Vector3(chimney.center.x,chimney.max.y,chimney.center.z)-shift);
        var building = new Building { Id = site.Id, Bounds = renderers[0].bounds };
        building.Renderers.AddRange(renderers);
        foreach (var renderer in renderers.Skip(1)) building.Bounds.Encapsulate(renderer.bounds);
        foreach (var bounds in hitBounds)
        {
            var world = new Bounds(group.TransformPoint(bounds.center - shift), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                world.Encapsulate(group.TransformPoint(bounds.center - shift + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            building.HitBounds.Add(world);
        }
        _buildings.Add(building);
        Plugin.Log.LogInfo($"[hamlet 3d model] {site.Id}: {model.TriangleCount} source triangles, {renderers.Count} material batches; position {group.localPosition}, size {building.Bounds.size}; {template.shader.name}");
    }

    private Material PaintMaterial(GltfModel.MaterialInfo info, Material template)
    {
        var material = new Material(template) { name = "Hamlet paint " + info.Name };
        _details.Add(material);
        var b = info.BaseColorFactor; var e = info.EmissiveFactor;
        bool paintedEmission = b[0] + b[1] + b[2] < .001f && info.Emissive != null;
        string path = paintedEmission ? info.Emissive : info.BaseColor ?? info.Emissive;
        Texture texture = path != null ? _paint[path] : Texture2D.whiteTexture;
        foreach (string property in new[] { "_BaseMap_texture", "_BaseMap", "_MainTex" })
            if (material.HasProperty(property)) { material.SetTexture(property, texture); material.SetTextureScale(property, Vector2.one); material.SetTextureOffset(property, Vector2.zero); }
        if (path!=null) Grade(material,HamletPalette.Building,false);
        var color=HamletPalette.BuildingTint(new(b[0],b[1],b[2]),new(e[0],e[1],e[2]),paintedEmission);
        var tint = new Color(color.X,color.Y,color.Z,b[3]);
        if (material.HasProperty(Tint)) material.SetColor(Tint, tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        bool cutout=info.Mask || info.Blend;
        float cutoff=info.Mask ? info.Cutoff : info.Blend ? .2f : 0;
        if (material.HasProperty("_____IsAlphaCutout")) material.SetFloat("_____IsAlphaCutout",cutout?1:0);
        if (cutout) material.EnableKeyword("_____ISALPHACUTOUT_ON"); else material.DisableKeyword("_____ISALPHACUTOUT_ON");
        foreach (string property in new[] { "_Cutoff","_AlphaClip_Override","_AlphaCutoff" })
            if (material.HasProperty(property)) material.SetFloat(property,cutoff);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", info.DoubleSided ? 0 : 2);
        return material;
    }

    private void ClearModels()
    {
        // An abandoned worker owns only managed bytes, never Unity or Addressables objects.
        // Observe failures even when the user leaves before parsing completes.
        _modelTask?.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _modelTask = null; _modelPack = null; _paintQueue = null;
        _paint.Clear(); _paintIndex = 0; _modelIndex = 0;
    }
}
