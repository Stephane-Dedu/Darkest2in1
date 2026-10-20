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
        Assert.All(HamletLandscape.Props,p=>Assert.True(p.X<HamletLayout.MinX || p.X>HamletLayout.MaxX || p.Z<HamletLayout.MinZ || p.Z>HamletLayout.MaxZ));
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
            Assert.InRange(MathF.Abs(paving.Uv[i*2]-(paving.Positions[i*3]-HamletLayout.MinX)/(HamletLayout.MaxX-HamletLayout.MinX)),0,.00001f);
            Assert.InRange(MathF.Abs(paving.Uv[i*2+1]-(paving.Positions[i*3+2]-HamletLayout.MinZ)/(HamletLayout.MaxZ-HamletLayout.MinZ)),0,.00001f);
        }
        // The gently sloping entrance is a road, not four three-metre-deep stair blocks.
        Assert.True(stairs.Positions.Where((_,i)=>i%3==2).Min()>-3);
        for (int face=0;face<stairs.Positions.Length/3;face+=4)
            foreach (var edge in new[] { (0,1),(1,2) })
            {
                int a=face+edge.Item1,b=face+edge.Item2;
                float length=MathF.Sqrt(Enumerable.Range(0,3).Sum(i=>MathF.Pow(stairs.Positions[a*3+i]-stairs.Positions[b*3+i],2)));
                float texel=MathF.Sqrt(Enumerable.Range(0,2).Sum(i=>MathF.Pow(stairs.Uv[a*2+i]-stairs.Uv[b*2+i],2)));
                Assert.InRange(MathF.Abs(length/texel-4.8f),0,.001f);
            }
        // Successive treads sample one floor plane, rather than restarting the same
        // dark strip of a wall atlas on every step.
        for (int block=0;block<stairs.Positions.Length/3;block+=24)
            for (int i=block+16;i<block+20;i++)
            {
                Assert.InRange(MathF.Abs(stairs.Uv[i*2]*4.8f-stairs.Positions[i*3]),0,.0001f);
                Assert.InRange(MathF.Abs(stairs.Uv[i*2+1]*4.8f-stairs.Positions[i*3+2]),0,.0001f);
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
    public void WornPavingKeepsTheLaneCentreAndReturnsToEarthAtTheMargins()
    {
        // Middle of the entrance lane, outside the forecourt and other branches.
        const float x=-45.5f,z=-33;
        Assert.Equal(1,HamletGroundPaint.LaneCover(x,z));
        Assert.Equal(1,HamletGroundPaint.LaneCover(0,-3));
        Assert.Equal(0,HamletGroundPaint.LaneCover(-60,50));
        float nx=-6/MathF.Sqrt(157),nz=11/MathF.Sqrt(157);
        float centre=HamletGroundPaint.LaneCover(x+nx*.6f,z+nz*.6f);
        float edge=HamletGroundPaint.LaneCover(x+nx*1.8f,z+nz*1.8f);
        float outside=HamletGroundPaint.LaneCover(x+nx*2.1f,z+nz*2.1f);
        Assert.True(centre>.95f && edge<.35f && outside==0);
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
    public void PaintCopiesPreserveAtlasResolutionAndCropProportionsWithoutUpscaling()
    {
        Assert.Equal((2048,1024),HamletPalette.PaintExtent(4096,2048,2048,HamletPalette.Building,false));
        Assert.Equal((125,270),HamletPalette.PaintExtent(125,270,2048,HamletPalette.Building,false));
        Assert.Equal((960,760),HamletPalette.PaintExtent(1024,1024,1024,HamletPalette.Rock,true));
        Assert.Equal((512,512),HamletPalette.PaintExtent(512,512,512,HamletPalette.Grass,false));
    }

    [Fact]
    public void BuildingAndBankContactHasBoundedShadowAndWearFadesAwayFromRoutes()
    {
        foreach (var site in HamletLayout.Sites)
        {
            Assert.InRange(HamletGroundPaint.ContactShade(site.X,site.Z),.7599f,.761f);
            Assert.Equal(.18f,HamletGroundPaint.EdgeWear(site.X,site.Z));
        }
        var samples=Enumerable.Range(0,500).Select(i=>(X:i%25*4-50f,Z:i/25*4-30f)).ToArray();
        Assert.All(samples,p=>Assert.InRange(HamletGroundPaint.ContactShade(p.X,p.Z),.7599f,1));
        Assert.All(samples,p=>Assert.InRange(HamletGroundPaint.EdgeWear(p.X,p.Z),0,.18f));
        Assert.Contains(samples,p=>HamletVegetation.Clearance(p.X,p.Z)>3 && HamletGroundPaint.EdgeWear(p.X,p.Z)==0);
        Assert.Contains(samples,p=>HamletGroundPaint.ContactShade(p.X,p.Z)>.98f);
    }

    [Fact]
    public void GroundPaintVariesAcrossTheSquareWithoutOverwritingNativeTexture()
    {
        var samples=Enumerable.Range(0,120).Select(i=>HamletGroundPaint.At(i%12*10-60,i/12*10-30)).ToArray();
        Assert.All(samples,s=>
        {
            Assert.InRange(s.Gravel,0,.16f);
            Assert.InRange(s.Tint.X,.6f,1); Assert.InRange(s.Tint.Y,.6f,1); Assert.InRange(s.Tint.Z,.6f,1);
        });
        Assert.True(samples.Max(s=>s.Gravel)-samples.Min(s=>s.Gravel)>.10f);
        Assert.True(samples.Max(s=>s.Tint.X)-samples.Min(s=>s.Tint.X)>.15f);
    }

    [Fact]
    public void ApproachContinuesTheMainRoadWithGroundedWalkableGrades()
    {
        var first=HamletLayout.Routes[0].Points[0]; var start=HamletLandscape.Approach[0];
        Assert.Equal((first.X,first.Y,first.Z),(start.X,start.Y,start.Z));
        for (int i=1;i<HamletLandscape.Approach.Length;i++)
        {
            var a=HamletLandscape.Approach[i-1]; var b=HamletLandscape.Approach[i];
            float run=MathF.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Z-a.Z)*(b.Z-a.Z));
            Assert.InRange(MathF.Abs(b.Y-a.Y)/run,0,.35f);
            for (float t=.25f;t<=1;t+=.25f)
            {
                float x=a.X+(b.X-a.X)*t,z=a.Z+(b.Z-a.Z)*t;
                Assert.InRange(MathF.Abs(HamletLandscape.Height(x,z)-(a.Y+(b.Y-a.Y)*t-.12f)),0,.001f);
                Assert.InRange(HamletLandscape.RoadCover(x,z),.99f,1);
            }
        }
    }

    [Fact]
    public void DistancePaintIsContinuousAndBackdropsAreGroundedNativeGeometry()
    {
        float previous=0;
        for (float z=-150;z<190;z+=.5f)
        {
            float haze=HamletLandscape.Haze(z);
            Assert.InRange(haze,previous,.68f); Assert.InRange(haze-previous,0,.01f); previous=haze;
        }
        Assert.Equal(0,HamletLandscape.Haze(52));
        Assert.DoesNotContain(HamletLandscape.Props,p=>p.Id=="camp_treeline");
        foreach (var p in HamletLandscape.Props)
            foreach (float dx in new[] { -.35f,0,.35f }) foreach (float dz in new[] { -.35f,0,.35f })
                Assert.True(p.Y<HamletLandscape.Height(p.X+p.Width*dx,p.Z+p.Width*dz),"Native root bank above its ground footprint.");
        foreach (float x in new[] { HamletLayout.MinX,HamletLayout.MaxX })
            Assert.InRange(MathF.Abs(HamletLandscape.GrassCover(x,0)-HamletVegetation.Cover(x,0)),0,.00001f);
        var surface=HamletLandscape.Build();
        Assert.All(surface.Uv,u=>Assert.InRange(u,0,1));
    }

    [Fact]
    public void WildwoodEnclosesTheClearingWithoutOccupyingServicesOrTheApproach()
    {
        var wildwood=HamletLandscape.Props.Where(p=>p.Id.StartsWith("forest_tile_")).ToArray();
        Assert.NotEmpty(wildwood);
        Assert.Contains(wildwood,p=>p.X<HamletLayout.MinX);
        Assert.Contains(wildwood,p=>p.X>HamletLayout.MaxX);
        Assert.Contains(wildwood,p=>p.Z>HamletLayout.MaxZ);
        Assert.All(wildwood,p=>
        {
            Assert.True(p.X<HamletLayout.MinX || p.X>HamletLayout.MaxX || p.Z<HamletLayout.MinZ || p.Z>HamletLayout.MaxZ);
            Assert.True(HamletVegetation.Clearance(p.X,p.Z)>p.Width/2+3,"Wildwood intrudes into a service or town route.");
            for (int i=1;i<HamletLandscape.Approach.Length;i++)
            {
                var a=HamletLandscape.Approach[i-1]; var b=HamletLandscape.Approach[i];
                float dx=b.X-a.X,dz=b.Z-a.Z;
                float t=Math.Clamp(((p.X-a.X)*dx+(p.Z-a.Z)*dz)/(dx*dx+dz*dz),0,1);
                float x=p.X-a.X-dx*t,z=p.Z-a.Z-dz*t;
                Assert.True(MathF.Sqrt(x*x+z*z)>p.Width/2+4,"Wildwood blocks the outward approach.");
            }
        });
    }

    [Fact]
    public void WildernessDarkensOuterBanksAndKeepsTheClearingAndApproachOpen()
    {
        foreach (var site in HamletLayout.Sites) Assert.Equal(0,HamletLandscape.Wilderness(site.X,site.Z));
        for (float z=HamletLayout.MinZ;z<=HamletLayout.MaxZ;z++)
        {
            Assert.Equal(0,HamletLandscape.Wilderness(HamletLayout.MinX,z));
            Assert.Equal(0,HamletLandscape.Wilderness(HamletLayout.MaxX,z));
        }
        for (int i=1;i<HamletLandscape.Approach.Length;i++)
            for (int j=0;j<=20;j++)
            {
                var a=HamletLandscape.Approach[i-1];var b=HamletLandscape.Approach[i];
                float t=j/20f;
                // The existing road mask fades over its first3m; the new ground fade must keep that join negligible.
                Assert.InRange(HamletLandscape.Wilderness(a.X+(b.X-a.X)*t,a.Z+(b.Z-a.Z)*t),0,.015f);
            }
        Assert.Equal(1,HamletLandscape.Wilderness(HamletLayout.MaxX+20,0));
    }
}
