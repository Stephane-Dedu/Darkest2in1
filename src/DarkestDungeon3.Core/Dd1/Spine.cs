using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// DD1 animates its town buildings, curios and heroes with Spine 2.1 (binary .skel + libgdx .atlas). We don't
/// animate them: this reads a skeleton's setup pose and turns it into textured triangles, which the game side
/// draws once into a still picture. Region, mesh and skinned-mesh attachments are supported.
/// </summary>
public sealed class SpineAtlas
{
    public sealed class Page
    {
        public string File;
        public int Width, Height;   // from the atlas "size:" line; 0 if absent (older atlases)
    }

    public sealed class Region
    {
        public Page Page;
        public string Name;
        public bool Rotate;
        public int X, Y, Width, Height;           // Width/Height: the unrotated image size
        public int OrigWidth, OrigHeight, OffsetX, OffsetY;
        /// <summary>Size of the rectangle the region occupies on its page (swapped when rotated).</summary>
        public int PageWidth => Rotate ? Height : Width;
        public int PageHeight => Rotate ? Width : Height;
    }

    public readonly List<Page> Pages = new();
    public readonly Dictionary<string, Region> Regions = new();

    public static SpineAtlas Parse(string text)
    {
        var atlas = new SpineAtlas();
        Page page = null;
        Region region = null;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.Trim().Length == 0) { page = null; region = null; continue; }
            int colon = line.IndexOf(':');
            if (page == null)
            {
                page = new Page { File = line.Trim() };
                atlas.Pages.Add(page);
                continue;
            }
            if (colon < 0)
            {
                region = new Region { Page = page, Name = line.Trim() };
                atlas.Regions[region.Name] = region;
                continue;
            }
            string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
            var nums = value.Split(',').Select(v => int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0).ToArray();
            if (region == null)
            {
                if (key == "size" && nums.Length == 2) { page.Width = nums[0]; page.Height = nums[1]; }
                continue;     // format, filter, repeat
            }
            switch (key)
            {
                case "rotate": region.Rotate = value == "true"; break;
                case "xy": region.X = nums[0]; region.Y = nums[1]; break;
                case "size": region.Width = nums[0]; region.Height = nums[1]; break;
                case "orig": region.OrigWidth = nums[0]; region.OrigHeight = nums[1]; break;
                case "offset": region.OffsetX = nums[0]; region.OffsetY = nums[1]; break;
            }
        }
        foreach (var r in atlas.Regions.Values)
        {
            if (r.OrigWidth == 0) r.OrigWidth = r.Width;
            if (r.OrigHeight == 0) r.OrigHeight = r.Height;
        }
        return atlas;
    }
}

public sealed partial class SpineSkeleton
{
    public sealed class Bone
    {
        public string Name;
        public int Parent;
        public float X, Y, ScaleX, ScaleY, Rotation, Length;
        public bool InheritScale, InheritRotation;
        // setup-pose world transform
        public float A, B, C, D, WorldX, WorldY, WorldScaleX, WorldScaleY, WorldRotation;

        public (float x, float y) ToWorld(float x, float y) => (x * A + y * B + WorldX, x * C + y * D + WorldY);
    }

    public sealed class Slot
    {
        public string Name;
        public int Bone;
        public uint Color;
        public string Attachment;
        public bool Additive;
    }

    public enum AttachmentType { Region, BoundingBox, Mesh, SkinnedMesh }

    public sealed class Attachment
    {
        public AttachmentType Type;
        public string Name, Path;
        public float X, Y, ScaleX, ScaleY, Rotation, Width, Height;   // region
        public float[] RegionUvs, Vertices;                         // mesh / skinned mesh (Vertices = raw weights for skinned)
        public short[] Triangles;
        public uint Color;
    }

    /// <summary>One textured piece of the setup pose: world positions (y up) and atlas page pixels (y down).</summary>
    public sealed class Piece
    {
        public string Slot;
        public SpineAtlas.Page Page;
        public float[] Positions;   // x0,y0,x1,y1,...
        public float[] PagePixels;  // u0,v0,... in page pixels, origin top-left
        public int[] Triangles;
        public bool Additive;
        public uint Color;          // slot colour * attachment colour, RGBA8888
    }

    public string Hash, Version;
    public float Width, Height;
    public readonly List<Bone> Bones = new();
    public readonly List<Slot> Slots = new();
    public readonly Dictionary<(int slot, string name), Attachment> DefaultSkin = new();
    public readonly Dictionary<string, Dictionary<(int slot, string name), Attachment>> Skins = new();

    public static SpineSkeleton Load(string skelPath) => Read(File.ReadAllBytes(skelPath));

