using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Code.Actor;
using Assets.Code.Combat.Events;
using Assets.Code.Events;
using DarkestDungeon3.Core.Presentation;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

/// <summary>Local Shieldbreaker mesh on her isolated Hellion presentation actor. Never changes shared assets.</summary>
[DefaultExecutionOrder(10020)]
internal sealed class ShieldbreakerModel : MonoBehaviour
{
    private static ShieldbreakerModelData _data;
    private static string _loadedPath;
    private static bool _loadFailed;
    private static Task<ShieldbreakerModelData> _reading;
    private sealed class CachedTexture { internal Texture2D Texture; internal int Users; }
    private static readonly Dictionary<string, CachedTexture> TextureCache = new();
    private static readonly System.Reflection.FieldInfo SharedMaterials = AccessTools.Field(typeof(MaterialPropertyBhv), "m_materialSharedCache");
    private ShieldbreakerModelData _modelData;
    private ActorBhv _actor;
    private SkinnedMeshRenderer _body;
    private Mesh _mesh, _originalMesh;
    private Material _material;
    private Material[] _originalMaterials;
    private Bounds _originalBounds;
    private readonly List<Texture2D> _textures = new();
    private readonly Dictionary<Renderer, bool> _hidden = new();
    private readonly Dictionary<Transform, Quaternion> _saved = new();
    private Transform _chest, _rs, _re, _rw, _ls, _le, _lw;
    private Matrix4x4 _chestBind, _wristRest;
    private string _clip = "idle";
    private float _since, _nextTry, _nextWeaponScan;
    private bool _ready, _failed, _listening, _dead;
    internal bool Ready => _ready;
    internal float BodyHeight => _body != null && _modelData != null ? _body.transform.TransformVector(Vector3.up * _modelData.BodyHeight).magnitude : 0;

    internal static bool Enabled => Plugin.Shieldbreaker3D?.Value == true;
    internal static bool IsShieldbreaker(ActorBhv actor) => actor != null && Dd2Api.Actor(actor.GetActorGuid())?.ActorDataId == "shieldbreaker";
    internal static bool IsReady(ActorBhv actor) => actor != null && actor.GetComponent<ShieldbreakerModel>() is { Ready: true };
    internal static void HideDonorWeapons(ActorBhv actor) => actor?.GetComponent<ShieldbreakerModel>()?.HideWeapons();

    internal static void Attach(ActorBhv actor)
    {
        if (!Enabled || !IsShieldbreaker(actor) || actor.GetComponent<ShieldbreakerModel>() != null) return;
        actor.gameObject.AddComponent<ShieldbreakerModel>()._actor = actor;
    }

    private static ShieldbreakerModelData Data()
    {
        string path = Path.Combine(Plugin.HeroModelPath.Value ?? "", "shieldbreaker.json");
        if (_loadedPath != path) { _data = null; _reading = null; _loadFailed = false; _loadedPath = path; }
        if (_data != null || _loadFailed) return _data;
        _reading ??= Task.Run(() => ShieldbreakerModelData.Read(path));
        if (!_reading.IsCompleted) return null;
        try { _data = _reading.GetAwaiter().GetResult(); }
        catch (Exception e) { _loadFailed = true; Plugin.Log.LogWarning("[shieldbreaker-3d] " + e.Message + "; retaining DD1 art"); }
        return _data;
    }

    private void Update()
    {
        // Undo last frame's procedural arm pose before Animator/Timeline evaluates this frame.
        RestorePose();
        if (_failed || _ready || !Enabled || _actor == null || _actor.IsLoading || Time.unscaledTime < _nextTry) return;
        _nextTry = Time.unscaledTime + .25f;
        var data = Data();
        if (data == null) { _failed = _loadFailed; return; }
        try { TryBuild(data); }
        catch (Exception e)
        {
            _failed = true; Release();
            Plugin.Log.LogWarning("[shieldbreaker-3d] model could not bind; retaining DD1 art: " + e);
        }
    }

