using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletLandscapeTests
{
    [Fact]
    public void ValleyMeetsThePlayableTerrainWithoutAHeightStep()
    {
        for (float x=HamletLayout.MinX;x<=HamletLayout.MaxX;x++)
            foreach (float z in new[] { HamletLayout.MinZ,HamletLayout.MaxZ })
                Assert.InRange(MathF.Abs(HamletLandscape.Height(x,z)-(HamletLayout.Height(x,z)-.12f)),0,.0001f);
        for (float z=HamletLayout.MinZ;z<=HamletLayout.MaxZ;z++)
            foreach (float x in new[] { HamletLayout.MinX,HamletLayout.MaxX })
                Assert.InRange(MathF.Abs(HamletLandscape.Height(x,z)-(HamletLayout.Height(x,z)-.12f)),0,.0001f);
    }

    [Fact]
    public void SurroundingsHaveUpwardFiniteFacesAndLeaveTheTownInteriorEmpty()
    {
        var mesh=HamletLandscape.Build();
        Assert.All(mesh.Positions.Concat(mesh.Uv),v=>Assert.True(float.IsFinite(v)));
        Assert.Equal(mesh.Positions.Length/3,mesh.Uv.Length/2);
        Assert.All(mesh.Indices,i=>Assert.InRange(i,0,mesh.Positions.Length/3-1));
        for (int i=0;i<mesh.Indices.Length;i+=3)
        {
            int a=mesh.Indices[i]*3,b=mesh.Indices[i+1]*3,c=mesh.Indices[i+2]*3;
            float normalY=(mesh.Positions[b+2]-mesh.Positions[a+2])*(mesh.Positions[c]-mesh.Positions[a])
                -(mesh.Positions[b]-mesh.Positions[a])*(mesh.Positions[c+2]-mesh.Positions[a+2]);
            Assert.True(normalY>0);
            float x=(mesh.Positions[a]+mesh.Positions[b]+mesh.Positions[c])/3;
            float z=(mesh.Positions[a+2]+mesh.Positions[b+2]+mesh.Positions[c+2])/3;
            Assert.True(x<=HamletLayout.MinX || x>=HamletLayout.MaxX || z<=HamletLayout.MinZ || z>=HamletLayout.MaxZ);
        }
        Assert.True(mesh.Positions.Where((_,i)=>i%3==0).Max()>170);
        Assert.True(mesh.Positions.Where((_,i)=>i%3==2).Max()>180);
        Assert.All(HamletLandscape.Props,p=>Assert.True(p.X<HamletLayout.MinX || p.X>HamletLayout.MaxX || p.Z>HamletLayout.MaxZ));
    }

    [Fact]
    public void ArchitecturalPaintCannotBecomeHdrButLanternsKeepTheirWarmLight()
    {
        var roof=HamletPalette.BuildingTint(new(0,0,0),new(1.5f,1.5f,1.5f),true);
        Assert.InRange(roof.X,.7f,1); Assert.Equal(roof.X,roof.Y); Assert.Equal(roof.Y,roof.Z);
        var masonry=HamletPalette.BuildingTint(new(.8f,.84f,.89f),new(.44f,.462f,.4895f),false);
        Assert.True(masonry.X<.8f && masonry.Z<.89f);
        var lantern=HamletPalette.BuildingTint(new(.65f,.32f,.08f),new(1.625f,.8f,.2f),false);
        Assert.True(lantern.X>1 && lantern.X>lantern.Y*1.8f && lantern.Y>lantern.Z*3);
    }

    [Fact]
    public void PathsShareOneProjectionAndStairTopsKeepTheirTexelAspect()
    {
        var terrain=HamletTerrain.Build(); var paving=terrain.Paving; var stairs=terrain.Stairs;
        for (int i=0;i<paving.Positions.Length/3;i++)
        {
            Assert.InRange(MathF.Abs(paving.Uv[i*2]-paving.Positions[i*3]/5),0,.00001f);
            Assert.InRange(MathF.Abs(paving.Uv[i*2+1]-paving.Positions[i*3+2]/5),0,.00001f);
        }
        // The gently sloping entrance is a road, not four three-metre-deep stair blocks.
        Assert.True(stairs.Positions.Where((_,i)=>i%3==2).Min()>-3);
        for (int face=0;face<stairs.Positions.Length/3;face+=4)
            foreach (var edge in new[] { (0,1),(1,2) })
            {
                int a=face+edge.Item1,b=face+edge.Item2;
                float length=MathF.Sqrt(Enumerable.Range(0,3).Sum(i=>MathF.Pow(stairs.Positions[a*3+i]-stairs.Positions[b*3+i],2)));
                float texel=MathF.Sqrt(Enumerable.Range(0,2).Sum(i=>MathF.Pow(stairs.Uv[a*2+i]-stairs.Uv[b*2+i],2)));
                Assert.InRange(MathF.Abs(length/texel-3),0,.001f);
            }
        var uvByPoint=new Dictionary<(float,float,float),(float,float)>();
        foreach (int i in terrain.Rock)
        {
            var p=(terrain.Positions[i*3],terrain.Positions[i*3+1],terrain.Positions[i*3+2]);
            var uv=(terrain.Uv[i*2],terrain.Uv[i*2+1]);
            if (uvByPoint.TryGetValue(p,out var previous)) Assert.Equal(previous,uv);
            else uvByPoint.Add(p,uv);
        }
    }

    [Fact]
    public void StonePaintRetainsDarkJointsAndReadableHighlights()
    {
        foreach (var paint in new[] { HamletPalette.Paving,HamletPalette.Rock,HamletPalette.Masonry })
        {
            int joint=(HamletPalette.GradeRgb(12,12,12,paint)>>16)&255;
            int stone=(HamletPalette.GradeRgb(110,110,110,paint)>>16)&255;
            Assert.InRange(joint,0,35);
            Assert.True(stone-joint>=80);
        }
    }

    [Fact]
    public void GroundPaintVariesAcrossTheSquareWithoutOverwritingNativeTexture()
    {
        var samples=Enumerable.Range(0,120).Select(i=>HamletGroundPaint.At(i%12*10-60,i/12*10-30)).ToArray();
        Assert.All(samples,s=>
        {
            Assert.InRange(s.Gravel,0,.6f);
            Assert.InRange(s.Tint.X,.6f,1); Assert.InRange(s.Tint.Y,.6f,1); Assert.InRange(s.Tint.Z,.6f,1);
        });
        Assert.True(samples.Max(s=>s.Gravel)-samples.Min(s=>s.Gravel)>.15f);
        Assert.True(samples.Max(s=>s.Tint.X)-samples.Min(s=>s.Tint.X)>.15f);
    }
}
