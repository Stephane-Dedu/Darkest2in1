using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>An event a Spine animation can fire (DD1 uses them to time hits and sounds).</summary>
public sealed class SpineEventData
{
    public string Name, String;
    public int Int;
    public float Float;
}

/// <summary>
/// One Spine 2.1 animation: timelines for bones (rotate, translate, scale), slots (attachment, colour), IK mixes,
/// mesh deformation (FFD), draw order and events. Read from DD1's .skel files after the skins; applied by
/// <see cref="SpineSkeleton.Pose"/>.
/// </summary>
public sealed class SpineAnimation
{
    public string Name;
    public float Duration;
    internal readonly List<Timeline> Timelines = new();
    public readonly List<(float Time, string Event)> Events = new();
    public int TimelineCount => Timelines.Count;
    public bool DeformsMeshes => Timelines.Any(t => t is FfdTimeline);

    // ---- curves (Spine 2.1: linear, stepped, or a bezier sampled in 10 segments) ----

    internal sealed class Curves
    {
        private const int Linear = 0, Stepped = 1, Bezier = 2, BezierSize = 19;
        private readonly float[] _c;

        public Curves(int frames) => _c = new float[Math.Max(0, frames - 1) * BezierSize];

        public void SetStepped(int frame) => _c[frame * BezierSize] = Stepped;

        public void SetBezier(int frame, float cx1, float cy1, float cx2, float cy2)
        {
            float subdiv1 = 1f / 10, subdiv2 = subdiv1 * subdiv1, subdiv3 = subdiv2 * subdiv1;
            float pre1 = 3 * subdiv1, pre2 = 3 * subdiv2, pre4 = 6 * subdiv2, pre5 = 6 * subdiv3;
            float tmp1x = -cx1 * 2 + cx2, tmp1y = -cy1 * 2 + cy2, tmp2x = (cx1 - cx2) * 3 + 1, tmp2y = (cy1 - cy2) * 3 + 1;
            float dfx = cx1 * pre1 + tmp1x * pre2 + tmp2x * subdiv3, dfy = cy1 * pre1 + tmp1y * pre2 + tmp2y * subdiv3;
            float ddfx = tmp1x * pre4 + tmp2x * pre5, ddfy = tmp1y * pre4 + tmp2y * pre5;
            float dddfx = tmp2x * pre5, dddfy = tmp2y * pre5;
            int i = frame * BezierSize;
            _c[i++] = Bezier;
            float x = dfx, y = dfy;
            for (int n = i + BezierSize - 1; i < n; i += 2)
            {
                _c[i] = x; _c[i + 1] = y;
                dfx += ddfx; dfy += ddfy; ddfx += dddfx; ddfy += dddfy;
                x += dfx; y += dfy;
            }
        }

        public float Percent(int frame, float percent)
        {
            int i = frame * BezierSize;
            float type = _c[i];
            if (type == Linear) return percent;
            if (type == Stepped) return 0;
            i++;
            float x = 0;
            for (int start = i, n = i + BezierSize - 1; i < n; i += 2)
            {
                x = _c[i];
                if (x >= percent)
                {
                    float prevX = i == start ? 0 : _c[i - 2], prevY = i == start ? 0 : _c[i - 1];
                    return prevY + (_c[i + 1] - prevY) * (percent - prevX) / (x - prevX);
                }
            }
            float y = _c[i - 1];
            return y + (1 - y) * (percent - x) / (1 - x);
        }
    }

    internal abstract class Timeline
    {
        public float[] Times;
        public Curves Curve;

        protected Timeline(int frames)
        {
            Times = new float[frames];
            Curve = new Curves(frames);
        }

