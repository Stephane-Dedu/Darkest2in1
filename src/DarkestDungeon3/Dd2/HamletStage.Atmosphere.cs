using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private Material _smokeMaterial;

    private void AddDepthMist(int layer)
    {
        const int width=192,height=64;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,false)
        { name="Hamlet soft distant mist",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
        var pixels=new Color32[width*height];
        for (int y=0;y<height;y++) for (int x=0;x<width;x++)
        {
            float u=x/(float)(width-1),v=y/(float)(height-1);
            float ridge=.49f+.065f*Mathf.Sin(u*17)+.035f*Mathf.Cos(u*29);
            float alpha=Mathf.Exp(-Mathf.Pow((v-ridge)/.22f,2))*Mathf.Pow(Mathf.Sin(u*Mathf.PI),.7f);
            alpha*=Mathf.Clamp01(v*9)*Mathf.Clamp01((1-v)*9);
            pixels[y*width+x]=new Color32(47,59,77,(byte)(255*alpha));
        }
        texture.SetPixels32(pixels); texture.Apply(false,true); _details.Add(texture);
        foreach (var band in HamletSetDressing.Mists)
        {
            var material=new Material(Shader.Find("Sprites/Default"))
            { name="Hamlet distant mist band",mainTexture=texture,color=new Color(1,1,1,band.Opacity) };
            _details.Add(material);
            var mesh=new Mesh { name=material.name,
                vertices=new[] { new Vector3(band.X-band.Width/2,band.Y-band.Height/2,band.Z),
                    new Vector3(band.X-band.Width/2,band.Y+band.Height/2,band.Z),
                    new Vector3(band.X+band.Width/2,band.Y+band.Height/2,band.Z),
                    new Vector3(band.X+band.Width/2,band.Y-band.Height/2,band.Z) },
                uv=new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right },triangles=new[] { 0,1,2,0,2,3 } };
            _details.Add(mesh); Surface(mesh,material,layer);
        }
    }

    private void AddChimneySmoke(Transform building,Vector3 localTop)
    {
        if (_smokeMaterial==null)
        {
            const int size=64;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            { name="Hamlet soft smoke",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            var pixels=new Color32[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                float u=(x+.5f)/size*2-1,v=(y+.5f)/size*2-1;
                float radius=Mathf.Sqrt(u*u+v*v);
                float edge=Mathf.Clamp01((1-radius)*2.5f);
                float grain=.78f+.12f*Mathf.Sin(u*17+v*7)+.1f*Mathf.Sin(u*9-v*13);
                pixels[y*size+x]=new Color32(255,255,255,(byte)(190*edge*edge*grain));
            }
            texture.SetPixels32(pixels); texture.Apply(false,true); _details.Add(texture);
            _smokeMaterial=new Material(Shader.Find("Sprites/Default"))
            { name="Hamlet chimney smoke",mainTexture=texture };
            _details.Add(_smokeMaterial);
        }
        var chimney=new GameObject("Hamlet chimney smoke");
        chimney.transform.SetParent(_root,false);
        chimney.transform.position=building.TransformPoint(localTop)+Vector3.up*.12f;
        var particles=chimney.AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;
        main.loop=true; main.prewarm=true; main.duration=6; main.maxParticles=20;
        main.simulationSpace=ParticleSystemSimulationSpace.World; main.useUnscaledTime=true;
        main.startLifetime=new ParticleSystem.MinMaxCurve(3.5f,5);
        main.startSpeed=new ParticleSystem.MinMaxCurve(.55f,.8f);
        main.startSize=new ParticleSystem.MinMaxCurve(.4f,.75f);
        main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=new Color(.46f,.48f,.52f,.2f);
        var emission=particles.emission; emission.rateOverTime=2;
        var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=8;
        shape.radius=.08f; shape.rotation=new Vector3(-90,0,0);
        var wind=particles.velocityOverLifetime; wind.enabled=true; wind.space=ParticleSystemSimulationSpace.World;
        wind.x=.16f; wind.y=0; wind.z=.025f;
        var growth=particles.sizeOverLifetime; growth.enabled=true;
        growth.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(1,2.8f)));
        var fade=particles.colorOverLifetime; fade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(.5f,.5f),new GradientAlphaKey(0,1) });
        fade.color=new ParticleSystem.MinMaxGradient(gradient);
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=_smokeMaterial; renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        if (MotionEnabled) particles.Play();
    }

    private void ApplyMotion()
    {
        if (_root==null) return;
        foreach (var particles in _root.GetComponentsInChildren<ParticleSystem>())
            if (MotionEnabled) particles.Play();
            else particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        MoveCamera(1);
    }
}
