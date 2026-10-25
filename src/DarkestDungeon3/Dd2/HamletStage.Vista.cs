using DarkestDungeon3.Core.Presentation;
using System.IO;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private bool _estateSkyBuilt;
    private string _estateSkyPath,_cliffPaintPath,_earthPaintPath,_pavingPaintPath,_turfPaintPath;
    private void PrepareTownArt()
    {
        string folder=Path.Combine(Plugin.NativeRoomSceneryPath.Value,"HamletMaterials");
        _estateSkyPath=Path.Combine(folder,"hamlet-sky-v17.png");
        _cliffPaintPath=Path.Combine(folder,"hamlet-cliff-v17.png");
        _earthPaintPath=Path.Combine(folder,"hamlet-earth-v23.png");
        _pavingPaintPath=Path.Combine(folder,"hamlet-cobbles-v23.png");
        _turfPaintPath=Path.Combine(folder,"hamlet-turf-v92.png");
        if (!File.Exists(_estateSkyPath)) _estateSkyPath=null;
        if (!File.Exists(_cliffPaintPath)) _cliffPaintPath=null;
        if (!File.Exists(_earthPaintPath)) _earthPaintPath=null;
        if (!File.Exists(_pavingPaintPath)) _pavingPaintPath=null;
        if (!File.Exists(_turfPaintPath)) _turfPaintPath=null;
        if (_estateSkyPath!=null) Art.PrepareImage(_estateSkyPath);
        if (_cliffPaintPath!=null) Art.PrepareImage(_cliffPaintPath);
        if (_earthPaintPath!=null) Art.PrepareImage(_earthPaintPath);
        if (_pavingPaintPath!=null) Art.PrepareImage(_pavingPaintPath);
        if (_turfPaintPath!=null) Art.PrepareImage(_turfPaintPath);
        if (Session.Current?.Dd1 is { } dd1) Art.PrepareImage(dd1.PathOf("campaign","town","town_bg.png"));
    }
    private bool BuildEstateSky(int layer)
    {
        if (_estateSkyBuilt) return true;
        if (_estateSkyPath!=null && !Art.ImageFinished(_estateSkyPath)) return false;
        var sky=_estateSkyPath!=null?Art.Png(_estateSkyPath):null;
        bool painted=sky!=null;
        sky ??= Art.TownBackdrop;
        var path=Session.Current?.Dd1.PathOf("campaign","town","town_bg.png");
        if (sky==null && path!=null && !Art.ImageFinished(path)) return false;
        var material=new Material(Shader.Find("Sprites/Default"))
        { name="Hamlet overcast estate sky",mainTexture=sky!=null?sky:Texture2D.grayTexture };
        _details.Add(material);
        float w=HamletVista.SkyWidth/2,z=HamletVista.SkyDepth,x=painted?-80:0;
        var mesh=new Mesh { name="Distant estate sky",vertices=new[]
        { new Vector3(x-w,HamletVista.SkyBottom,z),new Vector3(x-w,HamletVista.SkyTop,z),
          new Vector3(x+w,HamletVista.SkyTop,z),new Vector3(x+w,HamletVista.SkyBottom,z) },
            uv=new[] {new Vector2(0,painted?0:.65f),Vector2.up,Vector2.one,new Vector2(1,painted?0:.65f)},triangles=new[] {0,1,2,0,2,3} };
        _details.Add(mesh);Surface(mesh,material,layer);
        _estateSkyBuilt=true;return true;
    }
}
