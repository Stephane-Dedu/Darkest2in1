using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Native painted grass, with clear walking routes and building aprons.</summary>
public static class HamletVegetation
{
    public const string GroundPath="Assets/Art/Textures/Environments/Tileable/grass_dry.mat";
    public const string TuftPath="Assets/Art/Materials/Environments/Farm/Nodes/farm_gate_node_assets_grass.mat";
    public readonly struct Clump
    {
        public readonly float X,Y,Z,Height,Yaw;
        public readonly int Variant;
        public Clump(float x,float y,float z,float height,float yaw,int variant)
        { X=x; Y=y; Z=z; Height=height; Yaw=yaw; Variant=variant; }
    }
    // Reviewed rectangles in the original 4096px atlas, top-left image coordinates.
    // Keep the neighbouring gate, wall and bell outside these rectangles.
    private static readonly float[][] Crops={ new[] { .008f,.125f,.124f,.355f },
        new[] { .24f,.205f,.34f,.368f },new[] { .735f,.07f,.885f,.323f } };
    public static readonly IReadOnlyList<Clump> Clumps=Place();

    public static float Clearance(float x,float z)
    {
        float clearance=float.MaxValue;
        foreach (var route in HamletLayout.Routes) for (int i=1;i<route.Points.Length;i++)
        {
            var a=route.Points[i-1]; var b=route.Points[i];
            float dx=b.X-a.X,dz=b.Z-a.Z,length=dx*dx+dz*dz;
            float t=length>0?Math.Max(0,Math.Min(1,((x-a.X)*dx+(z-a.Z)*dz)/length)):0;
            float rx=x-a.X-dx*t,rz=z-a.Z-dz*t;
            clearance=Math.Min(clearance,(float)Math.Sqrt(rx*rx+rz*rz)-route.Width*.5f);
        }
        foreach (var site in HamletLayout.Sites)
        {
            float angle=site.Yaw*(float)Math.PI/180,c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            float dx=x-site.X,dz=z-site.Z;
            clearance=Math.Min(clearance,Math.Max(Math.Abs(dx*c-dz*s)-site.FootprintWidth*.5f,
                Math.Abs(dx*s+dz*c)-site.FootprintDepth*.5f)-.5f);
        }
        return clearance;
    }
    public static float Cover(float x,float z)
    {
        float clearance=Clearance(x,z);
        if (clearance<=.4f) return 0;
        float patch=(float)(Math.Sin(x*.14+Math.Sin(z*.09)*1.7)*Math.Cos(z*.16-x*.04));
        return Smooth(.4f,3.5f,clearance)*Smooth(-.65f,.55f,patch)*.82f;
    }
    private static float Smooth(float a,float b,float value)
    { float t=Math.Max(0,Math.Min(1,(value-a)/(b-a))); return t*t*(3-2*t); }
    private static Clump[] Place()
    {
        var result=new List<Clump>();
        for (int row=0;row<31;row++) for (int col=0;col<36;col++)
        {
            int seed=row*137+col*73;
            float x=HamletLayout.MinX+2+col*3.4f+(float)Math.Sin(seed*1.73)*1.1f;
            float z=HamletLayout.MinZ+2+row*3.3f+(float)Math.Cos(seed*2.31)*1.1f;
            if (x<HamletLayout.MinX+2 || z<HamletLayout.MinZ+2 || x>HamletLayout.MaxX-2 || z>HamletLayout.MaxZ-2
                || Cover(x,z)<.48f || Clearance(x,z)<1.5f) continue;
            float y=HamletLayout.Height(x,z);
            // Avoid floating tufts on sharp terrace faces or the edge of a stair cutting.
            float slope=0;
            foreach (var d in new[] { (1f,0f),(-1f,0f),(0f,1f),(0f,-1f) })
                slope=Math.Max(slope,Math.Abs(HamletLayout.Height(x+d.Item1,z+d.Item2)-y));
            if (slope>.65f) continue;
            float height=.9f+.65f*(.5f+.5f*(float)Math.Sin(seed*3.17));
            result.Add(new(x,y-.15f,z,height,(float)Math.Sin(seed*1.19)*32,seed%3));
        }
        return result.ToArray();
    }
    public static HamletTerrain.Surface Build()
    {
        var positions=new List<float>(); var uv=new List<float>(); var indices=new List<int>();
        foreach (var clump in Clumps)
        {
            var crop=Crops[clump.Variant];
            float width=clump.Height*(crop[2]-crop[0])/(crop[3]-crop[1]);
            // Two angled planes give the low native blades depth while retaining their painted silhouette.
            foreach (float turn in new[] { -24f,24f })
            {
                float angle=(clump.Yaw+turn)*(float)Math.PI/180;
                float dx=(float)Math.Cos(angle)*width*.5f,dz=(float)Math.Sin(angle)*width*.5f;
                int start=positions.Count/3;
                positions.AddRange(new[] { clump.X-dx,clump.Y,clump.Z-dz, clump.X-dx,clump.Y+clump.Height,clump.Z-dz,
                    clump.X+dx,clump.Y+clump.Height,clump.Z+dz, clump.X+dx,clump.Y,clump.Z+dz });
                uv.AddRange(new[] { crop[0],1-crop[3],crop[0],1-crop[1],crop[2],1-crop[1],crop[2],1-crop[3] });
                indices.AddRange(new[] { start,start+1,start+2,start,start+2,start+3 });
            }
        }
        return new() { Positions=positions.ToArray(),Uv=uv.ToArray(),Indices=indices.ToArray() };
    }
}
