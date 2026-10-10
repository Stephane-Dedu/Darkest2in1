using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>Where the crawl's scene lies in a screen-sized snapshot of the letterboxed 1920x1080 canvas.</summary>
internal static class FightBackdropLayout
{
    public const float CanvasWidth = 1920f, CanvasHeight = 1080f, SceneHeight = 720f;

    /// <summary>The scene strip (full width, virtual y 0..720) of a <paramref name="width"/>x<paramref name="height"/>
    /// snapshot, in texture coordinates (origin bottom-left).</summary>
    public static Rect SceneStrip(float width, float height)
    {
        float s = Mathf.Min(width / CanvasWidth, height / CanvasHeight);
        float ox = (width - CanvasWidth * s) / 2f, oy = (height - CanvasHeight * s) / 2f;
        return new Rect(ox / width, 1f - (oy + SceneHeight * s) / height, CanvasWidth * s / width, SceneHeight * s / height);
    }
}
