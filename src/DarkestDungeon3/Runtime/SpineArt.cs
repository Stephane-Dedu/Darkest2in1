using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Dd1;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// Still pictures of DD1's Spine animations (town buildings, curios), read from the user's DD1 install and
/// drawn on the CPU (Core's SpineRaster). Baking is spread over frames: callers get null until theirs is ready.
/// </summary>
internal static class SpineArt
{
    public sealed class Picture
    {
        public Texture2D Texture;
        public RgbaImage Pixels;      // kept for hit tests
        public Vector2 Pivot;         // skeleton origin, pixels from the top-left
        public float PixelsPerUnit;

        /// <summary>Screen rect when the skeleton origin sits at <paramref name="origin"/>, drawn at <paramref name="scale"/>.</summary>
        public Rect RectAt(Vector2 origin, float scale = 1f)
        {
            float k = scale / PixelsPerUnit;
            return new Rect(origin.x - Pivot.x * k, origin.y - Pivot.y * k, Texture.width * k, Texture.height * k);
        }

        public void Draw(Vector2 origin, float scale = 1f, bool flipX = false)
        {
            var r = RectAt(origin, scale);
            if (flipX)
            {
                r.x = origin.x - (Texture.width - Pivot.x) * scale / PixelsPerUnit;
                GUI.DrawTextureWithTexCoords(r, Texture, new Rect(1, 0, -1, 1), true);
            }
            else GUI.DrawTexture(r, Texture);
        }

        /// <summary>Is the picture opaque under this screen point?</summary>
        public bool Hit(Vector2 origin, float scale, Vector2 point)
        {
            var r = RectAt(origin, scale);
            if (!r.Contains(point)) return false;
            int x = (int)((point.x - r.x) / r.width * Pixels.Width), y = (int)((point.y - r.y) / r.height * Pixels.Height);
            if (x < 0 || y < 0 || x >= Pixels.Width || y >= Pixels.Height) return false;
            return Pixels.Pixels[(y * Pixels.Width + x) * 4 + 3] > 40;
        }
    }

    private static readonly Dictionary<string, Picture> Cache = new();
    private static readonly Dictionary<string, RgbaImage> Pages = new();
    private sealed class Prepared
    {
        public string Directory;
        public List<SpineSkeleton.Piece> Pieces;
        public SpineAtlas Atlas;
    }
    private sealed class Baked
    {
        public SpineRaster.Result Result;
        public byte[] BottomUp;
        public double Milliseconds;
    }
    private sealed class Job
    {
        public string Key, Folder, Variant;
        public float PixelsPerUnit;
        public Task<Prepared> Reading;
        public Prepared Prepared;
        public readonly Dictionary<SpineAtlas.Page, RgbaImage> Images = new();
        public int NextPage;
        public Task<Baked> Rendering;
    }
    private static readonly Dictionary<string, Job> Pending = new();
    private static Task<Baked> _worker;
    private const double UploadBudgetMs = 4;

    /// <summary>Queue file parsing and CPU rasterization; return null until Update has uploaded the picture.
    /// The slot predicate must use only skeleton data, never Unity APIs.</summary>
    public static Picture Get(string folder, string variant, Func<SpineSkeleton.Slot, bool> include, float pixelsPerUnit = 1f)
    {
        if (folder == null) return null;
        string key = $"{folder}|{variant}|{pixelsPerUnit:0.00}";
        if (Cache.TryGetValue(key, out var pic)) return pic;
        if (!Pending.ContainsKey(key))
            Pending[key] = new Job
            {
                Key = key, Folder = folder, Variant = variant, PixelsPerUnit = pixelsPerUnit,
                Reading = Task.Run(() => Read(folder, include)),
            };
        return null;
    }

