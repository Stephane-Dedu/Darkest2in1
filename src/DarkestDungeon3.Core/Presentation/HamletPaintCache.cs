using System;
using System.Security.Cryptography;
using System.Threading;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>One process-local town paint, shared by successive owned scene instances.</summary>
public sealed class HamletPaintCache
{
    private readonly object _gate = new();
    private string _key;
    private HamletPaintBaker.Result _paint;

    /// <summary>Returned buffers are shared, read-only paint. Never clear or edit them after upload.</summary>
    public HamletPaintBaker.Result GetOrBake(byte[] soil, byte[] stone, byte[] grass, byte[] rock,
        CancellationToken cancellation = default, int size = HamletPaintBaker.PaintSize, int fieldSize = 256)
    {
        cancellation.ThrowIfCancellationRequested();
        string key;
        using (var hash = SHA256.Create())
        {
            foreach (var bytes in new[] { BitConverter.GetBytes(size), BitConverter.GetBytes(fieldSize), soil, stone, grass, rock })
            {
                cancellation.ThrowIfCancellationRequested();
                var length = BitConverter.GetBytes(bytes?.Length ?? -1);
                hash.TransformBlock(length, 0, length.Length, null, 0);
                if (bytes != null) hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            key = BitConverter.ToString(hash.Hash);
        }
        // All callers are managed workers. Serializing misses avoids duplicate terrain bakes.
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_key == key) return _paint;
            var paint = HamletPaintBaker.Bake(soil, stone, grass, rock, cancellation, size, fieldSize);
            cancellation.ThrowIfCancellationRequested();
            _paint = paint; _key = key;
            return paint;
        }
    }
}
