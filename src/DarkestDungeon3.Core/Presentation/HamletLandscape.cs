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
        { Id=id; Path=path; X=x; Z=z; Y=Height(x,z)-.45f; Width=width; Yaw=yaw; HeightScale=heightScale; }
    }
    private const string Valley="Assets/Data/Biome/ValleyCommon/Map/DecorTiles/";
    private const string Camp="Assets/Art/Models/Environments/Camp/";
    public static readonly Prop[] Props =
    {
        new("camp_treeline",Camp+"camp_treeline.fbx",-79,132,115,8,.45f),
        new("camp_treeline",Camp+"camp_treeline.fbx",35,146,125,-5,.42f),
        new("camp_treeline",Camp+"camp_treeline.fbx",118,127,96,-12,.40f),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",-75,49,26,18),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",-49,75,24,-20),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",-16,94,28,35),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",38,88,27,12),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",70,59,25,-25),
        new("valley_tile_02",Valley+"valley_tile_02.prefab",87,28,24,20),
        new("valley_tile_01",Valley+"valley_tile_01.prefab",-88,4,24,35),
    };

    private static float Smooth(float x) { x=Math.Max(0,Math.Min(1,x)); return x*x*(3-2*x); }
    public static float Height(float x,float z)
    {
        float bx=Math.Max(HamletLayout.MinX,Math.Min(HamletLayout.MaxX,x));
        float bz=Math.Max(HamletLayout.MinZ,Math.Min(HamletLayout.MaxZ,z));
        float dx=x-bx,dz=z-bz,distance=(float)Math.Sqrt(dx*dx+dz*dz);
        float edge=HamletLayout.Height(bx,bz)-.12f;
        float wave=7*(float)Math.Sin(x*.025f+z*.011f)+4*(float)Math.Sin(z*.047f-x*.016f);
        float ridge=13*(float)Math.Exp(-Math.Pow((z-125)/38,2));
        float valley=-13+wave+ridge;
        float weight=Smooth(distance/48);
        return edge*(1-weight)+valley*weight;
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
        float[] rings={0,3,8,16,28,44,64,88,118};
        foreach (float spread in rings) foreach (var p in border)
        {
            float x=cx+(p.X-cx)*(1+spread/halfX),z=cz+(p.Z-cz)*(1+spread/halfZ);
            positions.Add(x); positions.Add(Height(x,z)); positions.Add(z);
            uv.Add(x/16); uv.Add(z/16);
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
