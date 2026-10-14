using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>The playable hillside continues into an unoccupied valley, rather than ending at a display plinth.</summary>
public static class HamletLandscape
{
    public sealed class Prop
    {
        public readonly string Id, Path;
        public readonly float X, Y, Z, Width, Yaw, HeightScale;
        public Prop(string id, string path, float x, float z, float width, float yaw = 0, float heightScale = 1)
        {
            Id=id; Path=path; X=x; Z=z; Width=width; Yaw=yaw; HeightScale=heightScale;
            // Leaf/root banks belong under the hillside, rather than forming flat floating islands.
            float ground=Height(x,z);
            foreach (float dx in new[] { -.35f,0,.35f }) foreach (float dz in new[] { -.35f,0,.35f })
                ground=Math.Min(ground,Height(x+width*dx,z+width*dz));
            Y=ground-.65f;
        }
    }
    private const string Valley="Assets/Data/Biome/ValleyCommon/Map/DecorTiles/";
    private const string Forest="Assets/Data/Biome/Forest/Map/DecorTiles/";
    public const float MinX=HamletLayout.MinX-118,MaxX=HamletLayout.MaxX+118;
    public const float MinZ=HamletLayout.MinZ-118,MaxZ=HamletLayout.MaxZ+118;
    public static readonly HamletLayout.Point[] Approach =
    {
        new(-51,-4,-36),new(-57,-6,-48),new(-72,-9,-63),new(-92,-12,-70),new(-105,-15,-94)
    };
    public static readonly Prop[] Props =
    {
        // Gnarled Tangle trees, roots and broken palisades enclose the inhabited clearing.
        new("forest_tile_12",Forest+"forest_tile_12.prefab",-75,49,30,18,.86f),
        new("forest_tile_01",Forest+"forest_tile_01.prefab",-49,75,28,-20,.85f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",-16,94,32,35,.80f),
        new("forest_tile_01",Forest+"forest_tile_01.prefab",38,88,30,12,.85f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",70,59,30,-25,.85f),
        new("forest_tile_01",Forest+"forest_tile_01.prefab",78,28,28,20,.80f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",-80,4,28,35,.82f),
        new("forest_tile_01",Forest+"forest_tile_01.prefab",-70,26,26,-38,.85f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",67,6,25,-15,.70f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",61,-19,22,25,.60f),
        new("forest_tile_01",Forest+"forest_tile_01.prefab",-77,-22,23,-15,.70f),
        new("forest_tile_04",Forest+"forest_tile_04.prefab",-29,-49,18,15,.90f),
        new("forest_tile_04",Forest+"forest_tile_04.prefab",31,-43,19,-25,.90f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",-15,-45,16,45,.40f),
        new("forest_tile_12",Forest+"forest_tile_12.prefab",50,-42,17,-30,.45f),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",-106,98,23,-8,.72f),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",-76,119,24,27,.68f),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",-43,131,22,-11,.65f),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",-6,139,24,15,.64f),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",61,126,23,31,.67f),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",97,107,24,-17,.70f),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",125,82,23,16,.75f),
    };

    private static float Smooth(float x) { x=Math.Max(0,Math.Min(1,x)); return x*x*(3-2*x); }
    public static float Height(float x,float z)
    {
        float bx=Math.Max(HamletLayout.MinX,Math.Min(HamletLayout.MaxX,x));
        float bz=Math.Max(HamletLayout.MinZ,Math.Min(HamletLayout.MaxZ,z));
        float dx=x-bx,dz=z-bz,distance=(float)Math.Sqrt(dx*dx+dz*dz);
        float edge=HamletLayout.Height(bx,bz)-.12f;
        float wave=7*(float)Math.Sin(x*.025f+z*.011f)+4*(float)Math.Sin(z*.047f-x*.016f);
        // Broad, uneven wooded shoulders. The lower centre keeps the Abbey and mountain apart.
        float shoulder=9+5*(float)Math.Sin(x*.023f+.7f)+4*(float)Math.Sin(x*.051f);
        float ridge=(10+shoulder)*(float)Math.Exp(-Math.Pow((z-127-9*Math.Sin(x*.018))/37,2));
        float foothill=8*(float)Math.Exp(-Math.Pow((z-90-5*Math.Sin(x*.032))/17,2));
        float valley=-13+wave+ridge+foothill;
        float weight=Smooth(distance/48);
        float height=edge*(1-weight)+valley*weight;
        ApproachAt(x,z,out float roadHeight,out float road);
        return height*(1-road)+roadHeight*road;
    }

    // Same road ribbon grades the outside terrain and paints its worn surface; no floating road plate.
    private static void ApproachAt(float x,float z,out float height,out float weight)
    {
        height=0; weight=0;
        for (int i=1;i<Approach.Length;i++)
        {
            var a=Approach[i-1]; var b=Approach[i];
            float dx=b.X-a.X,dz=b.Z-a.Z;
            float t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(z-a.Z)*dz)/(dx*dx+dz*dz)));
            float px=x-a.X-dx*t,pz=z-a.Z-dz*t;
            float w=1-Smooth(((float)Math.Sqrt(px*px+pz*pz)-1.5f)/2.4f);
            // Blend to the existing town at its boundary, including road margins.
            w*=Smooth((HamletLayout.MinZ-z)/3);
            if (w>weight) { weight=w; height=a.Y+(b.Y-a.Y)*t-.12f; }
        }
    }

    public static float RoadCover(float x,float z)
    { ApproachAt(x,z,out _,out float weight); return weight; }

    public static float Haze(float z) => .68f*Smooth((z-52)/80);

    // Darker undisturbed earth surrounds the cleared town, with the approach road left readable.
    public static float Wilderness(float x,float z)
    {
        float dx=Math.Max(0,Math.Max(HamletLayout.MinX-x,x-HamletLayout.MaxX));
        float dz=Math.Max(0,Math.Max(HamletLayout.MinZ-z,z-HamletLayout.MaxZ));
        return Smooth((float)Math.Sqrt(dx*dx+dz*dz)/16)*(1-RoadCover(x,z));
    }

    public static float GrassCover(float x,float z)
    {
        float dx=Math.Max(0,Math.Max(HamletLayout.MinX-x,x-HamletLayout.MaxX));
        float dz=Math.Max(0,Math.Max(HamletLayout.MinZ-z,z-HamletLayout.MaxZ));
        float far=Smooth((float)Math.Sqrt(dx*dx+dz*dz)/35);
        return HamletVegetation.Cover(x,z)*(1-far)+(.64f+HamletGroundPaint.At(x,z).Gravel*.4f)*far;
    }

    public static HamletTerrain.Surface Build()
    {
        var positions=new List<float>(); var uv=new List<float>(); var indices=new List<int>();
        var border=new List<HamletLayout.Point>();
        for (float x=HamletLayout.MinX;x<HamletLayout.MaxX;x++) border.Add(new(x,0,HamletLayout.MinZ));
        for (float z=HamletLayout.MinZ;z<HamletLayout.MaxZ;z++) border.Add(new(HamletLayout.MaxX,0,z));
        for (float x=HamletLayout.MaxX;x>HamletLayout.MinX;x--) border.Add(new(x,0,HamletLayout.MaxZ));
        for (float z=HamletLayout.MaxZ;z>HamletLayout.MinZ;z--) border.Add(new(HamletLayout.MinX,0,z));
        float cx=(HamletLayout.MinX+HamletLayout.MaxX)/2,cz=(HamletLayout.MinZ+HamletLayout.MaxZ)/2;
        float halfX=(HamletLayout.MaxX-HamletLayout.MinX)/2,halfZ=(HamletLayout.MaxZ-HamletLayout.MinZ)/2;
        float[] rings={0,3,8,16,24,32,40,48,56,64,72,80,88,98,108,118};
        foreach (float spread in rings) foreach (var p in border)
        {
            float x=cx+(p.X-cx)*(1+spread/halfX),z=cz+(p.Z-cz)*(1+spread/halfZ);
            positions.Add(x); positions.Add(Height(x,z)); positions.Add(z);
            uv.Add((x-MinX)/(MaxX-MinX)); uv.Add((z-MinZ)/(MaxZ-MinZ));
        }
        int count=border.Count;
        for (int ring=0;ring<rings.Length-1;ring++) for (int i=0;i<count;i++)
        {
            int a=ring*count+i,b=ring*count+(i+1)%count,c=a+count,d=b+count;
            indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(b); indices.Add(d); indices.Add(c);
        }
        return new() { Positions=positions.ToArray(),Uv=uv.ToArray(),Indices=indices.ToArray() };
    }
}
