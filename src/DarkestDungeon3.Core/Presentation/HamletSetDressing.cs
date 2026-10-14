using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Authored rock seams, service yards and restrained entrance light, shared with the private preview.</summary>
public static class HamletSetDressing
{
    public sealed class Prop
    {
        public readonly string Id, Path, Service;
        public readonly float X, Y, Z, Width, Yaw, HeightScale = 1, DepthScale = 1;
        public Prop(string id, string service, float x, float z, float width, float yaw = 0)
        {
            Id=id; Service=service; X=x; Z=z; Width=width; Yaw=yaw;
            Path=id=="common_prop_lamppost" ? "Assets/Art/Models/Environments/Common/Models/Props/"+id+".fbx"
                : "Assets/Art/Models/Environments/Camp/"+id+".fbx";
            Y=HamletLayout.Height(x,z)-.05f;
        }
    }
    public sealed class Rock
    {
        public readonly float X,Y,Z,Width,Height,Depth,Yaw;
        public Rock(float x,float z,float width,float height,float depth,float yaw=0)
        {
            X=x; Z=z; Width=width; Height=height; Depth=depth; Yaw=yaw;
            float ground=HamletLayout.Height(x,z);
            foreach (float dx in new[] { -.4f,0,.4f }) foreach (float dz in new[] { -.4f,0,.4f })
                ground=Math.Min(ground,HamletLayout.Height(x+width*dx,z+depth*dz));
            Y=ground-height*.25f;
        }
    }
    public readonly struct Light
    {
        public readonly float X,Y,Z,Radius,Strength;
        public Light(float x,float y,float z,float radius,float strength)
        { X=x; Y=y; Z=z; Radius=radius; Strength=strength; }
    }
    public readonly struct Mist
    {
        public readonly float X,Y,Z,Width,Height,Opacity;
        public Mist(float x,float y,float z,float width,float height,float opacity)
        { X=x; Y=y; Z=z; Width=width; Height=height; Opacity=opacity; }
    }
    public static readonly Prop[] Props =
    {
        new("camp_barrel","tavern",-39.5f,3,1.5f,18),
        new("camp_barrel","tavern",-41.3f,3.7f,1.25f,-12),
        new("camp_crate","tavern",-38.6f,5.4f,1.8f,7),
        new("camp_building","stage_coach",-42,-9,5.2f,12),
        new("camp_chest","stage_coach",-40,-12,1.5f,-8),
        new("common_prop_lamppost","stage_coach",-39,-14,1.15f,15),
        new("camp_barrel","sanitarium",-24.5f,25,1.4f,-12),
        new("camp_crate","sanitarium",-24,27,1.8f,8),
        new("common_prop_lamppost","sanitarium",-23.8f,22.5f,1.1f),
        new("camp_axe","guild",36,24,1.5f,-20),
        new("camp_chest","guild",36.3f,22,1.9f,90),
        new("camp_crate","blacksmith",43.8f,-2,1.8f,17),
        new("camp_barrel","blacksmith",43.3f,0,1.4f,-15),
        new("camp_axe","blacksmith",43,2,1.6f,35),
        new("camp_chest","nomad_wagon",19.4f,-10,1.65f,85),
        new("camp_crate","nomad_wagon",19.2f,-8.1f,1.25f,-7),
        new("common_prop_lamppost","abbey",-3.5f,37,1.15f),
    };
    public static readonly Rock[] Rocks = new Rock[]
    {
        new(-57,12,7,4,5,18),new(-55,17,8,5,5,-12),new(-53,30,7,4.2f,5,26),
        new(-35,20,6,3.6f,4,-18),new(-30,18,7,3,4,17),
        new(-9,32,5,3.4f,4,22),new(0,34,6,3.8f,4,-15),new(22,37,6,3.7f,4,14),
        new(37,29,6,3.6f,4,-10),new(47,15,5,4,5,25),new(49,4,6,4.5f,5,-15),
        new(49,-12,5,3.8f,5,10),new(47,-22,6,3.6f,4,-25),
        new(-42,-31,7,3.6f,5,15),new(-15,-32,8,3.2f,5,-15),new(25,-29,7,3,5,12),
    }.Where(r=>HamletVegetation.Clearance(r.X,r.Z)>Math.Max(r.Width,r.Depth)*.5f+.2f).ToArray();
    public static readonly Light[] Lights = MakeLights();
    public static readonly Mist[] Mists = { new(-8,25,78,260,28,.14f), new(-12,37,103,300,38,.13f) };