    /// <summary>Only PNG decoding and texture upload use Unity, on the main thread, in a small per-frame budget.
    /// At most one CPU raster runs at once; it owns its page snapshot and never touches Unity or the caches.</summary>
    public static void Update()
    {
        if (Pending.Count == 0) return;
        var budget = Stopwatch.StartNew();
        foreach (var job in Pending.Values.ToArray())
        {
            if (budget.Elapsed.TotalMilliseconds >= UploadBudgetMs) break;
            try
            {
                if (!job.Reading.IsCompleted) continue;
                job.Prepared ??= job.Reading.GetAwaiter().GetResult();
                if (job.Prepared == null) { Finish(job, null); continue; }
                if (job.Rendering != null)
                {
                    if (!job.Rendering.IsCompleted) continue;
                    var baked = job.Rendering.GetAwaiter().GetResult();
                    Picture picture = null;
                    if (baked.Result != null)
                    {
                        var result = baked.Result;
                        var img = result.Image;
                        var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
                        {
                            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                            hideFlags = HideFlags.HideAndDontSave, name = "DD1 " + Path.GetFileName(job.Folder),
                        };
                        tex.LoadRawTextureData(baked.BottomUp);
                        tex.Apply(false, true);
                        picture = new Picture { Texture = tex, Pixels = img,
                            Pivot = new Vector2(result.PivotX, result.PivotY), PixelsPerUnit = job.PixelsPerUnit };
                        Plugin.Log.LogInfo($"[spine] {Path.GetFileName(job.Folder)}/{job.Variant}: worker {baked.Milliseconds:0} ms");
                    }
                    Finish(job, picture);
                    continue;
                }
                // Decode no more pages than the main-thread budget permits. A single Unity upload cannot be preempted.
                while (job.NextPage < job.Prepared.Atlas.Pages.Count && budget.Elapsed.TotalMilliseconds < UploadBudgetMs)
                {
                    var page = job.Prepared.Atlas.Pages[job.NextPage++];
                    job.Images[page] = LoadPage(Path.Combine(job.Prepared.Directory, page.File));
                }
                if (job.NextPage < job.Prepared.Atlas.Pages.Count || _worker != null) continue;
                job.Rendering = _worker = Task.Run(() => Raster(job));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[spine] {job.Folder} ({job.Variant}): {e.Message}");
                Finish(job, null);
            }
        }
    }

    private static void Finish(Job job, Picture picture)
    {
        Cache[job.Key] = picture;
        Pending.Remove(job.Key);
        if (ReferenceEquals(_worker, job.Rendering)) _worker = null;
    }

    private static Prepared Read(string folder, Func<SpineSkeleton.Slot, bool> include)
    {
        var files = Dd1Install.SpineIn(folder);
        if (files == null) return null;
        var (skelPath, atlasPath) = files.Value;
        var skeleton = SpineSkeleton.Load(skelPath);
        var atlas = SpineAtlas.Parse(File.ReadAllText(atlasPath));
        return new Prepared { Directory = Path.GetDirectoryName(atlasPath), Atlas = atlas,
            Pieces = skeleton.SetupPose(atlas, include) };
    }

    private static Baked Raster(Job job)
    {
        var watch = Stopwatch.StartNew();
        var result = SpineRaster.Render(job.Prepared.Pieces, page => job.Images[page], job.PixelsPerUnit);
        if (result == null) return new Baked();
        var img = result.Image;
        var raw = new byte[img.Pixels.Length];
        int stride = img.Width * 4;
        for (int y = 0; y < img.Height; y++)
            Buffer.BlockCopy(img.Pixels, y * stride, raw, (img.Height - 1 - y) * stride, stride);
        return new Baked { Result = result, BottomUp = raw, Milliseconds = watch.Elapsed.TotalMilliseconds };
    }

    private static RgbaImage LoadPage(string path)
    {
        if (Pages.TryGetValue(path, out var page)) return page;
        page = null;
        if (File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path)))
            {
                var px = tex.GetPixels32();   // bottom-up
                int w = tex.width, h = tex.height;
                var bytes = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int src = (h - 1 - y) * w, dst = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[src + x];
                        int i = dst + x * 4;
                        bytes[i] = c.r; bytes[i + 1] = c.g; bytes[i + 2] = c.b; bytes[i + 3] = c.a;
                    }
                }
                page = new RgbaImage(w, h, bytes);
            }
            UnityEngine.Object.Destroy(tex);
        }
        Pages[path] = page;
        return page;
    }

    /// <summary>Atlas pages are only needed while baking; free them once a screen has everything it needs.</summary>
    public static void ReleasePages() => Pages.Clear();
}
