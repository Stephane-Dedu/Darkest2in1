using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Continuous lanes, stone stair treads and retaining courses, baked into three owned surfaces.</summary>
internal static class HamletGroundwork
{
    internal static void Build(out HamletTerrain.Surface paving,out HamletTerrain.Surface masonry,out HamletTerrain.Surface stairs)
    {
        var paths=new Builder(); var walls=new Builder(); var stepsMesh=new Builder();
        var landings=new HashSet<HamletLayout.Point>();
        foreach (var route in HamletLayout.Routes)
        {
            for (int i=1;i<route.Points.Length;i++)
            {
                var a=route.Points[i-1]; var b=route.Points[i];
                float dx=b.X-a.X,dz=b.Z-a.Z,distance=Length(dx,dz);
                float rise=Math.Abs(b.Y-a.Y);
                bool stair=rise>=.45f && rise/distance>.18f;
                float yaw=(float)Math.Atan2(dx,dz)*180/(float)Math.PI;
                if (stair)
                {
                    int steps=(int)Math.Ceiling(rise/.32f);
                    for (int step=0;step<steps;step++)
                    {
                        float t=(step+.5f)/steps,x=a.X+dx*t,z=a.Z+dz*t;
                        stepsMesh.Block(x,a.Y+(b.Y-a.Y)*t-.08f,z,route.Width*.94f,.36f,distance/steps+.08f,yaw);
                    }
                    foreach (var point in new[] { a,b }) if (landings.Add(point))
                        stepsMesh.Block(point.X,point.Y-.10f,point.Z,route.Width,.36f,route.Width,yaw);
                    continue;
                }
                int samples=Math.Max(1,(int)Math.Ceiling(distance/.65f)),start=paths.Count;
                for (int sample=0;sample<=samples;sample++)
                {
                    float t=sample/(float)samples;
                    for (int edge=0;edge<2;edge++)
                    {
                        float offset=route.Width*(edge==0?-.5f:.5f);
                        float x=a.X+dx*t+dz/distance*offset,z=a.Z+dz*t-dx/distance*offset;
                        // One world projection across branches and the square; joins cannot reset or shear the paint.
                        paths.Vertex(x,HamletLayout.Height(x,z)+.055f,z,x/5,z/5);
                    }
                    if (sample==samples) continue;
                    int v=start+sample*2; paths.Triangle(v,v+2,v+1); paths.Triangle(v+1,v+2,v+3);
                }
            }
        }
        const int count=32; int square=paths.Count;
        paths.Vertex(0,HamletLayout.Height(0,-3)+.055f,-3,0,-3/5f);
        for (int i=0;i<count;i++)
        {
            float angle=i*(float)Math.PI*2/count,r=1+.06f*(float)Math.Sin(i*2.1f),x=(float)Math.Cos(angle)*13*r,z=-3+(float)Math.Sin(angle)*7*r;
            paths.Vertex(x,HamletLayout.Height(x,z)+.055f,z,x/5,z/5);
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
        paving=paths.Finish(); masonry=walls.Finish(); stairs=stepsMesh.Finish();
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
                    float u=face<2 || face>=4?width:depth,v=face<4?height:depth;
                    Vertex(x+px*c+pz*s,y+p.Item2*height/2,z-px*s+pz*c,(i>1?u:0)/3,(i==1||i==2?v:0)/3);
                }
                Triangle(start,start+1,start+2); Triangle(start,start+2,start+3);
            }
        }
        public HamletTerrain.Surface Finish()=>new() { Positions=_positions.ToArray(),Uv=_uv.ToArray(),Indices=_indices.ToArray() };
    }
}
