using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Code.Actor;
using Assets.Code.Combat;
using Assets.Code.Rendering.MaterialPropertyBlocks;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json;

namespace DarkestDungeon3.Dd2;

/// <summary>Attaches an owner's animated export to its separate test hero, in both crawl and combat.</summary>
internal sealed class ExportedHeroes : MonoBehaviour
{
    internal sealed class Pack
    {
        internal GltfModel Model;
        internal Dictionary<string,byte[]> Images;
        internal WalkMotion Walk;
    }
    internal sealed class WalkMotion
    {
        public string Clip { get; set; }
        public float Duration { get; set; }
        public float[] Distances { get; set; }
    }
    private static Task<Pack> _reading;
    private static Pack _pack;
    private static string _path;
    private static bool _failed;
    private float _next;

    internal static void Prepare()
    {
        string path = SoulsHeroClasses.ModelPath;
        if (_path != path) { _path = path; _reading = null; _pack = null; _failed = false; }
        if (_pack != null || _failed || _reading != null || !File.Exists(path)) return;
        _reading = Task.Run(() =>
        {
            var model = GltfModel.Load(path);
            foreach (string clip in new[] { "a002_000000","a002_004000","a025_030000","a025_030010","a025_030100","a245_038700",
                "a000_042003","a000_042103","a000_042303","a000_005002","a000_017002","a000_018002" })
                if (!model.Animations.Any(a => a.Name == clip)) throw new FormatException("Missing Vilhelm animation "+clip);
            var images = new Dictionary<string,byte[]>();
            foreach (string image in model.Materials.Select(m => m.BaseColor).Where(p => p != null).Distinct())
            {
                if (new FileInfo(image).Length > 16*1024*1024) throw new FormatException("Oversized hero texture");
                images[image] = File.ReadAllBytes(image);
            }
            WalkMotion walk=null;
            string motion=Path.Combine(Path.GetDirectoryName(path),"motion.json");
            if (File.Exists(motion))
            {
                if (new FileInfo(motion).Length>65536) throw new FormatException("Oversized walk calibration");
                walk=JsonConvert.DeserializeObject<WalkMotion>(File.ReadAllText(motion));
                if (walk?.Clip!="a002_020000" || walk.Distances==null || walk.Distances.Length>512
                    || !model.Animations.Any(a => a.Name==walk.Clip && Math.Abs(a.Duration-walk.Duration)<.01f)) throw new FormatException("Invalid native walk clip");
                ExportedHeroMotion.AdvanceWalkByMotion(0,0,100,walk.Duration,walk.Distances);
            }
            if (walk==null && model.Animations.Any(a => a.Name=="a002_020000")) throw new FormatException("Native walk calibration is missing");
            return new Pack { Model = model, Images = images,Walk=walk };
        });
    }

    internal static Pack Data()
    {
        Prepare();
        if (_pack != null || _failed || _reading == null || !_reading.IsCompleted) return _pack;
        try { _pack = _reading.GetAwaiter().GetResult(); }
        catch (Exception e) { _failed = true; Plugin.Log.LogError("[vilhelm] private model: "+e.Message); }
        return _pack;
    }

    private void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime+.25f;
        foreach (var actor in FindObjectsOfType<ActorBhv>())
            if (SoulsHeroClasses.IsVilhelm(actor) && actor.GetComponent<ExportedHeroModel>() == null)
                actor.gameObject.AddComponent<ExportedHeroModel>().Actor = actor;
    }
}

