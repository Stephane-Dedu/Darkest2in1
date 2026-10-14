using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Compact DD1 composition: left cliff, low square and an upper Abbey/graveyard shelf.</summary>
public static class HamletLayout
{
    public readonly struct Point
    {
        public readonly float X, Y, Z;
        public Point(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    public sealed class Site
    {
        public readonly string Id;
        public readonly float X, Y, Z, Width, Yaw, FootprintWidth, FootprintDepth;
        public Site(string id, float x, float y, float z, float width, float yaw = 0, float footprintWidth = 1, float footprintDepth = 1)
        { Id = id; X = x; Y = y; Z = z; Width = width; Yaw = yaw; FootprintWidth = width * footprintWidth; FootprintDepth = width * footprintDepth; }
        public Point Entrance
        {
            get
            {
                float angle = Yaw * (float)Math.PI / 180, distance = FootprintDepth / 2 + .8f;
                return new Point(X - (float)Math.Sin(angle) * distance, Y, Z - (float)Math.Cos(angle) * distance);
            }
        }
    }
    public sealed class Route
    {
        public readonly string Id;
        public readonly float Width;
        public readonly Point[] Points;
        public Route(string id, float width, params Point[] points) { Id = id; Width = width; Points = points; }
    }

    public const float MinX = -66, MaxX = 58, MinZ = -36, MaxZ = 69, FieldOfView = 36;
    // 22-degree down pitch, 3-degree yaw. Keep facades and approaches beneath the town HUD.
    public static readonly Point CameraTarget = new(14, 13, 17), CameraOffset = new(6.60f, 50.95f, -125.92f);
    // Footprint ratios come from the authored v02 bounds. Rectangular pads allow a compact town without buried corners.
    public static readonly IReadOnlyList<Site> Sites = new[]
    {
        new Site("stage_coach", -34, -.5f, -9, 11, 12, 1, .43f),
        new Site("tavern", -28, 1, 5, 18, 6, 1, .74f),
        new Site("camping_trainer", -45, 24, 24, 12, 8, 1, .84f),
        new Site("sanitarium", -13, 8, 26, 18, 2, .95f),
        new Site("graveyard", 2, 12.5f, 24, 10, 0, .93f),
        new Site("abbey", 10, 17, 43, 24, 0, .64f),
        new Site("guild", 27, 6, 22, 14, -4, 1, .83f),
        new Site("blacksmith", 35, 0, -1, 15, -8, 1, .82f),
        new Site("statue", 0, 0, -5, 4.5f, 0, 1, .89f),
        new Site("nomad_wagon", 13, 0, -10, 10, -5, .91f),
    };
    private static Point Door(string id) => Sites.First(s => s.Id == id).Entrance;
    public static readonly IReadOnlyList<Route> Routes = new[]
    {
        new Route("main", 3.8f, new(-51,-4,-36), new(-40,-2,-30), new(-32,-1,-23), new(-25,0,-12), new(-8,0,0), new(2,0,4),
            new(9,4,12), new(9,9,18), new(8,12.5f,24), new(20,12.5f,28), new(20,17,34), Door("abbey")),
        new Route("stage_coach", 3, new(-25,0,-12), Door("stage_coach")),
        new Route("tavern", 3, new(-8,0,0), new(-20,1,-3), Door("tavern")),
        new Route("camping_trainer", 2.8f, new(-20,1,-3), new(-37,1,0), new(-43,1,1),
            new(-49,5,7), new(-40,10,10), new(-49,15,13), new(-40,20,16), Door("camping_trainer")),
        new Route("sanitarium", 3, Door("graveyard"), new(-3,12.5f,18), new(-6,10.25f,14), Door("sanitarium")),
        new Route("graveyard", 2.8f, new(9,9,18), new(5,12.5f,18), Door("graveyard")),
        new Route("guild", 3, new(9,4,12), new(16,6,14), Door("guild")),
        new Route("statue", 3, new(-8,0,0), new(-4,0,-8), Door("statue")),
        new Route("nomad_wagon", 3.4f, new(-4,0,-8), new(2,0,-16), Door("nomad_wagon")),
        new Route("blacksmith", 3.4f, new(2,0,-16), new(20,0,-18), new(29,0,-12), Door("blacksmith")),
    };

    private static float Smooth(float a, float b, float x)
    { float t = Math.Max(0, Math.Min(1, (x-a)/(b-a))); return t*t*(3-2*t); }

    private static float Hill(float x, float z)
    {
        float wave=(float)Math.Sin(x*.19f)*1.8f+(float)Math.Sin(x*.53f)*.65f;
        float civic = 12.5f * Ledge(16+wave, 21+wave, z) * Smooth(-26,-19,x) * (1-Smooth(28,39,x));
        float upper = 17 * Ledge(29+wave,34+wave,z) * Smooth(-8,-2,x) * (1-Smooth(22,32,x));
        float cliff = (24+.6f*(float)Math.Sin(z*.47f)) * Ledge(-33,-40,x) * Ledge(7+wave,13+wave,z) * (1-Smooth(37,47,z));
        float banks=Math.Max(Math.Max(civic,upper),cliff)*(1-Smooth(52,67,z));
        float rough=.32f*(float)Math.Sin(x*.43f+z*.23f)+.18f*(float)Math.Sin(x*.81f-z*.58f);
        float rim = 2 * Smooth(43, 58, Math.Abs(x));
        float edge = 13 * Smooth(55, 66, Math.Abs(x + 4));
        // An exposed eastern face sits beyond the workshop foundations, rather than a wide grassy roll-off.
        float eastWave=.65f*(float)Math.Sin(z*.27f);
        float eastCliff=13*Ledge(46+eastWave,50+eastWave,x)*Smooth(-31,-23,z)*(1-Smooth(38,46,z));
        edge=Math.Max(edge,eastCliff);
        float ditch = 8*(1-Smooth(-28,-18,z))*(1-Smooth(-35,-18,x)) + 6*(1-Smooth(-33,-25,z));
        return banks + rough + rim - edge - ditch;
    }

    private static float Ledge(float a,float b,float value)
    {
        float t=(value-a)/(b-a);
        return .22f*Smooth(0,.28f,t)+.46f*Smooth(.35f,.55f,t)+.32f*Smooth(.78f,1,t);
    }

    public static float Height(float x, float z)
    {
        float y = Hill(x, z), best = 0, roadHeight = y;
        foreach (var route in Routes) for (int i = 1; i < route.Points.Length; i++)
        {
            var a = route.Points[i-1]; var b = route.Points[i];
            float dx = b.X-a.X, dz = b.Z-a.Z, length = dx*dx+dz*dz;
            float t = length > 0 ? Math.Max(0, Math.Min(1, ((x-a.X)*dx+(z-a.Z)*dz)/length)) : 0;
            float rx = a.X+dx*t, rz = a.Z+dz*t;
            float distance = (float)Math.Sqrt((x-rx)*(x-rx)+(z-rz)*(z-rz));
            float weight = 1-Smooth(route.Width*.55f, route.Width*.55f+1.8f, distance);
            if (weight > best) { best = weight; roadHeight = a.Y+(b.Y-a.Y)*t; }
        }
        y = y*(1-best) + roadHeight*best;
        best = 0; Site closest = null;
        foreach (var site in Sites)
        {
            float angle = site.Yaw*(float)Math.PI/180, c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            float dx = x-site.X, dz = z-site.Z;
            float localX = dx*c-dz*s, localZ = dx*s+dz*c;
            float distance = Math.Max(Math.Abs(localX)-site.FootprintWidth/2-.35f, Math.Abs(localZ)-site.FootprintDepth/2-.35f);
            float weight = 1-Smooth(0, 1.8f, distance);
            if (weight > best) { best = weight; closest = site; }
        }
        return closest == null ? y : y*(1-best)+closest.Y*best;
    }
}
