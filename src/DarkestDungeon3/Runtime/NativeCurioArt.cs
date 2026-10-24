using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Nearby exact-ID paintings. Workers read/verify files; Unity uploads one PNG per frame.</summary>
internal static class NativeCurioArt
{
    internal const int MaxResidentPairs = 8;
    internal const long MaxResidentPixels = 24L * 1024 * 1024;
    private sealed class EncodedPair { public byte[] Closed, Used; public string Error; }
    private sealed class ManifestRead { public NativeCurioCatalog Catalog; public string Error; }
    private sealed class Pair
    {
        public NativeCurioPainting Art;
        public Task<EncodedPair> Reading;
        public EncodedPair Encoded;
        public Texture2D Closed, Used;
        public bool Finished, Failed;
        public long Stamp;
    }
    private static readonly Dictionary<string, Pair> Pairs = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Wanted = new(StringComparer.Ordinal);
    private static ExpeditionState _expedition;
    private static string _root;
    private static NativeCurioCatalog _catalog;
    private static Task<ManifestRead> _reading;
    private static bool _manifestDone;
    private static long _stamp;
    private static int _lastUploadFrame = -1;
    internal static int ResidentPairs => Pairs.Count;
    internal static long ResidentPixels => Pairs.Values.Sum(pair => pair.Art.PairPixels);
    internal static IReadOnlyCollection<NativeCurioDefinition> Definitions => _catalog?.Entries ?? Array.Empty<NativeCurioDefinition>();

    public static bool Ready => _manifestDone && Wanted.All(id => !Pairs.TryGetValue(id, out var pair) || pair.Finished);

    public static void Prepare(ExpeditionState expedition)
    {
        string parent = Plugin.NativeRoomSceneryPath?.Value;
        string root = string.IsNullOrWhiteSpace(parent) ? null : Path.Combine(parent, "curios");
        if (expedition == null || root == null) { Clear(); _manifestDone = true; return; }
        if (!ReferenceEquals(expedition, _expedition) || root != _root)
        {
            Clear(); _root = root; _expedition = expedition;
            _reading = Task.Run(() => ReadManifest(root));
        }
        if (_reading != null && _reading.IsCompleted)
        {
            var result = _reading.GetAwaiter().GetResult();
            _reading = null; _catalog = result.Catalog ?? NativeCurioCatalog.Empty; _manifestDone = true;
            if (result.Error != null) Plugin.Log.LogWarning("[curio art] " + result.Error);
            Plugin.Log.LogInfo("[curio art] exact paintings available for " + _catalog.Entries.Count + " identities");
        }
        if (_catalog == null) return;
        _stamp++;
        // Reserve the pixel budget in distance order before marking bodies as
        // wanted. Otherwise three large old neighbours can all stay protected
        // and prevent the new current tile from ever entering the cache.
        var desired = new List<NativeCurioPainting>();
        long desiredPixels = 0;
        foreach (var art in Nearby(expedition).Select(id => _catalog.Get(id)?.Art).Where(art => art != null)
                     .GroupBy(art => art.Id).Select(group => group.First()))
        {
            if (desired.Count >= MaxResidentPairs) break;
            if (desiredPixels + art.PairPixels > MaxResidentPixels) continue;
            desired.Add(art); desiredPixels += art.PairPixels;
        }
        Wanted.Clear();
        foreach (var art in desired) Wanted.Add(art.Id);
        foreach (var art in desired) Ensure(art);
        UploadOne();
    }

    public static bool TryGet(string id, bool used, out Texture2D texture, out NativeCurioPainting art)
    {
        texture = null; art = _catalog?.Get(id)?.Art;
        if (art == null || !Pairs.TryGetValue(art.Id, out var pair) || !pair.Finished || pair.Failed) return false;
        pair.Stamp = _stamp;
        texture = used ? pair.Used : pair.Closed;
        return texture != null;
    }

    private static void Ensure(NativeCurioPainting art)
    {
        if (Pairs.TryGetValue(art.Id, out var existing)) { existing.Stamp = _stamp; return; }
        while (Pairs.Count >= MaxResidentPairs || Pairs.Values.Sum(pair => pair.Art.PairPixels) + art.PairPixels > MaxResidentPixels)
        {
            var oldest = Pairs.Values.Where(pair => !Wanted.Contains(pair.Art.Id)).OrderBy(pair => pair.Stamp).FirstOrDefault();
            if (oldest == null) return; // Keep visible bodies. A capacity miss keeps the original exact-ID art.
            Destroy(oldest); Pairs.Remove(oldest.Art.Id);
        }
        string root = _root;
        Pairs.Add(art.Id, new Pair { Art = art, Stamp = _stamp, Reading = Task.Run(() => ReadPair(root, art)) });
    }

