using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Retimes one uninterrupted source motion to a native skill's presentation phases.</summary>
public static class ExportedHeroMotion
{
    public static float AdvanceWalk(float previous, float distancePixels, float clipSpeedPixels, float duration)
    {
        if (!Finite(duration) || duration <= 0) return 0;
        double time = Finite(previous) ? previous : 0;
        if (Finite(distancePixels) && Finite(clipSpeedPixels) && clipSpeedPixels > 1) time += distancePixels/clipSpeedPixels;
        return (float)((time % duration + duration) % duration);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float TimeInClip(string part, float progress, float windupEnd, float strikeEnd, float duration)
    {
        if (windupEnd < 0 || strikeEnd < windupEnd || duration < strikeEnd)
            throw new ArgumentOutOfRangeException(nameof(duration), "Motion segments must be ordered");
        float p = float.IsNaN(progress) ? 0 : Math.Max(0, Math.Min(1, progress));
        return part switch
        {
            "antic" => p * windupEnd,
            "idle" => windupEnd,
            "recover" => strikeEnd + p * (duration - strikeEnd),
            _ => windupEnd + p * (strikeEnd - windupEnd)
        };
    }
}
