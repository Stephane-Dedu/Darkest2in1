using System;

namespace DarkestDungeon3.Dd2;

/// <summary>In-place gait in leg-length units. The stage owns position; this owns foot contacts.</summary>
internal sealed class CorridorWalkCycle
{
    internal const double Stance = 0.62;
    internal const float Stride = 0.46f;
    internal double Phase { get; private set; }
    internal float Weight { get; private set; }

    internal CorridorWalkCycle(double phase) => Phase = Wrap(phase);

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
    private float _forward, _offset;

    internal float Forward(CorridorWalkCycle.Foot foot, float observedForward, float distanceInLegLengths)
    {
        if (float.IsNaN(distanceInLegLengths) || float.IsInfinity(distanceInLegLengths)
            || Math.Abs(distanceInLegLengths) < .000001f)
        { _moving = _planted = false; _offset = 0; return foot.Forward; }
        if (foot.Planted)
        {
            _forward = !_moving ? observedForward - distanceInLegLengths
                : !_planted ? foot.Forward : _forward - distanceInLegLengths;
            _offset = _forward - foot.Forward;
            _moving = _planted = true;
            return _forward;
        }
        _moving = true; _planted = false;
        float u = foot.SwingProgress;
        return foot.Forward + _offset * (1 - u*u*(3-2*u));
    }
}
