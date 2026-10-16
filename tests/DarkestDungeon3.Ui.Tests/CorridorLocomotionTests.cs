using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorLocomotionTests
{
    [Fact]
    public void StartingWithARearFootInSwingKeepsItsObservedHorizontalPosition()
    {
        var contact = new CorridorFootContact();
        var foot = CorridorWalkCycle.Sample(.67);
        Assert.False(foot.Planted);
        Assert.Equal(-.55f,contact.Forward(foot,-.55f,.002f),5);
    }

    [Fact]
    public void ReversingAcrossLandingReleasesTheOldSwingOffset()
    {
        var contact = new CorridorFootContact();
        var swing = CorridorWalkCycle.Sample(.64);
        contact.Forward(swing,-.1f,-.01f);
        var foot = CorridorWalkCycle.Sample(.61);
        Assert.True(foot.Planted);
        float distance = -.03f*CorridorWalkCycle.Stride/(float)CorridorWalkCycle.Stance;
        float after = contact.Forward(foot,0,distance);
        Assert.Equal(foot.Forward,after,5);
    }

    [Theory]
    [InlineData(1.161883f)]
    [InlineData(2.4f)]
    [InlineData(-1.161883f)]
    [InlineData(-2.4f)]
    public void ALongFrameThatSkipsLiftoffAndLandingStartsANewGroundContact(float distance)
    {
        var contact = new CorridorFootContact();
        double start = distance > 0 ? .55 : .2;
        contact.Forward(CorridorWalkCycle.Sample(start),-.3f,.002f*Math.Sign(distance));
        double next = start+distance*CorridorWalkCycle.Stance/CorridorWalkCycle.Stride;
        var foot = CorridorWalkCycle.Sample(next);
        Assert.True(foot.Planted);
        float target = contact.Forward(foot,0,distance);
        Assert.Equal(foot.Forward,target,4);
        Assert.InRange(target,-.46f,.46f);
    }

    [Theory]
    [InlineData(2,110f)]
    [InlineData(6,165f)]
    [InlineData(12,210f)]
    public void WalkingTakesReadableStepsRatherThanRapidShortShuffles(int squares, float legPixels)
    {
        // Replay one ordinary 1.2-second corridor square through the actual crop,
        // distance clock and sampler. Target a step covering .65..85 of the fitted
        // projected leg, rather than spending multiple cycles on a tiny foot arc.
        const int frames = 72;
        const float dt = 1.2f / frames;
        var cycle = new CorridorWalkCycle(.2);
        double cycles = 0;
        float previousGround = Camera(squares,0), distance = 0;
        for (int i = 1; i <= frames; i++)
        {
            float ground = Camera(squares,i/(float)frames);
            float delta = ground-previousGround;
            double phase = cycle.Phase;
            cycle.Advance(CorridorWalkCycle.Rate(delta,legPixels,dt),dt,true);
            cycles += CorridorWalkCycle.Wrap(cycle.Phase-phase);
            distance += delta;
            previousGround = ground;
        }
        double stepInLegLengths = distance / (2*cycles*legPixels);
        Assert.True(stepInLegLengths >= .65 && stepInLegLengths <= .85,
            $"A step covers only {stepInLegLengths:0.000} projected leg lengths, with {2*cycles/1.2:0.00} steps/s in this hall.");
    }

    [Fact]
    public void StartingCombatContactRemainsReachableWhileThePelvisBlendsUp()
    {
        // Native Plague Doctor first contact: requested 2.408, available 2.231.
        // Its retained combat stance must constrain the rising body, otherwise
        // the two-bone solver clamps the foot away from the moving ground.
        const float reach = 2.231f, horizontal = reach*.556f, requested = 2.408f;
        float vertical = MathF.Sqrt(requested*requested-horizontal*horizontal);
        float drop = CorridorWalkCycle.PelvisContactDrop(reach,horizontal*horizontal,vertical);
        float after = MathF.Sqrt(horizontal*horizontal+(vertical-drop)*(vertical-drop));
        Assert.True(drop > .2f);
        Assert.InRange(after,0,reach*.981f);
        Assert.Equal(0,CorridorWalkCycle.PelvisContactDrop(reach,.1f,reach*.8f));
    }

    // Exercise the actual panorama crop and planted-foot sampler in the same
    // virtual-pixel units. Native rig/IK acceptance still requires the game.
    [Theory]
    [InlineData(2,110f)]
    [InlineData(6,165f)]
    [InlineData(12,210f)]
    public void PlantedFootAndGroundCoverTheSamePixelsForEveryHallLengthAndRigScale(int squares, float legPixels)
    {
        const float dt = .001f;
        float before = Camera(squares,0), after = Camera(squares,dt/1.2f);
        float groundDistance = after-before;
        float cyclesPerSecond = CorridorWalkCycle.Rate(groundDistance,legPixels,dt);
        var cycle = new CorridorWalkCycle(.2);
        for (int i = 0; i < 2000; i++) cycle.Advance(cyclesPerSecond,dt,true);
        while (cycle.Phase < .08 || cycle.Phase > .50) cycle.Advance(cyclesPerSecond,dt,true);
        var footA = CorridorWalkCycle.Sample(cycle.Phase);
        cycle.Advance(cyclesPerSecond,dt,true);
        var footB = CorridorWalkCycle.Sample(cycle.Phase);
        Assert.True(footA.Planted && footB.Planted);
        float footDistance = (footA.Forward-footB.Forward)*legPixels;
        Assert.InRange(Math.Abs(footDistance-groundDistance),0,.001f);
    }

    [Fact]
    public void AStoppedBackgroundCannotKeepAdvancingTheFootCycle()
    {
        var cycle = new CorridorWalkCycle(.2);
        cycle.Advance(1,.016f,true);
        double phase = cycle.Phase;
        cycle.Advance(0,.016f,true);
        Assert.Equal(phase,cycle.Phase);
    }

    [Fact]
    public void ReversalChangesFootTravelOnTheSameFrameAsTheBackground()
    {
        var cycle = new CorridorWalkCycle(.3);
        for (int i = 0; i < 20; i++) cycle.Advance(1,.01f,true);
        double phase = cycle.Phase;
        cycle.Advance(-.5f,.01f,true);
        Assert.True(cycle.Phase < phase,"The floor reversed, but the planted foot kept moving forward.");
    }

    [Fact]
    public void PlantedContactKeepsItsPixelDistanceDuringBodyBlendingStopsAndImmediateReversal()
    {
        const float legPixels = 165;
        var contact = new CorridorFootContact();
        var foot = CorridorWalkCycle.Sample(.2);
        float observed = .3f;
        float first = contact.Forward(foot,observed,4/legPixels);
        Assert.Equal(4,(observed-first)*legPixels,3);
        // Animator's crouched pose may differ on the next frame; it cannot drag the contact.
        float second = contact.Forward(CorridorWalkCycle.Sample(.22),-.2f,3/legPixels);
        Assert.Equal(3,(first-second)*legPixels,3);
        float reverse = contact.Forward(CorridorWalkCycle.Sample(.21),-.3f,-2/legPixels);
        Assert.Equal(-2,(second-reverse)*legPixels,3);
        contact.Forward(foot,0,0);
        float restarted = contact.Forward(foot,.1f,1/legPixels);
        Assert.Equal(1,(.1f-restarted)*legPixels,3);
    }

    [Fact]
    public void SwingReleasesTheInitialContactOffsetContinuouslyBeforeTheNextTouchdown()
    {
        var contact = new CorridorFootContact();
        var planted = CorridorWalkCycle.Sample(CorridorWalkCycle.Stance-.000001);
        float old = contact.Forward(planted,.05f,.001f);
        var beginning = CorridorWalkCycle.Sample(CorridorWalkCycle.Stance+.000001);
        Assert.InRange(Math.Abs(old-contact.Forward(beginning,0,.001f)),0,.00001f);
        var end = CorridorWalkCycle.Sample(.999999);
        Assert.Equal(end.Forward,contact.Forward(end,0,.001f),5);
    }

    [Fact]
    public void ASlowFrameConsumesTheActualGroundDistanceInsteadOfClampingAnimationTime()
    {
        var cycle = new CorridorWalkCycle(.2);
        float rate = CorridorWalkCycle.Rate(40,165,.25f);
        cycle.Advance(rate,.25f,true);
        double expected = CorridorWalkCycle.Wrap(.2+40*CorridorWalkCycle.Stance/(CorridorWalkCycle.Stride*165));
        Assert.Equal(expected,cycle.Phase,6);
    }

    private static float Camera(int squares,float fraction)
    {
        return CorridorSceneMotion.Camera(4096,768,0,squares,false,-fraction*720);
    }
}