        /// <summary>The frame at or before <paramref name="t"/> (-1 before the first) and how far toward the next.</summary>
        protected (int frame, float percent) Locate(float t)
        {
            if (t < Times[0]) return (-1, 0);
            int last = Times.Length - 1;
            if (t >= Times[last]) return (last, 0);
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Times[mid] <= t) lo = mid; else hi = mid;
            }
            float span = Times[lo + 1] - Times[lo];
            float p = span <= 0 ? 0 : Math.Min(1, Math.Max(0, (t - Times[lo]) / span));
            return (lo, Curve.Percent(lo, p));
        }

        public abstract void Apply(SpinePose pose, float t);
    }

    internal sealed class RotateTimeline : Timeline
    {
        public int Bone;
        public float[] Angles;
        public RotateTimeline(int frames) : base(frames) => Angles = new float[frames];

        public override void Apply(SpinePose pose, float t)
        {
            var (f, p) = Locate(t);
            if (f < 0) return;
            float angle = f == Times.Length - 1 ? Angles[f] : Angles[f] + Wrap(Angles[f + 1] - Angles[f]) * p;
            pose.Bones[Bone].Rotation = pose.Setup.Bones[Bone].Rotation + angle;
        }

        private static float Wrap(float a)
        {
            if (float.IsNaN(a) || float.IsInfinity(a)) return 0;
            a %= 360;
            return a > 180 ? a - 360 : a < -180 ? a + 360 : a;
        }
    }

    internal sealed class TranslateTimeline : Timeline
    {
        public int Bone;
        public bool Scale;
        public float[] X, Y;
        public TranslateTimeline(int frames) : base(frames) { X = new float[frames]; Y = new float[frames]; }

        public override void Apply(SpinePose pose, float t)
        {
            var (f, p) = Locate(t);
            if (f < 0) return;
            bool end = f == Times.Length - 1;
            float x = end ? X[f] : X[f] + (X[f + 1] - X[f]) * p;
            float y = end ? Y[f] : Y[f] + (Y[f + 1] - Y[f]) * p;
            var setup = pose.Setup.Bones[Bone];
            var b = pose.Bones[Bone];
            if (Scale) { b.ScaleX = setup.ScaleX * x; b.ScaleY = setup.ScaleY * y; }
            else { b.X = setup.X + x; b.Y = setup.Y + y; }
        }
    }

    internal sealed class AttachmentTimeline : Timeline
    {
        public int Slot;
        public string[] Names;
        public AttachmentTimeline(int frames) : base(frames) => Names = new string[frames];

        public override void Apply(SpinePose pose, float t)
        {
            var (f, _) = Locate(t);
            if (f >= 0) pose.Attachments[Slot] = Names[f];
        }
    }

    internal sealed class ColorTimeline : Timeline
    {
        public int Slot;
        public uint[] Colors;
        public ColorTimeline(int frames) : base(frames) => Colors = new uint[frames];

        public override void Apply(SpinePose pose, float t)
        {
            var (f, p) = Locate(t);
            if (f < 0) return;
            if (f == Times.Length - 1) { pose.Colors[Slot] = Colors[f]; return; }
            uint a = Colors[f], b = Colors[f + 1], c = 0;
            for (int shift = 0; shift < 32; shift += 8)
            {
                float ca = (a >> shift) & 0xff, cb = (b >> shift) & 0xff;
                c |= (uint)Math.Round(ca + (cb - ca) * p) << shift;
            }
            pose.Colors[Slot] = c;
        }
    }

    internal sealed class IkTimeline : Timeline
    {
        public int Constraint;
        public float[] Mix;
        public bool[] BendPositive;
        public IkTimeline(int frames) : base(frames) { Mix = new float[frames]; BendPositive = new bool[frames]; }

        public override void Apply(SpinePose pose, float t)
        {
            var (f, p) = Locate(t);
            if (f < 0) return;
            pose.IkMix[Constraint] = f == Times.Length - 1 ? Mix[f] : Mix[f] + (Mix[f + 1] - Mix[f]) * p;
            pose.IkBendPositive[Constraint] = BendPositive[f];
        }
    }

    internal sealed class FfdTimeline : Timeline
    {
        public int Slot;
        public string Attachment;          // the attachment's key in its skin
        public float[][] Vertices;         // mesh: local vertices; skinned mesh: offsets per bone influence
        public FfdTimeline(int frames) : base(frames) => Vertices = new float[frames][];

        public override void Apply(SpinePose pose, float t)
        {
            var (f, p) = Locate(t);
            if (f < 0) return;
            var a = Vertices[f];
            if (f == Times.Length - 1) { pose.Deform[(Slot, Attachment)] = a; return; }
            var b = Vertices[f + 1];
            var v = new float[a.Length];
            for (int i = 0; i < v.Length; i++) v[i] = a[i] + (b[i] - a[i]) * p;
            pose.Deform[(Slot, Attachment)] = v;
        }
    }

    internal sealed class DrawOrderTimeline : Timeline
    {
        public int[][] Orders;              // null = the setup order
        public DrawOrderTimeline(int frames) : base(frames) => Orders = new int[frames][];

        public override void Apply(SpinePose pose, float t)
        {
            var (f, _) = Locate(t);
            if (f >= 0 && Orders[f] != null) pose.DrawOrder = Orders[f];
        }
    }
}

