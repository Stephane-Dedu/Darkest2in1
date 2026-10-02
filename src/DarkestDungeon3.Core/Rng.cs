using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core;

/// <summary>
/// Small deterministic RNG (xorshift64*). System.Random's seeded sequence isn't guaranteed to match between
/// Unity's Mono and modern .NET, and a saved seed must reproduce the same dungeon everywhere.
/// </summary>
public sealed class Rng
{
    private ulong _state;

    public Rng(int seed)
    {
        // splitmix64 so nearby seeds give unrelated streams; never zero.
        ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        _state = (z ^ (z >> 31)) | 1UL;
    }

    public ulong NextULong()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>[0, max)</summary>
    public int Next(int max) => max <= 0 ? 0 : (int)(NextULong() % (ulong)max);

    /// <summary>[min, max] inclusive.</summary>
    public int Range(int min, int max) => min >= max ? min : min + Next(max - min + 1);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public int Roll(Dd1.IntRange r) => Range(r.Min, r.Max);
}