    public static SpineSkeleton Read(byte[] data)
    {
        var r = new Reader(data);
        var sk = new SpineSkeleton { Hash = r.String(), Version = r.String(), Width = r.Float(), Height = r.Float() };
        if (sk.Version == null || !sk.Version.StartsWith("2.", StringComparison.Ordinal))
            throw new InvalidDataException($"Spine {sk.Version} is not supported (expected 2.x)");
        bool nonessential = r.Bool();
        if (nonessential) r.String();

        for (int i = 0, n = r.VarInt(); i < n; i++)
        {
            var b = new Bone { Name = r.String(), Parent = r.VarInt() - 1 };
            b.X = r.Float(); b.Y = r.Float(); b.ScaleX = r.Float(); b.ScaleY = r.Float(); b.Rotation = r.Float();
            b.Length = r.Float();
            r.Bool(); r.Bool();              // flipX, flipY
            b.InheritScale = r.Bool(); b.InheritRotation = r.Bool();
            if (nonessential) r.Int();       // colour
            sk.Bones.Add(b);
        }
        for (int i = 0, n = r.VarInt(); i < n; i++)   // IK constraints (the setup pose ignores them; animations use them)
        {
            var ik = new IkConstraint { Name = r.String() };
            for (int j = 0, m = r.VarInt(); j < m; j++) ik.Bones.Add(r.VarInt());
            ik.Target = r.VarInt();
            ik.Mix = r.Float();
            ik.BendPositive = (sbyte)r.Byte() >= 0;
            sk.IkConstraints.Add(ik);
        }
        for (int i = 0, n = r.VarInt(); i < n; i++)
            sk.Slots.Add(new Slot { Name = r.String(), Bone = r.VarInt(), Color = (uint)r.Int(), Attachment = r.String(), Additive = r.Bool() });

        ReadSkin(r, sk.DefaultSkin, nonessential);
        if (sk.DefaultSkin.Count > 0) sk.SkinOrder.Add(null);   // Spine lists the default skin first when it has any
        for (int i = 0, n = r.VarInt(); i < n; i++)
        {
            string name = r.String();
            var skin = new Dictionary<(int, string), Attachment>();
            ReadSkin(r, skin, nonessential);
            sk.Skins[name] = skin;
            sk.SkinOrder.Add(name);
        }
        sk.ComputeWorld();
        sk.ReadEventsAndAnimations(r);
        sk.BytesRead = r.Position;
        return sk;
    }

    private static void ReadSkin(Reader r, Dictionary<(int, string), Attachment> skin, bool nonessential)
    {
        for (int i = 0, n = r.VarInt(); i < n; i++)
        {
            int slot = r.VarInt();
            for (int j = 0, m = r.VarInt(); j < m; j++)
            {
                string key = r.String();
                skin[(slot, key)] = ReadAttachment(r, key, nonessential);
            }
        }
    }

    private static Attachment ReadAttachment(Reader r, string key, bool nonessential)
    {
        var a = new Attachment { Name = r.String() ?? key };
        int type = r.Byte();
        switch (type)
        {
            case 0:
                a.Type = AttachmentType.Region;
                a.Path = r.String() ?? a.Name;
                a.X = r.Float(); a.Y = r.Float(); a.ScaleX = r.Float(); a.ScaleY = r.Float(); a.Rotation = r.Float();
                a.Width = r.Float(); a.Height = r.Float();
                a.Color = (uint)r.Int();
                return a;
            case 1:
                a.Type = AttachmentType.BoundingBox;
                a.Vertices = r.Floats();
                return a;
            case 2:
            case 3:
                a.Type = type == 2 ? AttachmentType.Mesh : AttachmentType.SkinnedMesh;
                a.Path = r.String() ?? a.Name;
                a.RegionUvs = r.Floats();
                a.Triangles = r.Shorts();
                a.Vertices = r.Floats();
                a.Color = (uint)r.Int();
                r.VarInt();                  // hull length
                if (nonessential) { r.Ints(); r.Float(); r.Float(); }
                return a;
            default:
                throw new InvalidDataException($"Unknown Spine attachment type {type} at byte {r.Position}");
        }
    }

    private void ComputeWorld()
    {
        foreach (var b in Bones)
        {
            var p = b.Parent >= 0 ? Bones[b.Parent] : null;
            if (p != null)
            {
                b.WorldX = b.X * p.A + b.Y * p.B + p.WorldX;
                b.WorldY = b.X * p.C + b.Y * p.D + p.WorldY;
                b.WorldScaleX = b.InheritScale ? p.WorldScaleX * b.ScaleX : b.ScaleX;
                b.WorldScaleY = b.InheritScale ? p.WorldScaleY * b.ScaleY : b.ScaleY;
                b.WorldRotation = b.InheritRotation ? p.WorldRotation + b.Rotation : b.Rotation;
            }
            else
            {
                b.WorldX = b.X; b.WorldY = b.Y;
                b.WorldScaleX = b.ScaleX; b.WorldScaleY = b.ScaleY;
                b.WorldRotation = b.Rotation;
            }
            double rad = b.WorldRotation * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            b.A = cos * b.WorldScaleX; b.C = sin * b.WorldScaleX;
            b.B = -sin * b.WorldScaleY; b.D = cos * b.WorldScaleY;
        }
    }

