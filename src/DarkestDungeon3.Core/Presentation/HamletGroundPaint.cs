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
            Math.Min(.38f,.08f+.42f*Math.Max(0,patch+.25f)*Math.Max(0,.5f+broad)));
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
            float t=Math.Max(0,Math.Min(1,distance/2.1f));
            contact=Math.Max(contact,.24f*(1-t*t*(3-2*t)));
        }
        return 1-Math.Max(contact,.22f*Math.Max(0,Math.Min(1,(rise-.5f)/5)));
    }

    public static float EdgeWear(float x,float z)
    {
        float t=Math.Max(0,Math.Min(1,HamletVegetation.Clearance(x,z)/3));
        return .42f*(1-t*t*(3-2*t));
    }

    /// <summary>Continuous worn road margins shared by branches and the forecourt.</summary>
    public static float LaneCover(float x,float z)
    {
        float edge=100;
        foreach (var route in HamletLayout.Routes) for (int i=1;i<route.Points.Length;i++)
        {
            var a=route.Points[i-1]; var b=route.Points[i];
            float dx=b.X-a.X,dz=b.Z-a.Z,length=dx*dx+dz*dz;
            if (Math.Abs(b.Y-a.Y)>=.45f && Math.Abs(b.Y-a.Y)/Math.Sqrt(length)>.18f) continue;
            float t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(z-a.Z)*dz)/length));
            float rx=x-a.X-dx*t,rz=z-a.Z-dz*t;
            edge=Math.Min(edge,(float)Math.Sqrt(rx*rx+rz*rz)-route.Width/2);
        }
        float square=(float)Math.Sqrt(x*x/(13*13)+(z+3)*(z+3)/(7*7));
        edge=Math.Min(edge,(square-1)*7);
        float chip=.13f*(float)Math.Sin(x*2.7f+z*.83f)+.11f*(float)Math.Sin(z*3.3f-x*.73f);
        float fade=Math.Max(0,Math.Min(1,(-edge-.03f+chip)/.85f));
        return fade*fade*(3-2*fade);
    }
}
