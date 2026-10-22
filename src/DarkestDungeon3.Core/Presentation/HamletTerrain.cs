using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>A bounded, faceted hillside shared by the native renderer and the offline composition preview.</summary>
public sealed class HamletTerrain
{
    public sealed class Surface
    {
        public float[] Positions, Uv, Shade;
        public int[] Indices;
    }
    public float[] Positions, Uv, Shade;
    public int[] Ground, Rock;
    public Surface Paving, Masonry, Stairs, Surroundings;
    public static HamletTerrain Build(float spacing = 1, bool fullCliffTile = false)
    {
        if (spacing < .5f || spacing > 4 || float.IsNaN(spacing)) throw new ArgumentOutOfRangeException(nameof(spacing));
        var positions = new List<float>(); var uv = new List<float>(); var shade = new List<float>();
        var ground = new List<int>(); var rock = new List<int>();
        int columns = (int)Math.Ceiling((HamletLayout.MaxX-HamletLayout.MinX)/spacing);
        int rows = (int)Math.Ceiling((HamletLayout.MaxZ-HamletLayout.MinZ)/spacing);
        HamletLayout.Point At(int x, int z)
        {
            float px = HamletLayout.MinX+x*(HamletLayout.MaxX-HamletLayout.MinX)/columns;
            float pz = HamletLayout.MinZ+z*(HamletLayout.MaxZ-HamletLayout.MinZ)/rows;
            return new(px, HamletLayout.Height(px,pz)-.12f, pz);
        }
        void Triangle(HamletLayout.Point a, HamletLayout.Point b, HamletLayout.Point c, bool skirt = false)
        {
            float ux=b.X-a.X, uy=b.Y-a.Y, uz=b.Z-a.Z, vx=c.X-a.X, vy=c.Y-a.Y, vz=c.Z-a.Z;
            float nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
            float length=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz); nx/=length; ny/=length; nz/=length;
            bool cliff = skirt || ny < .50f;
            float light = Math.Max(.65f, Math.Min(1, .84f + .14f*(nx*-.35f+ny*.8f+nz*-.48f)));
            var indices = cliff ? rock : ground;
            foreach (var p in new[] { a,b,c })
            {
                indices.Add(positions.Count/3);
                positions.Add(p.X); positions.Add(p.Y); positions.Add(p.Z);
                float period = cliff ? HamletPalette.RockPeriod : 10;
                // A continuous diagonal projection avoids flipping the wall atlas at each changing facet normal.
                uv.Add(cliff ? (p.X+p.Z*.5f)/period : (p.X-HamletLayout.MinX)/(HamletLayout.MaxX-HamletLayout.MinX));
                // A cropped atlas is rectangular paint. Preserve its texel aspect on square world coordinates.
                float cropHeight = cliff ? (fullCliffTile ? 1 : HamletPalette.RockTexelAspect) : HamletPalette.Soil.UvScale.Y;
                uv.Add(cliff ? p.Y/(period*cropHeight) : (p.Z-HamletLayout.MinZ)/(HamletLayout.MaxZ-HamletLayout.MinZ)); shade.Add(light);
            }
        }
        void Skirt(HamletLayout.Point a, HamletLayout.Point b)
        {
            var lowA = new HamletLayout.Point(a.X,-32,a.Z); var lowB = new HamletLayout.Point(b.X,-32,b.Z);
            Triangle(a,lowA,b,true); Triangle(b,lowA,lowB,true);
        }
        for (int z=0; z<rows; z++) for (int x=0; x<columns; x++)
        { var a=At(x,z); var b=At(x,z+1); var c=At(x+1,z); var d=At(x+1,z+1); Triangle(a,b,c); Triangle(c,b,d); }
        for (int x=0; x<columns; x++) { Skirt(At(x+1,0),At(x,0)); Skirt(At(x,rows),At(x+1,rows)); }
        for (int z=0; z<rows; z++) { Skirt(At(0,z),At(0,z+1)); Skirt(At(columns,z+1),At(columns,z)); }
        HamletGroundwork.Build(out var paving,out var masonry,out var stairs);
        return new() { Positions=positions.ToArray(), Uv=uv.ToArray(), Shade=shade.ToArray(), Ground=ground.ToArray(), Rock=rock.ToArray(), Paving=paving, Masonry=masonry, Stairs=stairs, Surroundings=HamletLandscape.Build() };
    }
}
