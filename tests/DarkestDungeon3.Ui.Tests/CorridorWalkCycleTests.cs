using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorWalkCycleTests
{
    [Theory]
    [InlineData(.35f,-.55f,false)]
    [InlineData(-.55f,.35f,false)]
    [InlineData(-.10f,.05f,false)]
    [InlineData(.35f,-.55f,true)]
    [InlineData(-.55f,.35f,true)]
    [InlineData(-.10f,.05f,true)]
    public void RestartMatchesTheCurrentLeadFootAndReleasesTheRearFootBeforeOverextension(float left, float right, bool backwards)
    {
        double phase = CorridorWalkCycle.StartPhase(left,right,backwards);
        var cycle = new CorridorWalkCycle(phase);
        var contacts = new[] {new CorridorFootContact(),new CorridorFootContact()};
        float[] observed = {left,right};
        const float dt = 1f/600;
        int direction = backwards ? -1 : 1;
        float distance = direction*CorridorWalkCycle.Stride/(float)CorridorWalkCycle.Stance*dt;
        for (int i = 0; i < 600; i++)
        {
            cycle.Advance(direction,dt,true);
            for (int foot = 0; foot < 2; foot++)
            {
                var sample = CorridorWalkCycle.Sample(cycle.Phase+foot*.5);
                float target = contacts[foot].Forward(sample,observed[foot],distance);
                if (sample.Planted) Assert.InRange(target,-.66f,.66f);
            }
        }
    }

    [Fact]
    public void BodyRisesOverTheSupportingFootAndSettlesAtTheHandover()
    {
        const float leg = 0.76f, nativeHeight = 0.60f;
        double handover = (CorridorWalkCycle.Stance - 0.5) / 2;
        float atHandover = CorridorWalkCycle.PelvisLift(leg, nativeHeight, handover);
        float overLeft = CorridorWalkCycle.PelvisLift(leg, nativeHeight, handover + 0.25);
        float overRight = CorridorWalkCycle.PelvisLift(leg, nativeHeight, handover + 0.75);
        Assert.True(overLeft > atHandover);
        Assert.Equal(overLeft, overRight, 5);
        Assert.InRange(Math.Abs(CorridorWalkCycle.SupportShift(handover)), 0, 0.00001);
        Assert.True(CorridorWalkCycle.SupportShift(handover + 0.25) > CorridorWalkCycle.SupportShift(handover + 0.20));
        Assert.True(CorridorWalkCycle.SupportShift(handover + 0.25) > CorridorWalkCycle.SupportShift(handover + 0.30));
        Assert.Equal(-CorridorWalkCycle.SupportShift(handover + 0.25), CorridorWalkCycle.SupportShift(handover + 0.75), 5);
    }

    [Fact]
    public void WalkingRelaxesTheBattleCrouchWithoutLockingTheKnees()
    {
        // Leper's native stance: about 0.60 units of hip clearance on a 0.76-unit leg.
        // A planted leg should retain a modest bend throughout the weight shift.
        const float leg = 0.76f, nativeHeight = 0.60f;
        for (int i = 0; i < 100; i++)
        {
            double phase = i / 100.0;
            float height = nativeHeight + CorridorWalkCycle.PelvisLift(leg, nativeHeight, phase);
            var foot = CorridorWalkCycle.Sample(phase);
            if (!foot.Planted) continue;
            float horizontalSquared = foot.Forward*foot.Forward*leg*leg;
            // Use the same contact-constrained pelvis as the native two-leg solver.
            // Longer steps need the hips to settle before the leg extends fully.
            height -= CorridorWalkCycle.PelvisContactDrop(leg,horizontalSquared,height);
            double reach = Math.Sqrt(height * height + horizontalSquared) / leg;
            // Equal-length thigh/shin approximation: soft knees through the full stride.
            double bend = 2 * Math.Acos(Math.Min(1, reach)) * 180 / Math.PI;
            Assert.InRange(bend, 22, 55);
        }
    }

    [Fact]
    public void SharedPelvisSupportsBothFeetWithoutASquatAtEachHandover()
    {
        // The native solver lowers one common pelvis for both legs. Checking a
        // supporting leg alone missed the other leg's deep crouch at handover.
        const float leg = .76f, nativeHeight = .60f;
        float low = float.MaxValue, high = 0;
        for (int i = 0; i < 1000; i++)
        {
            double phase = i/1000.0;
            float height = nativeHeight+CorridorWalkCycle.PelvisLift(leg,nativeHeight,phase);
            var feet = new[] { CorridorWalkCycle.Sample(phase), CorridorWalkCycle.Sample(phase+.5) };
            float drop = feet.Max(foot => SupportDrop(leg,height,foot));
            height -= drop;
            low = Math.Min(low,height); high = Math.Max(high,height);
            foreach (var foot in feet.Where(foot => foot.Planted))
            {
                double reach = Math.Sqrt(height*height+foot.Forward*foot.Forward*leg*leg)/leg;
                double bend = 2*Math.Acos(Math.Min(1,reach))*180/Math.PI;
                Assert.True(bend <= 55,$"The other foot forces a {bend:0.0}-degree supporting knee bend.");
            }
        }
        Assert.True(high-low <= leg*.12f,$"Each stride lowers and raises the pelvis by {(high-low)/leg:0.000} leg lengths.");
    }

    [Fact]
    public void AnAirborneFootCannotPullTheBodyDownToReachItsSwingTarget()
    {
        Assert.True(CorridorWalkCycle.PelvisContactDrop(1,.7f*.7f,.9f,true) > .2f);
        Assert.Equal(0,CorridorWalkCycle.PelvisContactDrop(1,.7f*.7f,.9f,false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CorridorWalkCycle.Stance)]
    [InlineData(.5)]
    [InlineData(.5+CorridorWalkCycle.Stance)]
    public void WeightTransferDoesNotJumpTheHipsWhenFootSupportChanges(double boundary)
    {
        const float leg = .76f;
        float jump = Math.Abs(HipHeight(boundary-.0001)-HipHeight(boundary+.0001));
        Assert.True(jump <= leg*.001f,$"Changing foot support jumps the pelvis {jump/leg:0.0000} leg lengths.");
    }

    [Fact]
    public void MidSwingCannotPullDownThePelvisAndGroundContactsRetainFullSupport()
    {
        var planted = CorridorWalkCycle.Sample(.2);
        var airborne = CorridorWalkCycle.Sample((1+CorridorWalkCycle.Stance)/2);
        Assert.Equal(CorridorWalkCycle.PelvisContactDrop(1,.49f,.9f),
            CorridorWalkCycle.PelvisContactDrop(1,.49f,.9f,planted));
        Assert.Equal(0,CorridorWalkCycle.PelvisContactDrop(1,.49f,.9f,airborne));
        for (int i = 0; i < 1000; i++)
            Assert.InRange(CorridorWalkCycle.SupportWeight(CorridorWalkCycle.Sample(i/1000.0)),0,1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupportReleasesTheActualCombatFootAnchorInEitherDirection(bool backwards)
    {
        var contact = new CorridorSupportContact();
        float direction = backwards ? -1 : 1;
        float u = backwards ? .98f : .02f;
        var foot = CorridorWalkCycle.Sample(CorridorWalkCycle.Stance+(1-CorridorWalkCycle.Stance)*u);
        float observed = -.8f*direction;
        Assert.Equal(observed,contact.Forward(foot,observed,.002f*direction));
        // Its airborne arc can move on; the body must release the captured support
        // instead of jumping immediately to the narrower nominal walk stance.
        Assert.Equal(observed,contact.Forward(foot,observed-.1f*direction,.002f*direction));
        contact.Forward(foot,0,0);
        Assert.Equal(.2f,contact.Forward(foot,.2f,.002f*direction));
    }

    private static float HipHeight(double phase)
    {
        const float leg = .76f, nativeHeight = .60f;
        float height = nativeHeight+CorridorWalkCycle.PelvisLift(leg,nativeHeight,phase);
        var feet = new[] { CorridorWalkCycle.Sample(phase),CorridorWalkCycle.Sample(phase+.5) };
        return height-feet.Max(foot => SupportDrop(leg,height,foot));
    }

    private static float SupportDrop(float leg, float height, CorridorWalkCycle.Foot foot)
    {
        var anchor = CorridorWalkCycle.SupportPose(foot);
        return CorridorWalkCycle.PelvisContactDrop(leg,anchor.Forward*anchor.Forward*leg*leg,
            height-anchor.Lift*leg,foot);
    }

    [Fact]
    public void ContactFootStaysOnTheGroundAndTravelsAtConstantSpeed()
    {
        var a = CorridorWalkCycle.Sample(0.1);
        var b = CorridorWalkCycle.Sample(0.2);
        var c = CorridorWalkCycle.Sample(0.3);
        Assert.True(a.Planted && b.Planted && c.Planted);
        Assert.Equal(0, a.Lift);
        Assert.Equal(0, b.Lift);
        Assert.Equal(a.Forward - b.Forward, b.Forward - c.Forward, 5);
        Assert.True(a.Forward > c.Forward);
        Assert.InRange(CorridorWalkCycle.Sample((1+CorridorWalkCycle.Stance)/2).Lift, .11f, .13f);
    }

    [Fact]
    public void BothFeetNeverLeaveTheGroundTogether()
    {
        for (int i = 0; i < 1000; i++)
            Assert.True(CorridorWalkCycle.Sample(i / 1000.0).Planted
                     || CorridorWalkCycle.Sample(i / 1000.0 + 0.5).Planted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CorridorWalkCycle.Stance)]
    public void FootRotationDoesNotSnapToADifferentVelocityAtContactChanges(double boundary)
    {
        const double dt = .0001;
        float before = CorridorWalkCycle.Sample(boundary-dt).Pitch;
        float at = CorridorWalkCycle.Sample(boundary).Pitch;
        float after = CorridorWalkCycle.Sample(boundary+dt).Pitch;
        double incoming = (at-before)/dt, outgoing = (after-at)/dt;
        Assert.True(Math.Abs(incoming-outgoing) <= 1,
            $"Ankle rotation changes velocity abruptly by {Math.Abs(incoming-outgoing):0.0} degrees/cycle.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CorridorWalkCycle.Stance)]
    public void FootLiftDoesNotStartOrLandWithAnAccelerationSnap(double boundary)
    {
        const double dt = .0001;
        float before = CorridorWalkCycle.Sample(boundary-dt).Lift;
        float at = CorridorWalkCycle.Sample(boundary).Lift;
        float after = CorridorWalkCycle.Sample(boundary+dt).Lift;
        double acceleration = (before-2*at+after)/(dt*dt);
        Assert.True(Math.Abs(acceleration) < .1,
            $"Foot lift has a {acceleration:0.00} leg-length/cycle-squared acceleration jump at contact.");
    }

    [Fact]
    public void SwingRecoversTheToeAndBendsTheKneeBeforePassingTheSupportingLeg()
    {
        var recovery = CorridorWalkCycle.Sample(CorridorWalkCycle.Stance+(1-CorridorWalkCycle.Stance)*.4);
        Assert.True(recovery.Pitch >= 5,$"The swinging toe still points down at {recovery.Pitch:0.0} degrees.");
        Assert.InRange(recovery.Lift,.10f,.14f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CorridorWalkCycle.Stance)]
    public void SwingAndLoopJoinWithoutPopping(double boundary)
    {
        var a = CorridorWalkCycle.Sample(boundary - 0.00001);
        var b = CorridorWalkCycle.Sample(boundary + 0.00001);
        Assert.InRange(Math.Abs(a.Forward - b.Forward), 0, 0.0001);
        Assert.InRange(Math.Abs(a.Lift - b.Lift), 0, 0.0001);
        Assert.InRange(Math.Abs(a.Pitch - b.Pitch), 0, 0.01);
    }

    [Fact]
    public void BackingUpReversesTheGaitAtHalfTheCadence()
    {
        var forward = new CorridorWalkCycle(0.5);
        var backward = new CorridorWalkCycle(0.5);
        for (int i = 0; i < 10; i++)
        { forward.Advance(1, 0.01f, true); backward.Advance(-0.5f, 0.01f, true); }
        Assert.Equal((forward.Phase - 0.5) / 2, 0.5 - backward.Phase, 5);
    }

    [Fact]
    public void StoppingBlendsToIdleAndHiddenStageDoesNotAdvance()
    {
        var cycle = new CorridorWalkCycle(0.2);
        cycle.Advance(1, 0.05f, true);
        Assert.InRange(cycle.Weight, 0.01f, 0.99f);
        double phase = cycle.Phase;
        float weight = cycle.Weight;
        cycle.Advance(1, 0.05f, false);
        Assert.Equal(phase, cycle.Phase);
        Assert.Equal(weight, cycle.Weight);
        for (int i = 0; i < 200; i++) cycle.Advance(0, 0.02f, true);
        Assert.Equal(0, cycle.Weight);
        phase = cycle.Phase;
        cycle.Advance(0, 0.1f, true);
        Assert.Equal(phase, cycle.Phase);
    }
}
