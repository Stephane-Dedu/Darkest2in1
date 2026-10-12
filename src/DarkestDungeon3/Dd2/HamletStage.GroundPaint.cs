using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private Texture2D GroundPaint(Texture soil,Texture stone,Texture grass)
    {
        const int size=2048,fieldSize=256;
        var earth=ReadPaint(soil); var gravel=ReadPaint(stone); var turf=grass!=null?ReadPaint(grass):null;
        var pixels=new Color32[size*size]; var cover=new float[fieldSize*fieldSize];
        // Route/footprint distances are evaluated on a bounded field, not four million times in the bake.
        if (turf!=null) for (int y=0;y<fieldSize;y++) for (int x=0;x<fieldSize;x++)
            cover[y*fieldSize+x]=HamletVegetation.Cover(
                Mathf.Lerp(HamletLayout.MinX,HamletLayout.MaxX,(float)x/(fieldSize-1)),
                Mathf.Lerp(HamletLayout.MinZ,HamletLayout.MaxZ,(float)y/(fieldSize-1)));
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float px=Mathf.Lerp(HamletLayout.MinX,HamletLayout.MaxX,(x+.5f)/size);
            float pz=Mathf.Lerp(HamletLayout.MinZ,HamletLayout.MaxZ,(y+.5f)/size);
            var sample=HamletGroundPaint.At(px,pz);
            Color a=PaintTile(earth,px/10,pz/3),b=PaintTile(gravel,px/5.5f+.37f,pz/5.5f+.19f);
            var color=Color.Lerp(a,b,sample.Gravel);
            if (turf!=null)
            {
                float fx=(x+.5f)/size*(fieldSize-1),fy=(y+.5f)/size*(fieldSize-1);
                int ix=(int)fx,iy=(int)fy;
                float weight=Mathf.Lerp(Mathf.Lerp(cover[iy*fieldSize+ix],cover[iy*fieldSize+ix+1],fx-ix),
                    Mathf.Lerp(cover[(iy+1)*fieldSize+ix],cover[(iy+1)*fieldSize+ix+1],fx-ix),fy-iy);
                color=Color.Lerp(color,PaintTile(turf,px/7.5f,pz/7.5f),weight);
            }
            pixels[y*size+x]=new Color(color.r*sample.Tint.X,color.g*sample.Tint.Y,color.b*sample.Tint.Z,1);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
        { name="Hamlet world earth, grass and worn gravel",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4 };
        _details.Add(texture); texture.SetPixels32(pixels); texture.Apply(true,true); return texture;
    }

    private Texture2D ValleyPaint(Texture soil,Texture stone,Texture grass)
    {
        const int size=2048,fieldSize=256;
        var earth=ReadPaint(soil); var gravel=ReadPaint(stone); var turf=grass!=null?ReadPaint(grass):earth;
        var cover=new float[fieldSize*fieldSize];
        for (int y=0;y<fieldSize;y++) for (int x=0;x<fieldSize;x++)
            cover[y*fieldSize+x]=HamletLandscape.GrassCover(
                Mathf.Lerp(HamletLandscape.MinX,HamletLandscape.MaxX,(float)x/(fieldSize-1)),
                Mathf.Lerp(HamletLandscape.MinZ,HamletLandscape.MaxZ,(float)y/(fieldSize-1)));
        var pixels=new Color32[size*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float px=Mathf.Lerp(HamletLandscape.MinX,HamletLandscape.MaxX,(x+.5f)/size);
            float pz=Mathf.Lerp(HamletLandscape.MinZ,HamletLandscape.MaxZ,(y+.5f)/size);
            var sample=HamletGroundPaint.At(px,pz);
            Color soilColor=PaintTile(earth,px/10,pz/3),grassColor=PaintTile(turf,px/7.5f,pz/7.5f);
            float fx=(x+.5f)/size*(fieldSize-1),fy=(y+.5f)/size*(fieldSize-1);
            int ix=(int)fx,iy=(int)fy;
            float weight=Mathf.Lerp(Mathf.Lerp(cover[iy*fieldSize+ix],cover[iy*fieldSize+ix+1],fx-ix),
                Mathf.Lerp(cover[(iy+1)*fieldSize+ix],cover[(iy+1)*fieldSize+ix+1],fx-ix),fy-iy);
            var color=Color.Lerp(soilColor,PaintTile(gravel,px/5.5f+.37f,pz/5.5f+.19f),sample.Gravel);
            color=Color.Lerp(color,grassColor,weight);
            color=new Color(color.r*sample.Tint.X,color.g*sample.Tint.Y,color.b*sample.Tint.Z,1);
            color=Color.Lerp(color,PaintTile(gravel,px/5,pz/5),HamletLandscape.RoadCover(px,pz)*.88f);
            pixels[y*size+x]=Color.Lerp(color,new Color(.13f,.17f,.23f,1),HamletLandscape.Haze(pz));
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
        { name="Hamlet painted valley and outward road",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4 };
        _details.Add(texture); texture.SetPixels32(pixels); texture.Apply(true,true); return texture;
    }

    private static Color32 PaintTile(Color32[] tile,float u,float v)
    {
        const int size=512;
        int x=Mathf.Clamp((int)(Mathf.PingPong(u,1)*(size-1)),0,size-1);
        int y=Mathf.Clamp((int)(Mathf.PingPong(v,1)*(size-1)),0,size-1);
        return tile[y*size+x];
    }

    private static Color32[] ReadPaint(Texture source)
    {
        const int size=512;
        var temporary=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        Texture2D copy=null;
        try
        {
            Graphics.Blit(source,temporary); RenderTexture.active=temporary;
            copy=new Texture2D(size,size,TextureFormat.RGBA32,false,false);
            copy.ReadPixels(new Rect(0,0,size,size),0,0,false);
            return copy.GetPixels32();
        }
        finally { RenderTexture.active=previous; if (copy!=null) Destroy(copy); RenderTexture.ReleaseTemporary(temporary); }
    }
}
