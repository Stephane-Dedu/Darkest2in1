using System;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Hold the supporting heel or toe against the moving floor while the ankle rolls.</summary>
public sealed class HumanoidFootContact
{
    private bool _moving,_planted;
    private float _forward,_offset,_swingStart,_progress,_length,_toeOffset;
    private int _direction;

    public void Reset() { _moving=_planted=false;_forward=_offset=_swingStart=_progress=_toeOffset=0;_direction=0; }

    public float Forward(HumanoidWalk.Foot foot,float toeOffset,float observed,float distance,float deltaPhase)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || Math.Abs(distance)<.000001f)
        { Reset();return foot.Position.Z; }
        int direction=distance>0 ? 1 : -1;
        float nominal=foot.Position.Z;
        if (foot.Planted)
        {
            float advanced=_progress+deltaPhase/Math.Max(.000001f,_length);
            bool same=_moving && _planted && advanced>=0 && advanced<1;
            float roll=foot.ToePivot ? toeOffset-_toeOffset : 0;
            _forward=!_moving ? observed-distance : same ? _forward-distance+roll : nominal;
            _offset=_forward-nominal;_swingStart=0;
        }
        else
        {
            float travel=direction>0 ? foot.SwingProgress : 1-foot.SwingProgress;
            if (!_moving) { _offset=observed-nominal;_swingStart=travel; }
            else if (!_planted && direction!=_direction) { _offset=_forward-nominal;_swingStart=travel; }
            else if (Math.Abs(deltaPhase)>=1-foot.ContactLength) { _offset=_swingStart=0; }
            float u=Math.Min(1,Math.Max(0,(travel-_swingStart)/Math.Max(.000001f,1-_swingStart)));
            _forward=nominal+_offset*(1-u*u*(3-2*u));
        }
        _moving=true;_planted=foot.Planted;_direction=direction;_progress=foot.ContactProgress;
        _length=foot.ContactLength;_toeOffset=toeOffset;
        return _forward;
    }
}
