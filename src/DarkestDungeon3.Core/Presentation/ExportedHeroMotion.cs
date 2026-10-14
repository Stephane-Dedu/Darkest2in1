using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Retimes one uninterrupted source motion to a native skill's presentation phases.</summary>
public static class ExportedHeroMotion
{
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