/// <summary>A skeleton's state at one moment of an animation: bone locals, slot attachments and colours, IK, deforms.</summary>
public sealed class SpinePose
{
    public readonly SpineSkeleton Setup;
    public readonly SpineSkeleton.Bone[] Bones;
    public readonly string[] Attachments;
    public readonly uint[] Colors;
    public readonly float[] IkMix;
    public readonly bool[] IkBendPositive;
    internal readonly float[] RotationIk;
    public int[] DrawOrder;
    public readonly Dictionary<(int slot, string attachment), float[]> Deform = new();

    public SpinePose(SpineSkeleton setup)
    {
        Setup = setup;
        Bones = setup.Bones.Select(b => new SpineSkeleton.Bone
        {
            Name = b.Name, Parent = b.Parent, X = b.X, Y = b.Y, ScaleX = b.ScaleX, ScaleY = b.ScaleY, Rotation = b.Rotation,
            Length = b.Length, InheritScale = b.InheritScale, InheritRotation = b.InheritRotation,
        }).ToArray();
        Attachments = setup.Slots.Select(s => s.Attachment).ToArray();
        Colors = setup.Slots.Select(s => s.Color).ToArray();
        IkMix = setup.IkConstraints.Select(c => c.Mix).ToArray();
        IkBendPositive = setup.IkConstraints.Select(c => c.BendPositive).ToArray();
        RotationIk = new float[Bones.Length];
        DrawOrder = Enumerable.Range(0, setup.Slots.Count).ToArray();
    }

    /// <summary>World transforms the way Spine 2.1 does them: bones in IK groups, each constraint solved in turn.</summary>
    internal void UpdateWorld()
    {
        for (int i = 0; i < Bones.Length; i++) RotationIk[i] = Bones[i].Rotation;
        var cache = Setup.BoneCache();
        for (int i = 0; ; i++)
        {
            foreach (int b in cache[i]) UpdateBone(b);
            if (i == cache.Count - 1) break;
            SolveIk(i);
        }
    }

    private void UpdateBone(int index)
    {
        var b = Bones[index];
        var p = b.Parent >= 0 ? Bones[b.Parent] : null;
        float rotation = RotationIk[index];
        if (p != null)
        {
            b.WorldX = b.X * p.A + b.Y * p.B + p.WorldX;
            b.WorldY = b.X * p.C + b.Y * p.D + p.WorldY;
            b.WorldScaleX = b.InheritScale ? p.WorldScaleX * b.ScaleX : b.ScaleX;
            b.WorldScaleY = b.InheritScale ? p.WorldScaleY * b.ScaleY : b.ScaleY;
            b.WorldRotation = b.InheritRotation ? p.WorldRotation + rotation : rotation;
        }
        else
        {
            b.WorldX = b.X; b.WorldY = b.Y;
            b.WorldScaleX = b.ScaleX; b.WorldScaleY = b.ScaleY;
            b.WorldRotation = rotation;
        }
        double rad = b.WorldRotation * Math.PI / 180.0;
        float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
        b.A = cos * b.WorldScaleX; b.C = sin * b.WorldScaleX;
        b.B = -sin * b.WorldScaleY; b.D = cos * b.WorldScaleY;
    }

    private const float RadDeg = 180f / (float)Math.PI;

    private void SolveIk(int c)
    {
        var ik = Setup.IkConstraints[c];
        var target = Bones[ik.Target];
        float mix = IkMix[c];
        if (ik.Bones.Count == 1) SolveOne(ik.Bones[0], target.WorldX, target.WorldY, mix);
        else if (ik.Bones.Count == 2) SolveTwo(ik.Bones[0], ik.Bones[1], target.WorldX, target.WorldY, IkBendPositive[c] ? 1 : -1, mix);
    }

