using System;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Sample an owned humanoid walk in distance space, retaining its coordinated body and foot roll.</summary>
public sealed class HumanoidWalk
{
    // Small scalar pose types keep the runtime independent of Unity and extra
    // numerics assemblies which the game's Mono installation does not ship.
    public readonly struct Vector3
    {
        public readonly float X,Y,Z;
        public Vector3(float x,float y,float z) { X=x;Y=y;Z=z; }
        public static Vector3 Zero => new(0,0,0);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Vector3 operator *(Vector3 a,float b) => new(a.X*b,a.Y*b,a.Z*b);
        public static Vector3 operator /(Vector3 a,float b) => a*(1/b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) => a+(b-a)*t;
        public static float Distance(Vector3 a,Vector3 b)
        { var d=a-b;return (float)Math.Sqrt(d.X*d.X+d.Y*d.Y+d.Z*d.Z); }
    }

    public readonly struct Quaternion
    {
        public readonly float X,Y,Z,W;
        public Quaternion(float x,float y,float z,float w) { X=x;Y=y;Z=z;W=w; }
        public static Quaternion Normalize(Quaternion q)
        {
            float length=(float)Math.Sqrt(Dot(q,q));
            if (length<.000001f) throw new FormatException("Invalid walk rotation");
            return new(q.X/length,q.Y/length,q.Z/length,q.W/length);
        }
        public static float Dot(Quaternion a,Quaternion b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z+a.W*b.W;
        public static Quaternion Inverse(Quaternion q) => new(-q.X,-q.Y,-q.Z,q.W);
        public static Quaternion operator *(Quaternion a,Quaternion b) => new(
            a.W*b.X+a.X*b.W+a.Y*b.Z-a.Z*b.Y,a.W*b.Y-a.X*b.Z+a.Y*b.W+a.Z*b.X,
            a.W*b.Z+a.X*b.Y-a.Y*b.X+a.Z*b.W,a.W*b.W-a.X*b.X-a.Y*b.Y-a.Z*b.Z);
        public Vector3 Transform(Vector3 v)
        {
            var r=this*new Quaternion(v.X,v.Y,v.Z,0)*Inverse(this);
            return new(r.X,r.Y,r.Z);
        }
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {
            float dot=Dot(a,b),sign=dot<0 ? -1 : 1;dot=Math.Abs(dot);
            double x=1-t,y=t;
            if (dot<.9995f)
            { double angle=Math.Acos(Math.Min(1,dot)),s=Math.Sin(angle);x=Math.Sin((1-t)*angle)/s;y=Math.Sin(t*angle)/s; }
            return Normalize(new Quaternion((float)(a.X*x+b.X*y*sign),(float)(a.Y*x+b.Y*y*sign),
                (float)(a.Z*x+b.Z*y*sign),(float)(a.W*x+b.W*y*sign)));
        }
    }

    private struct Transform
    {
        public Vector3 Translation;
        public Quaternion Rotation;
        public float Scale;
    }
    public struct Foot
    {
        public Vector3 Position, Knee, Toe;
        public Quaternion Rotation;
        public bool Planted, ToePivot;
        public float ContactProgress, ContactLength, SwingProgress, Clearance;
    }

    public struct Pose
    {
        public Vector3 Hips;
        public Quaternion Pelvis, Spine, Chest, Head;
        public Foot Left, Right;
    }

    private const int Count = 240;
    private readonly Pose[] _poses = new Pose[Count+1];
    private readonly float[] _travel = new float[Count+1];
    private readonly float[] _start = new float[2], _stance = new float[2];
    public float LegLength { get; }
    public float DistancePerCycle { get; }
    public float LeftHipSide { get; }
    public float RightHipSide { get; }

    public HumanoidWalk(GltfModel model, string clipName, float[] distances)
    {
        var clip=model.Animations.Single(c => c.Name==clipName);
        ExportedHeroMotion.AdvanceWalkByMotion(0,0,1,clip.Duration,distances);
        DistancePerCycle=distances[distances.Length-1];
        int Find(string name) => model.Nodes.FindIndex(n => n.Name==name);
        string[] names={"Pelvis","Spine1","Spine2","Head","L_Thigh","L_Knee","L_Foot","L_Toe0","R_Thigh","R_Knee","R_Foot","R_Toe0"};
        var joints=names.Select(Find).ToArray();
        if (joints.Any(i => i<0)) throw new FormatException("Walk is missing humanoid joints");
        var needed=new bool[model.Nodes.Count];
        foreach (int joint in joints)
            for (int i=joint,depth=0;i>=0;i=model.Nodes[i].Parent)
            { if (++depth>model.Nodes.Count) throw new FormatException("Cyclic walk hierarchy");needed[i]=true; }
        var positions=new Vector3[Count+1][];
        var rotations=new Quaternion[Count+1][];
        var local=new Transform[model.Nodes.Count];
        var world=new Transform[model.Nodes.Count];
        var done=new bool[model.Nodes.Count];
        var translation=new Vector3[model.Nodes.Count];
        var rotation=new Quaternion[model.Nodes.Count];
        var scale=new Vector3[model.Nodes.Count];
        var scratch=new float[4];
        Vector3 V(float[] a) => new(a[0],a[1],a[2]);
        Quaternion Q(float[] a) => Quaternion.Normalize(new Quaternion(a[0],a[1],a[2],a[3]));
        Transform World(int i)
        {
            if (done[i]) return world[i];
            done[i]=true;
            if (model.Nodes[i].Parent<0) return world[i]=local[i];
            var p=World(model.Nodes[i].Parent);var n=local[i];
            return world[i]=new Transform { Translation=p.Translation+p.Rotation.Transform(n.Translation*p.Scale),
                Rotation=Quaternion.Normalize(p.Rotation*n.Rotation),Scale=p.Scale*n.Scale };
        }
        for (int frame=0;frame<=Count;frame++)
        {
            float time=frame==Count ? clip.Duration : ExportedHeroMotion.AdvanceWalkByMotion(0,
                frame/(float)Count*DistancePerCycle,1,clip.Duration,distances);
            for (int i=0;i<model.Nodes.Count;i++)
            { var n=model.Nodes[i]; translation[i]=V(n.Translation); rotation[i]=Q(n.Rotation); scale[i]=V(n.Scale); }
            foreach (var channel in clip.Channels)
            {
                if (!needed[channel.Node]) continue;
                GltfModel.Sample(channel,time,scratch);
                if (channel.Path=='r') rotation[channel.Node]=Q(scratch);
                else if (channel.Path=='t') translation[channel.Node]=V(scratch);
                else scale[channel.Node]=V(scratch);
            }
            Array.Clear(done,0,done.Length);
            for (int i=0;i<local.Length;i++)
            {
                if (!needed[i]) continue;
                if (Math.Abs(scale[i].X-scale[i].Y)>.0001f || Math.Abs(scale[i].X-scale[i].Z)>.0001f)
                    throw new FormatException("Walk has nonuniform bone scale");
                local[i]=new Transform { Translation=translation[i],Rotation=rotation[i],Scale=scale[i].X };
            }
            positions[frame]=joints.Select(i => World(i).Translation).ToArray();
            rotations[frame]=joints.Select(i => World(i).Rotation).ToArray();
        }
        var first=positions[0];
        LegLength=Vector3.Distance(first[4],first[5])+Vector3.Distance(first[5],first[6]);
        if (LegLength<.1f || DistancePerCycle/LegLength<.5f || DistancePerCycle/LegLength>5)
            throw new FormatException("Invalid walk proportions");
        float ankleFloor=positions.Min(p => Math.Min(p[6].Y,p[10].Y));
        float toeFloor=positions.Min(p => Math.Min(p[7].Y,p[11].Y));
        Vector3 centre=positions.Take(Count).Select(p => (p[4]+p[8])*.5f).Aggregate(Vector3.Zero,(a,b) => a+b)/Count;
        LeftHipSide=(positions.Take(Count).Average(p => p[4].X)-centre.X)/LegLength;
        RightHipSide=(positions.Take(Count).Average(p => p[8].X)-centre.X)/LegLength;
        Quaternion Mean(int bone)
        {
            var reference=rotations[0][bone];float x=0,y=0,z=0,w=0;
            for (int i=0;i<Count;i++)
            { var q=rotations[i][bone];float sign=Quaternion.Dot(reference,q)<0 ? -1 : 1;x+=q.X*sign;y+=q.Y*sign;z+=q.Z*sign;w+=q.W*sign; }
            return Quaternion.Normalize(new Quaternion(x,y,z,w));
        }
        var means=Enumerable.Range(0,4).Select(Mean).ToArray();
        int Flat(int bone) => Enumerable.Range(0,Count).OrderBy(i => positions[i][bone].Y).First();
        var flats=new[]{rotations[Flat(6)][6],rotations[Flat(10)][10]};
        for (int i=0;i<=Count;i++)
        {
            var p=positions[i];var r=rotations[i];
            Vector3 Normal(Vector3 v) => (v-new Vector3(centre.X,ankleFloor,centre.Z))/LegLength;
            Quaternion Delta(int bone) => Quaternion.Normalize(r[bone]*Quaternion.Inverse(means[bone]));
            Foot FootAt(int side)
            {
                int hip=4+side*4,ankle=hip+2,toe=hip+3;
                float a=p[ankle].Y-ankleFloor,t=p[toe].Y-toeFloor;
                return new Foot { Position=Normal(p[ankle]),Knee=Normal(p[hip+1]),Toe=Normal(p[toe]),
                    Rotation=Quaternion.Normalize(r[ankle]*Quaternion.Inverse(flats[side])),
                    Clearance=Math.Max(0,Math.Min(a,t))/LegLength,
                    Planted=Math.Min(a,t)<LegLength*.025f,ToePivot=t<a };
            }
            _poses[i]=new Pose { Hips=Normal((p[4]+p[8])*.5f),Pelvis=Delta(0),Spine=Delta(1),
                Chest=Delta(2),Head=Delta(3),Left=FootAt(0),Right=FootAt(1) };
        }
        for (int i=0;i<Count;i++)
        {
            Foot Contact(Foot foot,Foot before,Foot after)
            {
                float backwards=foot.ToePivot ? before.Toe.Z-after.Toe.Z : before.Position.Z-after.Position.Z;
                // A heel can be close to the floor while still reaching forward.
                // It takes support only once its contact point travels backward.
                foot.Planted=foot.Planted && backwards>.0001f;
                return foot;
            }
            var before=_poses[(i+Count-1)%Count];var after=_poses[(i+1)%Count];
            var p=_poses[i];p.Left=Contact(p.Left,before.Left,after.Left);p.Right=Contact(p.Right,before.Right,after.Right);_poses[i]=p;
        }
        _poses[Count]=_poses[0];
        for (int side=0;side<2;side++)
        {
            bool Ground(int i) => (side==0 ? _poses[(i+Count)%Count].Left : _poses[(i+Count)%Count].Right).Planted;
            var starts=Enumerable.Range(0,Count).Where(i => Ground(i) && !Ground(i-1)).ToArray();
            var ends=Enumerable.Range(0,Count).Where(i => !Ground(i) && Ground(i-1)).ToArray();
            if (starts.Length!=1 || ends.Length!=1) throw new FormatException("Walk must have one support and recovery per foot");
            _start[side]=starts[0]/(float)Count;
            _stance[side]=Wrap(ends[0]/(float)Count-_start[side]);
        }
        // The exported movement curve includes motion while a foot is still
        // arriving or leaving. Calibrate retargeted travel to the actual sole,
        // favouring the foot nearest the floor during the support transfer.
        for (int i=1;i<=Count;i++)
        {
            float weighted=0,total=0;
            void Support(Foot a,Foot b)
            {
                if (!a.Planted && !b.Planted) return;
                float clearance=Math.Max(a.Clearance,b.Clearance);
                float weight=Math.Max(0,1-clearance/.04f);weight*=weight;
                if (weight<=0) return;
                float roll=b.ToePivot ? (b.Position.Z-b.Toe.Z)-(a.Position.Z-a.Toe.Z) : 0;
                float travel=a.Position.Z-b.Position.Z+roll;
                if (travel<=0) return;
                weighted+=travel*weight;total+=weight;
            }
            Support(_poses[i-1].Left,_poses[i].Left);Support(_poses[i-1].Right,_poses[i].Right);
            _travel[i]=_travel[i-1]+Math.Max(.000001f,total>0 ? weighted/total : 0);
        }
        float period=_travel[Count];DistancePerCycle=period*LegLength;
        for (int i=0;i<=Count;i++) _travel[i]/=period;
        for (int side=0;side<2;side++)
        {
            int start=(int)Math.Round(_start[side]*Count),end=(int)Math.Round(Wrap(_start[side]+_stance[side])*Count)%Count;
            _start[side]=_travel[start];_stance[side]=Wrap(_travel[end]-_start[side]);
        }
    }

    public Pose Sample(float phase)
    {
        phase=Wrap(phase);int lo=0,hi=Count;
        while(hi-lo>1) { int mid=(lo+hi)/2;if(_travel[mid]<=phase)lo=mid;else hi=mid; }
        int i=lo;float t=(phase-_travel[i])/(_travel[i+1]-_travel[i]);
        var a=_poses[i];var b=_poses[i+1];
        Foot Blend(Foot x,Foot y,int side)
        {
            float p=Wrap(phase-_start[side]);bool planted=p<_stance[side];
            return new Foot { Position=Vector3.Lerp(x.Position,y.Position,t),Knee=Vector3.Lerp(x.Knee,y.Knee,t),
                Toe=Vector3.Lerp(x.Toe,y.Toe,t),Rotation=Quaternion.Slerp(x.Rotation,y.Rotation,t),
                Clearance=x.Clearance+(y.Clearance-x.Clearance)*t,ToePivot=t<.5 ? x.ToePivot : y.ToePivot,
                Planted=planted,ContactLength=_stance[side],ContactProgress=p/_stance[side],
                SwingProgress=planted ? 0 : (p-_stance[side])/(1-_stance[side]) };
        }
        return new Pose { Hips=Vector3.Lerp(a.Hips,b.Hips,t),Pelvis=Quaternion.Slerp(a.Pelvis,b.Pelvis,t),
            Spine=Quaternion.Slerp(a.Spine,b.Spine,t),Chest=Quaternion.Slerp(a.Chest,b.Chest,t),
            Head=Quaternion.Slerp(a.Head,b.Head,t),Left=Blend(a.Left,b.Left,0),Right=Blend(a.Right,b.Right,1) };
    }

    public static float Wrap(float phase) => float.IsNaN(phase) || float.IsInfinity(phase) ? 0 : phase-(float)Math.Floor(phase);
}
