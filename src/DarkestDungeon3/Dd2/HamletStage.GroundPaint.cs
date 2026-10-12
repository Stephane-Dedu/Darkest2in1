using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private Texture2D GroundPaint(Texture soil,Texture stone,Texture grass)
    {
        const int tileSize=512,size=2048,fieldSize=256;
        Color32[] Read(Texture source)
        {
            var temporary=RenderTexture.GetTemporary(tileSize,tileSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;
            Texture2D copy=null;
            try
            {
                Graphics.Blit(source,temporary); RenderTexture.active=temporary;
                copy=new Texture2D(tileSize,tileSize,TextureFormat.RGBA32,false,false);
                copy.ReadPixels(new Rect(0,0,tileSize,tileSize),0,0,false);
                return copy.GetPixels32();
            }
            finally { RenderTexture.active=previous; if (copy!=null) Destroy(copy); RenderTexture.ReleaseTemporary(temporary); }
        }
        var earth=Read(soil); var gravel=Read(stone); var turf=grass!=null?Read(grass):null;
        var pixels=new Color32[size*size]; var cover=new float[fieldSize*fieldSize];
        // Route/footprint distances are evaluated on a bounded field, not four million times in the bake.
        if (turf!=null) for (int y=0;y<fieldSize;y++) for (int x=0;x<fieldSize;x++)
            cover[y*fieldSize+x]=HamletVegetation.Cover(
                Mathf.Lerp(HamletLayout.MinX,HamletLayout.MaxX,(float)x/(fieldSize-1)),
                Mathf.Lerp(HamletLayout.MinZ,HamletLayout.MaxZ,(float)y/(fieldSize-1)));
        Color32 Tile(Color32[] tile,float u,float v)
        {
            // Mirror the source patch without seams; world-space stains conceal its repeat.
            int x=Mathf.Clamp((int)(Mathf.PingPong(u,1)*(tileSize-1)),0,tileSize-1);
            int y=Mathf.Clamp((int)(Mathf.PingPong(v,1)*(tileSize-1)),0,tileSize-1);
            return tile[y*tileSize+x];
        }
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float px=Mathf.Lerp(HamletLayout.MinX,HamletLayout.MaxX,(x+.5f)/size);
            float pz=Mathf.Lerp(HamletLayout.MinZ,HamletLayout.MaxZ,(y+.5f)/size);
            var sample=HamletGroundPaint.At(px,pz);
            Color a=Tile(earth,px/10,pz/3),b=Tile(gravel,px/5.5f+.37f,pz/5.5f+.19f);
            var color=Color.Lerp(a,b,sample.Gravel);
            if (turf!=null)
            {
                float fx=(x+.5f)/size*(fieldSize-1),fy=(y+.5f)/size*(fieldSize-1);
                int ix=(int)fx,iy=(int)fy;
                float weight=Mathf.Lerp(Mathf.Lerp(cover[iy*fieldSize+ix],cover[iy*fieldSize+ix+1],fx-ix),
                    Mathf.Lerp(cover[(iy+1)*fieldSize+ix],cover[(iy+1)*fieldSize+ix+1],fx-ix),fy-iy);
                color=Color.Lerp(color,Tile(turf,px/7.5f,pz/7.5f),weight);
            }
            pixels[y*size+x]=new Color(color.r*sample.Tint.X,color.g*sample.Tint.Y,color.b*sample.Tint.Z,1);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
        { name="Hamlet world earth, grass and worn gravel",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4 };
        _details.Add(texture); texture.SetPixels32(pixels); texture.Apply(true,true); return texture;
    }
}
