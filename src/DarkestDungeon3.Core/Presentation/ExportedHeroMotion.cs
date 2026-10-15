using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Retimes one uninterrupted source motion to a native skill's presentation phases.</summary>
public static class ExportedHeroMotion
{
    /// <summary>Match rendered travel to the original clip's extracted root-motion curve.</summary>
    public static float AdvanceWalkByMotion(float previous,float distancePixels,float pixelsPerSourceUnit,float duration,IReadOnlyList<float> distances)
    {
        if (!Finite(duration) || duration<=0 || !Finite(pixelsPerSourceUnit) || pixelsPerSourceUnit<=0
            || distances==null || distances.Count<2 || distances[0]!=0) throw new ArgumentException("Invalid walk calibration");
        for (int i=1;i<distances.Count;i++)
            if (!Finite(distances[i]) || distances[i]<=distances[i-1]) throw new ArgumentException("Walk distances must increase");
        double period=distances[distances.Count-1];
        double time=Finite(previous) ? (previous%duration+duration)%duration : 0;
        double frame=time/duration*(distances.Count-1);
        int first=Math.Min((int)frame,distances.Count-2);
        double travel=distances[first]+(distances[first+1]-distances[first])*(frame-first);
        if (Finite(distancePixels)) travel+=distancePixels/pixelsPerSourceUnit;
        travel=(travel%period+period)%period;
        int lower=0,upper=distances.Count-1;
        while (upper-lower>1) { int mid=(lower+upper)/2; if (distances[mid]<=travel) lower=mid; else upper=mid; }
        return (float)((lower+(travel-distances[lower])/(distances[upper]-distances[lower]))/(distances.Count-1)*duration);
    }

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
