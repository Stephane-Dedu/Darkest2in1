using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletLayoutTests
{
    [Fact]
    public void CompactTownKeepsDd1sLeftCliffAndUpperCivicBuildings()
    {
        Assert.Equal(10, HamletLayout.Sites.Select(s => s.Id).Distinct().Count());
        var coach = Site("stage_coach"); var camp = Site("camping_trainer");
        var abbey = Site("abbey"); var graves = Site("graveyard"); var forge = Site("blacksmith");
        Assert.True(camp.X < coach.X && camp.Y-coach.Y >= 24);
        Assert.True(abbey.Y-forge.Y >= 16 && graves.Y-forge.Y >= 12);
        Assert.True(forge.X > Site("guild").X && forge.Z < Site("guild").Z);
        Assert.True(Site("sanitarium").X < graves.X && graves.X < abbey.X);
        Assert.InRange(HamletLayout.Sites.Max(s => s.Z)-HamletLayout.Sites.Min(s => s.Z), 40, 55);
        Assert.True((HamletLayout.MaxX-HamletLayout.MinX)*(HamletLayout.MaxZ-HamletLayout.MinZ) < 15000);
    }

    [Fact]
    public void EveryRotatedBuildingFootprintHasLevelGround()
    {
        foreach (var site in HamletLayout.Sites)
        {
            double angle=site.Yaw*Math.PI/180;
            foreach (float x in new[] { -.5f, 0, .5f }) foreach (float z in new[] { -.5f, 0, .5f })
            {
                float dx=x*site.FootprintWidth, dz=z*site.FootprintDepth;
                float px=site.X+(float)(dx*Math.Cos(angle)+dz*Math.Sin(angle));
                float pz=site.Z+(float)(-dx*Math.Sin(angle)+dz*Math.Cos(angle));
                Assert.InRange(px,HamletLayout.MinX,HamletLayout.MaxX);
                Assert.InRange(pz,HamletLayout.MinZ,HamletLayout.MaxZ);
                Assert.True(Math.Abs(HamletLayout.Height(px,pz)-site.Y)<.001f, $"{site.Id} corner ({px}, {pz})");
            }
        }
    }

    [Fact]
    public void AllServiceDoorsHaveGroundedRoutesBackToTheTownEntrance()
    {
        var connected = new List<HamletLayout.Point>(HamletLayout.Routes[0].Points);
        var remaining = HamletLayout.Routes.Skip(1).ToList();
        while (remaining.Count>0)
        {
            var next=remaining.FirstOrDefault(r=>r.Points.Any(p=>connected.Any(q=>Distance(p,q)<.001f)));
            Assert.NotNull(next);
            connected.AddRange(next.Points); remaining.Remove(next);
        }
        foreach (var site in HamletLayout.Sites)
        {
            var door=site.Entrance;
            Assert.Contains(connected,p=>Distance(p,door)<.001f);
            Assert.True(Math.Abs(HamletLayout.Height(door.X,door.Z)-site.Y)<.15f,$"{site.Id} entrance ground");
        }
    }

    [Fact]
    public void TerrainHasClosedCliffsAndValidFacetedGeometry()
    {
        var mesh=HamletTerrain.Build();
        Assert.NotEmpty(mesh.Ground); Assert.NotEmpty(mesh.Rock);
        Assert.Equal(mesh.Positions.Length/3,mesh.Uv.Length/2);
        Assert.Equal(mesh.Positions.Length/3,mesh.Shade.Length);
        Assert.All(mesh.Positions.Concat(mesh.Uv).Concat(mesh.Shade),v=>Assert.True(float.IsFinite(v)));
        Assert.All(mesh.Ground.Concat(mesh.Rock),i=>Assert.InRange(i,0,mesh.Shade.Length-1));
        Assert.True(mesh.Positions.Where((_,i)=>i%3==1).Min()<=-20);
        Assert.True(mesh.Positions.Where((_,i)=>i%3==1).Max()>=18);
        foreach (var indices in new[] { mesh.Ground,mesh.Rock }) for (int i=0;i<indices.Length;i+=3)
        {
            var a=Point(indices[i]); var b=Point(indices[i+1]); var c=Point(indices[i+2]);
            float ux=b.X-a.X,uy=b.Y-a.Y,uz=b.Z-a.Z,vx=c.X-a.X,vy=c.Y-a.Y,vz=c.Z-a.Z;
            float nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
            Assert.True(nx*nx+ny*ny+nz*nz>.00001f);
        }
        HamletLayout.Point Point(int i)=>new(mesh.Positions[i*3],mesh.Positions[i*3+1],mesh.Positions[i*3+2]);
    }

    [Fact]
    public void StairsAndRetainingCoursesHaveFiniteNondegenerateGeometry()
    {
        var terrain=HamletTerrain.Build();
        foreach (var mesh in new[] { terrain.Paving,terrain.Masonry })
        {
            Assert.NotEmpty(mesh.Indices);
            Assert.Equal(mesh.Positions.Length/3,mesh.Uv.Length/2);
            Assert.All(mesh.Positions.Concat(mesh.Uv),v=>Assert.True(float.IsFinite(v)));
            Assert.All(mesh.Indices,i=>Assert.InRange(i,0,mesh.Positions.Length/3-1));
            for (int i=0;i<mesh.Indices.Length;i+=3)
            {
                int a=mesh.Indices[i]*3,b=mesh.Indices[i+1]*3,c=mesh.Indices[i+2]*3;
                float ux=mesh.Positions[b]-mesh.Positions[a],uy=mesh.Positions[b+1]-mesh.Positions[a+1],uz=mesh.Positions[b+2]-mesh.Positions[a+2];
                float vx=mesh.Positions[c]-mesh.Positions[a],vy=mesh.Positions[c+1]-mesh.Positions[a+1],vz=mesh.Positions[c+2]-mesh.Positions[a+2];
                float nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                Assert.True(nx*nx+ny*ny+nz*nz>.000001f);
            }
        }
    }

    private static HamletLayout.Site Site(string id)=>HamletLayout.Sites.Single(s=>s.Id==id);
    private static float Distance(HamletLayout.Point a,HamletLayout.Point b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)+Math.Abs(a.Z-b.Z);
}
