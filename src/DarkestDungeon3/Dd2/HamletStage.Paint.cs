using System.Collections.Generic;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private readonly HashSet<Material> _gradedMaterials=new();
    private readonly Dictionary<(int,HamletPalette.Paint,bool),Texture2D> _gradedPaint=new();

    private void Grade(Material material,HamletPalette.Paint paint,bool crop=true)
    {
        if (material==null || !_gradedMaterials.Add(material)) return;
        if (BaseTexture(material) is { } source)
        {
            var key=(source.GetInstanceID(),paint,crop);
            if (!_gradedPaint.TryGetValue(key,out var texture))
            {
                int size=material.name.Contains("mountain")?1024:512;
                texture=PaintCopy(source,paint,crop,size); _gradedPaint.Add(key,texture);
            }
            material.SetTexture("_BaseMap_texture",texture);
        }
        if (material.HasProperty(Tint)) material.SetColor(Tint,Color.white);
        if (material.HasProperty("_UseColorControl")) material.SetFloat("_UseColorControl",0);
        material.DisableKeyword("_COLOR_CONTROLS");
        if (material.HasProperty("_HasVertexPainting")) material.SetFloat("_HasVertexPainting",0);
        material.DisableKeyword("_HASVERTEXPAINTING_ON");
    }

    // One bounded GPU readback per distinct surface during construction. Never runs in the render loop.
    private Texture2D PaintCopy(Texture source,HamletPalette.Paint paint,bool crop,int size)
    {
        var scratch=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        try
        {
            Graphics.Blit(source,scratch,crop?new Vector2(paint.UvScale.X,paint.UvScale.Y):Vector2.one,
                crop?new Vector2(paint.UvOffset.X,paint.UvOffset.Y):Vector2.zero);
            RenderTexture.active=scratch;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
            { name="Hamlet graded "+source.name,wrapMode=crop?TextureWrapMode.Mirror:TextureWrapMode.Repeat,
                filterMode=FilterMode.Trilinear,anisoLevel=4 };
            _details.Add(texture);
            texture.ReadPixels(new Rect(0,0,size,size),0,0,false);
            var pixels=texture.GetPixels32();
            for (int i=0;i<pixels.Length;i++)
            {
                int rgb=HamletPalette.GradeRgb(pixels[i].r,pixels[i].g,pixels[i].b,paint);
                pixels[i]=new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,pixels[i].a);
            }
            texture.SetPixels32(pixels); texture.Apply(true,true); return texture;
        }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(scratch); }
    }
}