    /// <summary>
    /// The setup pose as textured pieces, in draw order. <paramref name="include"/> picks slots (DD1 buildings
    /// carry both an "idle" and a highlighted "active" picture, plus smoke and lights).
    /// </summary>
    public List<Piece> SetupPose(SpineAtlas atlas, Func<Slot, bool> include = null, string skin = null)
    {
        var pieces = new List<Piece>();
        Dictionary<(int, string), Attachment> extra = null;
        if (skin != null) Skins.TryGetValue(skin, out extra);
        for (int si = 0; si < Slots.Count; si++)
        {
            var slot = Slots[si];
            if (slot.Attachment == null || (include != null && !include(slot))) continue;
            Attachment a = null;
            if (extra != null) extra.TryGetValue((si, slot.Attachment), out a);
            if (a == null) DefaultSkin.TryGetValue((si, slot.Attachment), out a);
            if (a == null || a.Type == AttachmentType.BoundingBox) continue;
            if (!atlas.Regions.TryGetValue(a.Path, out var region)) continue;
            var bone = Bones[slot.Bone];
            var piece = new Piece { Slot = slot.Name, Page = region.Page, Additive = slot.Additive, Color = Multiply(slot.Color, a.Color) };
            switch (a.Type)
            {
                case AttachmentType.Region: RegionPiece(piece, a, region, bone); break;
                case AttachmentType.Mesh: MeshPiece(piece, a, region, bone); break;
                case AttachmentType.SkinnedMesh: SkinnedPiece(piece, a, region); break;
            }
            pieces.Add(piece);
        }
        return pieces;
    }

    /// <summary>A region attachment as a textured quad (BL, UL, UR, BR), in the bone's world space.</summary>
    public static Piece RegionQuad(Attachment a, SpineAtlas.Region region, Bone bone)
    {
        var piece = new Piece { Page = region.Page, Color = 0xffffffff };
        RegionPiece(piece, a, region, bone);
        return piece;
    }

    /// <summary>A bone at the origin with no rotation or scale.</summary>
    public static Bone IdentityBone() => new() { Name = "root", Parent = -1, ScaleX = 1, ScaleY = 1, A = 1, D = 1, WorldScaleX = 1, WorldScaleY = 1 };

    private static void RegionPiece(Piece piece, Attachment a, SpineAtlas.Region region, Bone bone)
    {
        float regionScaleX = a.Width / region.OrigWidth * a.ScaleX, regionScaleY = a.Height / region.OrigHeight * a.ScaleY;
        float lx = -a.Width / 2f * a.ScaleX + region.OffsetX * regionScaleX;
        float ly = -a.Height / 2f * a.ScaleY + region.OffsetY * regionScaleY;
        float lx2 = lx + region.Width * regionScaleX, ly2 = ly + region.Height * regionScaleY;
        double rad = a.Rotation * Math.PI / 180.0;
        float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
        (float, float) Local(float x, float y) => (x * cos - y * sin + a.X, x * sin + y * cos + a.Y);
        var corners = new[] { Local(lx, ly), Local(lx, ly2), Local(lx2, ly2), Local(lx2, ly) };   // BL UL UR BR
        piece.Positions = new float[8];
        for (int i = 0; i < 4; i++)
        {
            var (wx, wy) = bone.ToWorld(corners[i].Item1, corners[i].Item2);
            piece.Positions[i * 2] = wx;
            piece.Positions[i * 2 + 1] = wy;
        }
        float u = region.X, v = region.Y, u2 = region.X + region.PageWidth, v2 = region.Y + region.PageHeight;
        piece.PagePixels = region.Rotate
            ? new[] { u2, v2, u, v2, u, v, u2, v }      // the page holds the image turned 90 degrees
            : new[] { u, v2, u, v, u2, v, u2, v2 };
        piece.Triangles = new[] { 0, 1, 2, 2, 3, 0 };
    }

    private static float[] MeshUvs(Attachment a, SpineAtlas.Region region)
    {
        float u = region.X, v = region.Y, w = region.PageWidth, h = region.PageHeight;
        var uvs = new float[a.RegionUvs.Length];
        for (int i = 0; i < uvs.Length; i += 2)
        {
            if (region.Rotate)
            {
                uvs[i] = u + a.RegionUvs[i + 1] * w;
                uvs[i + 1] = v + h - a.RegionUvs[i] * h;
            }
            else
            {
                uvs[i] = u + a.RegionUvs[i] * w;
                uvs[i + 1] = v + a.RegionUvs[i + 1] * h;
            }
        }
        return uvs;
    }

