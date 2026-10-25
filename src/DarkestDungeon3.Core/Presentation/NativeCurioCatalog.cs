using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Private, exact-identity paintings. Never changes encounters, outcomes or save IDs.</summary>
public sealed class NativeCurioCatalog
{
    private sealed class Document
    {
        public int Version;
        public NativeCurioPainting[] Artifacts;
        public Entry[] Entries;
    }
    private sealed class RegionArt
    {
        public string ArtId { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, string> ResultTexts { get; set; }
    }
    private sealed class Entry
    {
        public string Id { get; set; }
        public string ArtId { get; set; }
        public Dictionary<string, RegionArt> Regions { get; set; }
    }
    private readonly Dictionary<string, NativeCurioDefinition> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, NativeCurioDefinition>> _regions = new(StringComparer.Ordinal);
    public IReadOnlyCollection<NativeCurioDefinition> Entries => _entries.Values;
    public static NativeCurioCatalog Empty { get; } = new(new Document
        { Version = 2, Artifacts = Array.Empty<NativeCurioPainting>(), Entries = Array.Empty<Entry>() });

    private NativeCurioCatalog(Document document)
    {
        if (document == null || (document.Version != 2 && document.Version != 3) || document.Artifacts == null || document.Entries == null ||
            document.Artifacts.Length > 256 || document.Entries.Length > 256)
            throw new InvalidDataException("Unsupported curio painting manifest.");
        var paintings = new Dictionary<string, NativeCurioPainting>(StringComparer.Ordinal);
        foreach (var art in document.Artifacts)
        {
            if (art == null) throw new InvalidDataException("Missing curio painting.");
            RequireKey(art.Id);
            if (art.Id == "faded_memory" || paintings.ContainsKey(art.Id) ||
                float.IsNaN(art.Width) || art.Width < 100 || art.Width > 400 ||
                art.PixelWidth < 16 || art.PixelWidth > 4096 || art.PixelHeight < 16 || art.PixelHeight > 2304 ||
                (long)art.PixelWidth * art.PixelHeight > 4194304 || art.Padding != 16 ||
                art.Width * art.PixelHeight / art.PixelWidth > 430.001f ||
                art.MaskCellSize < 1 || art.MaskCellSize > 32 ||
                art.MaskWidth != (art.PixelWidth + art.MaskCellSize - 1) / art.MaskCellSize ||
                art.MaskHeight != (art.PixelHeight + art.MaskCellSize - 1) / art.MaskCellSize ||
                art.MaskWidth > 256 || art.MaskHeight > 256 || art.Mask == null ||
                art.Mask.Length != (art.MaskWidth * art.MaskHeight + 7) / 8 ||
                !ValidHash(art.ClosedSha256) || !ValidHash(art.UsedSha256))
                throw new InvalidDataException("Invalid curio painting: " + art.Id);
            paintings.Add(art.Id, art);
        }
        foreach (var entry in document.Entries)
        {
            if (entry == null) throw new InvalidDataException("Missing curio identity.");
            RequireKey(entry.Id); RequireKey(entry.ArtId);
            if (entry.Id == "faded_memory" || _entries.ContainsKey(entry.Id) || !paintings.TryGetValue(entry.ArtId, out var art))
                throw new InvalidDataException("Invalid curio identity: " + entry.Id);
            _entries.Add(entry.Id, new NativeCurioDefinition(entry.Id, art));
            if (entry.Regions == null) continue;
            if (document.Version != 3 || entry.Regions.Count > 4)
                throw new InvalidDataException("Unsupported regional curio presentation.");
            var variants = new Dictionary<string, NativeCurioDefinition>(StringComparer.Ordinal);
            foreach (var region in entry.Regions)
            {
                var binding = region.Value;
                if (!(region.Key is "dd2_city" or "dd2_forest" or "dd2_farm" or "dd2_coast") || binding == null ||
                    string.IsNullOrWhiteSpace(binding.DisplayName) || binding.DisplayName.Length > 80 ||
                    binding.DisplayName.Any(char.IsControl) || !ValidResultTexts(binding.ResultTexts) ||
                    binding.ArtId == null || !paintings.TryGetValue(binding.ArtId, out var regionalArt))
                    throw new InvalidDataException("Invalid regional curio presentation: " + entry.Id);
                variants.Add(region.Key, new NativeCurioDefinition(entry.Id, regionalArt, binding.DisplayName, binding.ResultTexts));
            }
            _regions.Add(entry.Id, variants);
        }
    }

