using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class CorridorSceneMotionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void ShortHallsDoNotFastForwardTheWholePanoramaThroughOneSquare(int count)
    {
        const float dt = .001f;
        float start = CorridorSceneMotion.Camera(4096,768,0,count,false,0);
        float after = CorridorSceneMotion.Camera(4096,768,0,count,false,-720*dt/1.2f);
        float speed = (after-start)/dt;
        Assert.True(speed <= 260,$"A {count}-square hall fast-forwards the walking ground at {speed:0} px/s.");
    }

    [Fact]
    public void AButtonStepHasWalkingCadenceInsteadOfAQuarterSecondSprint()
    {
        const float dt = .001f;
        float maximum = 0;
        for (int i = 0; i < 1200; i++)
        {
            float before = CorridorSceneMotion.Camera(4096,768,2,6,false,CorridorSceneMotion.Slide(i*dt,1,0));
            float after = CorridorSceneMotion.Camera(4096,768,2,6,false,CorridorSceneMotion.Slide((i+1)*dt,1,0));
            maximum = Math.Max(maximum,(after-before)/dt);
        }
        Assert.True(maximum <= 330,$"A button step drives the ground at {maximum:0} px/s.");
    }

    [Theory]
    [InlineData(false,0)]
    [InlineData(true,5)]
    public void ForwardWalkingMovesTheGroundLeftFromEitherEntrance(bool reverse,int tile)
    {
        float a = CorridorSceneMotion.Camera(4096,768,tile,6,reverse,0);
        float b = CorridorSceneMotion.Camera(4096,768,tile,6,reverse,-72);
        Assert.True(b > a);
        float backward = CorridorSceneMotion.Camera(4096,768,tile,6,reverse,72);
        Assert.True(backward <= a);
        Assert.Equal(CorridorPanoramaLayout.ViewProgress(0,6,false,-72),
            CorridorPanoramaLayout.ViewProgress(5,6,true,-72),5);
    }

    [Theory]
    [InlineData(false,1,2)]
    [InlineData(true,4,3)]
    public void ContinuousTileCarryNeverInventsAnotherFootstep(bool reverse,int before,int after)
    {
        float a = CorridorSceneMotion.Camera(4096,768,before,6,reverse,-.5f*720);
        float b = CorridorSceneMotion.Camera(4096,768,after,6,reverse,.5f*720);
        Assert.Equal(a,b,3);
    }

    [Fact]
    public void ButtonStepUsesTheSameEasedSceneDistanceAsTheFeetAndStopsExactly()
    {
        float start = CorridorSceneMotion.Slide(0,1,0);
        float middle = CorridorSceneMotion.Slide(.6f,1,0);
        float end = CorridorSceneMotion.Slide(1.2f,1,0);
        Assert.Equal(720,start); Assert.Equal(360,middle); Assert.Equal(0,end);
        Assert.True(CorridorSceneMotion.Camera(4096,768,2,6,false,start)
            < CorridorSceneMotion.Camera(4096,768,2,6,false,middle));
        Assert.Equal(end,CorridorSceneMotion.Slide(2,1,0));
        Assert.Equal(-180,CorridorSceneMotion.Slide(1,0,.25f));
    }

    [Fact]
    public void HallEntryArtAdoptionResizeAndReturnCannotAdvanceTheGaitByATeleport()
    {
        var motion = new CorridorSceneMotion();
        float Sample(int seed,int hall,int heading,string art,int width,int height,float x) =>
            motion.Sample(seed,hall,heading,art,width,height,x);
        Assert.Equal(0,Sample(7,1,2,"wall",4096,768,700));
        Assert.Equal(12,Sample(7,1,2,"wall",4096,768,712));
        Assert.Equal(0,Sample(7,1,2,"other",4096,768,200));
        Assert.Equal(0,Sample(7,1,2,"other",2880,1080,50));
        Assert.Equal(0,Sample(7,2,3,"other",2880,1080,700));
        Assert.Equal(0,Sample(8,2,3,"other",2880,1080,100));
        Assert.Equal(0,Sample(8,2,4,"other",2880,1080,50));
        Assert.Equal(-5,Sample(8,2,4,"other",2880,1080,45));
        motion.Reset();
        Assert.Equal(0,Sample(8,2,4,"other",2880,1080,100));
        Assert.Equal(0,Sample(8,2,4,"other",2880,1080,float.NaN));
        Assert.Equal(0,Sample(8,2,4,"other",2880,1080,300));
    }
}
