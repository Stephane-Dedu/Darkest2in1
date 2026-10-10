using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Combat;
using Assets.Code.Game;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// Trying characters exported from other games in DD2's fights (owner request 2026-10-10, Dark Souls III's Soul of
/// Cinder). F1 in a dungeon starts a fight against a lone Lost Battalion knight (a large, two-rank DD2 enemy); the
/// private glTF export (Paths/ExportedBossModel) stands in its place on the knight's own character material. The
/// export brings no animation: its bones follow the knight's animated DD2 bones (<see cref="RigRetarget"/>), with the
/// two rest poses aligned, so DD2's idle, attacks, hits and death move it. The knight's model is hidden; DD2 still runs
/// the fight.
/// </summary>
[DefaultExecutionOrder(32000)]   // after DD2's animation and its late bone work
internal sealed class ExportedBoss : MonoBehaviour
{
    public const string Donor = "lost_battalion_knight";
    private const float Size = 1.5f, ColourGain = 1.0f, EdgeShare = 0.06f;

    private sealed class Pair
    {
        public Transform Node, Dd2;
        public Quaternion Ds3Rest, Dd2Rest;   // the shared starting pose: export (root space), knight (actor space)
        public int Depth;
    }

    private static GltfModel _model;
    private static string _modelPath;
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static bool _armed;
    private static float _armedAt;
    private static GameObject _root;
    private static Transform _actor;
    private static readonly List<Pair> Pairs = new();
    private static Transform _pelvis, _dd2Root;
    private static Vector3 _pelvisOffset;
    private static readonly List<Renderer> Hidden = new();
    private static readonly List<(Animator Animator, AnimatorCullingMode Mode)> Animators = new();
    private static readonly List<UnityEngine.Object> Made = new();

