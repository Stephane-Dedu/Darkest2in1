using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Continuous lanes, stone stair treads and retaining courses, baked into two owned surfaces.</summary>
internal static class HamletGroundwork
{
    internal static void Build(out HamletTerrain.Surface paving,out HamletTerrain.Surface masonry)
    {
        var paths=new Builder(); var walls=new Builder();
        foreach (var route in HamletLayout.Routes)
        {
            var points=new List<HamletLayout.Point>();
            for (int i=1;i<route.Points.Length;i++)
            {
                var a=route.Points[i-1]; var b=route.Points[i];
                float dx=b.X-a.X,dz=b.Z-a.Z,distance=Length(dx,dz);
                int samples=Math.Max(1,(int)Math.Ceiling(distance/.65f));
                for (int sample=0;sample<samples;sample++)
                { float t=sample/(float)samples; points.Add(new(a.X+dx*t,0,a.Z+dz*t)); }
                if (i==route.Points.Length-1) points.Add(b);
                float rise=Math.Abs(b.Y-a.Y);
                if (rise<.45f) continue;
                int steps=(int)Math.Ceiling(rise/.32f); float yaw=(float)Math.Atan2(dx,dz)*180/(float)Math.PI;
                for (int step=0;step<steps;step++)
                {
                    float t=(step+.5f)/steps,x=a.X+dx*t,z=a.Z+dz*t;
                    paths.Block(x,HamletLayout.Height(x,z)-.08f,z,route.Width*.94f,.36f,distance/steps+.13f,yaw);
                }
            }
            float length=0; int start=paths.Count;
            for (int i=0;i<points.Count;i++)
            {
                var before=points[Math.Max(0,i-1)]; var after=points[Math.Min(i+1,points.Count-1)];
                float dx=after.X-before.X,dz=after.Z-before.Z,d=Length(dx,dz);
                if (i>0) length+=Length(points[i].X-points[i-1].X,points[i].Z-points[i-1].Z);
                for (int edge=0;edge<2;edge++)
                {
                    float ragged=1+.045f*(float)Math.Sin(i*1.7f+edge*2.2f),offset=route.Width*ragged*(edge==0?-.5f:.5f);
                    float x=points[i].X+dz/d*offset,z=points[i].Z-dx/d*offset;
                    paths.Vertex(x,HamletLayout.Height(x,z)+.035f,z,edge*route.Width/5,length/5);
                }
                if (i==points.Count-1) continue;
                int v=start+i*2; paths.Triangle(v,v+2,v+1); paths.Triangle(v+1,v+2,v+3);
            }
        }
        const int count=32; int square=paths.Count;
        paths.Vertex(0,HamletLayout.Height(0,-3)+.045f,-3,0,-.5f);
        for (int i=0;i<count;i++)
        {
            float angle=i*(float)Math.PI*2/count,r=1+.06f*(float)Math.Sin(i*2.1f),x=(float)Math.Cos(angle)*13*r,z=-3+(float)Math.Sin(angle)*7*r;
            paths.Vertex(x,HamletLayout.Height(x,z)+.045f,z,x/6,z/6);
            paths.Triangle(square,square+(i+1)%count+1,square+i+1);
        }
        foreach (var site in HamletLayout.Sites.Where(s=>s.Y>3))
        {
            float front=-site.FootprintDepth/2-.15f,a=site.Yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            for (float x=-site.FootprintWidth/2;x<site.FootprintWidth/2;x+=2)
            {
                if (Math.Abs(x)<2.2f) continue;
                walls.Block(site.X+x*c+front*s,site.Y-.6f,site.Z-x*s+front*c,1.95f,1.4f,.85f,site.Yaw);
            }
        }
        // Low broken boundary walls frame the forecourt, with a gap at the town entrance.
        foreach (var line in new[] { new[] { -51f,-17,-38,-20 },new[] { -25f,-24,-7,-26 },new[] { 3f,-26,24,-23 },new[] { 32f,-20,45,-13 } })
        {
            float dx=line[2]-line[0],dz=line[3]-line[1],length=Length(dx,dz),yaw=(float)Math.Atan2(dx,dz)*180/(float)Math.PI;
            int courses=(int)Math.Ceiling(length/1.9f);
            for (int i=0;i<courses;i++)
            {
                if (i%7==4) continue;
                float t=(i+.5f)/courses,x=line[0]+dx*t,z=line[1]+dz*t;
                float height=.9f+.35f*(float)Math.Sin(i*1.7f+line[0]);
                walls.Block(x,HamletLayout.Height(x,z)+height/2,z,.8f,height,length/courses-.08f,yaw);
            }
        }
        paving=paths.Finish(); masonry=walls.Finish();
    }
    private static float Length(float x,float z)=>(float)Math.Sqrt(x*x+z*z);

    private sealed class Builder
    {
        private readonly List<float> _positions=new(),_uv=new();
        private readonly List<int> _indices=new();
        public int Count=>_positions.Count/3;
        public void Vertex(float x,float y,float z,float u,float v)
        { _positions.Add(x); _positions.Add(y); _positions.Add(z); _uv.Add(u); _uv.Add(v); }
        public void Triangle(int a,int b,int c) { _indices.Add(a); _indices.Add(b); _indices.Add(c); }
        public void Block(float x,float y,float z,float width,float height,float depth,float yaw)
        {
            float a=yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            var corners=new[] { (-1,-1,-1),(-1,1,-1),(1,1,-1),(1,-1,-1),
                (1,-1,1),(1,1,1),(-1,1,1),(-1,-1,1) };
            var faces=new[] { 0,1,2,3, 4,5,6,7, 7,6,1,0, 3,2,5,4, 1,6,5,2, 7,0,3,4 };
            for (int face=0;face<6;face++)
            {
                int start=Count;
                for (int i=0;i<4;i++)
                {
                    var p=corners[faces[face*4+i]]; float px=p.Item1*width/2,pz=p.Item3*depth/2;
                    Vertex(x+px*c+pz*s,y+p.Item2*height/2,z-px*s+pz*c,(i>1?width:0)/3,(i==1||i==2?height:0)/3);
                }
                Triangle(start,start+1,start+2); Triangle(start,start+2,start+3);
            }
        }
        public HamletTerrain.Surface Finish()=>new() { Positions=_positions.ToArray(),Uv=_uv.ToArray(),Indices=_indices.ToArray() };
    }
}
