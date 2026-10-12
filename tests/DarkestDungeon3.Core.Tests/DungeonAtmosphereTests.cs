using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public sealed class DungeonAtmosphereTests
{
    [Theory]
    [InlineData("dd2_city", 24, "smoke")]
    [InlineData("dd2_farm", 9, "gas")]
    [InlineData("dd2_forest", 9, "forest")]
    [InlineData("dd2_coast", 9, "mist")]
    [InlineData("crypts", 0, null)]
    public void RegionalMotionIsBoundedRepeatableAndIndependentOfGameplayRandom(string region, int count, string effect)
    {
        var first = new List<DungeonAtmosphere.Particle>();
        var second = new List<DungeonAtmosphere.Particle>();
        DungeonAtmosphere.Fill(first, region, int.MinValue, 12.5f, 100);
        DungeonAtmosphere.Fill(second, region, int.MinValue, 12.5f, 100);
        Assert.Equal(count, first.Count);
        Assert.Equal(first, second);
        if (count == 0) return;
        Assert.Equal(effect, first[0].Effect);
        Assert.All(first, p => { Assert.InRange(p.Alpha,0,1); Assert.True(p.Width > 0 && p.Height > 0); });
        DungeonAtmosphere.Fill(second, region, int.MinValue, 13, 100);
        Assert.NotEqual(first, second);
        DungeonAtmosphere.Fill(second, region, int.MinValue, 12.5f, 0);
        for (int i = 0; i < first.Count; i++)
        { Assert.Equal(first[i].X, second[i].X); Assert.Equal(first[i].Y, second[i].Y); Assert.Equal(first[i].Alpha*.35f,second[i].Alpha,5); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.5f)]
    [InlineData(1)]
    public void EffectsFollowTheSamePanWithoutMirroringOrEnteringTheHud(float progress)
    {
        var particles = new List<DungeonAtmosphere.Particle>();
        DungeonAtmosphere.Fill(particles,"dd2_city",7,10,100);
        var crop = CorridorPanoramaLayout.Crop(2880,1080,progress);
        foreach (var p in particles)
            if (DungeonAtmosphere.Project(p,crop,new ExpeditionMap3D.MapRect(.25f,.375f,.125f,.125f),out var r,out var uv))
            {
                Assert.InRange(r.X,0,1920); Assert.InRange(r.Y,0,720);
                Assert.InRange(r.X+r.Width,0,1920.001f); Assert.InRange(r.Y+r.Height,0,720.001f);
                Assert.InRange(uv.X,.25f,.375f); Assert.InRange(uv.Y,.375f,.5f);
                Assert.True(uv.Width > 0 && uv.Height > 0);
                Assert.True(uv.X+uv.Width <= .37501f && uv.Y+uv.Height <= .50001f);
            }
    }

    [Fact]
    public void ClippingTheTopAndBottomSelectsTheCorrectPartOfTheAtlas()
    {
        var crop = new PanoramaCrop(0,0,1,1);
        var atlas = new ExpeditionMap3D.MapRect(.25f,.5f,.25f,.25f);
        var p = new DungeonAtmosphere.Particle("mist",960,0,400,100,0,1,1,1,1);
        Assert.True(DungeonAtmosphere.Project(p,crop,atlas,out var rect,out var uv));
        Assert.Equal(0,rect.Y,3); Assert.Equal(50,rect.Height,3);
        Assert.Equal(.5f,uv.Y,6); Assert.Equal(.125f,uv.Height,6);
        p = new DungeonAtmosphere.Particle("mist",960,720,400,100,0,1,1,1,1);
        Assert.True(DungeonAtmosphere.Project(p,crop,atlas,out rect,out uv));
        Assert.Equal(670,rect.Y,3); Assert.Equal(50,rect.Height,3);
        Assert.Equal(.625f,uv.Y,6); Assert.Equal(.125f,uv.Height,6);
    }
}
