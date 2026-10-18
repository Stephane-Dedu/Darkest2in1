using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletSetDressingTests
{
    [Fact]
    public void ServiceYardsAndOutcropsLeaveTheWalkingNetworkClear()
    {
        var objects=HamletSetDressing.Props.Select(p=>(p.X,p.Z,Radius:p.Width*.5f))
            .Concat(HamletSetDressing.Rocks.Select(r=>(r.X,r.Z,Radius:Math.Max(r.Width,r.Depth)*.5f)));
        foreach (var obj in objects)
            foreach (var route in HamletLayout.Routes) for (int i=1;i<route.Points.Length;i++)
            {
                var a=route.Points[i-1]; var b=route.Points[i];
                float dx=b.X-a.X,dz=b.Z-a.Z;
                float t=Math.Clamp(((obj.X-a.X)*dx+(obj.Z-a.Z)*dz)/(dx*dx+dz*dz),0,1);
                float distance=MathF.Sqrt(MathF.Pow(obj.X-a.X-dx*t,2)+MathF.Pow(obj.Z-a.Z-dz*t,2));
                Assert.True(distance>route.Width*.5f+obj.Radius+.15f,$"Dressing at {obj.X},{obj.Z} covers {route.Id}");
            }
    }

    [Fact]
    public void OutcropsKeepBuildingFootprintsClearAndHaveBoundedFiniteGeometry()
    {
        foreach (var rock in HamletSetDressing.Rocks)
        {
            Assert.True(HamletVegetation.Clearance(rock.X,rock.Z)>Math.Max(rock.Width,rock.Depth)*.5f,
                $"Rock at {rock.X},{rock.Z} intersects a foundation or walking route");
            foreach (float dx in new[] { -.4f,0,.4f }) foreach (float dz in new[] { -.4f,0,.4f })
                Assert.True(rock.Y<HamletLayout.Height(rock.X+rock.Width*dx,rock.Z+rock.Depth*dz),"Outcrop base floats above a cliff edge");
        }
        var mesh=HamletSetDressing.BuildRocks();
        Assert.InRange(mesh.Indices.Length/3,200,1500);
        Assert.All(mesh.Positions.Concat(mesh.Uv),v=>Assert.True(float.IsFinite(v)));
        Assert.All(mesh.Indices,i=>Assert.InRange(i,0,mesh.Positions.Length/3-1));
        Assert.Equal(mesh.Positions.Length/3,mesh.Uv.Length/2);
        Assert.All(mesh.Shade,v=>Assert.InRange(v,.65f,1));
    }

    [Fact]
    public void EntranceLightAndMistStayConfinedToTheIntendedDepths()
    {
        Assert.Equal(0,HamletSetDressing.EntranceLight(90,-80));
        foreach (var light in HamletSetDressing.Lights)
        {
            Assert.InRange(HamletSetDressing.EntranceLight(light.X,light.Z),.1f,.2f);
        }
        // Enlarged service footprints can put this lamp's old test point inside
        // a neighbouring pool. Check a point outside the entire lit settlement.
        Assert.Equal(0,HamletSetDressing.EntranceLight(
            HamletSetDressing.Lights.Max(l=>l.X+l.Radius)+1,
            HamletSetDressing.Lights.Max(l=>l.Z+l.Radius)+1));
        float back=HamletLayout.Sites.Max(s=>s.Z+s.FootprintDepth/2);
        Assert.All(HamletSetDressing.Mists,m=>Assert.True(m.Z>back+20 && m.Opacity<=.15f));
        var roof=HamletSetDressing.FacetTint(0,1,0);
        var wall=HamletSetDressing.FacetTint(1,0,0);
        Assert.True(roof.Z>roof.X && roof.Y>wall.Y);
        Assert.All(new[] { roof.X,roof.Y,roof.Z,wall.X,wall.Y,wall.Z },v=>Assert.InRange(v,.7f,1.15f));
    }
}
