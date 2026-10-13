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
                int size=material.name.StartsWith("Hamlet paint ")?2048:material.name.Contains("mountain")
                    || material.name=="Hamlet worn stone paths" || material.name=="Hamlet exposed cliff rock"?1024:512;
                texture=PaintCopy(source,paint,crop,size); _gradedPaint.Add(key,texture);
            }
            SetPaintTexture(material,texture);
        }
        // Grading changes RGB only. Hidden native backing meshes use zero tint alpha.
        foreach (string property in new[] { "_BaseMap_ColorTint","_BaseColor","_Color" })
            if (material.HasProperty(property)) material.SetColor(property,new Color(1,1,1,material.GetColor(property).a));
        if (material.HasProperty("_BaseMap_Tiling")) material.SetVector("_BaseMap_Tiling",new Vector4(1,1,0,0));
        if (material.HasProperty("_UseColorControl")) material.SetFloat("_UseColorControl",0);
        material.DisableKeyword("_COLOR_CONTROLS");
        if (material.HasProperty("_HasVertexPainting")) material.SetFloat("_HasVertexPainting",0);
        material.DisableKeyword("_HASVERTEXPAINTING_ON");
    }

    private static void SetPaintTexture(Material material,Texture texture)
    {
        foreach (string property in new[] { "_BaseMap_texture","_BaseMap","_MainTex" })
            if (material.HasProperty(property))
            { material.SetTexture(property,texture); material.SetTextureScale(property,Vector2.one); material.SetTextureOffset(property,Vector2.zero); }
    }

    private static void SetPaintTint(Material material,Color color)
    {
        foreach (string property in new[] { "_BaseMap_ColorTint","_BaseColor","_Color" })
            if (material.HasProperty(property)) material.SetColor(property,color);
    }

    // One bounded GPU readback per distinct surface during construction. Never runs in the render loop.
    private Texture2D PaintCopy(Texture source,HamletPalette.Paint paint,bool crop,int size)
    {
        var extent=HamletPalette.PaintExtent(source.width,source.height,size,paint,crop);
        var scratch=RenderTexture.GetTemporary(extent.Width,extent.Height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        bool previousSrgb=GL.sRGBWrite;
        try
        {
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(source,scratch,crop?new Vector2(paint.UvScale.X,paint.UvScale.Y):Vector2.one,
                crop?new Vector2(paint.UvOffset.X,paint.UvOffset.Y):Vector2.zero);
            RenderTexture.active=scratch;
            var texture=new Texture2D(extent.Width,extent.Height,TextureFormat.RGBA32,true,false)
            { name="Hamlet graded "+source.name,wrapMode=crop?TextureWrapMode.Mirror:TextureWrapMode.Repeat,
                filterMode=FilterMode.Trilinear,anisoLevel=4 };
            _details.Add(texture);
            texture.ReadPixels(new Rect(0,0,extent.Width,extent.Height),0,0,false);
            var pixels=texture.GetPixels32();
            for (int i=0;i<pixels.Length;i++)
            {
                int rgb=HamletPalette.GradeRgb(pixels[i].r,pixels[i].g,pixels[i].b,paint);
                pixels[i]=new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,pixels[i].a);
            }
            texture.SetPixels32(pixels); texture.Apply(true,true); return texture;
        }
        finally { RenderTexture.active=previous; GL.sRGBWrite=previousSrgb; RenderTexture.ReleaseTemporary(scratch); }
    }
}
