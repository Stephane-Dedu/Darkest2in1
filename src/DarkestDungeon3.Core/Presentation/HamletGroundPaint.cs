using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Broad stains and scattered worn stone, baked once in world space so every town facet agrees.</summary>
public static class HamletGroundPaint
{
    public readonly struct Sample
    {
        public readonly HamletLayout.Point Tint;
        public readonly float Gravel;
        public Sample(float r,float g,float b,float gravel) { Tint=new(r,g,b); Gravel=gravel; }
    }
    public static Sample At(float x,float z)
    {
        float broad=(float)(Math.Sin(x*.087+Math.Sin(z*.073)*1.4)*Math.Cos(z*.11-x*.031));
        float patch=(float)(Math.Sin(x*.31+z*.17)*Math.Cos(z*.27-x*.13));
        float damp=Math.Max(0,broad),dry=Math.Max(0,-broad);
        return new(.84f+.14f*dry-.09f*damp,.86f+.10f*dry-.04f*damp,.78f+.09f*dry-.08f*damp,
            Math.Min(.58f,.12f+.60f*Math.Max(0,patch+.25f)*Math.Max(0,.5f+broad)));
    }
}
