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

    public static float ContactShade(float x,float z)
    {
        float height=HamletLayout.Height(x,z),rise=0,contact=0;
        foreach (var d in new[] { (-2.5f,0f),(2.5f,0f),(0f,-2.5f),(0f,2.5f) })
            rise=Math.Max(rise,HamletLayout.Height(x+d.Item1,z+d.Item2)-height);
        foreach (var site in HamletLayout.Sites)
        {
            float a=site.Yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            float dx=x-site.X,dz=z-site.Z;
            float distance=Math.Max(Math.Abs(dx*c-dz*s)-site.FootprintWidth/2,Math.Abs(dx*s+dz*c)-site.FootprintDepth/2);
            float t=Math.Max(0,Math.Min(1,distance/2.5f));
            contact=Math.Max(contact,.16f*(1-t*t*(3-2*t)));
        }
        return 1-Math.Max(contact,.22f*Math.Max(0,Math.Min(1,(rise-.5f)/5)));
    }

    public static float EdgeWear(float x,float z)
    {
        float t=Math.Max(0,Math.Min(1,HamletVegetation.Clearance(x,z)/3));
        return .42f*(1-t*t*(3-2*t));
    }
}
