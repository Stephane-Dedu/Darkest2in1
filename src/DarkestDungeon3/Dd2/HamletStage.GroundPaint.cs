using System.Threading;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private Task<HamletPaintBaker.Result> _groundPaintTask;
    private CancellationTokenSource _groundPaintCancellation;
    private HamletPaintBaker.Result _groundPaint;
    private int _groundUpload;

    private void BeginGroundPaint(Texture soil, Texture stone, Texture grass, Texture rock)
    {
        // GPU access stays on Unity's main thread. Reuse each readback for both terrain images.
        var earth = ReadPaint(soil); var gravel = ReadPaint(stone);
        var turf = grass != null ? ReadPaint(grass) : null; var bedrock = ReadPaint(rock);
        _groundPaintCancellation = new CancellationTokenSource();
        var cancellation = _groundPaintCancellation.Token;
        _groundPaintTask = Task.Run(() => HamletPaintBaker.Bake(earth, gravel, turf, bedrock, cancellation), cancellation);
    }

    private bool UploadGroundPaint()
    {
        if (!_groundPaintTask.IsCompleted) return false;
        _groundPaint ??= _groundPaintTask.GetAwaiter().GetResult();
        // One owned 2048px texture per frame. The torch can animate between uploads.
        if (_groundUpload == 0)
        {
            SetPaintTexture(_groundValley, UploadPaint(_groundPaint.Valley, "Hamlet painted valley and outward road"));
            _groundPaint.Valley = null;
        }
        else if (_groundUpload == 1)
        {
            SetPaintTexture(_groundMaterial, UploadPaint(_groundPaint.Ground, "Hamlet world earth, grass and worn gravel"));
            _groundPaint.Ground = null;
        }
        else if (_groundUpload == 2)
        {
            SetPaintTexture(_groundPaving, UploadPaint(_groundPaint.Lanes, "Hamlet continuous worn paving"));
            _groundPaint.Lanes = null;
        }
        else return true;
        _groundUpload++;
        return false;
    }

    private Texture2D UploadPaint(byte[] pixels, string name)
    {
        var texture = new Texture2D(_groundPaint.Size, _groundPaint.Size, TextureFormat.RGBA32, true, false)
        { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        _details.Add(texture); texture.SetPixelData(pixels, 0); texture.Apply(true, true); return texture;
    }

    private void ClearGroundPaint()
    {
        _groundPaintCancellation?.Cancel();
        _groundPaintCancellation?.Dispose();
        _groundPaintCancellation = null;
        // Abandoned work owns only bytes. Observe errors and never publish into a replacement town.
        _groundPaintTask?.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _groundPaintTask = null; _groundPaint = null; _groundUpload = 0;
        _groundPaving = _groundRock = _groundStone = _groundValley = null;
        _groundGrass = null;
    }

    private static byte[] ReadPaint(Texture source)
    {
        const int size=HamletPaintBaker.TileSize;
        var temporary=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        bool previousSrgb=GL.sRGBWrite;
        Texture2D copy=null;
        try
        {
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(source,temporary); RenderTexture.active=temporary;
            copy=new Texture2D(size,size,TextureFormat.RGBA32,false,false);
            copy.ReadPixels(new Rect(0,0,size,size),0,0,false);
            // Keep the copied byte[] overload: the texture is destroyed before the worker consumes these bytes.
            return copy.GetRawTextureData();
        }
        finally { RenderTexture.active=previous; GL.sRGBWrite=previousSrgb; if (copy!=null) Destroy(copy); RenderTexture.ReleaseTemporary(temporary); }
    }
}
