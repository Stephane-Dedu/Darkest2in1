using System;

namespace DarkestDungeon3.Core.Dungeon;

/// <summary>One scene pan for the ground, atmosphere and hero feet, in virtual pixels.</summary>
public sealed class CorridorSceneMotion
{
    public const float StepSeconds = 1.2f;
    private bool _sampled;
    private int _seed, _corridor, _heading, _width, _height;
    private string _art;
    private float _camera;
    public double Travel { get; private set; }

    public static float Slide(float secondsSinceStep, int stepDirection, float walkProgress)
    {
        float t = Math.Max(0, Math.Min(1, secondsSinceStep / StepSeconds));
        float eased = t * t * (3 - 2 * t);
        return stepDirection * 720 * (1 - eased) - walkProgress * 720;
    }

    public static float Camera(int width, int height, int tile, int count, bool reverse, float slide)
    {
        if (width > 0 && height > 0)
        {
            var crop = CorridorPanoramaLayout.Crop(width,height,
                CorridorPanoramaLayout.ViewProgress(tile,count,reverse,slide));
            return 1920 * crop.X / crop.Width;
        }
        return (reverse ? count - 1 - tile : tile) * 720 - slide;
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
