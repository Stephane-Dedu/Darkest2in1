using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class HeroFormationTests
{
    [Fact]
    public void CampKeepsRanksOnTwoSidesOfTheFireWithDistinctPointerColumns()
    {
        for (int rank = 0; rank < 4; rank++)
        {
            float x = HeroFormation.X(rank, camping: true);
            Assert.Equal(rank < 2, HeroFormation.FacesLeft(rank, camping: true));
            Assert.True(System.Math.Abs(x - HeroFormation.FireX) >= 290);
            for (int other = rank + 1; other < 4; other++)
                Assert.True(System.Math.Abs(x - HeroFormation.X(other, true)) >= 160);
        }
    }

    [Fact]
    public void LeavingCampRestoresFrontToBackCorridorOrderAndFacing()
    {
        for (int rank = 0; rank < 4; rank++)
        {
            Assert.False(HeroFormation.FacesLeft(rank, camping: false));
            if (rank > 0) Assert.True(HeroFormation.X(rank - 1, false) > HeroFormation.X(rank, false));
        }
        Assert.Equal(788, HeroFormation.X(0, false));
        Assert.Equal(284, HeroFormation.X(3, false));
    }
}
