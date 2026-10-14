using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletVegetationTests
{
    [Fact]
    public void GrassLeavesWalkingRoutesAndServiceApronsClear()
    {
        foreach (var route in HamletLayout.Routes) for (int i=1;i<route.Points.Length;i++)
            for (int j=0;j<=20;j++)
            {
                var a=route.Points[i-1]; var b=route.Points[i]; float t=j/20f;
                float x=a.X+(b.X-a.X)*t,z=a.Z+(b.Z-a.Z)*t;
                Assert.Equal(0,HamletVegetation.Cover(x,z));
            }
        foreach (var site in HamletLayout.Sites)
        {
            Assert.Equal(0,HamletVegetation.Cover(site.X,site.Z));
            Assert.Equal(0,HamletVegetation.Cover(site.Entrance.X,site.Entrance.Z));
        }
        var field=Enumerable.Range(0,1200).Select(i=>HamletVegetation.Cover(i%40*3-60,i/40*3-30)).ToArray();
        Assert.All(field,g=>Assert.InRange(g,0,.82f));
        Assert.Contains(field,g=>g>.7f);
        Assert.Contains(field,g=>g==0);
    }
    [Fact]
    public void TuftsSitOnGentleGroundAndKeepAVisibleGapFromBuildingsAndStairs()
    {
        Assert.InRange(HamletVegetation.Clumps.Count,35,120);
        Assert.Equal(3,HamletVegetation.Clumps.Select(c=>c.Variant).Distinct().Count());
        foreach (var clump in HamletVegetation.Clumps)
        {
            Assert.InRange(clump.X,HamletLayout.MinX+1,HamletLayout.MaxX-1);
            Assert.InRange(clump.Z,HamletLayout.MinZ+1,HamletLayout.MaxZ-1);
            Assert.True(HamletVegetation.Clearance(clump.X,clump.Z)>=1.5f);
            Assert.InRange(clump.Height,.62f,1.02f);
            Assert.InRange(HamletVegetation.Clearance(clump.X,clump.Z),1.5f,7);
            Assert.InRange(MathF.Abs(clump.Y+.15f-HamletLayout.Height(clump.X,clump.Z)),0,.0001f);
            foreach (var d in new[] { (1f,0f),(-1f,0f),(0f,1f),(0f,-1f) })
                Assert.InRange(MathF.Abs(HamletLayout.Height(clump.X+d.Item1,clump.Z+d.Item2)-(clump.Y+.15f)),0,.6501f);
        }
    }
    [Fact]
    public void BatchedGrassHasFiniteGeometryAndSamplesOnlyTheReviewedAtlasRegions()
    {
        var mesh=HamletVegetation.Build();
        Assert.Equal(HamletVegetation.Clumps.Count*8,mesh.Positions.Length/3);
        Assert.Equal(mesh.Positions.Length/3,mesh.Uv.Length/2);
        Assert.All(mesh.Positions.Concat(mesh.Uv),v=>Assert.True(float.IsFinite(v)));
        Assert.All(mesh.Indices,i=>Assert.InRange(i,0,mesh.Positions.Length/3-1));
        for (int i=0;i<mesh.Uv.Length;i+=2)
        {
            float u=mesh.Uv[i],v=mesh.Uv[i+1];
            const float tolerance=.00001f;
            Assert.True(u>=.008f-tolerance && u<=.124f+tolerance && v>=.645f-tolerance && v<=.875f+tolerance
                || u>=.24f-tolerance && u<=.34f+tolerance && v>=.632f-tolerance && v<=.795f+tolerance
                || u>=.735f-tolerance && u<=.885f+tolerance && v>=.677f-tolerance && v<=.93f+tolerance);
        }
    }
}
