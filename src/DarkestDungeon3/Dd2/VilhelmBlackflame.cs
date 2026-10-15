using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DarkestDungeon3.Dd2;

/// <summary>Original Onyx Blade flame RGBA, attached to the original weapon bone.</summary>
internal sealed class VilhelmBlackflame : IDisposable
{
    private GameObject _root;
    private ParticleSystem _particles;
    private ParticleSystem _coreParticles;
    private GameObject _coreRoot;
    private Material _material;
    private Texture2D _texture;
    private ParticleSystemRenderer _renderer;
    private AsyncOperationHandle<Material> _template;
    private static IResourceLocation _templateLocation;
    private float _weight;
    internal bool Visible => _weight>.01f;
    internal int ParticleCount => (_particles!=null ? _particles.particleCount : 0)+(_coreParticles!=null ? _coreParticles.particleCount : 0);
    internal bool MaterialReady => _material!=null;
    private float _diagnoseAt;
    private int _diagnosed;

    internal static VilhelmBlackflame Create(Transform weapon,GltfModel model,Matrix4x4 bind,int layer)
    {
        string path=Path.Combine(SoulsHeroClasses.Folder,"vfx","blackflame.png");
        if (!File.Exists(path)) { Plugin.Log.LogWarning("[vilhelm] private black-flame atlas is missing"); return null; }
        var result=new VilhelmBlackflame();
        try { result.Build(weapon,model,bind,layer,File.ReadAllBytes(path)); return result; }
        catch (Exception e) { result.Dispose(); Plugin.Log.LogWarning("[vilhelm] black flame: "+e.Message); return null; }
    }
    private void Build(Transform weapon,GltfModel model,Matrix4x4 bind,int layer,byte[] bytes)
    {
        if (bytes.Length>8*1024*1024) throw new FormatException("Oversized flame atlas");
        _texture=new Texture2D(2,2,TextureFormat.RGBA32,false) { name="Onyx Blade native flame",wrapMode=TextureWrapMode.Clamp };
        if (!_texture.LoadImage(bytes,true) || _texture.width!=1024 || _texture.height!=1024) throw new FormatException("Invalid 8x8 flame atlas");
        // Native alpha contains the black core too. Preserve RGBA instead of deriving alpha from brightness.
        // Shipping shaders live in Addressable bundles, outside Shader.Find's registry.
        // Load the real combat particle material so its shader and variants stay alive.
        const string asset="Assets/Data/Biome/City/VFX/material/vfx_burningcity_torch_fire_soft_sim_03.mat";
        if (_templateLocation==null)
            foreach (var locator in Addressables.ResourceLocators)
                foreach (var key in locator.Keys)
                    if (locator.Locate(key,typeof(Material),out var locations))
                        foreach (var location in locations)
                            if (string.Equals(location.InternalId,asset,StringComparison.OrdinalIgnoreCase)) _templateLocation=location;
        if (_templateLocation==null) throw new InvalidOperationException("Native combat flame material is unavailable");
        _template=Addressables.LoadAssetAsync<Material>(_templateLocation);
        var vertices=model.Primitives.Where(p => p.Mesh.Contains("Onyx")).SelectMany(p => Enumerable.Range(0,p.VertexCount)
            .Select(i => bind.MultiplyPoint3x4(new Vector3(p.Positions[i*3],p.Positions[i*3+1],p.Positions[i*3+2])))).ToArray();
        if (vertices.Length==0) throw new FormatException("Onyx Blade geometry missing");
        var bounds=new Bounds(vertices[0],Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
        int axis=bounds.size.x>bounds.size.y ? bounds.size.x>bounds.size.z ? 0 : 2 : bounds.size.y>bounds.size.z ? 1 : 2;
        var direction=axis==0 ? Vector3.right : axis==1 ? Vector3.up : Vector3.forward;
        float length=bounds.size[axis];
        _root=new GameObject("Onyx Blade black flame") { layer=0 };
        _root.transform.SetParent(weapon,false);
        _root.transform.localPosition=bounds.center;
        _root.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
        _particles=_root.AddComponent<ParticleSystem>(); _particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=_particles.main; main.playOnAwake=false; main.loop=true; main.maxParticles=80;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime=true;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.5f); main.startSpeed=new ParticleSystem.MinMaxCurve(.04f,.12f);
        main.startSize=new ParticleSystem.MinMaxCurve(.28f,.42f); main.startColor=new Color(.75f,.71f,.82f,.65f);
        var shape=_particles.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(.025f,length*.72f,.025f);
        var emission=_particles.emission; emission.rateOverTime=0;
        var sheet=_particles.textureSheetAnimation; sheet.enabled=true; sheet.numTilesX=8; sheet.numTilesY=8;
        sheet.animation=ParticleSystemAnimationType.WholeSheet; sheet.timeMode=ParticleSystemAnimationTimeMode.FPS; sheet.fps=30;
        sheet.startFrame=new ParticleSystem.MinMaxCurve(0,1);
        var colour=_particles.colorOverLifetime; colour.enabled=true;
        var gradient=new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.7f,.6f),new GradientAlphaKey(0,1) }); colour.color=gradient;
        _renderer=_particles.GetComponent<ParticleSystemRenderer>();
        _renderer.renderMode=ParticleSystemRenderMode.Billboard; _renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        // Native FFX440461 has a near-black layer and separate pale flame layers.
        // Share the original atlas, but retain both value ranges instead of white fuzz.
        _coreRoot=UnityEngine.Object.Instantiate(_root,weapon,false);
        _coreRoot.name="Onyx Blade dark flame";
        _coreParticles=_coreRoot.GetComponent<ParticleSystem>();
        var core=_coreParticles.main; core.maxParticles=40;
        core.startSize=new ParticleSystem.MinMaxCurve(.32f,.48f);
        core.startColor=new Color(.008f,.006f,.012f,.95f);
        _coreParticles.GetComponent<ParticleSystemRenderer>().sortingFudge=.05f;
        _coreParticles.Play();
        _particles.Play();
        Plugin.Log.LogInfo($"[vilhelm] black-flame atlas ready; original weapon extent {length:0.00}m, axis {axis}");
    }
    private void PrepareMaterial()
    {
        if (_material!=null || !_template.IsValid() || !_template.IsDone || _template.Status!=AsyncOperationStatus.Succeeded || _template.Result==null) return;
        // This shader's UniversalForward pass uses the transparent queue. Retain
        // the native material's queue and blend/depth states.
        _material=new Material(_template.Result) { name="Onyx Blade black flame" };
        var tags=Enumerable.Range(0,_material.shader.passCount).Select(i => _material.shader.FindPassTagValue(i,new UnityEngine.Rendering.ShaderTagId("LightMode")).name).ToArray();
        foreach (string property in new[] { "_Texture","_MainTex" }) if (_material.HasProperty(property)) _material.SetTexture(property,_texture);
        foreach (string property in new[] { "_Color","_BaseColor","_Tint" }) if (_material.HasProperty(property)) _material.SetColor(property,Color.white);
        void Set(string property,float value) { if (_material.HasProperty(property)) _material.SetFloat(property,value); }
        Set("_Alpha",1); Set("_Power",1); Set("_RGBAlpha",0); Set("_Use_LerpColor",0);
        Set("_CameraFade",0); Set("_DepthFade",0); Set("_Cull",0);
        // The DS3 atlas has independent alpha, including its near-black core.
        _material.DisableKeyword("_RGBALPHA_ON");
        _material.DisableKeyword("_USE_LERPCOLOR_ON"); _material.DisableKeyword("_CAMERAFADE_ON"); _material.DisableKeyword("_DEPTHFADE_ON");
        _renderer.sharedMaterial=_material;
        _coreParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial=_material;
        Plugin.Log.LogInfo("[vilhelm] native flame material ready: "+_material.shader.name+"; queue "+_material.renderQueue+"; passes "+string.Join(",",tags));
    }
    internal void Update(bool active,bool ignition,float delta)
    {
        if (_particles==null) return;
        PrepareMaterial();
        _weight=Mathf.MoveTowards(_weight,active ? 1 : 0,Mathf.Max(0,delta)*8);
        var emission=_particles.emission; emission.rateOverTime=_weight*(ignition ? 35 : 20);
        var coreEmission=_coreParticles.emission; coreEmission.rateOverTime=_weight*(ignition ? 85 : 50);
        if (_weight>.5f && _diagnosed<3 && Time.unscaledTime>_diagnoseAt && Environment.GetCommandLineArgs().Contains("-dd3-vilhelm-check"))
        {
            _diagnoseAt=Time.unscaledTime+1; _diagnosed++;
            var renderer=_particles.GetComponent<ParticleSystemRenderer>();
            Plugin.Log.LogInfo($"[vilhelm flame sample] count={ParticleCount}; playing={_particles.isPlaying}; visible={renderer.isVisible}; enabled={renderer.enabled}; hidden={renderer.forceRenderingOff}; bounds={renderer.bounds}; scale={_root.transform.lossyScale}");
        }
    }
    public void Dispose()
    {
        if (_root!=null) UnityEngine.Object.Destroy(_root);
        if (_coreRoot!=null) UnityEngine.Object.Destroy(_coreRoot);
        if (_material!=null) UnityEngine.Object.Destroy(_material);
        if (_texture!=null) UnityEngine.Object.Destroy(_texture);
        if (_template.IsValid()) Addressables.Release(_template);
    }
}
