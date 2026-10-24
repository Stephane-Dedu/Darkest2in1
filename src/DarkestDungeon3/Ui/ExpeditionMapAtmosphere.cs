using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Native masks and simulation sheets, animated locally beneath the quest controls without gameplay RNG.</summary>
internal static class ExpeditionMapAtmosphere
{
    internal const int OverviewDrawCount = 32;
    internal static int LastDrawCount { get; private set; }
    public static void Draw(ExpeditionMap3D layout, ExpeditionMap3D.MapRect camera, float seconds)
    {
        if (Event.current.type != EventType.Repaint) return;
        LastDrawCount = 0;
        ExpeditionMapEffects.Prepare();
        var clip = new Rect(535, 115, RosterColumn.X - 551, 710);
        var colour = GUI.color;
        GUI.BeginGroup(clip);
        try
        {
            foreach (var spot in layout.Spots.Values)
            {
                switch (spot.Location)
                {
                    case "crypts": BurningCity(spot, camera, clip.position, seconds); break;
                    case "warrens": Clouds("gas", spot, camera, clip.position, seconds, new Color(0.462745f, 0.25881f, 0.192157f), 0.09f, 4, 250, 34); break;
                    case "cove": Clouds("mist", spot, camera, clip.position, seconds, new Color(0.48f, 0.62f, 0.7f), 0.065f, 4, 360, 48); break;
                    case "weald": Clouds("forest", spot, camera, clip.position, seconds, new Color(0.183295f, 0.413723f, 0.453f), 0.045f, 4, 330, 36); break;
                }
            }
        }
        finally { GUI.color = colour; GUI.EndGroup(); }
    }

    private static void Clouds(string effect, ExpeditionMap3D.Spot spot, ExpeditionMap3D.MapRect camera,
        Vector2 offset, float time, Color tint, float opacity, int count, float span, float below)
    {
        for (int i = 0; i < count; i++)
        {
            float seed = Fraction(i * 0.61803399f + 0.13f);
            float phase = Fraction(time / (9 + seed * 4) + seed);
            float x = spot.HubX - span / 2 + span * seed + (phase - 0.5f) * 45;
            float y = spot.HubY + below - phase * 30 + Mathf.Sin(time * 0.22f + i) * 5;
            float alpha = Mathf.Sin(phase * Mathf.PI) * opacity;
            Sprite(effect, camera, offset, x, y, 105 + seed * 55, 24 + seed * 16,
                time, seed, new Color(tint.r, tint.g, tint.b, alpha));
        }
    }

    private static void BurningCity(ExpeditionMap3D.Spot spot, ExpeditionMap3D.MapRect camera, Vector2 offset, float time)
    {
        for (int i = 0; i < 4; i++)
        {
            float seed = Fraction(i * 0.61803399f + 0.2f);
            float x = spot.HubX - 145 + i * 95, y = spot.HubY - 24 - seed * 15;
            float phase = Fraction(time / 5.5f + seed);
            Sprite("smoke", camera, offset, x + phase * 18, y - 38 - phase * 65,
                45 + phase * 30, 55 + phase * 45, time, seed,
                new Color(0.26f, 0.24f, 0.25f, Mathf.Sin(phase * Mathf.PI) * 0.14f));
            Sprite("fire", camera, offset, x, y - 12, 19 + seed * 9, 28 + seed * 16,
                time, seed, new Color(1, 0.396745f, 0.127358f, 0.588235f));
        }
        for (int i = 0; i < 12; i++)
        {
            float seed = Fraction(i * 0.61803399f + 0.32f), phase = Fraction(time / (3 + seed * 2) + seed);
            float x = spot.HubX - 210 + seed * 420 + Mathf.Sin(time * 0.7f + i) * 9 + phase * 16;
            float y = spot.HubY - 12 - phase * 145;
            float size = 2 + seed * 2.5f;
            Sprite("ember", camera, offset, x, y, size, size * 1.5f, time, seed,
                new Color(1, 0.64f, 0.22f, Mathf.Sin(phase * Mathf.PI) * 0.8f));
        }
    }

    private static void Sprite(string effect, ExpeditionMap3D.MapRect camera, Vector2 offset,
        float x, float y, float width, float height, float time, float phase, Color colour)
    {
        if (!ExpeditionMapEffects.TryGet(effect, out var animation, out var texture)) return;
        var world = new ExpeditionMap3D.MapRect(x - width / 2, y - height / 2, width, height);
        var r = ExpeditionMapCamera.Transform(world, camera);
        var uv = animation.Uv(time, phase);
        // The scenery doubles in size on approach. Keep the same native masks
        // registered to it, but reduce their opacity as their screen area grows.
        colour.a *= Mathf.Min(1, ExpeditionMap3D.Width / camera.Width);
        GUI.color = colour;
        GUI.DrawTextureWithTexCoords(new Rect(r.X - offset.x, r.Y - offset.y, r.Width, r.Height), texture,
            new Rect(uv.X, uv.Y, uv.Width, uv.Height), true);
        LastDrawCount++;
    }

    private static float Fraction(float value) => value - Mathf.Floor(value);
}
