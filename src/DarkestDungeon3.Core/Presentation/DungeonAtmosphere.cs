using System;
using System.Collections.Generic;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Decorative motion in panorama space. Never consumes expedition RNG or follows party heading.</summary>
public static class DungeonAtmosphere
{
    public readonly struct Particle
    {
        public readonly string Effect;
        public readonly float X, Y, Width, Height, Phase, R, G, B, Alpha;
        public Particle(string effect, float x, float y, float width, float height, float phase,
            float r, float g, float b, float alpha)
        { Effect = effect; X = x; Y = y; Width = width; Height = height; Phase = phase; R = r; G = g; B = b; Alpha = alpha; }
    }

    public static void Fill(List<Particle> output, string region, int seed, float seconds, float light,
        IReadOnlyList<ExpeditionMap3D.MapRect> fires = null)
    {
        output.Clear();
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
        if (float.IsNaN(light) || float.IsInfinity(light)) light = 100;
        float visibility = .35f + .65f * Math.Max(0, Math.Min(100, light)) / 100;
        bool city = region == "dd2_city";
        string cloud = region switch { "dd2_city" => "smoke", "dd2_farm" => "gas", "dd2_forest" => "forest", "dd2_coast" => "mist", _ => null };
        if (cloud == null) return;
        for (int i = 0; i < (city ? 6 : 9); i++)
        {
            float phase = Hash(seed, i), cycle = Fraction(seconds / (13 + phase*9) + phase);
            float x = 110 + Hash(seed, i+31)*1700 + (cycle-.5f)*110;
            float y = (city ? 310 : 470) + Hash(seed, i+67)*(city ? 180 : 110) - cycle*(city ? 150 : 32);
            float alpha = (float)Math.Sin(cycle*Math.PI) * (city ? .22f : .16f) * visibility;
            output.Add(new Particle(cloud, x, y, city ? 170+cycle*170 : 380+phase*210,
                city ? 230+cycle*140 : 95+phase*65, phase,
                city ? .42f : region == "dd2_farm" ? .58f : .42f,
                city ? .32f : region == "dd2_farm" ? .40f : .57f,
                city ? .28f : region == "dd2_farm" ? .27f : .64f, alpha));
        }
        if (!city) return;
        for (int i = 0; i < 18; i++)
        {
            float phase = Hash(seed, i+101), cycle = Fraction(seconds/(3+phase*4)+phase);
            output.Add(new Particle("ember", 50+Hash(seed,i+131)*1820+cycle*42,
                610-cycle*420, 2+phase*3, 4+phase*4, phase, 1,.56f,.18f,
                (float)Math.Sin(cycle*Math.PI)*.7f*visibility));
        }
        if (fires == null) return;
        for (int i = 0; i < fires.Count; i++)
        {
            var fire = fires[i];
            float phase = Hash(seed,i+211), flicker = .85f+.15f*(float)Math.Sin(seconds*5+phase*13);
            output.Add(new Particle("mist",fire.X,fire.Y+fire.Height*.2f,fire.Width*2.5f,fire.Height*1.2f,
                phase,1,.32f,.06f,.11f*flicker*visibility));
            output.Add(new Particle("fire",fire.X,fire.Y,fire.Width,fire.Height,phase,1,.396745f,.127358f,.588235f*visibility));
        }
    }

    /// <summary>Apply exactly the same crop as the background, then clip without reversing atlas UVs.</summary>
    public static bool Project(Particle particle, PanoramaCrop crop, ExpeditionMap3D.MapRect atlas,
        out ExpeditionMap3D.MapRect rect, out ExpeditionMap3D.MapRect uv)
    {
        float left = ((particle.X-particle.Width/2)/1920-crop.X) / crop.Width*1920;
        float top = ((particle.Y-particle.Height/2)/720-(1-crop.Y-crop.Height)) / crop.Height*720;
        float width = particle.Width/crop.Width, height = particle.Height/crop.Height;
        float x = Math.Max(0,left), y = Math.Max(0,top), right = Math.Min(1920,left+width), bottom = Math.Min(720,top+height);
        rect = default; uv = default;
        if (right <= x || bottom <= y || particle.Alpha <= 0) return false;
        rect = new ExpeditionMap3D.MapRect(x,y,right-x,bottom-y);
        uv = new ExpeditionMap3D.MapRect(atlas.X+(x-left)/width*atlas.Width,
            atlas.Y+(top+height-bottom)/height*atlas.Height,
            (right-x)/width*atlas.Width, (bottom-y)/height*atlas.Height);
        return true;
    }

    private static float Fraction(float value) => value-(float)Math.Floor(value);
    private static float Hash(int seed, int index)
    {
        unchecked
        {
            uint value = (uint)seed ^ ((uint)index+1)*0x9e3779b9u;
            value = (value^(value>>16))*0x85ebca6bu;
            value = (value^(value>>13))*0xc2b2ae35u;
            return (value^(value>>16))%65536/65536f;
        }
    }
}
