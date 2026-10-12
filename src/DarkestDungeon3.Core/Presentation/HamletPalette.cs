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
    // Crop the painted soil and rough-road patch; wall stone is a seamless native tile.
    public static readonly Paint Soil = new(.72f,.73f,.66f,.009f,.65f,vScale:.30f,vOffset:.70f);
    public static readonly Paint Rock = new(1.4f,1.5f,1.6f,.002f,.55f);
    public static readonly Paint Rubble = new(1.25f,1.35f,1.45f,.022f,.45f);
    public static readonly Paint Paving = new(1.05f,1.02f,.94f,.002f,.75f,uScale:.64f,vScale:.64f,uOffset:.18f,vOffset:.18f);
    public static readonly Paint Masonry = new(1.6f,1.65f,1.55f,.003f,.45f);
    public static readonly Paint Mountain = new(1.2f,1.4f,1.65f,.025f,.5f);
    public static readonly Paint Woodland = new(.66f,.78f,.95f,.003f,.45f);
    public static readonly Paint Building = new(.94f,.96f,1,.001f,.76f);
    public static readonly Paint Grass = new(.48f,.64f,.38f,.002f,.72f);
    public static readonly Paint GrassTuft = new(.48f,.62f,.44f,0,1);

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