    private void SolveOne(int index, float targetX, float targetY, float alpha)
    {
        var bone = Bones[index];
        float parentRotation = !bone.InheritRotation || bone.Parent < 0 ? 0 : Bones[bone.Parent].WorldRotation;
        float rotation = bone.Rotation;
        float rotationIk = (float)Math.Atan2(targetY - bone.WorldY, targetX - bone.WorldX) * RadDeg - parentRotation;
        RotationIk[index] = rotation + (rotationIk - rotation) * alpha;
    }

    private void SolveTwo(int parentIndex, int childIndex, float targetX, float targetY, int bendDirection, float alpha)
    {
        var parent = Bones[parentIndex];
        var child = Bones[childIndex];
        float childRotation = child.Rotation, parentRotation = parent.Rotation;
        if (alpha == 0) { RotationIk[childIndex] = childRotation; RotationIk[parentIndex] = parentRotation; return; }
        float positionX, positionY;
        if (parent.Parent >= 0)
        {
            var pp = Bones[parent.Parent];
            (positionX, positionY) = WorldToLocal(pp, targetX, targetY);
            targetX = (positionX - parent.X) * pp.WorldScaleX;
            targetY = (positionY - parent.Y) * pp.WorldScaleY;
        }
        else
        {
            targetX -= parent.X;
            targetY -= parent.Y;
        }
        if (child.Parent == parentIndex) { positionX = child.X; positionY = child.Y; }
        else
        {
            (positionX, positionY) = Bones[child.Parent].ToWorld(child.X, child.Y);
            (positionX, positionY) = WorldToLocal(parent, positionX, positionY);
        }
        float childX = positionX * parent.WorldScaleX, childY = positionY * parent.WorldScaleY;
        float offset = (float)Math.Atan2(childY, childX);
        float len1 = (float)Math.Sqrt(childX * childX + childY * childY), len2 = child.Length * child.WorldScaleX;
        float cosDenom = 2 * len1 * len2;
        if (cosDenom < 0.0001f)
        {
            RotationIk[childIndex] = childRotation + ((float)Math.Atan2(targetY, targetX) * RadDeg - parentRotation - childRotation) * alpha;
            return;
        }
        float cos = (targetX * targetX + targetY * targetY - len1 * len1 - len2 * len2) / cosDenom;
        cos = Math.Max(-1, Math.Min(1, cos));
        float childAngle = (float)Math.Acos(cos) * bendDirection;
        float adjacent = len1 + len2 * cos, opposite = len2 * (float)Math.Sin(childAngle);
        float parentAngle = (float)Math.Atan2(targetY * adjacent - targetX * opposite, targetX * adjacent + targetY * opposite);
        float rotation = (parentAngle - offset) * RadDeg - parentRotation;
        rotation = rotation > 180 ? rotation - 360 : rotation < -180 ? rotation + 360 : rotation;
        RotationIk[parentIndex] = parentRotation + rotation * alpha;
        rotation = (childAngle + offset) * RadDeg - childRotation;
        rotation = rotation > 180 ? rotation - 360 : rotation < -180 ? rotation + 360 : rotation;
        // The parent's world rotation is still last update's here, as in Spine 2.1.
        RotationIk[childIndex] = childRotation + (rotation + parent.WorldRotation - Bones[child.Parent].WorldRotation) * alpha;
    }

    /// <summary>Spine 2.1's worldToLocal (its inverse uses m00/m11 as written there; exact for the uniform scales DD1 uses).</summary>
    private static (float, float) WorldToLocal(SpineSkeleton.Bone b, float worldX, float worldY)
    {
        float dx = worldX - b.WorldX, dy = worldY - b.WorldY;
        float invDet = 1 / (b.A * b.D - b.B * b.C);
        return (dx * b.A * invDet - dy * b.B * invDet, dy * b.D * invDet - dx * b.C * invDet);
    }
}

public sealed partial class SpineSkeleton
{
    public sealed class IkConstraint
    {
        public string Name;
        public readonly List<int> Bones = new();
        public int Target;
        public float Mix;
        public bool BendPositive;
    }

    public readonly List<IkConstraint> IkConstraints = new();
    /// <summary>Skins in file order (null = the default skin, listed first when it has attachments); FFD timelines index this.</summary>
    public readonly List<string> SkinOrder = new();
    public readonly List<SpineEventData> Events = new();
    public readonly List<SpineAnimation> Animations = new();
    /// <summary>How far the reader got: the whole file when everything parsed.</summary>
    public int BytesRead;