    private static void UploadOne()
    {
        if (_lastUploadFrame == Time.frameCount) return;
        foreach (var pair in Pairs.Values.OrderByDescending(pair => Wanted.Contains(pair.Art.Id)).ToArray())
        {
            if (pair.Finished || !pair.Reading.IsCompleted) continue;
            try
            {
                pair.Encoded ??= pair.Reading.GetAwaiter().GetResult();
                if (pair.Encoded.Error != null) throw new InvalidDataException(pair.Encoded.Error);
                if (pair.Closed == null)
                {
                    pair.Closed = Upload(pair.Encoded.Closed, pair.Art);
                    pair.Encoded.Closed = null;
                    _lastUploadFrame = Time.frameCount;
                    return;
                }
                pair.Used = Upload(pair.Encoded.Used, pair.Art);
                pair.Encoded = null; pair.Reading = null; pair.Finished = true;
                _lastUploadFrame = Time.frameCount;
                return;
            }
            catch (Exception error)
            {
                Destroy(pair); pair.Encoded = null; pair.Reading = null; pair.Failed = pair.Finished = true;
                Plugin.Log.LogWarning("[curio art] " + pair.Art.Id + ": " + error.Message + "; retaining original art");
            }
        }
    }

    private static Texture2D Upload(byte[] encoded, NativeCurioPainting art)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave, name = "DD2 curio " + art.Id,
        };
        try
        {
            if (!texture.LoadImage(encoded, markNonReadable: true) || texture.width != art.PixelWidth || texture.height != art.PixelHeight)
                throw new InvalidDataException("Curio PNG dimensions changed during upload");
            return texture;
        }
        catch { UnityEngine.Object.Destroy(texture); throw; }
    }

    private static ManifestRead ReadManifest(string root)
    {
        try
        {
            string path = Path.Combine(root, "manifest.json");
            if (!File.Exists(path)) return new ManifestRead { Catalog = NativeCurioCatalog.Empty };
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Curio manifest exceeds size limit");
            return new ManifestRead { Catalog = NativeCurioCatalog.Parse(File.ReadAllText(path)) };
        }
        catch (Exception error) { return new ManifestRead { Error = error.Message }; }
    }

    private static EncodedPair ReadPair(string root, NativeCurioPainting art)
    {
        try
        {
            byte[] Read(bool used)
            {
                string path = Path.Combine(root, art.File(used).Replace('/', Path.DirectorySeparatorChar));
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < 24 || info.Length > 8 * 1024 * 1024)
                    throw new InvalidDataException("Missing or oversized curio PNG");
                byte[] bytes = File.ReadAllBytes(path);
                int IntAt(int at) => bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3];
                if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                    IntAt(16) != art.PixelWidth || IntAt(20) != art.PixelHeight)
                    throw new InvalidDataException("Curio PNG header does not match the registered pair");
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (hash != (used ? art.UsedSha256 : art.ClosedSha256))
                    throw new InvalidDataException("Curio PNG changed after its pack was built");
                return bytes;
            }
            return new EncodedPair { Closed = Read(false), Used = Read(true) };
        }
        catch (Exception error) { return new EncodedPair { Error = error.Message }; }
    }

    private static IEnumerable<string> Nearby(ExpeditionState expedition)
    {
        var map = expedition.Map;
        if (map == null) yield break;
        if (expedition.InRoom)
        {
            if (expedition.RoomId >= map.Rooms.Count) yield break;
            yield return map.Room(expedition.RoomId).CurioId;
            foreach (var corridor in map.Corridors.Where(c => c.RoomA == expedition.RoomId || c.RoomB == expedition.RoomId))
            {
                bool fromA = corridor.RoomA == expedition.RoomId;
                for (int offset = 0; offset < Math.Min(3, corridor.Tiles.Count); offset++)
                {
                    var tile = corridor.Tiles[fromA ? offset : corridor.Tiles.Count - 1 - offset];
                    if (tile.Content == HallContent.Curio) yield return tile.ContentId;
                }
            }
        }
        else
        {
            if (expedition.CorridorId < 0 || expedition.CorridorId >= map.Corridors.Count) yield break;
            var corridor = map.Corridor(expedition.CorridorId);
            for (int distance = 0; distance <= 3; distance++)
                foreach (int index in distance == 0 ? new[] { expedition.TileIndex } : new[] { expedition.TileIndex + distance, expedition.TileIndex - distance })
                    if (index >= 0 && index < corridor.Tiles.Count && corridor.Tiles[index].Content == HallContent.Curio)
                        yield return corridor.Tiles[index].ContentId;
            yield return map.Room(corridor.RoomA).CurioId;
            yield return map.Room(corridor.RoomB).CurioId;
        }
    }

    private static void Destroy(Pair pair)
    {
        if (pair.Closed != null) UnityEngine.Object.Destroy(pair.Closed);
        if (pair.Used != null) UnityEngine.Object.Destroy(pair.Used);
        pair.Closed = pair.Used = null;
    }
    public static void Clear()
    {
        foreach (var pair in Pairs.Values) Destroy(pair);
        Pairs.Clear(); Wanted.Clear();
        _expedition = null; _root = null; _catalog = null; _reading = null; _manifestDone = false;
    }
}
