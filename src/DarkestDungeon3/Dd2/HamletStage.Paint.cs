using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private readonly HashSet<Material> _gradedMaterials=new();
    private readonly Dictionary<(int,HamletPalette.Paint,bool),Texture2D> _gradedPaint=new();
    private sealed class PaintJob
    {
        internal Texture Source;
        internal Texture2D Texture;
        internal HamletPalette.Paint Paint;
        internal bool Crop;
        internal Task<byte[]> Work;
    }
    private readonly List<PaintJob> _paintJobs = new();
    private CancellationTokenSource _paintCancellation;
    private bool PaintReady => _paintJobs.Count == 0;

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

    // Construction only queues the copy, so a model with several materials cannot stall a frame on grading.
    private Texture2D PaintCopy(Texture source,HamletPalette.Paint paint,bool crop,int size)
    {
        var extent=HamletPalette.PaintExtent(source.width,source.height,size,paint,crop);
        var texture=new Texture2D(extent.Width,extent.Height,TextureFormat.RGBA32,true,false)
        { name="Hamlet graded "+source.name,wrapMode=crop?TextureWrapMode.Mirror:TextureWrapMode.Repeat,
            filterMode=FilterMode.Trilinear,anisoLevel=4 };
        _details.Add(texture);
        _paintJobs.Add(new PaintJob { Source=source,Texture=texture,Paint=paint,Crop=crop });
        return texture;
    }

    private void AdvancePaint()
    {
        // Keep GPU work on the main thread and bound it to one readback and one upload per frame.
        for (int i=0;i<_paintJobs.Count;i++)
        {
            var job=_paintJobs[i];
            if (job.Work==null || !job.Work.IsCompleted) continue;
            var pixels=job.Work.GetAwaiter().GetResult();
            job.Texture.SetPixelData(pixels,0); job.Texture.Apply(true,true);
            _paintJobs.RemoveAt(i);
            break;
        }
        if (_paintJobs.Count(j=>j.Work!=null)>=2) return;
        var next=_paintJobs.FirstOrDefault(j=>j.Work==null && !_paintJobs.Any(p=>p.Texture==j.Source));
        if (next==null) return;
        var bytes=ReadGradePixels(next);
        next.Source=null;
        _paintCancellation ??= new CancellationTokenSource();
        var cancellation=_paintCancellation.Token; var paint=next.Paint;
        next.Work=Task.Run(()=> { HamletPalette.GradeRgba(bytes,paint,cancellation); return bytes; },cancellation);
    }

    private static byte[] ReadGradePixels(PaintJob job)
    {
        int width=job.Texture.width,height=job.Texture.height;
        var scratch=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        bool previousSrgb=GL.sRGBWrite;
        Texture2D copy=null;
        try
        {
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(job.Source,scratch,job.Crop?new Vector2(job.Paint.UvScale.X,job.Paint.UvScale.Y):Vector2.one,
                job.Crop?new Vector2(job.Paint.UvOffset.X,job.Paint.UvOffset.Y):Vector2.zero);
            RenderTexture.active=scratch;
            copy=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
            copy.ReadPixels(new Rect(0,0,width,height),0,0,false);
            // The byte[] overload owns a copy. A NativeArray view would expire when this texture is destroyed.
            return copy.GetRawTextureData();
        }
        finally
        {
            RenderTexture.active=previous; GL.sRGBWrite=previousSrgb;
            if (copy!=null) Destroy(copy); RenderTexture.ReleaseTemporary(scratch);
        }
    }

    private void ClearPaintJobs()
    {
        _paintCancellation?.Cancel(); _paintCancellation?.Dispose(); _paintCancellation=null;
        foreach (var job in _paintJobs)
            job.Work?.ContinueWith(t=> { var ignored=t.Exception; },TaskContinuationOptions.OnlyOnFaulted);
        _paintJobs.Clear();
    }
}