[DefaultExecutionOrder(32500)]
internal sealed class ExportedHeroModel : MonoBehaviour
{
    internal ActorBhv Actor;
    internal bool Ready { get; private set; }
    internal string SampledClip { get; private set; }
    internal float SampledTime { get; private set; }
    internal bool FlameVisible => _flame?.Visible==true;
    internal int FlameParticles => _flame?.ParticleCount ?? 0;
    internal bool FlameMaterialReady => _flame?.MaterialReady==true;
    internal bool NativeWalkReady => _walk!=null;
    internal bool BorrowedCastRim => _renderers.Any(r => r!=null && (r.sharedMaterial.IsKeywordEnabled("_RIM") || r.sharedMaterial.IsKeywordEnabled("_SOFT_RIM")));
    internal int SampledFrame { get; private set; }
    internal float BodyHeight => _root != null ? _root.transform.TransformVector(Vector3.up*_bodyHeight).magnitude : 0;
    private GameObject _root;
    private Transform[] _nodes;
    private GltfModel _model;
    private ExportedHeroes.WalkMotion _walk;
    private Dictionary<string,GltfModel.Clip> _clips;
    private readonly List<UnityEngine.Object> _made = new();
    private readonly Dictionary<string,Texture2D> _textures = new();
    private readonly Dictionary<Renderer,bool> _hidden = new();
    private Animator _animator;
    private CombatActorBhv _combat;
    private string _skill;
    private readonly List<SkinnedMeshRenderer> _renderers = new();
    private readonly ExportedHeroClock _clock = new();
    private readonly HashSet<MaterialPropertyBhv> _castBlocks=new();
    private readonly MaterialPropertyOverride[] _castPalette={ new("_RIM",false,true),new("_UseRim",0f),new("_RimLight",0f),
        new("_SOFT_RIM",false,true),new("_Use_Soft_Rim",0f),new("_Soft_Rim_Blend",0f) };
    private VilhelmBlackflame _flame;
    private AnimatorCullingMode _culling;
    private Vector3[] _fromPosition;
    private Quaternion[] _fromRotation;
    private string _state;
    private float _stateAt, _blendAt, _retryAt, _bodyHeight, _scanAt, _walkClock;
    private readonly float[] _sample = new float[4];
    private bool _failed;

