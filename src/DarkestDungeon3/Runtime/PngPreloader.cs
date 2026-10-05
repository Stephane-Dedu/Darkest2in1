using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>A bounded menu warmup: file reads on workers, one native PNG upload per Update.</summary>
internal sealed class PngPreloader
{
    internal static readonly string[] InitialTownImages =
    {
        "campaign/town/town_bg.png",
        "campaign/town/estate_title/estate_nameplate.png",
        "shared/estate/currency.gold.large_icon.png",
        "shared/estate/currency.bust.icon.png",
        "shared/estate/currency.portrait.icon.png",
        "shared/estate/currency.deed.icon.png",
        "shared/estate/currency.crest.icon.png",
        "campaign/town/heirloom_exchange/he_icon_idle.png",
        "campaign/town/realm_inventory/realm_inventory.icon.png",
        "campaign/town/activity_log/activity_log.icon.png",
        "campaign/town/embark_party/embark_party.background.png",
        "campaign/town/town_event/town_event.icon.png",
    };

    private sealed class Job
    {
        public string Path;
        public Task<byte[]> Reading;
        public Texture2D Texture;
        public bool Finished;
    }

    private readonly Dictionary<string, Job> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public void Request(string path)
    {
        if (string.IsNullOrEmpty(path) || _jobs.ContainsKey(path)) return;
        _jobs[path] = new Job
        {
            Path = path,
            Reading = Task.Run(() => File.Exists(path) ? File.ReadAllBytes(path) : null),
        };
    }

    /// <summary>True for a queued path; pending paths return null without blocking or decoding.</summary>
    public bool TryGet(string path, out Texture2D texture, out bool finished)
    {
        texture = null;
        finished = false;
        if (path == null || !_jobs.TryGetValue(path, out var job)) return false;
        texture = job.Texture;
        finished = job.Finished;
        return true;
    }

    /// <summary>Call only from the Unity thread. A single native upload cannot be preempted.</summary>
    public void Update()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Finished || !job.Reading.IsCompleted) continue;
            Texture2D texture = null;
            try
            {
                byte[] bytes = job.Reading.GetAwaiter().GetResult();
                if (bytes == null) continue;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave, name = "DD1 " + System.IO.Path.GetFileName(job.Path),
                };
                if (!texture.LoadImage(bytes, markNonReadable: true)) throw new InvalidDataException("PNG decode failed");
                job.Texture = texture;
            }
            catch (Exception e)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Plugin.Log.LogWarning($"[art] {job.Path}: {e.Message}");
            }
            finally
            {
                job.Finished = true;
                job.Reading = null; // release encoded bytes after the upload or failure
            }
            if (texture != null) return;
        }
    }
}
