using System;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>One scene pan for the ground, atmosphere and hero feet, in virtual pixels.</summary>
public sealed class CorridorSceneMotion
{
    public const float StepSeconds = 1.2f;
    public const float WalkingPixelsPerSecond = 320f;
    public const float MaximumWalkingFrameSeconds = .1f;

    public static float StepDuration(float squarePixels,float speedLimit,bool forward)
    {
        if (float.IsNaN(squarePixels) || float.IsInfinity(squarePixels) || squarePixels<=0
            || float.IsNaN(speedLimit) || float.IsInfinity(speedLimit) || speedLimit<=0) return StepSeconds;
        // Smoothstep's peak speed is 1.5 times its average. A discrete pan must
        // obey the same fitted walking limit as held movement, including reverse.
        return Math.Max(StepSeconds,1.5f*squarePixels/(speedLimit*(forward ? 1 : .5f)));
    }

    // Keep a fitted short-legged hero at walking cadence, rather than making it
    // spend multiple gait cycles on one logical square. Very small model scales
    // also reduce camera speed. Native step arc/poses remain untouched.
    public static float WalkingSpeedForLeg(float projectedLegPixels) =>
        float.IsNaN(projectedLegPixels) || float.IsInfinity(projectedLegPixels) || Math.Abs(projectedLegPixels) < 1
            ? WalkingPixelsPerSecond : Math.Min(WalkingPixelsPerSecond, Math.Abs(projectedLegPixels) * 1.8f);

    public static float WalkingDelta(float seconds, float squarePixels, float speedLimit, bool forward)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0
            || float.IsNaN(squarePixels) || float.IsInfinity(squarePixels) || squarePixels <= 0
            || float.IsNaN(speedLimit) || float.IsInfinity(speedLimit) || speedLimit <= 0) return 0;
        // A render/asset stall is not time spent walking unseen. Resume from the
        // last visible position rather than catching up with a multi-stride jump.
        return Math.Min(seconds, MaximumWalkingFrameSeconds) * speedLimit / squarePixels * (forward ? 1 : -.5f);
    }
    private bool _sampled;
    private int _seed, _corridor, _heading, _width, _height;
    private string _art;
    private float _camera;
    public double Travel { get; private set; }

    public static float Slide(float secondsSinceStep, int stepDirection, float walkProgress,float duration=StepSeconds)
    {
        float t = Math.Max(0, Math.Min(1, secondsSinceStep / duration));
        float eased = t * t * (3 - 2 * t);
        return stepDirection * 720 * (1 - eased) - walkProgress * 720;
    }

    public static float Camera(int width, int height, int tile, int count, bool reverse, float slide)
    {
        if (width > 0 && height > 0)
        {
            var crop = CorridorPanoramaLayout.Crop(width,height,
                CorridorPanoramaLayout.ViewProgress(tile,count,reverse,slide),count);
            return 1920 * crop.X / crop.Width;
        }
        return (reverse ? count - 1 - tile : tile) * 720 - slide;
    }

    public static float PropOffset(int width, int height, int count, int neighbor, float slide)
    {
        if (width <= 0 || height <= 0) return neighbor * 720 + slide;
        var start = CorridorPanoramaLayout.Crop(width, height, 0,count);
        var end = CorridorPanoramaLayout.Crop(width, height, 1,count);
        float square = 1920 * (end.X - start.X) / start.Width / CorridorPanoramaLayout.TravelSpan(count);
        return (neighbor + slide / 720) * square;
    }

    public float Sample(int seed, int corridor, int heading, string art, int width, int height, float camera)
    {
        if (float.IsNaN(camera) || float.IsInfinity(camera)) { Reset(); return 0; }
        bool same = _sampled && _seed == seed && _corridor == corridor && _heading == heading
            && _art == art && _width == width && _height == height;
        float distance = same ? camera - _camera : 0;
        _sampled = true; _seed = seed; _corridor = corridor; _heading = heading;
        _art = art; _width = width; _height = height; _camera = camera;
        Travel += distance;
        return distance;
    }

    public void Reset() { _sampled = false; Travel = 0; }
}