    private void Update()
    {
        if (Ready || _failed || Actor == null || Actor.IsLoading || Time.unscaledTime < _retryAt) return;
        _retryAt = Time.unscaledTime+.25f;
        var pack = ExportedHeroes.Data();
        if (pack == null) return;
        _animator = Actor.GetCurrentAnimator();
        var candidates = _animator?.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.sharedMesh != null && r.bones.Length > 20).ToArray();
        var body = candidates?.Where(r => r.enabled && r.gameObject.activeInHierarchy)
            .OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
        if (body == null || body.bounds.size.y < .1f) return;
        try { Build(pack,body); }
        catch (Exception e) { _failed = true; Plugin.Log.LogError("[vilhelm] model build: "+e); Release(); }
    }

    private void Build(ExportedHeroes.Pack pack, SkinnedMeshRenderer body)
    {
        _model = pack.Model;
        _walk = pack.Walk;
        _combat=FindObjectsOfType<CombatActorBhv>().FirstOrDefault(c => c.CachedActorBhv==Actor);
        _clips = _model.Animations.GroupBy(c => c.Name).ToDictionary(g => g.Key,g => g.First());
        _root = new GameObject("DD3 Sir Vilhelm");
        _root.layer = body.gameObject.layer;
        _nodes = _model.Nodes.Select(n => new GameObject(n.Name).transform).ToArray();
        for (int i=0;i<_nodes.Length;i++)
        {
            var n = _model.Nodes[i]; var t = _nodes[i];
            t.SetParent(n.Parent >= 0 ? _nodes[n.Parent] : _root.transform,false);
            t.localPosition = V(n.Translation); t.localRotation = Q(n.Rotation); t.localScale = V(n.Scale);
            t.gameObject.layer = body.gameObject.layer;
        }
        var bones = _model.Joints.Select(i => _nodes[i]).ToArray();
        var binds = _model.InverseBind.Select(Matrix).ToArray();
        var materials = new Dictionary<int,Material>();
        foreach (var original in _model.Primitives)
        {
            var p = original;
            var info = p.Material >= 0 && p.Material < _model.Materials.Count ? _model.Materials[p.Material] : null;
            Texture2D texture = Texture(info?.BaseColor,pack);
            if (info?.Mask == true && texture != null)
            {
                var pixels = texture.GetPixels32(); int width = texture.width, height = texture.height;
                p = p.CutOut((u,v) => pixels[Mathf.Clamp((int)((v-Mathf.Floor(v))*height),0,height-1)*width
                    +Mathf.Clamp((int)((u-Mathf.Floor(u))*width),0,width-1)].a/255f,info.Cutoff);
            }
            if (info?.DoubleSided == true) p = p.WithBackFaces();
            var mesh = new Mesh { name = "Vilhelm "+p.Mesh,indexFormat = IndexFormat.UInt32 };
            int count = p.VertexCount;
            mesh.vertices = Enumerable.Range(0,count).Select(i => new Vector3(p.Positions[i*3],p.Positions[i*3+1],p.Positions[i*3+2])).ToArray();
            if (p.Normals != null) mesh.normals = Enumerable.Range(0,count).Select(i => new Vector3(p.Normals[i*3],p.Normals[i*3+1],p.Normals[i*3+2])).ToArray();
            // GltfModel already converts glTF's image origin to Unity's convention.
            if (p.Uv != null) mesh.uv = Enumerable.Range(0,count).Select(i => new Vector2(p.Uv[i*2],p.Uv[i*2+1])).ToArray();
            mesh.colors = Enumerable.Repeat(new Color(1,0,0,1),count).ToArray();
            mesh.boneWeights = Enumerable.Range(0,count).Select(i => Weight(p,i)).ToArray();
            mesh.bindposes = binds; mesh.triangles = p.Triangles; mesh.RecalculateBounds();
            if (p.Normals == null) mesh.RecalculateNormals();
            _made.Add(mesh);
            if (!materials.TryGetValue(p.Material,out var material))
            {
                material = new Material(body.sharedMaterial) { name = "Vilhelm "+info?.Name };
                if (material.HasProperty("_Base")) material.SetTexture("_Base",texture ?? Texture2D.whiteTexture);
                if (material.HasProperty("_Ink")) material.SetTexture("_Ink",Ink(info?.BaseColor,texture));
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex",texture ?? Texture2D.whiteTexture);
                materials[p.Material] = material; _made.Add(material);
            }
            var go = new GameObject(p.Mesh) { layer = body.gameObject.layer };
            go.transform.SetParent(_root.transform,false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.bones = bones;
            renderer.rootBone = bones.FirstOrDefault() ?? _root.transform;
            renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderers.Add(renderer);
        }
        var extent = _model.Extent(p => !p.Mesh.Contains("Onyx"));
        _bodyHeight = extent.Height;
        _root.transform.localScale = Vector3.one*(body.bounds.size.y/Mathf.Max(.01f,_bodyHeight));
        Transform N(string name) => _nodes.FirstOrDefault(t => t.name == name);
        Transform D(string name) => body.bones.FirstOrDefault(t => t != null && t.name == name);
        Vector3 Forward(Transform left,Transform right)
        {
            if (left == null || right == null) return Vector3.forward;
            var f = Vector3.Cross(right.position-left.position,Vector3.up); f.y=0;
            return f.sqrMagnitude>.001f ? f.normalized : Vector3.forward;
        }
        _root.transform.rotation = Quaternion.FromToRotation(Forward(N("L_Thigh"),N("R_Thigh")),Forward(D("l_Leg_HipSHJnt"),D("r_Leg_HipSHJnt")));
        var ground = D("TrajectorySHJnt") ?? Actor.transform;
        _root.transform.position = new Vector3(body.bounds.center.x,ground.position.y-extent.Bottom*_root.transform.localScale.y,body.bounds.center.z);
        _root.transform.SetParent(ground,true);
        _fromPosition = new Vector3[_nodes.Length]; _fromRotation = new Quaternion[_nodes.Length];
        _culling = _animator.cullingMode; _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Ready = true;
        int weapon=Array.FindIndex(_model.Joints,i => _nodes[i].name=="R_Weapon");
        if (weapon>=0) _flame=VilhelmBlackflame.Create(_nodes[_model.Joints[weapon]],_model,Matrix(_model.InverseBind[weapon]),body.gameObject.layer);
        HideDonor();
        Plugin.Log.LogInfo($"[vilhelm] native model ready for actor {Actor.GetActorGuid()}: {_model.Primitives.Count} meshes, {_model.TriangleCount} triangles, {_nodes.Length} nodes, {_clips.Count} original clips; body {_bodyHeight:0.00}m");
    }

    private void LateUpdate()
    {
        if (!Ready || _animator == null) return;
        HideDonor();
        // Native controllers only discover their prefab renderers. Apply their current
        // overrides to the added meshes too, preserving the export's textures.
        foreach (var block in Actor.MaterialPropertyBlocks)
            if (block != null && block.isActiveAndEnabled) foreach (var renderer in _renderers) block.Apply(renderer,false,false);
        bool stage = HeroStage.Owns(Actor);
        float now = stage ? Time.unscaledTime : Time.time;
        var nativeFrame = stage ? (Name:"crusader_idle_A",Progress:0f,Timeline:false) : _clock.Read(Actor,_animator);
        string native=nativeFrame.Name;
        var split = ExportedMoveset.Split(native,"crusader");
        if (nativeFrame.Timeline && split.Part=="") split=(split.Move,"action");
        var actor=Dd2Api.Actor(Actor.GetActorGuid());
        string selected=VilhelmKit.Find(actor?.GetSelectedSkillId())?.Key;
        string action=VilhelmKit.Find(_combat?.CurrentSkillPresentation?.SkillId)?.Key;
        if (split.Move=="idle") _skill=null;
        else if (action!=null || selected!=null) _skill=action ?? selected;
        bool castingFlame=!stage && _skill=="blackflame" && !split.Move.Contains("impact") && !split.Move.Contains("death");
        if (castingFlame)
            foreach (var block in Actor.MaterialPropertyBlocks.Where(b => b!=null && b.isActiveAndEnabled))
            {
                // Native controllers apply again at beginContextRendering, after
                // LateUpdate. Register an owned override in that same stack.
                foreach (var value in _castPalette) block.AddOverride(value,uint.MaxValue,1,this);
                _castBlocks.Add(block);
            }
        else ClearCastPalette();
        bool walking=stage && Mathf.Abs(HeroStage.WalkDistancePixels)>.0001f;
        string state = stage ? walking ? HeroStage.WalkDistancePixels<0 ? "walk-back" : "walk-forward" : "idle" : native;
        bool changed=state!=_state;
        if (changed)
        {
            for (int i=0;i<_nodes.Length;i++) { _fromPosition[i]=_nodes[i].localPosition; _fromRotation[i]=_nodes[i].localRotation; }
            _blendAt = _state == null ? now-.15f : now; _stateAt = now; _state = state;
        }
        bool impact=split.Move.Contains("impact"), death=split.Move.Contains("death");
        bool healing=_skill=="heal" && !impact && !death;
        string clipName = walking ? _walk?.Clip ?? "a002_004000" : stage || split.Move == "idle" ? "a002_000000"
            : impact ? "a000_005002" : death ? "a000_017002"
            : _skill=="blackflame" ? "a245_038700" : _skill=="sweep" ? "a025_030010"
            : _skill=="advance" ? "a025_030100"
            : healing ? split.Part=="antic" ? "a000_042003" : split.Part=="recover" ? "a000_042303" : "a000_042103"
            : _skill=="slash" ? "a025_030000" : "a002_000000";
        if (!_clips.TryGetValue(clipName,out var clip)) return;
        float duration=clip.Duration;
        bool loop=clipName=="a002_000000" || walking;
        float windup=impact || death ? 0 : clipName=="a245_038700" ? .5f : Mathf.Min(.7f,duration*.3f);
        float strike=death ? duration : clipName=="a245_038700" ? 1.4f : Mathf.Min(1.3f,duration*.65f);
        if (walking)
        {
            _walkClock = _walk==null ? ExportedHeroMotion.AdvanceWalk(_walkClock,HeroStage.WalkDistancePixels,100,duration)
                : ExportedHeroMotion.AdvanceWalkByMotion(_walkClock,HeroStage.WalkDistancePixels,
                    _root.transform.TransformVector(Vector3.forward).magnitude*HeroStage.PixelsPerUnit,duration,_walk.Distances);
        }
        float t=walking ? _walkClock%Mathf.Max(.01f,duration)
            : healing && !stage ? nativeFrame.Progress*duration : loop ? Time.unscaledTime%Mathf.Max(.01f,duration)
            : split.Part=="" ? Mathf.Min(now-_stateAt,duration)
            : ExportedHeroMotion.TimeInClip(split.Part,nativeFrame.Progress,windup,strike,duration);
        SampledClip=clipName; SampledTime=t;
        if (changed) Plugin.Log.LogInfo($"[vilhelm pose] {Actor.GetActorGuid()} {state}; {_skill ?? "stance"} -> {clipName} at {t:0.000}s; {split.Part} {nativeFrame.Progress:0.00}; timeline={nativeFrame.Timeline}");
        for (int i=0;i<_nodes.Length;i++)
        {
            _nodes[i].localPosition=V(_model.Nodes[i].Translation); _nodes[i].localRotation=Q(_model.Nodes[i].Rotation);
            _nodes[i].localScale=V(_model.Nodes[i].Scale);
        }
        foreach (var channel in clip.Channels)
        {
            GltfModel.Sample(channel,t,_sample); var node=_nodes[channel.Node];
            if (channel.Path=='r') node.localRotation=Q(_sample);
            else if (channel.Path=='t') node.localPosition=V(_sample);
            else node.localScale=V(_sample);
        }
        float blend = Mathf.SmoothStep(0,1,Mathf.Clamp01((now-_blendAt)/.15f));
        if (blend<1)
            for (int i=0;i<_nodes.Length;i++)
            {
                _nodes[i].localPosition=Vector3.Lerp(_fromPosition[i],_nodes[i].localPosition,blend);
                _nodes[i].localRotation=Quaternion.Slerp(_fromRotation[i],_nodes[i].localRotation,blend);
            }
        bool buff=actor?.BuffContainer?.GetInstances().Any(b => b.Definition.Id=="cru_radiance_melee_bonus"
            || b.Definition.Id=="cru_radiance_u_melee_bonus")==true;
        bool ignition=!stage && clipName=="a245_038700" && t>=.5666667f && t<=1.4f;
        _flame?.Update(!stage && buff || ignition,ignition,Time.unscaledDeltaTime);
        SampledFrame=Time.frameCount;
        if (Environment.GetCommandLineArgs().Contains("-dd3-vilhelm-check")) Ui.VilhelmCombatCheck.ObserveModel(this);
    }

    private void HideDonor()
    {
        if (Time.unscaledTime >= _scanAt)
        {
            _scanAt = Time.unscaledTime+.5f;
            foreach (var renderer in Actor.GetComponentsInChildren<Renderer>(true))
                if ((renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && !renderer.transform.IsChildOf(_root.transform) && !_hidden.ContainsKey(renderer))
                    _hidden.Add(renderer,renderer.enabled);
        }
        foreach (var renderer in _hidden.Keys) if (renderer != null) renderer.enabled=false;
    }

    private Texture2D Texture(string path,ExportedHeroes.Pack pack)
    {
        if (path == null || !pack.Images.TryGetValue(path,out var bytes)) return null;
        if (_textures.TryGetValue(path,out var cached)) return cached;
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,true);
        _made.Add(texture);
        if (!ImageConversion.LoadImage(texture,bytes)) throw new FormatException("Invalid hero texture "+path);
        if (texture.width>4096 || texture.height>4096) throw new FormatException("Oversized decoded hero texture");
        // DS3's near-black metal diffuse expects a separate specular reflection pass.
        // DD2's painted shader has no such pass. Lift that owned diffuse copy while
        // retaining native detail, color and alpha; source PNGs remain untouched.
        var pixels=texture.GetPixels32();
        var tone=Enumerable.Range(0,256).Select(i => (byte)(255*.85f*Mathf.Pow(i/255f,.6f))).ToArray();
        byte Paint(byte value) => tone[value];
        for (int i=0;i<pixels.Length;i++)
        { var p=pixels[i]; pixels[i]=new Color32(Paint(p.r),Paint(p.g),Paint(p.b),p.a); }
        texture.SetPixels32(pixels); texture.Apply(true,false);
        _textures[path] = texture; return texture;
    }

    private Texture2D Ink(string path,Texture2D original)
    {
        if (original==null) return Texture2D.whiteTexture;
        string key="ink|"+path;
        if (_textures.TryGetValue(key,out var cached)) return cached;
        var pixels=original.GetPixels32();
        for (int i=0;i<pixels.Length;i++)
        {
            var p=pixels[i];
            // DD2's ink is painted shading, while the DS3 export is a dark PBR albedo.
            // Lift that shading into the hero shader's usable range. Original colour,
            // UVs, geometry and fine texture detail stay in the separate _Base map.
            float luma=(p.r*.2126f+p.g*.7152f+p.b*.0722f)/255f;
            byte shade=(byte)(255*(.35f+.65f*Mathf.Sqrt(luma)));
            pixels[i]=new Color32(shade,shade,shade,255);
        }
        var texture=new Texture2D(original.width,original.height,TextureFormat.RGBA32,true) { name="Vilhelm painted shading" };
        texture.SetPixels32(pixels); texture.Apply(true,true); _made.Add(texture);
        return _textures[key]=texture;
    }
    private static BoneWeight Weight(GltfModel.Primitive p,int i)
    {
        if (p.Joints == null || p.Weights == null) return new BoneWeight { boneIndex0=0,weight0=1 };
        float sum=Enumerable.Range(0,4).Sum(k => p.Weights[i*4+k]);
        if (sum<=0) return new BoneWeight { boneIndex0=0,weight0=1 };
        return new BoneWeight { boneIndex0=p.Joints[i*4],boneIndex1=p.Joints[i*4+1],boneIndex2=p.Joints[i*4+2],boneIndex3=p.Joints[i*4+3],
            weight0=p.Weights[i*4]/sum,weight1=p.Weights[i*4+1]/sum,weight2=p.Weights[i*4+2]/sum,weight3=p.Weights[i*4+3]/sum };
    }
    private static Vector3 V(float[] a) => new(a[0],a[1],a[2]);
    private static Quaternion Q(float[] a) => new(a[0],a[1],a[2],a[3]);
    private static Matrix4x4 Matrix(float[] a) => new(new Vector4(a[0],a[1],a[2],a[3]),new Vector4(a[4],a[5],a[6],a[7]),
        new Vector4(a[8],a[9],a[10],a[11]),new Vector4(a[12],a[13],a[14],a[15]));
    private void Release()
    {
        Ready=false;
        ClearCastPalette();
        foreach (var renderer in _hidden) if (renderer.Key != null) renderer.Key.enabled=renderer.Value;
        if (_animator != null) _animator.cullingMode=_culling;
        _flame?.Dispose(); _flame=null;
        foreach (var material in _renderers.Where(r => r!=null).SelectMany(r => r.sharedMaterials).Distinct())
            if (material!=null && !_made.Contains(material)) Destroy(material);
        if (_root != null) Destroy(_root);
        foreach (var item in _made) if (item != null) Destroy(item);
        _made.Clear(); _hidden.Clear(); _textures.Clear(); _renderers.Clear();
    }
    private void OnDestroy() => Release();

    private void ClearCastPalette()
    {
        foreach (var block in _castBlocks)
            if (block!=null) foreach (var value in _castPalette) block.RemoveOverride(value,this);
        _castBlocks.Clear();
    }
}