    private void TryBuild(ShieldbreakerModelData d)
    {
        _modelData = d;
        var animator = _actor.GetCurrentAnimator();
        if (animator == null || !animator.isInitialized) return;
        var body = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.sharedMesh != null && r.bones.Length == d.Bones.Length)
            .FirstOrDefault(r => r.bones.Select(t => t != null ? t.name : "").SequenceEqual(d.Bones));
        if (body == null) throw new InvalidOperationException("The loaded Hellion skeleton does not match the converted model");
        var bones = body.bones;
        Transform Find(string n) => bones[Array.IndexOf(d.Bones, n)];
        _chest = Find("Spine_02SHJnt");
        _rs = Find("r_Arm_ShoulderSHJnt"); _re = Find("r_Arm_ElbowSHJnt"); _rw = Find("r_Arm_WristSHJnt");
        _ls = Find("l_Arm_ShoulderSHJnt"); _le = Find("l_Arm_ElbowSHJnt"); _lw = Find("l_Arm_WristSHJnt");
        Matrix4x4 Matrix(float[] a)
        {
            var m = new Matrix4x4();
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] = a[r * 4 + c];
            return m;
        }
        var binds = d.Bindposes.Select(Matrix).ToArray();
        // Reject a different mesh revision rather than attaching weights to incompatible bind poses.
        var nativeBinds = body.sharedMesh.bindposes;
        if (nativeBinds.Length != binds.Length) throw new InvalidOperationException("Native bind count changed");
        for (int i = 0; i < binds.Length; i++)
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                if (Math.Abs(nativeBinds[i][r, c] - binds[i][r, c]) > .01f)
                    throw new InvalidOperationException("Native bind matrices changed; rebuild the private model");
        _chestBind = binds[Array.IndexOf(d.Bones, "Spine_02SHJnt")];
        _wristRest = binds[Array.IndexOf(d.Bones, "r_Arm_WristSHJnt")].inverse;
        _mesh = new Mesh { name = "DD3 Shieldbreaker", indexFormat = IndexFormat.UInt32 };
        _mesh.vertices = d.Vertices.Select(V).ToArray();
        _mesh.normals = d.Normals.Select(V).ToArray();
        _mesh.uv = d.Uv.Select(a => new Vector2(a[0], a[1])).ToArray();
        _mesh.bindposes = binds;
        _mesh.boneWeights = Enumerable.Range(0, d.Vertices.Length).Select(i => new BoneWeight {
            boneIndex0=d.Indices[i][0], boneIndex1=d.Indices[i][1], boneIndex2=d.Indices[i][2], boneIndex3=d.Indices[i][3],
            weight0=d.Weights[i][0], weight1=d.Weights[i][1], weight2=d.Weights[i][2], weight3=d.Weights[i][3] }).ToArray();
        _mesh.triangles = d.Triangles; _mesh.RecalculateBounds();
        _material = new Material(body.sharedMaterial) { name = "DD3 Shieldbreaker painted" };
        _material.SetTexture("_Base", Texture("shieldbreaker_base.png"));
        _material.SetTexture("_Ink", Texture("shieldbreaker_ink.png"));
        _body = body; _originalMesh = body.sharedMesh; _originalMaterials = body.sharedMaterials; _originalBounds = body.localBounds;
        var bounds = _mesh.bounds; bounds.Expand(bounds.size * .5f);
        body.sharedMesh = _mesh; body.sharedMaterials = new[] { _material }; body.localBounds = bounds;
        RefreshMaterials();
        _ready = true; _since = Time.unscaledTime;
        EventManager.AddListener<EventCombatSkillPresentation>(OnSkill);
        EventManager.AddListener<EventCombatPresentationSkillTarget>(OnTarget);
        EventManager.AddListener<EventCombatActorDeath>(OnDeath);
        _listening = true;
        Plugin.Log.LogInfo($"[shieldbreaker-3d] {_actor.GetActorGuid()}: {_mesh.vertexCount} vertices, native rig, idle/attack/defend ready");
    }

    private Texture2D Texture(string name)
    {
        string path = Path.Combine(Plugin.HeroModelPath.Value, name);
        if (TextureCache.TryGetValue(path, out var cached) && cached.Texture != null)
        { cached.Users++; _textures.Add(cached.Texture); return cached.Texture; }
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 16 * 1024 * 1024) throw new FormatException("Missing or oversized model texture");
        var bytes = File.ReadAllBytes(path);
        if (!ShieldbreakerModelData.ValidTexturePng(bytes)) throw new FormatException("Invalid model texture PNG");
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        _textures.Add(t);
        if (!t.LoadImage(bytes, true)) throw new FormatException("Model texture decode failed");
        TextureCache[path] = new CachedTexture { Texture = t, Users = 1 };
        return t;
    }

    private static Vector3 V(float[] a) => new(a[0], a[1], a[2]);
    private static Vector3 V(ModelPoint p) => new(p.X, p.Y, p.Z);

    private void RefreshMaterials()
    {
        if (_actor == null || _body == null) return;
        foreach (var block in _actor.MaterialPropertyBlocks)
        {
            if (block == null) continue;
            // DD2 restores this baseline when an override or actor is disabled.
            // RefreshRenderers alone only clears its instance cache.
            if (SharedMaterials?.GetValue(block) is Dictionary<Renderer, List<Material>> cache
                && cache.TryGetValue(_body, out var baseline))
            { baseline.Clear(); baseline.AddRange(_body.sharedMaterials); }
            block.RefreshRenderers();
        }
    }

    private void OnSkill(EventCombatSkillPresentation e)
    {
        var data = e.m_SkillPresentationData;
        if (!_ready || _actor == null || data == null || data.PerformerGuid != _actor.GetActorGuid()) return;
        _clip = (data.SkillId ?? "").Contains("serpents_sway") ? "defend" : "attack";
        _since = Time.unscaledTime;
    }

    private void OnTarget(EventCombatPresentationSkillTarget e)
    {
        if (_ready && e.m_Actor != null && e.m_Actor.GetActorGuid() == _actor.GetActorGuid() && e.m_PerformerGuid != _actor.GetActorGuid())
        { _clip = "defend"; _since = Time.unscaledTime; }
    }

    private void OnDeath(EventCombatActorDeath e)
    {
        if (e.m_combatActor != null && _actor != null && e.m_combatActor.GetActorGuid() == _actor.GetActorGuid())
        { _dead = true; RestorePose(); }
    }

    private void LateUpdate()
    {
        if (!_ready || _actor == null || _body == null || !_body.gameObject.activeInHierarchy) return;
        if (!Enabled) { Release(); _failed = true; return; }
        HideWeapons();
        if (_dead) return; // The native rig owns death and removal.
        var clip = _modelData.Clips[_clip];
        if (!clip.Loop && Time.unscaledTime - _since >= clip.Duration) { _clip = "idle"; _since = Time.unscaledTime; clip = _modelData.Clips[_clip]; }
        var pose = clip.Sample(Time.unscaledTime - _since);
        foreach (var t in new[] { _rs, _re, _rw, _ls, _le, _lw }) _saved[t] = t.localRotation;
        var side = _body.transform.TransformDirection(Vector3.right).normalized;
        Solve(_rs, _re, _rw, _chest.TransformPoint(_chestBind.MultiplyPoint3x4(V(pose.Right))), side - Vector3.up);
        Solve(_ls, _le, _lw, _chest.TransformPoint(_chestBind.MultiplyPoint3x4(V(pose.Left))), -side - Vector3.up);
        var chestToWorld = _chest.localToWorldMatrix * _chestBind;
        _rw.rotation = chestToWorld.rotation * Quaternion.FromToRotation(Vector3.up, V(pose.Spear).normalized) * _wristRest.rotation;
    }

    private void HideWeapons()
    {
        if (!_ready || _actor == null) return;
        if (Time.unscaledTime >= _nextWeaponScan)
        {
            _nextWeaponScan = Time.unscaledTime + .5f;
            foreach (var r in _actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r != _body && r.sharedMesh != null && r.sharedMesh.name.StartsWith("msh_hellion_wpn", StringComparison.Ordinal))
                { if (!_hidden.ContainsKey(r)) _hidden[r] = r.forceRenderingOff; r.forceRenderingOff = true; }
        }
        foreach (var r in _hidden.Keys) if (r != null) r.forceRenderingOff = true;
    }

    private static void Solve(Transform shoulder, Transform elbow, Transform wrist, Vector3 target, Vector3 pole)
    {
        float a = Vector3.Distance(shoulder.position, elbow.position), b = Vector3.Distance(elbow.position, wrist.position);
        var delta = target - shoulder.position;
        if (a < .0001f || b < .0001f || delta.sqrMagnitude < .0000001f) return;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
        var axis = delta.normalized; var bend = Vector3.ProjectOnPlane(pole, axis).normalized;
        if (bend.sqrMagnitude < .5f) bend = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis).normalized;
        float along = (a*a - b*b + d*d) / (2*d), height = Mathf.Sqrt(Mathf.Max(0, a*a - along*along));
        var elbowGoal = shoulder.position + axis * along + bend * height;
        shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, elbowGoal - shoulder.position) * shoulder.rotation;
        elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, shoulder.position + axis * d - elbow.position) * elbow.rotation;
    }

    private void RestorePose() { foreach (var p in _saved) if (p.Key != null) p.Key.localRotation = p.Value; _saved.Clear(); }
    private void OnDisable() => RestorePose();
    private void OnDestroy() => Release();
    private void Release()
    {
        RestorePose();
        if (_listening) { EventManager.RemoveListener<EventCombatSkillPresentation>(OnSkill); EventManager.RemoveListener<EventCombatPresentationSkillTarget>(OnTarget); EventManager.RemoveListener<EventCombatActorDeath>(OnDeath); _listening = false; }
        if (_body != null && _originalMesh != null)
        {
            _body.sharedMesh = _originalMesh; _body.sharedMaterials = _originalMaterials; _body.localBounds = _originalBounds;
            RefreshMaterials();
        }
        foreach (var p in _hidden) if (p.Key != null) p.Key.forceRenderingOff = p.Value;
        _hidden.Clear(); _ready = false;
        if (_mesh != null) Destroy(_mesh); if (_material != null) Destroy(_material);
        foreach (var t in _textures)
        {
            var entry = TextureCache.FirstOrDefault(p => p.Value.Texture == t);
            if (entry.Value == null) { if (t != null) Destroy(t); continue; }
            if (--entry.Value.Users <= 0) { TextureCache.Remove(entry.Key); if (t != null) Destroy(t); }
        }
        _textures.Clear(); _mesh = null; _material = null; _body = null; _originalMesh = null; _modelData = null;
    }
}

[HarmonyPatch(typeof(ActorBhv), "Update")]
internal static class ShieldbreakerActorPresentationPatch
{
    private static void Postfix(ActorBhv __instance) => ShieldbreakerModel.Attach(__instance);
}
