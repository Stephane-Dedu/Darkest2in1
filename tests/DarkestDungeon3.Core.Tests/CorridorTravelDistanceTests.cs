using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class CorridorTravelDistanceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public void LongerTravelKeepsTheAuthoredFootlineAndBoundsWithoutRepeatingThePlate(int count)
    {
        foreach(float progress in new[] {0f,.5f,1f})
        {
            var crop=CorridorPanoramaLayout.Crop(4096,768,progress,count);
            Assert.InRange(crop.X,0,1-crop.Width+.0001f);
            Assert.InRange(crop.Y,0,1-crop.Height+.0001f);
            float foot=(1-(1-680f/720-crop.Y)/crop.Height)*720;
            Assert.Equal(680,foot,3);
            Assert.Equal(1920f/720,crop.Width*4096/(crop.Height*768),4);
        }
    }

    [Fact] public void TheVisibleDoorAndPropsStayOnTheFasterGroundInBothDirections()
    {
        foreach(bool reverse in new[] {false,true}) foreach(bool ahead in new[] {false,true})
        {
            float a=CorridorDoorLayout.X(4096,768,9,4,reverse,ahead,0);
            float b=CorridorDoorLayout.X(4096,768,9,4,reverse,ahead,-72);
            float camera=CorridorSceneMotion.Camera(4096,768,4,9,reverse,-72)-CorridorSceneMotion.Camera(4096,768,4,9,reverse,0);
            Assert.Equal(-camera,b-a,3);
        }
        Assert.Equal(0,CorridorDoorLayout.Alpha(0,.5f));Assert.Equal(0,CorridorDoorLayout.Alpha(.5f,1));
        Assert.Equal(1,CorridorDoorLayout.Alpha(.5f,.4f));
    }
    [Theory]
    [InlineData(4096,768,3)]
    [InlineData(4096,768,6)]
    [InlineData(4096,768,12)]
    [InlineData(5760,1080,9)]
    public void OneWalkingSquareMovesEnoughOfTheActualPainting(int width,int height,int count)
    {
        int tile=count/2;
        float before=CorridorSceneMotion.Camera(width,height,tile,count,false,0);
        float after=CorridorSceneMotion.Camera(width,height,tile,count,false,-720);
        float distance=after-before;
        Assert.InRange(distance,350,400);
    }
}
