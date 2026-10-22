using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Bounded rock relief outside grounded building pads, routes and the terrain boundary.</summary>
public static class HamletCliffForm
{
    public static HamletLayout.Point At(float x, float z)
    {
        float y = HamletLayout.Height(x, z) - .12f;
        float boundary = Math.Min(Math.Min(x-HamletLayout.MinX, HamletLayout.MaxX-x),
            Math.Min(z-HamletLayout.MinZ, HamletLayout.MaxZ-z));
        if (boundary <= 0) return new(x,y,z);
        float clearance = HamletVegetation.Clearance(x,z);
        if (clearance <= 1) return new(x,y,z);
        const float step = .65f;
        float dx = (HamletLayout.Height(x+step,z)-HamletLayout.Height(x-step,z))/(2*step);
        float dz = (HamletLayout.Height(x,z+step)-HamletLayout.Height(x,z-step))/(2*step);
        float slope = (float)Math.Sqrt(dx*dx+dz*dz);
        if (slope <= 1.1f) return new(x,y,z);
        float weight = Smooth(1.1f,2.5f,slope)*Smooth(1,2.2f,clearance)*Smooth(0,1.8f,boundary);
        float u = (x+z*.57f)/3.8f, v = y/2.7f;
        float broad = Noise(u,v), chip = Noise(u*2.35f+7.1f,v*2.8f-3.4f);
        float relief = weight*(.18f+.40f*broad+.12f*chip);
        // Move outward from the hillside, retaining the upper walking surfaces.
        return new(x-dx/slope*relief, y+weight*(chip-.5f)*.36f, z-dz/slope*relief);
    }

    private static float Smooth(float a,float b,float value)
    { float t=Math.Max(0,Math.Min(1,(value-a)/(b-a))); return t*t*(3-2*t); }

    private static float Noise(float x,float y)
    {
        int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
        float fx=x-ix,fy=y-iy;
        // Linear cells preserve broad angular planes rather than rounded bumps.
        float a=Hash(ix,iy)*(1-fx)+Hash(ix+1,iy)*fx;
        float b=Hash(ix,iy+1)*(1-fx)+Hash(ix+1,iy+1)*fx;
        return a*(1-fy)+b*fy;
    }

    private static float Hash(int x,int y)
    {
        unchecked
        {
            uint h=(uint)x*374761393u+(uint)y*668265263u+2246822519u;
            h=(h^(h>>13))*1274126177u; h^=h>>16;
            return (h&65535)/65535f;
        }
    }
}
