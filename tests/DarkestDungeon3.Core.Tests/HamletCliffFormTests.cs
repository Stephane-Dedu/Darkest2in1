using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletCliffFormTests
{
    [Fact]
    public void EveryBuildingAndRoadRetainsItsGroundedSurface()
    {
        foreach (var site in HamletLayout.Foundations)
        {
            double angle=site.Yaw*Math.PI/180;
            foreach (float x in new[] { -.5f,0,.5f }) foreach (float z in new[] { -.5f,0,.5f })
            {
                float dx=x*site.FootprintWidth,dz=z*site.FootprintDepth;
                Unchanged(site.X+(float)(dx*Math.Cos(angle)+dz*Math.Sin(angle)),
                    site.Z+(float)(-dx*Math.Sin(angle)+dz*Math.Cos(angle)));
            }
        }
        foreach (var route in HamletLayout.Routes) for (int i=1;i<route.Points.Length;i++)
            for (int s=0;s<=12;s++)
            {
                var a=route.Points[i-1];var b=route.Points[i];float t=s/12f;
                Unchanged(a.X+(b.X-a.X)*t,a.Z+(b.Z-a.Z)*t);
            }
    }

    [Fact]
    public void RockReliefIsBoundedAndSharedVerticesStayClosed()
    {
        var smooth=HamletTerrain.Build(cliffRelief:false);
        var shaped=HamletTerrain.Build();
        Assert.Equal(smooth.Positions.Length,shaped.Positions.Length);
        var shared=new Dictionary<(float,float,float),(float,float,float)>();int changed=0;
        for (int i=0;i<smooth.Positions.Length;i+=3)
        {
            var a=(smooth.Positions[i],smooth.Positions[i+1],smooth.Positions[i+2]);
            var b=(shaped.Positions[i],shaped.Positions[i+1],shaped.Positions[i+2]);
            if (shared.TryGetValue(a,out var previous)) Assert.Equal(previous,b); else shared.Add(a,b);
            Assert.InRange(MathF.Sqrt((a.Item1-b.Item1)*(a.Item1-b.Item1)+(a.Item3-b.Item3)*(a.Item3-b.Item3)),0,.701f);
            Assert.InRange(MathF.Abs(a.Item2-b.Item2),0,.181f);
            if (a.Item1==HamletLayout.MinX || a.Item1==HamletLayout.MaxX || a.Item3==HamletLayout.MinZ || a.Item3==HamletLayout.MaxZ)
                Assert.Equal(a,b);
            if (a!=b) changed++;
        }
        Assert.True(changed>200,"The study must add visible relief outside the protected routes/pads.");
        Assert.All(shaped.Positions.Concat(shaped.Uv),p=>Assert.True(float.IsFinite(p)));
    }

    private static void Unchanged(float x,float z)
    {
        var p=HamletCliffForm.At(x,z);
        Assert.Equal(x,p.X);Assert.Equal(z,p.Z);Assert.Equal(HamletLayout.Height(x,z)-.12f,p.Y);
    }
}