    private static Light[] MakeLights()
    {
        var lights=new List<Light>();
        foreach (var site in HamletLayout.Sites)
        {
            if (site.Id=="graveyard" || site.Id=="statue") continue;
            var p=site.Entrance;
            lights.Add(new(p.X,site.Y+.2f,p.Z,site.Id=="tavern"?5:3.6f,site.Id=="blacksmith"?.19f:.13f));
        }
        return lights.ToArray();
    }
    public static float EntranceLight(float x,float z)
    {
        float light=0;
        foreach (var lamp in Lights)
        {
            float dx=(x-lamp.X)/lamp.Radius,dz=(z-lamp.Z)/lamp.Radius;
            float weight=Math.Max(0,1-dx*dx-dz*dz);
            light=Math.Max(light,lamp.Strength*weight*weight);
        }
        return light;
    }
    public static HamletLayout.Point FacetTint(float nx,float ny,float nz)
    {
        // Albedo grading, not emission. Preserve ink shadows and painted warm windows.
        float key=nx*-.35f+ny*.75f+nz*-.55f;
        return key>.35f ? new(.98f,1.04f,1.12f) : key<-.15f ? new(.76f,.79f,.84f) : new(.93f,.94f,.96f);
    }
    public static HamletTerrain.Surface BuildRocks()
    {
        var vertices=new List<float>(); var uv=new List<float>(); var indices=new List<int>(); var shade=new List<float>();
        int seed=0;
        foreach (var rock in Rocks)
        {
            float yaw=rock.Yaw*(float)Math.PI/180,c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw);
            var rings=new HamletLayout.Point[3,7];
            for (int ring=0;ring<3;ring++) for (int i=0;i<7;i++)
            {
                float angle=i*(float)Math.PI*2/7;
                float radius=(ring==0?1: ring==1?.94f:.57f)*(1+.12f*(float)Math.Sin(i*2.7+seed));
                float x=(float)Math.Cos(angle)*rock.Width*.5f*radius,z=(float)Math.Sin(angle)*rock.Depth*.5f*radius;
                float y=rock.Y+rock.Height*(ring==0?0:ring==1?.60f:1)+rock.Height*.06f*(float)Math.Sin(i*2.1+seed);
                rings[ring,i]=new(rock.X+x*c+z*s,y,rock.Z-x*s+z*c);
            }
            void Triangle(HamletLayout.Point a,HamletLayout.Point b,HamletLayout.Point d)
            {
                int start=vertices.Count/3;
                float ux=b.X-a.X,uy=b.Y-a.Y,uz=b.Z-a.Z,vx=d.X-a.X,vy=d.Y-a.Y,vz=d.Z-a.Z;
                float nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                float length=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);
                float light=Math.Max(.65f,Math.Min(1,.84f+.14f*(nx*-.35f+ny*.8f+nz*-.48f)/length));
                foreach (var p in new[] { a,b,d })
                { vertices.Add(p.X); vertices.Add(p.Y); vertices.Add(p.Z); uv.Add((p.X+p.Z*.5f)/HamletPalette.RockPeriod); uv.Add(p.Y/(HamletPalette.RockPeriod*HamletPalette.RockTexelAspect)); shade.Add(light); }
                indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            }
            for (int ring=0;ring<2;ring++) for (int i=0;i<7;i++)
            {
                int next=(i+1)%7;
                Triangle(rings[ring,i],rings[ring+1,i],rings[ring,next]);
                Triangle(rings[ring,next],rings[ring+1,i],rings[ring+1,next]);
            }
            var centre=new HamletLayout.Point(rock.X,rock.Y+rock.Height,rock.Z);
            for (int i=0;i<7;i++) Triangle(centre,rings[2,(i+1)%7],rings[2,i]);
            seed++;
        }
        return new() { Positions=vertices.ToArray(),Uv=uv.ToArray(),Indices=indices.ToArray(),Shade=shade.ToArray() };
    }
}
