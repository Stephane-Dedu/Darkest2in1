using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Service placement and a terraced hillside, shared by terrain, paths and building foundations.</summary>
public static class HamletLayout
{
    public sealed class Site
    {
        public readonly string Id;
        public readonly float X, Z, Width, Yaw;
        public float Y => Hill(X, Z);
        public Site(string id, float x, float z, float width, float yaw = 0)
        { Id = id; X = x; Z = z; Width = width; Yaw = yaw; }
    }

    public static readonly IReadOnlyList<Site> Sites = new[]
    {
        new Site("stage_coach", -24, -1, 12, 12),
        new Site("nomad_wagon", 14, 1, 12, -12),
        new Site("statue", -4, 9, 5.5f),
        new Site("camping_trainer", -41, 11, 12, 15),
        new Site("blacksmith", -11, 23, 13, 5),
        new Site("tavern", -31, 32, 17, 12),
        new Site("sanitarium", 25, 24, 13, -14),
        new Site("guild", 9, 47, 13, -8),
        new Site("abbey", -16, 63, 15, 8),
        new Site("graveyard", 33, 61, 13, -12),
    };

    private static float Smooth(float a, float b, float x)
    {
        float t = Math.Max(0, Math.Min(1, (x-a)/(b-a)));
        return t*t*(3-2*t);
    }

    public static float RoadX(float z) => (float)Math.Sin((z+12)*.046f)*4;
    private static float Hill(float x, float z) => 4.5f*Smooth(8, 20, z) + 5.5f*Smooth(33, 44, z)
        + 5*Smooth(50, 61, z) + 5*Smooth(54, 95, Math.Abs(x)) - 3*Smooth(-30, -65, z);

    public static float Height(float x, float z)
    {
        float y = Hill(x, z), best = 0;
        Site closest = null;
        foreach (var site in Sites)
        {
            float distance = Math.Max(Math.Abs(x-site.X), Math.Abs(z-site.Z));
            float weight = 1-Smooth(site.Width*.59f, site.Width*.59f+4, distance);
            if (weight > best) { best = weight; closest = site; }
        }
        return closest == null ? y : y*(1-best)+closest.Y*best;
    }
}
