using System;
using System.Threading;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Material grading shared by the native town and its private composition preview.</summary>
public static class HamletPalette
{
    private static readonly float[] Linear = BuildLinear();
    private static readonly byte[] Gamma = BuildGamma();
    public readonly struct Paint
    {
        public readonly HamletLayout.Point Tint, UvScale, UvOffset;
        public readonly float Brightness, Saturation;
        public Paint(float r, float g, float b, float brightness, float saturation,
            float uScale = 1, float vScale = 1, float uOffset = 0, float vOffset = 0)
        { Tint = new(r,g,b); Brightness = brightness; Saturation = saturation; UvScale = new(uScale,vScale,0); UvOffset = new(uOffset,vOffset,0); }
    }
    // Exposed hills use fractured native rock; dressed masonry is reserved for built retaining courses.
    public static readonly Paint Soil = new(.72f,.73f,.66f,.009f,.65f,vScale:.30f,vOffset:.70f);
    public static readonly Paint Rock = new(1.70f,1.85f,2.05f,.008f,.45f,
        uScale:960f/1024,vScale:760f/1024,uOffset:32f/1024,vOffset:144f/1024);
    public const float RockTexelAspect=760f/960;
    public const float RockPeriod=16;
    public static readonly Paint Rubble = new(1.25f,1.35f,1.45f,.022f,.45f);
    public static readonly Paint Paving = new(.74f,.76f,.70f,.001f,.68f,uScale:.64f,vScale:.64f,uOffset:.18f,vOffset:.18f);
    public static readonly Paint Masonry = new(1.12f,1.2045f,1.178f,.003f,.45f);
    public static readonly Paint Mountain = new(.075f,.105f,.16f,.002f,.46f);
    public static readonly Paint Woodland = new(.54f,.70f,.86f,.004f,.38f);
    public static readonly Paint DistantWoodland = new(.40f,.60f,.82f,.009f,.24f);
    public static readonly Paint Wildwood = new(.62f,.66f,.60f,.003f,.24f);
    public static readonly Paint Building = new(.94f,.96f,1,.001f,.76f);
    public static readonly Paint Grass = new(.48f,.64f,.38f,.002f,.72f);
    public static readonly Paint GrassTuft = new(.31f,.44f,.30f,0,.7f);

    public static (int Width,int Height) PaintExtent(int width,int height,int maximum,Paint paint,bool crop)
    {
        if (width<1 || height<1 || maximum<1) throw new System.ArgumentOutOfRangeException(nameof(width));
        float w=width*(crop?paint.UvScale.X:1),h=height*(crop?paint.UvScale.Y:1);
        float scale=System.Math.Min(1,maximum/System.Math.Max(w,h));
        return (System.Math.Max(1,(int)System.Math.Round(w*scale)),System.Math.Max(1,(int)System.Math.Round(h*scale)));
    }

    public static HamletLayout.Point BuildingTint(HamletLayout.Point basis,HamletLayout.Point emission,bool paintedEmission)
    {
        if (paintedEmission)
        {
            // Blender used emission to preview native painted scenery. It is albedo here, not a light source.
            float peak=System.Math.Max(.001f,System.Math.Max(emission.X,System.Math.Max(emission.Y,emission.Z)));
            return new(emission.X/peak*.9f,emission.Y/peak*.9f,emission.Z/peak*.9f);
        }
        bool light=emission.X>basis.X*1.1f || emission.Y>basis.Y*1.1f || emission.Z>basis.Z*1.1f;
        return light ? new(basis.X*.4f+emission.X,basis.Y*.4f+emission.Y,basis.Z*.4f+emission.Z)
            : new(basis.X*.88f,basis.Y*.88f,basis.Z*.88f);
    }

    // Bake lifted blacks into owned paint. Native Environment shader variants do not reliably lift them.
    public static int GradeRgb(byte red,byte green,byte blue,Paint paint)
    {
        float r=Linear[red]*paint.Tint.X,g=Linear[green]*paint.Tint.Y,b=Linear[blue]*paint.Tint.Z;
        float luminance=.2126f*r+.7152f*g+.0722f*b;
        byte Encode(float value)=>Gamma[(int)(System.Math.Max(0,System.Math.Min(1,value))*4095+.5f)];
        return Encode(luminance+(r-luminance)*paint.Saturation+paint.Brightness)<<16
            | Encode(luminance+(g-luminance)*paint.Saturation+paint.Brightness)<<8
            | Encode(luminance+(b-luminance)*paint.Saturation+paint.Brightness);
    }

    /// <summary>Grade an exclusively owned RGBA readback on a worker, preserving every alpha byte.</summary>
    public static void GradeRgba(byte[] pixels, Paint paint, CancellationToken cancellation = default)
    {
        if (pixels == null || pixels.Length % 4 != 0)
            throw new ArgumentException("Paint grading requires complete RGBA pixels.", nameof(pixels));
        cancellation.ThrowIfCancellationRequested();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
            int rgb = GradeRgb(pixels[i], pixels[i + 1], pixels[i + 2], paint);
            pixels[i] = (byte)(rgb >> 16); pixels[i + 1] = (byte)(rgb >> 8); pixels[i + 2] = (byte)rgb;
        }
    }

    private static float[] BuildLinear()
    {
        var table=new float[256];
        for (int i=0;i<table.Length;i++) { double x=i/255d; table[i]=(float)(x<=.04045?x/12.92:System.Math.Pow((x+.055)/1.055,2.4)); }
        return table;
    }
    private static byte[] BuildGamma()
    {
        var table=new byte[4096];
        for (int i=0;i<table.Length;i++) { double x=i/4095d; table[i]=(byte)System.Math.Round(255*(x<=.0031308?x*12.92:1.055*System.Math.Pow(x,1/2.4)-.055)); }
        return table;
    }
}
