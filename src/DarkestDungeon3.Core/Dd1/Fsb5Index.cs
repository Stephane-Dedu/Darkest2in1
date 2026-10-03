using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// The sample banks (FSB5) inside DD1's FMOD .bank files, by sample name. DD2's FMOD Studio can't load DD1's
/// format-103 banks, but its core can play an FSB5 straight from the file at its offset, so DD1's music, ambience and
/// sounds are reached through this index: name → (file, FSB5 offset and length, sample index).
/// </summary>
public sealed class Fsb5Index
{
    public sealed class Chunk
    {
        public string File;
        public long Offset, Length;
        public int Codec;
        public readonly List<string> Names = new();
    }

    public readonly List<Chunk> Chunks = new();
    private readonly Dictionary<string, (Chunk Chunk, int Index)> _byName = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byName.Count;
    public IEnumerable<string> Names => _byName.Keys;

    public bool TryGet(string name, out Chunk chunk, out int index)
    {
        if (name != null && _byName.TryGetValue(name, out var e)) { chunk = e.Chunk; index = e.Index; return true; }
        chunk = null;
        index = -1;
        return false;
    }

    public bool Has(string name) => name != null && _byName.ContainsKey(name);

    /// <summary>Index every FSB5 in these bank files (missing files are skipped).</summary>
    public static Fsb5Index Load(IEnumerable<string> bankFiles)
    {
        var index = new Fsb5Index();
        foreach (var file in bankFiles)
            if (System.IO.File.Exists(file)) index.Add(file);
        return index;
    }

    private void Add(string file)
    {
        using var fs = System.IO.File.OpenRead(file);
        var buffer = new byte[1 << 20];
        long pos = 0;
        while (pos < fs.Length)
        {
            long at = Find(fs, pos, buffer);
            if (at < 0) return;
            var header = new byte[60];
            fs.Position = at;
            if (fs.Read(header, 0, 60) < 60) return;
            uint num = BitConverter.ToUInt32(header, 8), sampleHeaders = BitConverter.ToUInt32(header, 12);
            uint nameTable = BitConverter.ToUInt32(header, 16), data = BitConverter.ToUInt32(header, 20);
            var chunk = new Chunk { File = file, Offset = at, Length = 60L + sampleHeaders + nameTable + data, Codec = (int)BitConverter.ToUInt32(header, 24) };
            if (nameTable > 0 && num > 0 && nameTable < 64 << 20)
            {
                var table = new byte[nameTable];
                fs.Position = at + 60 + sampleHeaders;
                fs.Read(table, 0, table.Length);
                for (int i = 0; i < num; i++)
                {
                    int o = (int)BitConverter.ToUInt32(table, i * 4);
                    int end = Array.IndexOf(table, (byte)0, o);
                    string name = Encoding.ASCII.GetString(table, o, (end < 0 ? table.Length : end) - o);
                    chunk.Names.Add(name);
                    if (!_byName.ContainsKey(name)) _byName[name] = (chunk, i);
                }
            }
            Chunks.Add(chunk);
            pos = at + Math.Max(4, chunk.Length);
        }
    }

    /// <summary>The next "FSB5" at or after <paramref name="from"/>, or -1.</summary>
    private static long Find(Stream fs, long from, byte[] buffer)
    {
        long pos = from;
        while (pos < fs.Length)
        {
            fs.Position = pos;
            int n = fs.Read(buffer, 0, buffer.Length);
            if (n < 4) return -1;
            for (int i = 0; i + 3 < n; i++)
                if (buffer[i] == 'F' && buffer[i + 1] == 'S' && buffer[i + 2] == 'B' && buffer[i + 3] == '5') return pos + i;
            pos += n - 3;
        }
        return -1;
    }
}
