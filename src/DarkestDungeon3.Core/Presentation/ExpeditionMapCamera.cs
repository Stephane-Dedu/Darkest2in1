using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>A persistent map camera. Retargeting keeps its position and velocity; scenery never swaps frames.</summary>
public sealed class ExpeditionMapCamera
{
    private float _zoom, _zoomVelocity, _u = 0.5f, _v = 0.5f, _uVelocity, _vVelocity;
    private float _targetZoom, _targetU = 0.5f, _targetV = 0.5f;
    private const float Frequency = 10;

    public string Location { get; private set; }
    public bool IsOverview => Location == null;

    public ExpeditionMap3D.MapRect Frame
    {
        get
        {
            float scale = (float)Math.Exp(_zoom);
            float width = ExpeditionMap3D.Width * scale, height = ExpeditionMap3D.Height * scale;
            return new ExpeditionMap3D.MapRect(-_u * (width - ExpeditionMap3D.Width),
                -_v * (height - ExpeditionMap3D.Height), width, height);
        }
    }

    public void Focus(ExpeditionMap3D.Spot spot)
    {
        if (spot == null) { Overview(); return; }
        var target = ExpeditionMap3D.OverviewRect(spot, 1);
        Location = spot.Location;
        _targetZoom = (float)Math.Log(spot.ZoomScale);
        _targetU = Clamp(-target.X / (target.Width - ExpeditionMap3D.Width), 0, 1);
        _targetV = Clamp(-target.Y / (target.Height - ExpeditionMap3D.Height), 0, 1);
    }

    public void Overview()
    {
        Location = null;
        _targetZoom = 0;
        // Shrink towards the same physical point; resetting pan here would add a sideways jump.
        _targetU = _u; _targetV = _v;
    }

    public void Reset()
    {
        Location = null; _zoom = _targetZoom = _zoomVelocity = _uVelocity = _vVelocity = 0;
        _u = _v = _targetU = _targetV = 0.5f;
    }

    public void Step(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, 0.1f); // A stalled frame must not skip the whole approach.
        Spring(ref _zoom, ref _zoomVelocity, _targetZoom, seconds);
        Spring(ref _u, ref _uVelocity, _targetU, seconds);
        Spring(ref _v, ref _vVelocity, _targetV, seconds);
        Bound(ref _zoom, ref _zoomVelocity, 0, (float)Math.Log(ExpeditionMap3D.MaxZoom));
        Bound(ref _u, ref _uVelocity, 0, 1);
        Bound(ref _v, ref _vVelocity, 0, 1);
    }

    public static ExpeditionMap3D.MapRect Transform(ExpeditionMap3D.MapRect rect, ExpeditionMap3D.MapRect camera)
    {
        float scale = camera.Width / ExpeditionMap3D.Width;
        return new ExpeditionMap3D.MapRect(camera.X + rect.X * scale, camera.Y + rect.Y * scale,
            rect.Width * scale, rect.Height * scale);
    }

    private static void Spring(ref float value, ref float velocity, float target, float seconds)
    {
        float error = value - target, tangent = velocity + Frequency * error;
        float decay = (float)Math.Exp(-Frequency * seconds);
        value = target + (error + tangent * seconds) * decay;
        velocity = (velocity - Frequency * tangent * seconds) * decay;
        if (Math.Abs(value - target) < 0.00001f && Math.Abs(velocity) < 0.0001f)
        { value = target; velocity = 0; }
    }

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    private static void Bound(ref float value, ref float velocity, float min, float max)
    {
        float bounded = Clamp(value, min, max);
        if (bounded != value) { value = bounded; velocity = 0; }
    }
}
