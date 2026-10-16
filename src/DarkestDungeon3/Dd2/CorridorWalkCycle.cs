using System;

namespace DarkestDungeon3.Dd2;

/// <summary>In-place gait in leg-length units. The stage owns position; this owns foot contacts.</summary>
internal sealed class CorridorWalkCycle
{
    internal const double Stance = 0.62;
    // The planted arc must cover a walking step. A .46-leg arc forced two
    // gait cycles into the distance of one ordinary stride, producing a shuffle.
    internal const float Stride = 0.90f;
    internal double Phase { get; private set; }
    internal float Weight { get; private set; }

    internal CorridorWalkCycle(double phase) => Phase = Wrap(phase);

    internal void Begin(double phase) => Phase = Wrap(phase);

    internal static double StartPhase(float leftForward, float rightForward, bool backwards = false)
    {
        if (backwards) return Wrap(Stance-StartPhase(-leftForward,-rightForward));
        // Start over the forward foot. The rear foot must leave contact soon,
        // rather than inheriting a long stance from the phase used before stopping.
        float leading = Math.Max(leftForward,rightForward);
        double phase = Math.Max(Stance-.5,Stance-Stance*(leading+.65)/Stride);
        return Wrap(Math.Min(Stance-.000001,phase)+(rightForward>leftForward ? .5 : 0));
    }

    internal void Advance(float cyclesPerSecond, float deltaTime, bool visible)
    {
        if (!visible) return;
        if (float.IsNaN(cyclesPerSecond) || float.IsInfinity(cyclesPerSecond)) cyclesPerSecond = 0;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return;
        double dt = deltaTime;
        float blend = (float)(1 - Math.Exp(-dt / 0.14));
        Weight += ((Math.Abs(cyclesPerSecond) > 0.0001f ? 1f : 0f) - Weight) * blend;
        if (Weight < 0.0001f) Weight = 0;
        Phase = Wrap(Phase + cyclesPerSecond * dt);
    }

    internal static float Rate(float distancePixels, float projectedLegPixels, float deltaTime) =>
        deltaTime > 0 && Math.Abs(projectedLegPixels) >= 1
            ? distancePixels * (float)Stance / (Stride * projectedLegPixels * deltaTime) : 0;

    internal readonly struct Foot
    {
        internal readonly float Forward, Lift, Pitch;
        internal readonly bool Planted;
        internal readonly float SwingProgress;
        internal Foot(double forward, double lift, double pitch, bool planted, double swingProgress = 0)
        { Forward = (float)forward; Lift = (float)lift; Pitch = (float)pitch; Planted = planted; SwingProgress = (float)swingProgress; }
    }

    internal static Foot Sample(double phase)
    {
        double p = Wrap(phase);
        if (p < Stance)
        {
            double t = p / Stance;
            // Constant backwards speed during contact, with a heel strike and a late toe-off.
            double pitch = 10 * Math.Pow(Math.Max(0, 1 - t / 0.18), 2)
                         - 14 * Math.Pow(Math.Max(0, (t - 0.8) / 0.2), 2);
            return new Foot(Stride * (0.5 - t), 0, pitch, true);
        }
        double u = (p - Stance) / (1 - Stance);
        double u2 = u * u, u3 = u2 * u;
        // Hermite tangents match the planted foot velocity at both ends of the swing.
        double x = 3 * u2 - 2 * u3 - 0.5
                 - (1 - Stance) / Stance * (2 * u3 - 3 * u2 + u);
        double lift = 0.08 * Math.Pow(Math.Sin(Math.PI * u), 2);
        double pitchSwing = -14 + 24 * (3 * u2 - 2 * u3) - 12 * Math.Sin(Math.PI * u);
        return new Foot(Stride * x, lift, pitchSwing, false, u);
    }

    // Combat idle is a crouch. Raise that pose toward a relaxed walking height, retaining soft knees
    // and a small weight transfer instead of compressing the already bent native stance further.
    internal static float PelvisLift(float legLength, float nativeHipHeight, double phase) =>
        Math.Max(0, legLength * 0.93f - nativeHipHeight)
        + legLength * (-0.015f - 0.008f * (float)Math.Cos((phase - Handover) * Math.PI * 4));

    internal static float PelvisContactDrop(float reach, float horizontalSquared, float vertical) =>
        Math.Max(0,vertical-(float)Math.Sqrt(Math.Max(0,reach*reach*.98f*.98f-horizontalSquared)));

    // Centre the transfer in double support; the body then passes over each planted foot.
    private const double Handover = (Stance - 0.5) / 2;
    internal static float SupportShift(double phase) => (float)Math.Sin((phase - Handover) * Math.PI * 2);

    internal static double Wrap(double phase) => double.IsNaN(phase) || double.IsInfinity(phase)
        ? 0 : phase - Math.Floor(phase);
}

/// <summary>Keep a planted foot on the moving ground even while the upper body blends out of combat idle.</summary>
internal sealed class CorridorFootContact
{
    private bool _moving, _planted;
    private float _forward, _offset, _swingStart;
    private double _phase;
    private int _direction;

    internal void Reset() { _moving = _planted = false; _offset = _swingStart = 0; _phase = 0; _direction = 0; }

    internal float Forward(CorridorWalkCycle.Foot foot, float observedForward, float distanceInLegLengths)
    {
        if (float.IsNaN(distanceInLegLengths) || float.IsInfinity(distanceInLegLengths)
            || Math.Abs(distanceInLegLengths) < .000001f)
        { Reset(); return foot.Forward; }
        double phase = foot.Planted ? CorridorWalkCycle.Stance*(.5-foot.Forward/CorridorWalkCycle.Stride)
            : CorridorWalkCycle.Stance+foot.SwingProgress*(1-CorridorWalkCycle.Stance);
        double advanced = _phase+distanceInLegLengths*CorridorWalkCycle.Stance/CorridorWalkCycle.Stride;
        int direction = distanceInLegLengths > 0 ? 1 : -1;
        if (foot.Planted)
        {
            // A long frame can skip an entire lift/landing while both sampled
            // poses are planted. That is a new contact, not the old foot dragged
            // through the missed swing and past the skeleton's reach.
            bool sameContact = _moving && _planted && advanced >= 0 && advanced < CorridorWalkCycle.Stance;
            _forward = !_moving ? observedForward-distanceInLegLengths
                : sameContact ? _forward-distanceInLegLengths : foot.Forward;
            _offset = _forward - foot.Forward;
            _swingStart = 0;
        }
        else
        {
            float travel = direction > 0 ? foot.SwingProgress : 1-foot.SwingProgress;
            if (!_moving) { _offset = observedForward-foot.Forward; _swingStart = travel; }
            else if (!_planted && direction != _direction) { _offset = _forward-foot.Forward; _swingStart = travel; }
            else if (direction > 0 ? advanced >= 1 : advanced < CorridorWalkCycle.Stance-1)
            { _offset = _swingStart = 0; }
            float u = Math.Min(1,Math.Max(0,(travel-_swingStart)/Math.Max(.000001f,1-_swingStart)));
            _forward = foot.Forward+_offset*(1-u*u*(3-2*u));
        }
        _moving = true; _planted = foot.Planted; _phase = phase; _direction = direction;
        return _forward;
    }
}
