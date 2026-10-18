using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private bool _estateSkyBuilt;
    private bool BuildEstateSky(int layer)
    {
        if (_estateSkyBuilt) return true;
        var sky=Art.TownBackdrop;
        var path=Session.Current?.Dd1.PathOf("campaign","town","town_bg.png");
        if (sky==null && path!=null && !Art.ImageFinished(path)) return false;
        var material=new Material(Shader.Find("Sprites/Default"))
        { name="Hamlet overcast estate sky",mainTexture=sky!=null?sky:Texture2D.grayTexture };
        _details.Add(material);
        float w=HamletVista.SkyWidth/2,z=HamletVista.SkyDepth;
        var mesh=new Mesh { name="Distant estate sky",vertices=new[]
        { new Vector3(-w,HamletVista.SkyBottom,z),new Vector3(-w,HamletVista.SkyTop,z),
          new Vector3(w,HamletVista.SkyTop,z),new Vector3(w,HamletVista.SkyBottom,z) },
            uv=new[] {new Vector2(0,.65f),Vector2.up,Vector2.one,new Vector2(1,.65f)},triangles=new[] {0,1,2,0,2,3} };
        _details.Add(mesh);Surface(mesh,material,layer);
        _estateSkyBuilt=true;return true;
    }
}
