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
/// private glTF export (Paths/ExportedBossModel) stands in its place: at the knight's feet, a little taller, facing the
/// party, on the knight's own character material with the export's colours. The knight's model is hidden; DD2 still
/// runs the fight. The export brings no animation, so it stands in its bind pose with the arms lowered.
/// </summary>
internal sealed class ExportedBoss : MonoBehaviour
{
    public const string Donor = "lost_battalion_knight";
    private const float Taller = 1.2f, ColourGain = 2.2f;

    private static GltfModel _model;
    private static string _modelPath;
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static bool _armed;
    private static float _armedAt;
    private static GameObject _root;
    private static readonly List<Renderer> Hidden = new();
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

        var body = shown.OfType<SkinnedMeshRenderer>().Where(r => r.sharedMesh != null).OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
        int layer = (body != null ? body.gameObject : shown[0].gameObject).layer;
        Func<GltfModel.Primitive, bool> include = p => !p.Mesh.Contains("#0") || p.Mesh.Contains("#01#");   // one weapon form, as DS3 shows
        _root = Build(_model, body != null ? body.sharedMaterial : null, layer, include);

        // At the knight's feet, a little taller than it, facing the party; it then moves with the knight.
        var (height, bottom) = _model.Extent(include);
        float scale = bounds.size.y * Taller / Mathf.Max(0.01f, height);
        var heroes = actors.Where(a => party.Contains(a.GetActorGuid())).Select(a => a.transform.position).ToList();
        var toward = heroes.Count > 0 ? heroes.Aggregate(Vector3.zero, (s, p) => s + p) / heroes.Count - knight.transform.position : Vector3.left;
        toward.y = 0;
        _root.transform.localScale = Vector3.one * scale;
        _root.transform.rotation = Quaternion.LookRotation(toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.left, Vector3.up);
        _root.transform.position = new Vector3(bounds.center.x, bounds.min.y - bottom * scale, bounds.center.z);
        _root.transform.SetParent(knight.transform, worldPositionStays: true);

        foreach (var r in natives) { r.forceRenderingOff = true; Hidden.Add(r); }
        _armed = false;
        Plugin.Log.LogInfo($"[exported boss] {Path.GetFileName(path)} stands in for {Donor}: {_model.Primitives.Count(include)} meshes, "
            + $"{_model.TriangleCount} triangles, {_model.Joints.Length} bones, {height:0.00} m x{scale:0.###} (knight {bounds.size.y:0.00}), "
            + $"{(body != null ? "knight material " + body.sharedMaterial?.shader?.name : "fallback shader")}");
    }

    /// <summary>The export as Unity objects: its node hierarchy as bones, one skinned mesh per primitive.</summary>
    private static GameObject Build(GltfModel m, Material template, int layer, Func<GltfModel.Primitive, bool> include)
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

        foreach (var p in m.Primitives.Where(include))
        {
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
            mesh.colors = Enumerable.Repeat(Color.white, count).ToArray();
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

        // No animation comes with the export: lower the arms out of its T-pose (model space, before placing).
        Lower(nodes, m, "L_UpperArm", "L_Forearm");
        Lower(nodes, m, "R_UpperArm", "R_Forearm");
        return root;
    }

    private static void Lower(Transform[] nodes, GltfModel m, string upper, string lower)
    {
        int u = m.Nodes.FindIndex(n => n.Name == upper), l = m.Nodes.FindIndex(n => n.Name == lower);
        if (u < 0 || l < 0) return;
        var dir = nodes[l].position - nodes[u].position;
        if (dir.sqrMagnitude < 1e-8f) return;
        var want = Vector3.Slerp(dir.normalized, Vector3.down, 0.8f);
        nodes[u].rotation = Quaternion.FromToRotation(dir, want) * nodes[u].rotation;
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

    /// <summary>The knight's own character material (DD2's lighting) with the export's colours; else an unlit one.</summary>
    private static Material MaterialFor(GltfModel m, int index, Material template)
    {
        var info = index >= 0 && index < m.Materials.Count ? m.Materials[index] : null;
        var colours = Colours(info?.BaseColor, info?.Emissive);
        Material material;
        if (template != null)
        {
            material = new Material(template) { name = "DD3 exported " + info?.Name };
            if (material.HasProperty("_Base")) material.SetTexture("_Base", colours);
            if (material.HasProperty("_Ink")) material.SetTexture("_Ink", White());
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", colours);
        }
        else material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default")) { name = "DD3 exported " + info?.Name, mainTexture = colours };
        Made.Add(material);
        return material;
    }

    /// <summary>The base colour brightened (DS3's armour is near-black under DD2's light) with its embers added.</summary>
    private static Texture2D Colours(string basePath, string emissivePath)
    {
        string key = basePath + "|" + emissivePath;
        if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
        var colours = Load(basePath);
        if (colours == null) return Textures[key] = White();
        var pixels = colours.GetPixels32();
        var embers = Load(emissivePath);
        var glow = embers != null && embers.width == colours.width && embers.height == colours.height ? embers.GetPixels32() : null;
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            int r = (int)(c.r * ColourGain), g = (int)(c.g * ColourGain), b = (int)(c.b * ColourGain);
            if (glow != null) { r += glow[i].r; g += glow[i].g; b += glow[i].b; }
            pixels[i] = new Color32((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b), c.a);
        }
        colours.SetPixels32(pixels);
        colours.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        if (embers != null) Destroy(embers);
        return Textures[key] = colours;
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

    /// <summary>The fight is over: the export goes, the knight's model comes back.</summary>
    private static void Clear()
    {
        foreach (var r in Hidden) if (r != null) r.forceRenderingOff = false;
        Hidden.Clear();
        if (_root != null) Destroy(_root);
        _root = null;
        foreach (var o in Made) if (o != null) Destroy(o);
        Made.Clear();
    }
}
