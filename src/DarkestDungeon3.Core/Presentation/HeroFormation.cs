using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>One set of screen-space anchors for the native party, its bars and pointer targets.</summary>
public static class HeroFormation
{
    public const float FireX = 960;
    private static readonly float[] Corridor = { 788, 620, 452, 284 };
    private static readonly float[] Camp = { 1250, 1450, 670, 470 };

    public static float X(int rank, bool camping)
    {
        if (rank < 0 || rank >= Corridor.Length) throw new ArgumentOutOfRangeException(nameof(rank));
        return (camping ? Camp : Corridor)[rank];
    }

    public static bool FacesLeft(int rank, bool camping) => camping && X(rank, true) > FireX;
}
