using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// DD1's binary data files (the hand-made maps in <c>maps/*.dm</c>, and the saves): a 64-byte header (magic 0xB101),
/// a table of objects, a table of named fields in pre-order, then the fields' data. Values carry no type, so the caller
/// reads each one as what it expects (int, float, bool, string, nested file).
/// </summary>
public sealed class Dd1Binary
{
    public const uint Magic = 0xB101;

    public Node Root { get; private set; }

    public sealed class Node
    {
        public string Name;
        public bool IsObject;
        public List<Node> Children = new();

        internal byte[] Data;
        internal int ValueStart, End;   // in Data: right after the name's null, and where the next field starts

        public Node this[string name] => Children.FirstOrDefault(c => c.Name == name);

        /// <summary>The field at this path of child names (null if any part is missing).</summary>
        public Node At(params string[] path)
        {
            var node = this;
            foreach (var p in path) { node = node?[p]; if (node == null) return null; }
            return node;
        }

        // Ints, floats and strings start on a 4-byte boundary of the data block; bools don't.
        private int Aligned => (ValueStart + 3) & ~3;

        public int Int => BitConverter.ToInt32(Data, Aligned);
        public float Float => BitConverter.ToSingle(Data, Aligned);
        public bool Bool => Data[ValueStart] != 0;

        /// <summary>A pair of floats (DD1's vectors: map positions, bounds).</summary>
        public (float X, float Y) Vector => (BitConverter.ToSingle(Data, Aligned), BitConverter.ToSingle(Data, Aligned + 4));

        /// <summary>A length-prefixed run of bytes (strings and nested files).</summary>
        public byte[] Bytes
        {
            get
            {
                int len = BitConverter.ToInt32(Data, Aligned);
                if (len < 0 || Aligned + 4 + len > Data.Length) return Array.Empty<byte>();
                var b = new byte[len];
                Array.Copy(Data, Aligned + 4, b, 0, len);
                return b;
            }
        }

        public string String
        {
            get
            {
                var b = Bytes;
                int n = b.Length > 0 && b[b.Length - 1] == 0 ? b.Length - 1 : b.Length;
                return Encoding.UTF8.GetString(b, 0, n);
            }
        }

        /// <summary>A whole DD1 binary file stored as this field's value (e.g. a map's <c>static_save</c>).</summary>
        public Dd1Binary Nested => Parse(Bytes);

        public override string ToString() => IsObject ? $"{Name} {{{Children.Count}}}" : Name;
    }

    public static Dd1Binary Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>DD1's string hash (ids in its saves and maps: h = h * 53 + byte).</summary>
    public static uint Hash(string s)
    {
        uint h = 0;
        foreach (byte b in Encoding.UTF8.GetBytes(s ?? "")) h = unchecked(h * 53 + b);
        return h;
    }

    public static Dd1Binary Parse(byte[] file)
    {
        if (file == null || file.Length < 64 || BitConverter.ToUInt32(file, 0) != Magic)
            throw new InvalidDataException("not a DD1 binary file");
        int I(int at) => BitConverter.ToInt32(file, at);
        int objects = I(20), objectsAt = I(24), fields = I(44), fieldsAt = I(48), dataLength = I(56), dataAt = I(60);

        var data = new byte[dataLength];
        Array.Copy(file, dataAt, data, 0, dataLength);

        // Object table: (parent, field index, direct children, all children), 16 bytes each.
        var directChildren = new int[objects];
        for (int i = 0; i < objects; i++) directChildren[i] = I(objectsAt + 16 * i + 8);

        // Field table: (name hash, data offset, info), 12 bytes each. Info: bit 0 object, bits 2-10 name length with
        // its null, bits 11-30 the object's index in the object table.
        var nodes = new Node[fields];
        var objectOf = new int[fields];
        for (int i = 0; i < fields; i++)
        {
            int offset = I(fieldsAt + 12 * i + 4);
            uint info = BitConverter.ToUInt32(file, fieldsAt + 12 * i + 8);
            int nameLength = (int)((info >> 2) & 0x1FF);
            nodes[i] = new Node
            {
                Name = Encoding.UTF8.GetString(data, offset, Math.Max(0, nameLength - 1)),
                IsObject = (info & 1) != 0,
                Data = data,
                ValueStart = offset + nameLength,
            };
            objectOf[i] = (int)((info >> 11) & 0xFFFFF);   // 20 bits; the top bit is used for something else
        }
        for (int i = 0; i < fields; i++) nodes[i].End = i + 1 < fields ? I(fieldsAt + 12 * (i + 1) + 4) : dataLength;

        // Fields come in pre-order: an object's direct children follow it.
        var result = new Dd1Binary();
        var open = new Stack<(Node Node, int Left)>();
        for (int i = 0; i < fields; i++)
        {
            while (open.Count > 0 && open.Peek().Left == 0) open.Pop();
            var node = nodes[i];
            if (open.Count == 0) result.Root ??= node;
            else
            {
                var (parent, left) = open.Pop();
                parent.Children.Add(node);
                open.Push((parent, left - 1));
            }
            if (node.IsObject) open.Push((node, directChildren[objectOf[i]]));
        }
        return result;
    }
}