    private static void MeshPiece(Piece piece, Attachment a, SpineAtlas.Region region, Bone bone)
    {
        piece.Positions = new float[a.Vertices.Length];
        for (int i = 0; i < a.Vertices.Length; i += 2)
        {
            var (wx, wy) = bone.ToWorld(a.Vertices[i], a.Vertices[i + 1]);
            piece.Positions[i] = wx;
            piece.Positions[i + 1] = wy;
        }
        piece.PagePixels = MeshUvs(a, region);
        piece.Triangles = a.Triangles.Select(t => (int)t).ToArray();
    }

    private void SkinnedPiece(Piece piece, Attachment a, SpineAtlas.Region region)
    {
        var w = a.Vertices;
        var positions = new List<float>();
        for (int i = 0; i < w.Length;)
        {
            int boneCount = (int)w[i++];
            float x = 0, y = 0;
            for (int k = 0; k < boneCount; k++, i += 4)
            {
                var bone = Bones[(int)w[i]];
                var (bx, by) = bone.ToWorld(w[i + 1], w[i + 2]);
                x += bx * w[i + 3];
                y += by * w[i + 3];
            }
            positions.Add(x);
            positions.Add(y);
        }
        piece.Positions = positions.ToArray();
        piece.PagePixels = MeshUvs(a, region);
        piece.Triangles = a.Triangles.Select(t => (int)t).ToArray();
    }

    private static uint Multiply(uint c1, uint c2)
    {
        uint Ch(uint c, int shift) => (c >> shift) & 0xff;
        uint M(int shift) => Ch(c1, shift) * Ch(c2, shift) / 255;
        return (M(24) << 24) | (M(16) << 16) | (M(8) << 8) | M(0);
    }

    /// <summary>Bounds of a set of pieces in world units: min x, min y, max x, max y.</summary>
    public static (float minX, float minY, float maxX, float maxY) Bounds(IEnumerable<Piece> pieces)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pieces)
            for (int i = 0; i < p.Positions.Length; i += 2)
            {
                minX = Math.Min(minX, p.Positions[i]); maxX = Math.Max(maxX, p.Positions[i]);
                minY = Math.Min(minY, p.Positions[i + 1]); maxY = Math.Max(maxY, p.Positions[i + 1]);
            }
        return (minX, minY, maxX, maxY);
    }

    private sealed class Reader
    {
        private readonly byte[] _b;
        public int Position;

        public Reader(byte[] b) => _b = b;

        public int Byte() => _b[Position++];
        public bool Bool() => _b[Position++] != 0;

        public int Int()
        {
            int v = (_b[Position] << 24) | (_b[Position + 1] << 16) | (_b[Position + 2] << 8) | _b[Position + 3];
            Position += 4;
            return v;
        }

        public float Float()
        {
            var bytes = new[] { _b[Position + 3], _b[Position + 2], _b[Position + 1], _b[Position] };
            Position += 4;
            return BitConverter.ToSingle(bytes, 0);
        }

        public short Short()
        {
            short v = (short)((_b[Position] << 8) | _b[Position + 1]);
            Position += 2;
            return v;
        }

        public int VarInt()
        {
            int result = 0, shift = 0;
            while (true)
            {
                int b = _b[Position++];
                result |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }

        public string String()
        {
            int n = VarInt();
            if (n == 0) return null;
            if (n == 1) return "";
            n--;
            var sb = new StringBuilder(n);
            for (int i = 0; i < n; i++)
            {
                int b = _b[Position++];
                switch (b >> 4)
                {
                    case 12:
                    case 13:
                        sb.Append((char)(((b & 0x1f) << 6) | (_b[Position++] & 0x3f)));
                        break;
                    case 14:
                        sb.Append((char)(((b & 0x0f) << 12) | ((_b[Position] & 0x3f) << 6) | (_b[Position + 1] & 0x3f)));
                        Position += 2;
                        break;
                    default:
                        sb.Append((char)b);
                        break;
                }
            }
            return sb.ToString();
        }

        public float[] Floats()
        {
            var a = new float[VarInt()];
            for (int i = 0; i < a.Length; i++) a[i] = Float();
            return a;
        }

        public short[] Shorts()
        {
            var a = new short[VarInt()];
            for (int i = 0; i < a.Length; i++) a[i] = Short();
            return a;
        }

        /// <summary>Spine's varint without "optimize positive": zigzag-encoded.</summary>
        public int SignedVarInt()
        {
            int v = VarInt();
            return (v >> 1) ^ -(v & 1);
        }

        public bool AtEnd => Position >= _b.Length;

        public int[] Ints()
        {
            var a = new int[VarInt()];
            for (int i = 0; i < a.Length; i++) a[i] = VarInt();
            return a;
        }
    }
}
