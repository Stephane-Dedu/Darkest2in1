using System;

namespace DarkestDungeon3.Dd2;

/// <summary>In-place gait in leg-length units. The stage owns position; this owns foot contacts.</summary>
internal sealed class CorridorWalkCycle
{
    internal const double Stance = 0.62;
    internal const float Stride = 0.46f;
    internal double Phase { get; private set; }
    internal float Weight { get; private set; }
    private float _speed;

    internal CorridorWalkCycle(double phase) => Phase = Wrap(phase);

    internal void Advance(float speed, float deltaTime, bool visible)
    {
        if (!visible) return;
        if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return;
        double dt = Math.Min(deltaTime, 0.1f);
        speed = Math.Max(-1, Math.Min(1, speed));
        float blend = (float)(1 - Math.Exp(-dt / 0.14));
        _speed += (speed - _speed) * blend;
        Weight += ((Math.Abs(speed) > 0.01f ? 1f : 0f) - Weight) * blend;
        if (Math.Abs(_speed) < 0.0001f) _speed = 0;
        if (Weight < 0.0001f) Weight = 0;
        Phase = Wrap(Phase + _speed * dt / 1.28);
    }

    internal readonly struct Foot
    {
        internal readonly float Forward, Lift, Pitch;
        internal readonly bool Planted;
        internal Foot(double forward, double lift, double pitch, bool planted)
        { Forward = (float)forward; Lift = (float)lift; Pitch = (float)pitch; Planted = planted; }
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
        return new Foot(Stride * x, lift, pitchSwing, false);
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
