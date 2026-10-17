using System;

namespace DarkestDungeon3.Core.Dungeon;

public static class CorridorDoorLayout
{
    public static float X(int width,int height,int count,int tile,bool reverse,bool ahead,float slide)
    {
        int here=reverse ? count-1-tile : tile;
        return (ahead ? 1680 : 240)+CorridorSceneMotion.PropOffset(width,height,count,ahead ? count-1-here : -here,slide);
    }

    /// <summary>Feather the installed DD1 doorway into an outdoor painting instead of overlaying its square wall tile.</summary>
    public static float Alpha(float u,float v)
    {
        float arch=Math.Max(0,(v-.40f)/.48f);
        float halfWidth=.36f*(float)Math.Sqrt(Math.Max(0,1-arch*arch));
        return Smooth((halfWidth-Math.Abs(u-.5f))/.035f)*Smooth((v-.015f)/.055f);
    }
    private static float Smooth(float x) { x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x); }
}
