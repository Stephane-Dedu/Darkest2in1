using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorWalkCycleTests
{
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
            double reach = Math.Sqrt(height * height + Math.Pow(foot.Forward * leg, 2)) / leg;
            // Equal-length thigh/shin approximation: 35..52 degrees of flexion, rather than ~80.
            double bend = 2 * Math.Acos(Math.Min(1, reach)) * 180 / Math.PI;
            Assert.InRange(bend, 30, 55);
        }
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
        Assert.InRange(CorridorWalkCycle.Sample(0.81).Lift, 0.079f, 0.081f);
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
