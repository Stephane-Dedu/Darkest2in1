using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletLayoutTests
{
    [Fact]
    public void TenServicesHaveDistinctSitesOnThreeRaisedTerraces()
    {
        Assert.Equal(10, HamletLayout.Sites.Count);
        Assert.Equal(10, HamletLayout.Sites.Select(s => s.Id).Distinct().Count());
        var coach = HamletLayout.Sites.Single(s => s.Id == "stage_coach");
        var forge = HamletLayout.Sites.Single(s => s.Id == "blacksmith");
        var abbey = HamletLayout.Sites.Single(s => s.Id == "abbey");
        Assert.True(forge.Y-coach.Y >= 4);
        Assert.True(abbey.Y-forge.Y >= 10);
    }

    [Fact]
    public void BuildingFootprintsHaveLevelGroundWithoutFloatingRearCorners()
    {
        foreach (var site in HamletLayout.Sites)
            foreach (float x in new[] { -.5f, 0, .5f })
                foreach (float z in new[] { -.5f, 0, .5f })
                    Assert.InRange(Math.Abs(HamletLayout.Height(site.X+x*site.Width, site.Z+z*site.Width)-site.Y), 0, .001f);
    }

    [Fact]
    public void RisingRoadAndTerrainRemainFiniteAcrossTheWholeScene()
    {
        for (float z = -80; z <= 110; z += 1)
            for (float x = -110; x <= 110; x += 2)
                Assert.True(float.IsFinite(HamletLayout.Height(x, z)));
        Assert.True(HamletLayout.Height(HamletLayout.RoadX(70), 70)-HamletLayout.Height(HamletLayout.RoadX(-15), -15) > 14);
    }
}