    public static NativeCurioCatalog Parse(string json) => new(JsonConvert.DeserializeObject<Document>(json));
    public NativeCurioDefinition Get(string id, string region = null)
    {
        if (id == null || !_entries.TryGetValue(id, out var entry)) return null;
        return region != null && _regions.TryGetValue(id, out var variants) && variants.TryGetValue(region, out var regional)
            ? regional : entry;
    }
    private static bool ValidHash(string hash) => hash?.Length == 64 && hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    private static bool ValidResultTexts(Dictionary<string, string> texts) => texts == null ||
        texts.Count <= 16 && texts.All(pair => ValidResultText(pair.Key) && ValidResultText(pair.Value));
    private static bool ValidResultText(string text) => !string.IsNullOrWhiteSpace(text) &&
        text.Length <= 512 && !text.Any(char.IsControl);
    private static void RequireKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 80 || key.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_'))
            throw new InvalidDataException("Unsafe curio painting key.");
    }
}

public sealed class NativeCurioDefinition
{
    private readonly Dictionary<string, string> _resultTexts;
    public string Id { get; }
    public NativeCurioPainting Art { get; }
    public string DisplayName { get; }
    internal NativeCurioDefinition(string id, NativeCurioPainting art, string displayName = null,
        Dictionary<string, string> resultTexts = null)
    {
        Id = id; Art = art; DisplayName = displayName;
        if (resultTexts != null) _resultTexts = new Dictionary<string, string>(resultTexts, StringComparer.Ordinal);
    }
    public string PresentResult(string text) => text != null && _resultTexts != null &&
        _resultTexts.TryGetValue(text, out var presented) ? presented : text;
}

public sealed class NativeCurioPainting
{
    public string Id, ClosedSha256, UsedSha256;
    public float Width;
    public int PixelWidth, PixelHeight, Padding, MaskCellSize, MaskWidth, MaskHeight;
    public byte[] Mask;
    public long PairPixels => (long)PixelWidth * PixelHeight * 2;
    public string File(bool used) => "art/" + Id + (used ? "-used.png" : "-closed.png");
    public NativeCurioPlacement At(float feetX, float feetY) => new(this, feetX, feetY);

    /// <summary>Closed-state silhouette, top-down coordinates; padding and holes are not clickable.</summary>
    public bool Hit(float u, float v)
    {
        if (!(u >= 0 && u < 1 && v >= 0 && v < 1)) return false;
        int x = (int)(u * PixelWidth) / MaskCellSize, y = (int)(v * PixelHeight) / MaskCellSize;
        int bit = y * MaskWidth + x;
        return (Mask[bit >> 3] & 1 << (bit & 7)) != 0;
    }
}

/// <summary>Both states use the same body registration, dimensions and16px foot padding.</summary>
public readonly struct NativeCurioPlacement
{
    private readonly NativeCurioPainting _art;
    public float Left { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }
    public NativeCurioPlacement(NativeCurioPainting art, float feetX, float feetY)
    {
        _art = art; Width = art.Width; Height = Width * art.PixelHeight / art.PixelWidth;
        Left = feetX - Width / 2;
        Top = feetY - Height + art.Padding * Width / art.PixelWidth;
    }
    public bool Hit(float x, float y) => _art.Hit((x - Left) / Width, (y - Top) / Height);
}
