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
        float physical = Progress(tileIndex,tileCount,reverse,slide);
        float heading = reverse ? 1 - physical : physical;
        // A two-square hall must not sweep the same 2,227px panorama range as
        // a long hall in one square. Short routes use a centred part of the scene,
        // retaining the painting's scale and a readable walking speed.
        float span = Math.Max(0,tileCount-1) / (float)Math.Max(9,tileCount-1);
        return .5f + (heading-.5f)*span;
    }

    // Uniform 8% overscan protects the frame edges. Wider plates provide more lateral travel.
    // Keep the authored y=680 contact fixed; walking props retain their physical square coordinates.
    public static PanoramaCrop Crop(int width, int height, float progress)
    {
        float scale = Math.Max(1920f / width, 720f / height) * 1.08f;
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