    /// <summary>The next fight's knight takes the export's place (called once the fight has started).</summary>
    public static void Arm()
    {
        _armed = true;
        _armedAt = Time.unscaledTime;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame && !kb.ctrlKey.isPressed && !kb.shiftKey.isPressed && !kb.altKey.isPressed)
            Runtime.Driver.Instance?.DebugExportedBossFight();
        try
        {
            if (_root != null && GameModeMgr.CurrentMode != GameModeType.COMBAT) Clear();
            if (!_armed) return;
            if (Time.unscaledTime - _armedAt > 30f) { _armed = false; Plugin.Log.LogWarning("[exported boss] no knight to stand in for"); return; }
            if (GameModeMgr.CurrentMode == GameModeType.COMBAT && Dd2Api.Modes != null && !Dd2Api.Modes.IsChangingState() && _root == null)
                TryPlace();
        }
        catch (Exception e)
        {
            _armed = false;
            Plugin.Log.LogError("[exported boss] " + e);
            Clear();
        }
    }

    /// <summary>Each frame after DD2 has posed the knight: the export takes the same pose.</summary>
    private void LateUpdate()
    {
        if (_root == null || Pairs.Count == 0) return;
        if (_actor == null || _dd2Root == null) { Clear(); return; }
        try
        {
            var rootRot = _root.transform.rotation;
            var actorRot = _actor.rotation;
            _pelvis.position = _dd2Root.position + _root.transform.TransformVector(_pelvisOffset);
            foreach (var p in Pairs)
            {
                if (p.Dd2 == null) continue;
                var delta = p.Dd2.rotation * Quaternion.Inverse(actorRot * p.Dd2Rest);
                p.Node.rotation = delta * (rootRot * p.Ds3Rest);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("[exported boss] pose: " + e);
            Pairs.Clear();
        }
    }

    private static void TryPlace()
    {
        var party = new HashSet<uint>(Runtime.Driver.Instance?.Party?.Guids ?? Enumerable.Empty<uint>());
        var actors = FindObjectsOfType<CombatActorBhv>().Where(a => a != null && a.ActorInstance != null).ToList();
        var knight = actors.FirstOrDefault(a => !party.Contains(a.GetActorGuid()) && a.ActorInstance.ActorDataClass?.Id == Donor);
        if (knight == null) return;
        var natives = knight.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToList();
        var shown = natives.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
        if (shown.Count == 0) return;
        var bounds = shown[0].bounds;
        foreach (var r in shown.Skip(1)) bounds.Encapsulate(r.bounds);
        if (bounds.size.y < 0.01f) return;                       // its parts load a little after the actor appears

        string path = Plugin.ExportedBossModel?.Value;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { _armed = false; Plugin.Log.LogWarning($"[exported boss] no export at {path}"); return; }
        if (_model == null || _modelPath != path) { _model = GltfModel.Load(path); _modelPath = path; }

        // The knight's body: its biggest skinned mesh, with the bones DD2 animates.
        var body = shown.OfType<SkinnedMeshRenderer>().Where(r => r.sharedMesh != null && r.bones != null)
            .OrderByDescending(r => r.bones.Length).ThenByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
        int layer = (body != null ? body.gameObject : shown[0].gameObject).layer;
        Func<GltfModel.Primitive, bool> include = p => !p.Mesh.Contains("#0") || p.Mesh.Contains("#01#");   // one weapon form, as DS3 shows
        var (root, nodes) = Build(_model, body != null ? body.sharedMaterial : null, layer, include);
        _root = root;

        string how = body != null && Retarget(_model, nodes, body, knight.transform) ? $"following the knight's {Pairs.Count} bones"
            : StandStill(_model, nodes, include, bounds, knight.transform);
        foreach (var r in natives) { r.forceRenderingOff = true; Hidden.Add(r); }
        foreach (var animator in knight.GetComponentsInChildren<Animator>(true))
        {
            // Hidden renderers can make an animator stop posing bones; the export still needs them.
            Animators.Add((animator, animator.cullingMode));
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        knight.ActorInstance.SetActorName(Path.GetFileNameWithoutExtension(path).Replace('_', ' ') is var name && name.Length > 0
            ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name) : Donor);
        _armed = false;
        Plugin.Log.LogInfo($"[exported boss] {Path.GetFileName(path)} stands in for {Donor}: {_model.Primitives.Count(include)} meshes, "
            + $"{_model.TriangleCount} triangles, {_model.Joints.Length} bones, scale {_root.transform.lossyScale.y:0.###}, {how}; "
            + $"{(body != null ? "knight material " + body.sharedMaterial?.shader?.name : "fallback shader")}, {Animators.Count} animators");
    }

    /// <summary>
    /// Pairs the export's bones with the knight's, then makes the knight's pose at this moment the shared starting pose:
    /// the export is sized by its bone chains (spine and leg, independent of pose), turned to the knight's facing (its
    /// hips' line), set pelvis on pelvis, and each of its bones bent to point where the knight's does. From then on each
    /// export bone turns as its knight bone turns from that pose.
    /// </summary>
    private static bool Retarget(GltfModel m, Transform[] nodes, SkinnedMeshRenderer body, Transform knight)
    {
        var dd2Bones = new Dictionary<string, Transform>();
        foreach (var b in body.bones) if (b != null && !dd2Bones.ContainsKey(b.name)) dd2Bones[b.name] = b;
        var byName = new Dictionary<string, Transform>();
        foreach (var n in nodes) if (!byName.ContainsKey(n.name)) byName[n.name] = n;
        var pairs = RigRetarget.Pairs(byName.Keys, dd2Bones.Keys);
        string[] need = { "Pelvis", "Spine", "Spine1", "Spine2", "Neck", "Head", "L_Thigh", "L_Calf", "L_Foot", "R_Thigh" };
        if (pairs.Count < 20 || need.Any(n => !byName.ContainsKey(n) || !dd2Bones.ContainsKey(RigRetarget.Ds3ToDd2[n]))) return false;
        Transform D(string ds3) => dd2Bones[RigRetarget.Ds3ToDd2[ds3]];

        float Chain(Func<string, Vector3> at, params string[] bones)
        {
            float length = 0;
            for (int i = 1; i < bones.Length; i++) length += Vector3.Distance(at(bones[i - 1]), at(bones[i]));
            return length;
        }
        string[] chain = { "L_Foot", "L_Calf", "L_Thigh", "Pelvis", "Spine", "Spine1", "Spine2", "Neck", "Head" };
        float dd2Length = Chain(n => D(n).position, chain), ds3Length = Chain(n => byName[n].position, chain);
        if (dd2Length <= 0.01f || ds3Length <= 0.01f) return false;

        var root = _root.transform;
        root.localScale = Vector3.one * (dd2Length / ds3Length * Size);
        Vector3 Forward(Vector3 left, Vector3 right) { var f = Vector3.Cross(right - left, Vector3.up); f.y = 0; return f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.forward; }
        var dd2Forward = Forward(D("L_Thigh").position, D("R_Thigh").position);
        var ds3Forward = Forward(byName["L_Thigh"].position, byName["R_Thigh"].position);
        root.rotation = Quaternion.FromToRotation(ds3Forward, dd2Forward) * root.rotation;
        root.position += D("Pelvis").position - byName["Pelvis"].position;
        root.SetParent(knight, worldPositionStays: true);

        var depth = new Dictionary<Transform, int>();
        int Depth(Transform x) { if (x == null || x == root) return 0; if (!depth.TryGetValue(x, out int d)) depth[x] = d = Depth(x.parent) + 1; return d; }
        Pairs.Clear();
        foreach (var (ds3, dd2) in pairs)
            Pairs.Add(new Pair { Node = byName[ds3], Dd2 = dd2Bones[dd2], Depth = Depth(byName[ds3]) });
        Pairs.Sort((a, b) => a.Depth.CompareTo(b.Depth));

        // Bend the export into the knight's pose, parents first so each child starts from its parent's new frame.
        foreach (var p in Pairs)
        {
            string ds3 = p.Node.name;
            if (!RigRetarget.Aim.TryGetValue(ds3, out var child) || !byName.ContainsKey(child) || !dd2Bones.ContainsKey(RigRetarget.Ds3ToDd2[child])) continue;
            var from = byName[child].position - p.Node.position;
            var to = D(child).position - p.Dd2.position;
            if (from.sqrMagnitude > 1e-10f && to.sqrMagnitude > 1e-10f) p.Node.rotation = Quaternion.FromToRotation(from, to) * p.Node.rotation;
        }
        var rootRot = root.rotation;
        foreach (var p in Pairs)
        {
            p.Ds3Rest = Quaternion.Inverse(rootRot) * p.Node.rotation;
            p.Dd2Rest = Quaternion.Inverse(knight.rotation) * p.Dd2.rotation;
        }
        _actor = knight;
        _dd2Root = D("Pelvis");
        _pelvis = byName["Pelvis"];
        _pelvisOffset = Vector3.zero;
        return true;
    }

    /// <summary>Without a rig to follow: at the knight's feet, facing the party, arms lowered from the T-pose.</summary>
    private static string StandStill(GltfModel m, Transform[] nodes, Func<GltfModel.Primitive, bool> include, Bounds bounds, Transform knight)
    {
        var (height, bottom) = m.Extent(include);
        float scale = bounds.size.y / Mathf.Max(0.01f, height);
        var root = _root.transform;
        root.localScale = Vector3.one * scale;
        root.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        root.position = new Vector3(bounds.center.x, bounds.min.y - bottom * scale, bounds.center.z);
        root.SetParent(knight, worldPositionStays: true);
        foreach (var (upper, lower) in new[] { ("L_UpperArm", "L_Forearm"), ("R_UpperArm", "R_Forearm") })
        {
            int u = m.Nodes.FindIndex(n => n.Name == upper), l = m.Nodes.FindIndex(n => n.Name == lower);
            if (u < 0 || l < 0) continue;
            var dir = nodes[l].position - nodes[u].position;
            if (dir.sqrMagnitude > 1e-8f) nodes[u].rotation = Quaternion.FromToRotation(dir, Vector3.Slerp(dir.normalized, Vector3.down, 0.8f)) * nodes[u].rotation;
        }
        return "standing still (no knight rig found)";
    }

    /// <summary>The export as Unity objects: its node hierarchy as bones, one skinned mesh per primitive.</summary>
    private static (GameObject Root, Transform[] Nodes) Build(GltfModel m, Material template, int layer, Func<GltfModel.Primitive, bool> include)
    {
        var root = new GameObject("DD3 exported boss");
        root.layer = layer;
        var nodes = new Transform[m.Nodes.Count];
        for (int i = 0; i < nodes.Length; i++) nodes[i] = new GameObject(m.Nodes[i].Name).transform;
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = m.Nodes[i];
            nodes[i].SetParent(n.Parent >= 0 ? nodes[n.Parent] : root.transform, false);
            nodes[i].localPosition = new Vector3(n.Translation[0], n.Translation[1], n.Translation[2]);
            nodes[i].localRotation = new Quaternion(n.Rotation[0], n.Rotation[1], n.Rotation[2], n.Rotation[3]);
            nodes[i].localScale = new Vector3(n.Scale[0], n.Scale[1], n.Scale[2]);
            nodes[i].gameObject.layer = layer;
        }
        var bones = m.Joints.Select(j => nodes[j]).ToArray();
        var bindposes = m.InverseBind.Select(ToMatrix).ToArray();
        var materials = new Dictionary<int, Material>();

        int cut = 0, sides = 0;
        foreach (var source in m.Primitives.Where(include))
        {
            // DD2's character shader neither clips nor draws back faces: cut-outs and two-sided cloth are made in geometry.
            var p = source;
            var info = p.Material >= 0 && p.Material < m.Materials.Count ? m.Materials[p.Material] : null;
            if (info != null && info.Mask && AlphaOf(info.BaseColor) is { } alpha)
            {
                p = p.CutOut(alpha, info.Cutoff);
                cut += (source.Triangles.Length - p.Triangles.Length) / 3;
            }
            if (info != null && info.DoubleSided) { p = p.WithBackFaces(); sides += p.Triangles.Length / 6; }
            int count = p.VertexCount;
            var mesh = new Mesh { name = "DD3 " + p.Mesh };
            if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var vertices = new Vector3[count];
            var normals = p.Normals != null ? new Vector3[count] : null;
            var uv = p.Uv != null ? new Vector2[count] : null;
            var weights = new BoneWeight[count];
            for (int v = 0; v < count; v++)
            {
                vertices[v] = new Vector3(p.Positions[v * 3], p.Positions[v * 3 + 1], p.Positions[v * 3 + 2]);
                if (normals != null) normals[v] = new Vector3(p.Normals[v * 3], p.Normals[v * 3 + 1], p.Normals[v * 3 + 2]);
                if (uv != null) uv[v] = new Vector2(p.Uv[v * 2], p.Uv[v * 2 + 1]);
                if (p.Joints != null && p.Weights != null) weights[v] = Weight(p.Joints, p.Weights, v);
            }
            mesh.vertices = vertices;
            if (normals != null) mesh.normals = normals;
            if (uv != null) mesh.uv = uv;
            // DD2's character meshes carry (1, 0, 0) almost everywhere: red is their shading, green and blue stay off.
            mesh.colors = Enumerable.Repeat(new Color(1f, 0f, 0f, 1f), count).ToArray();
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.triangles = p.Triangles;
            mesh.RecalculateBounds();
            if (normals == null) mesh.RecalculateNormals();
            Made.Add(mesh);

            var go = new GameObject(p.Mesh) { layer = layer };
            go.transform.SetParent(root.transform, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones.Length > 0 ? bones[0] : root.transform;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.Off;
            if (!materials.TryGetValue(p.Material, out var material))
                materials[p.Material] = material = MaterialFor(m, p.Material, template);
            smr.sharedMaterial = material;
        }
        if (cut + sides > 0) Plugin.Log.LogInfo($"[exported boss] {cut} cut-out triangles dropped, {sides} two-sided triangles doubled");
        return (root, nodes);
    }

    /// <summary>A texture's alpha at a UV (nearest texel, repeating), or null without one.</summary>
    private static Func<float, float, float> AlphaOf(string path)
    {
        var texture = Load(path);
        if (texture == null) return null;
        int w = texture.width, h = texture.height;
        var pixels = texture.GetPixels32();
        Destroy(texture);
        return (u, v) =>
        {
            u -= Mathf.Floor(u); v -= Mathf.Floor(v);
            int x = Math.Min(w - 1, (int)(u * w)), y = Math.Min(h - 1, (int)(v * h));
            return pixels[y * w + x].a / 255f;
        };
    }

    /// <summary>Up to four strongest influences, renormalised (Unity's default skin quality).</summary>
    private static BoneWeight Weight(int[] joints, float[] weights, int v)
    {
        var pairs = Enumerable.Range(0, 4).Select(k => (Joint: joints[v * 4 + k], W: weights[v * 4 + k])).Where(x => x.W > 0)
            .OrderByDescending(x => x.W).ToList();
        float sum = pairs.Sum(x => x.W);
        if (sum <= 0) return new BoneWeight { boneIndex0 = 0, weight0 = 1 };
        (int, float) At(int k) => k < pairs.Count ? (pairs[k].Joint, pairs[k].W / sum) : (0, 0f);
        var (b0, w0) = At(0); var (b1, w1) = At(1); var (b2, w2) = At(2); var (b3, w3) = At(3);
        return new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = w1, boneIndex2 = b2, weight2 = w2, boneIndex3 = b3, weight3 = w3 };
    }

    private static Matrix4x4 ToMatrix(float[] c) => new(
        new Vector4(c[0], c[1], c[2], c[3]), new Vector4(c[4], c[5], c[6], c[7]),
        new Vector4(c[8], c[9], c[10], c[11]), new Vector4(c[12], c[13], c[14], c[15]));

    /// <summary>The knight's own character material (DD2's lighting) with the export's colours and ink; else an unlit one.</summary>
    private static Material MaterialFor(GltfModel m, int index, Material template)
    {
        var info = index >= 0 && index < m.Materials.Count ? m.Materials[index] : null;
        var colours = Colours(info?.BaseColor, info?.Emissive);
        Material material;
        if (template != null)
        {
            material = new Material(template) { name = "DD3 exported " + info?.Name };
            if (material.HasProperty("_Base")) material.SetTexture("_Base", colours);
            if (material.HasProperty("_Ink")) material.SetTexture("_Ink", Ink(info?.Normal));
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", colours);
        }
        else material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default")) { name = "DD3 exported " + info?.Name, mainTexture = colours };
        Made.Add(material);
        return material;
    }

    /// <summary>The base colour toned to DD2's own darker albedo, with the embers added.</summary>
    private static Texture2D Colours(string basePath, string emissivePath)
    {
        string key = "colour|" + basePath + "|" + emissivePath;
        if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
        var colours = Load(basePath);
        if (colours == null) return Textures[key] = White();
        var pixels = colours.GetPixels32();
        var embers = Load(emissivePath);
        var glow = embers != null && embers.width == colours.width && embers.height == colours.height ? embers.GetPixels32() : null;
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            // A cut-out's transparent texels still show along kept triangles' edges (the shader can't clip): torn
            // cloth then reads as dark tatters instead of the pale colour stored there.
            if (c.a < 128) { pixels[i] = new Color32(16, 12, 10, c.a); continue; }
            int r = (int)(c.r * ColourGain), g = (int)(c.g * ColourGain), b = (int)(c.b * ColourGain);
            if (glow != null) { r += glow[i].r; g += glow[i].g; b += glow[i].b; }
            pixels[i] = new Color32((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b), c.a);
        }
        colours.SetPixels32(pixels);
        colours.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        if (embers != null) Destroy(embers);
        return Textures[key] = colours;
    }

    /// <summary>
    /// DD2's _Ink (black = drawn lines): where the export's normal map turns sharply (plate edges, folds, rivets), the
    /// strongest share of turns becomes a line, a pixel wider so it holds at fight distance.
    /// </summary>
    private static Texture2D Ink(string normalPath)
    {
        string key = "ink|" + normalPath;
        if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
        var normals = Load(normalPath);
        if (normals == null) return Textures[key] = White();
        int w = normals.width, h = normals.height;
        var n = Smooth(normals.GetPixels32(), w, h);   // the export's fine surface grain would otherwise ink as speckle
        Destroy(normals);
        var turn = new int[n.Length];
        var histogram = new int[766];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var a = n[y * w + Math.Max(0, x - 1)]; var b = n[y * w + Math.Min(w - 1, x + 1)];
                var c = n[Math.Max(0, y - 1) * w + x]; var d = n[Math.Min(h - 1, y + 1) * w + x];
                int g = Math.Min(765, (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) + Math.Abs(c.r - d.r) + Math.Abs(c.g - d.g) + Math.Abs(c.b - d.b)) / 2);
                turn[y * w + x] = g;
                histogram[g]++;
            }
        int cut = 765;
        for (int sum = 0; cut > 0 && sum + histogram[cut] < n.Length * EdgeShare; cut--) sum += histogram[cut];
        var ink = new Color32[n.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool line = turn[y * w + x] > cut || (x + 1 < w && turn[y * w + x + 1] > cut) || (y + 1 < h && turn[(y + 1) * w + x] > cut);
                byte v = line ? (byte)0 : (byte)255;
                ink[y * w + x] = new Color32(v, v, v, 255);
            }
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = Path.GetFileNameWithoutExtension(normalPath) + " ink", wrapMode = TextureWrapMode.Repeat };
        texture.SetPixels32(ink);
        texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        return Textures[key] = texture;
    }

    /// <summary>Each texel averaged with its eight neighbours.</summary>
    private static Color32[] Smooth(Color32[] p, int w, int h)
    {
        var result = new Color32[p.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, count = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        var c = p[yy * w + xx];
                        r += c.r; g += c.g; b += c.b; count++;
                    }
                result[y * w + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
            }
        return result;
    }

    private static Texture2D Load(string path)
    {
        if (path == null || !File.Exists(path)) return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true) { name = Path.GetFileNameWithoutExtension(path), wrapMode = TextureWrapMode.Repeat };
        if (texture.LoadImage(File.ReadAllBytes(path), markNonReadable: false)) return texture;
        Destroy(texture);
        return null;
    }

    private static Texture2D _white;

    private static Texture2D White()
    {
        if (_white != null) return _white;
        _white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "DD3 white" };
        _white.SetPixels32(Enumerable.Repeat(new Color32(255, 255, 255, 255), 16).ToArray());
        _white.Apply();
        return _white;
    }

    /// <summary>The fight is over: the export goes, the knight's model and animators come back.</summary>
    private static void Clear()
    {
        foreach (var r in Hidden) if (r != null) r.forceRenderingOff = false;
        Hidden.Clear();
        foreach (var (animator, mode) in Animators) if (animator != null) animator.cullingMode = mode;
        Animators.Clear();
        Pairs.Clear();
        _actor = null; _dd2Root = null; _pelvis = null;
        if (_root != null) Destroy(_root);
        _root = null;
        foreach (var o in Made) if (o != null) Destroy(o);
        Made.Clear();
    }
}
