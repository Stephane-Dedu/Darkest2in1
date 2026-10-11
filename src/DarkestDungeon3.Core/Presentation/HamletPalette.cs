namespace DarkestDungeon3.Core.Presentation;

/// <summary>Material grading shared by the native town and its private composition preview.</summary>
public static class HamletPalette
{
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
    public static readonly Paint Rock = new(1.35f,1.48f,1.6f,.028f,.4f,vScale:.24f,vOffset:.76f);
    public static readonly Paint Rubble = new(1.25f,1.35f,1.45f,.022f,.45f);
    public static readonly Paint Paving = new(.95f,.91f,.79f,.018f,.65f);
    public static readonly Paint Masonry = new(.78f,.78f,.74f,.035f,.3f);
    public static readonly Paint Mountain = new(1.2f,1.4f,1.65f,.025f,.5f);
}
