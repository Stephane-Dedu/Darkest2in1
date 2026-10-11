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
    // Sample only painted soil and rock within the native atlases, excluding grass fringe/empty black areas.
    public static readonly Paint Soil = new(.58f,.68f,.84f,.012f,.45f,vScale:.30f,vOffset:.70f);
    public static readonly Paint Rock = new(1.35f,1.48f,1.6f,.028f,.4f,vScale:.46f,vOffset:.54f);
    public static readonly Paint Rubble = new(1.25f,1.35f,1.45f,.022f,.45f);
    public static readonly Paint Paving = new(1.12f,1.06f,.90f,.026f,.65f);
    public static readonly Paint Masonry = new(.78f,.78f,.74f,.035f,.3f);
    public static readonly Paint Mountain = new(1.2f,1.4f,1.65f,.025f,.5f);

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
