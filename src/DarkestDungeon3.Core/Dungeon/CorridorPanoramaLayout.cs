using System;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>A complete perspective scene is a bounded plate, never a repeatable wall tile.</summary>
public static class CorridorPanoramaLayout
{
    public static float Progress(int tileIndex, int tileCount, bool reverse, float slide)
    {
        if (tileCount <= 1) return .5f;
        float position = tileIndex - (reverse ? -1 : 1) * slide / 720f;
        return Clamp(position / (tileCount - 1));
    }

    // DD1 presents forward travel toward screen right from either entrance.
    // Retain the original painting's orientation, turning the view's path rather than its UVs.
    public static float ViewProgress(int tileIndex, int tileCount, bool reverse, float slide)
    {
        if (tileCount < 1) return .5f;
        float heading = reverse ? tileCount - 1 - tileIndex : tileIndex;
        float position = heading - slide / 720f;
        // Include the half-square approach at each door. Clamping at the first
        // and last square centres froze the floor while the party still walked.
        return Clamp(.5f + (position - (tileCount - 1) * .5f) / TravelSpan(tileCount));
    }

    public static int TravelSpan(int tileCount) => Math.Max(6,tileCount);

    // Base overscan protects edges; hall length can increase zoom up to1.6x to retain useful travel.
    // Keep the authored y=680 contact fixed, with props on the same physical square coordinates.
    public static PanoramaCrop Crop(int width, int height, float progress, int tileCount=0)
    {
        float scale = Math.Max(1920f / width, 720f / height) * 1.08f;
        if (tileCount>0)
        {
            // Bound zoom while giving the painting a useful physical walking distance.
            float required=(1920+TravelSpan(tileCount)*360f)/width;
            scale=Math.Max(scale,Math.Min(required,scale*1.6f));
        }
        float w = width * scale, h = height * scale;
        float top = 680f / 720 * h - 680;
        return new PanoramaCrop((w - 1920) * Clamp(progress) / w, (h - top - 720) / h, 1920 / w, 720 / h);
    }

    // Native passes are registered at the centre of the hall. Distant silhouettes
    // travel less, while ground/walls and their fire emitters retain the original pan.
    public static float LayerProgress(float progress, int layer)
    {
        float travel = layer == 0 ? .18f : layer == 1 ? .48f : 1f;
        return .5f + (Clamp(progress) - .5f) * travel;
    }

    private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
}

public readonly struct PanoramaCrop
{
    public readonly float X, Y, Width, Height;
    public PanoramaCrop(float x, float y, float width, float height)
    { X = x; Y = y; Width = width; Height = height; }
}