    public SpineAnimation Animation(string name) => Animations.FirstOrDefault(a => a.Name == name) ?? (name == null ? Animations.FirstOrDefault() : null);

    private List<List<int>> _boneCache;

    /// <summary>Spine 2.1's bone groups: bones outside every IK chain first, then the bones each constraint moves.</summary>
    internal List<List<int>> BoneCache()
    {
        if (_boneCache != null) return _boneCache;
        int ikCount = IkConstraints.Count;
        var cache = Enumerable.Range(0, ikCount + 1).Select(_ => new List<int>()).ToList();
        for (int b = 0; b < Bones.Count; b++)
        {
            int current = b;
            bool placed = false;
            do
            {
                for (int c = 0; c < ikCount && !placed; c++)
                {
                    var ik = IkConstraints[c];
                    int parent = ik.Bones[0], child = ik.Bones[ik.Bones.Count - 1];
                    while (true)
                    {
                        if (current == child) { cache[c].Add(b); cache[c + 1].Add(b); placed = true; break; }
                        if (child == parent || child < 0) break;
                        child = Bones[child].Parent;
                    }
                }
                if (placed) break;
                current = Bones[current].Parent;
            } while (current >= 0);
            if (!placed) cache[0].Add(b);
        }
        return _boneCache = cache;
    }

    /// <summary>
    /// The animation at <paramref name="time"/> seconds (looped when asked) as textured pieces in draw order, like
    /// <see cref="SetupPose"/>. A missing animation gives the setup pose.
    /// </summary>
    public List<Piece> Pose(SpineAtlas atlas, string animation, float time, bool loop = true, Func<Slot, bool> include = null, string skin = null)
    {
        var pose = new SpinePose(this);
        var anim = Animation(animation);
        if (anim != null)
        {
            float t = loop && anim.Duration > 0 ? time % anim.Duration : Math.Min(time, anim.Duration);
            if (t < 0) t += anim.Duration;
            foreach (var tl in anim.Timelines) tl.Apply(pose, t);
        }
        pose.UpdateWorld();
        return Pieces(pose, atlas, include, skin);
    }

    private List<Piece> Pieces(SpinePose pose, SpineAtlas atlas, Func<Slot, bool> include, string skin)
    {
        var pieces = new List<Piece>();
        Dictionary<(int, string), Attachment> extra = null;
        if (skin != null) Skins.TryGetValue(skin, out extra);
        foreach (int si in pose.DrawOrder)
        {
            var slot = Slots[si];
            string name = pose.Attachments[si];
            if (name == null || (include != null && !include(slot))) continue;
            Attachment a = null;
            if (extra != null) extra.TryGetValue((si, name), out a);
            if (a == null) DefaultSkin.TryGetValue((si, name), out a);
            if (a == null || a.Type == AttachmentType.BoundingBox) continue;
            if (!atlas.Regions.TryGetValue(a.Path, out var region)) continue;
            var bone = pose.Bones[slot.Bone];
            var piece = new Piece { Slot = slot.Name, Page = region.Page, Additive = slot.Additive, Color = Multiply(pose.Colors[si], a.Color) };
            pose.Deform.TryGetValue((si, name), out var deform);
            switch (a.Type)
            {
                case AttachmentType.Region: RegionPiece(piece, a, region, bone); break;
                case AttachmentType.Mesh:
                    if (deform != null && deform.Length == a.Vertices.Length) MeshPiece(piece, new Attachment { Vertices = deform, RegionUvs = a.RegionUvs, Triangles = a.Triangles }, region, bone);
                    else MeshPiece(piece, a, region, bone);
                    break;
                case AttachmentType.SkinnedMesh: SkinnedPiece(piece, a, region, pose.Bones, deform); break;
            }
            pieces.Add(piece);
        }
        return pieces;
    }

