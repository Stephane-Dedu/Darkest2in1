using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private static readonly HamletPaintCache GroundPaintCache = new();
    private Task<(HamletPaintBaker.Result Paint, double Seconds)> _groundPaintTask;
    private CancellationTokenSource _groundPaintCancellation;
    private HamletPaintBaker.Result _groundPaint;
    private int _groundUpload;

    private void AdvanceGroundPaint()
    {
        if (_cliffPaintPath!=null && !Art.ImageFinished(_cliffPaintPath)) return;
        if (_earthPaintPath!=null && !Art.ImageFinished(_earthPaintPath)) return;
        if (_pavingPaintPath!=null && !Art.ImageFinished(_pavingPaintPath)) return;
        if (_groundMaterial == null) PrepareGround();
        if (_groundPaintTask != null) return;
        var inputs = new[] { BaseTexture(_groundMaterial), BaseTexture(_groundPaving), _groundGrass, BaseTexture(_groundRock) };
        // Building paint can finish alongside terrain. Only the four terrain
        // input textures must be uploaded before their readbacks are safe.
        if (_paintJobs.Any(job => inputs.Contains(job.Texture))) return;
        BeginGroundPaint(inputs[0], inputs[1], inputs[2], inputs[3]);
    }

    private void BeginGroundPaint(Texture soil, Texture stone, Texture grass, Texture rock)
    {
        // GPU access stays on Unity's main thread. Reuse each readback for both terrain images.
        var earth = ReadPaint(soil); var gravel = ReadPaint(stone);
        var turf = grass != null ? ReadPaint(grass) : null; var bedrock = ReadPaint(rock);
        _groundPaintCancellation = new CancellationTokenSource();
        var cancellation = _groundPaintCancellation.Token;
        var models = _modelTask;
        _groundPaintTask = Task.Run(async () =>
        {
            // The model task already owns terrain/package validation. Share its
            // optional field without delaying main-thread texture/model uploads.
            var pack = await models.ConfigureAwait(false);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var paint = GroundPaintCache.GetOrBake(earth, gravel, turf, bedrock, cancellation, sun: pack.Sun);
            return (paint, timer.Elapsed.TotalSeconds);
        }, cancellation);
    }

    private bool UploadGroundPaint()
    {
        if (!_groundPaintTask.IsCompleted) return false;
        if (_groundPaint == null)
        {
            var completed = _groundPaintTask.GetAwaiter().GetResult();
            _groundPaint = completed.Paint;
            Plugin.Log.LogInfo($"[hamlet ground paint] {completed.Paint.Size}px maps ready in {completed.Seconds:F3}s; sunlight {_modelPack.Sun != null}");
        }
        // One owned texture per frame. The torch can animate between uploads.
        if (_groundUpload == 0)
        {
            SetPaintTexture(_groundValley, UploadPaint(_groundPaint.Valley, "Hamlet painted valley and outward road"));
        }
        else if (_groundUpload == 1)
        {
            SetPaintTexture(_groundMaterial, UploadPaint(_groundPaint.Ground, "Hamlet world earth, grass and worn gravel"));
        }
        else if (_groundUpload == 2)
        {
            SetPaintTexture(_groundPaving, UploadPaint(_groundPaint.Lanes, "Hamlet continuous worn paving"));
        }
        else return true;
        _groundUpload++;
        return false;
    }

    private Texture2D UploadPaint(byte[] pixels, string name)
    {
        var texture = new Texture2D(_groundPaint.Size, _groundPaint.Size, TextureFormat.RGBA32, true, false)
        { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
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
        _groundPaving = _groundRock = _groundStone = _groundSteps = _groundValley = null;
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