    private static void SkinnedPiece(Piece piece, Attachment a, SpineAtlas.Region region, IReadOnlyList<Bone> bones, float[] offsets)
    {
        var w = a.Vertices;
        var positions = new List<float>();
        int influence = 0;
        for (int i = 0; i < w.Length;)
        {
            int boneCount = (int)w[i++];
            float x = 0, y = 0;
            for (int k = 0; k < boneCount; k++, i += 4, influence++)
            {
                var bone = bones[(int)w[i]];
                float lx = w[i + 1], ly = w[i + 2];
                if (offsets != null && influence * 2 + 1 < offsets.Length) { lx += offsets[influence * 2]; ly += offsets[influence * 2 + 1]; }
                var (bx, by) = bone.ToWorld(lx, ly);
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

    private static int Influences(Attachment a)
    {
        int n = 0;
        for (int i = 0; i < a.Vertices.Length;)
        {
            int boneCount = (int)a.Vertices[i++];
            n += boneCount;
            i += boneCount * 4;
        }
        return n;
    }

    // ---- reading (Spine 2.1 binary, after the skins) ----

    private const int TimelineScale = 0, TimelineRotate = 1, TimelineTranslate = 2, TimelineAttachment = 3, TimelineColor = 4,
                      TimelineFlipX = 5, TimelineFlipY = 6;

    private void ReadEventsAndAnimations(Reader r)
    {
        if (r.AtEnd) return;
        for (int i = 0, n = r.VarInt(); i < n; i++)
            Events.Add(new SpineEventData { Name = r.String(), Int = r.SignedVarInt(), Float = r.Float(), String = r.String() });
        for (int i = 0, n = r.VarInt(); i < n; i++)
        {
            string name = r.String();
            Animations.Add(ReadAnimation(r, name));
        }
    }

    private static void ReadCurve(Reader r, SpineAnimation.Curves curves, int frame)
    {
        switch (r.Byte())
        {
            case 1: curves.SetStepped(frame); break;
            case 2: curves.SetBezier(frame, r.Float(), r.Float(), r.Float(), r.Float()); break;
        }
    }

    private SpineAnimation ReadAnimation(Reader r, string name)
    {
        var anim = new SpineAnimation { Name = name };
        void Add(SpineAnimation.Timeline t)
        {
            anim.Timelines.Add(t);
            anim.Duration = Math.Max(anim.Duration, t.Times[t.Times.Length - 1]);
        }

        for (int i = 0, n = r.VarInt(); i < n; i++)          // slot timelines
        {
            int slot = r.VarInt();
            for (int j = 0, m = r.VarInt(); j < m; j++)
            {
                int type = r.Byte(), frames = r.VarInt();
                switch (type)
                {
                    case TimelineColor:
                    {
                        var t = new SpineAnimation.ColorTimeline(frames) { Slot = slot };
                        for (int f = 0; f < frames; f++)
                        {
                            t.Times[f] = r.Float();
                            t.Colors[f] = (uint)r.Int();
                            if (f < frames - 1) ReadCurve(r, t.Curve, f);
                        }
                        Add(t);
                        break;
                    }
                    case TimelineAttachment:
                    {
                        var t = new SpineAnimation.AttachmentTimeline(frames) { Slot = slot };
                        for (int f = 0; f < frames; f++) { t.Times[f] = r.Float(); t.Names[f] = r.String(); }
                        Add(t);
                        break;
                    }
                    default: throw new InvalidDataException($"Unknown Spine slot timeline {type} at byte {r.Position}");
                }
            }
        }

        for (int i = 0, n = r.VarInt(); i < n; i++)          // bone timelines
        {
            int bone = r.VarInt();
            for (int j = 0, m = r.VarInt(); j < m; j++)
            {
                int type = r.Byte(), frames = r.VarInt();
                switch (type)
                {
                    case TimelineRotate:
                    {
                        var t = new SpineAnimation.RotateTimeline(frames) { Bone = bone };
                        for (int f = 0; f < frames; f++)
                        {
                            t.Times[f] = r.Float();
                            t.Angles[f] = r.Float();
                            if (f < frames - 1) ReadCurve(r, t.Curve, f);
                        }
                        Add(t);
                        break;
                    }
                    case TimelineTranslate:
                    case TimelineScale:
                    {
                        var t = new SpineAnimation.TranslateTimeline(frames) { Bone = bone, Scale = type == TimelineScale };
                        for (int f = 0; f < frames; f++)
                        {
                            t.Times[f] = r.Float();
                            t.X[f] = r.Float();
                            t.Y[f] = r.Float();
                            if (f < frames - 1) ReadCurve(r, t.Curve, f);
                        }
                        Add(t);
                        break;
                    }
                    case TimelineFlipX:
                    case TimelineFlipY:
                    {
                        // Bone flips: read and skip (DD1 doesn't flip bones in its animations).
                        float last = 0;
                        for (int f = 0; f < frames; f++) { last = r.Float(); r.Byte(); }
                        anim.Duration = Math.Max(anim.Duration, last);
                        break;
                    }
                    default: throw new InvalidDataException($"Unknown Spine bone timeline {type} at byte {r.Position}");
                }
            }
        }

        for (int i = 0, n = r.VarInt(); i < n; i++)          // IK timelines
        {
            int constraint = r.VarInt(), frames = r.VarInt();
            var t = new SpineAnimation.IkTimeline(frames) { Constraint = constraint };
            for (int f = 0; f < frames; f++)
            {
                t.Times[f] = r.Float();
                t.Mix[f] = r.Float();
                t.BendPositive[f] = (sbyte)r.Byte() >= 0;
                if (f < frames - 1) ReadCurve(r, t.Curve, f);
            }
            Add(t);
        }

        for (int i = 0, n = r.VarInt(); i < n; i++)          // FFD (mesh deform) timelines
        {
            int skinIndex = r.VarInt();
            string skinName = skinIndex < SkinOrder.Count ? SkinOrder[skinIndex] : null;
            var skin = skinName == null ? DefaultSkin : Skins[skinName];
            for (int j = 0, m = r.VarInt(); j < m; j++)
            {
                int slot = r.VarInt();
                for (int k = 0, o = r.VarInt(); k < o; k++)
                {
                    string key = r.String();
                    skin.TryGetValue((slot, key), out var attachment);
                    int frames = r.VarInt();
                    bool mesh = attachment?.Type == AttachmentType.Mesh;
                    int vertexCount = attachment == null ? 0 : mesh ? attachment.Vertices.Length : Influences(attachment) * 2;
                    var t = new SpineAnimation.FfdTimeline(frames) { Slot = slot, Attachment = key };
                    for (int f = 0; f < frames; f++)
                    {
                        t.Times[f] = r.Float();
                        int end = r.VarInt();
                        float[] v;
                        if (end == 0) v = mesh ? (float[])attachment.Vertices.Clone() : new float[vertexCount];
                        else
                        {
                            int start = r.VarInt();
                            end += start;
                            v = new float[Math.Max(vertexCount, end)];
                            for (int x = start; x < end; x++) v[x] = r.Float();
                            if (mesh) for (int x = 0; x < attachment.Vertices.Length; x++) v[x] += attachment.Vertices[x];
                        }
                        t.Vertices[f] = v;
                        if (f < frames - 1) ReadCurve(r, t.Curve, f);
                    }
                    Add(t);
                }
            }
        }

        int drawOrders = r.VarInt();                          // draw order timeline
        if (drawOrders > 0)
        {
            var t = new SpineAnimation.DrawOrderTimeline(drawOrders);
            int slotCount = Slots.Count;
            for (int i = 0; i < drawOrders; i++)
            {
                int offsets = r.VarInt();
                var order = Enumerable.Repeat(-1, slotCount).ToArray();
                var unchanged = new int[slotCount - offsets];
                int original = 0, unchangedIndex = 0;
                for (int j = 0; j < offsets; j++)
                {
                    int slot = r.VarInt();
                    while (original != slot) unchanged[unchangedIndex++] = original++;
                    order[original + r.VarInt()] = original++;
                }
                while (original < slotCount) unchanged[unchangedIndex++] = original++;
                for (int j = slotCount - 1; j >= 0; j--)
                    if (order[j] == -1) order[j] = unchanged[--unchangedIndex];
                t.Times[i] = r.Float();
                t.Orders[i] = order;
            }
            Add(t);
        }

        int events = r.VarInt();                              // event timeline
        for (int i = 0; i < events; i++)
        {
            float time = r.Float();
            var data = Events[r.VarInt()];
            r.SignedVarInt();
            r.Float();
            if (r.Bool()) r.String();
            anim.Events.Add((time, data.Name));
            anim.Duration = Math.Max(anim.Duration, time);
        }
        return anim;
    }
}
