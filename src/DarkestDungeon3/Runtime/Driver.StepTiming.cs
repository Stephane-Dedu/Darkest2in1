using DarkestDungeon3.Core.Dungeon;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    private float _stepSeconds=CorridorSceneMotion.StepSeconds;
    private float SecondsPerSquare => _stepSeconds;
    internal float StepAnimationSeconds => _stepSeconds;
    internal float StepSlide => CorridorSceneMotion.Slide(Time.unscaledTime-LastStepTime,LastStepDir,WalkProgress,_stepSeconds);

    private void CaptureStepDuration(int direction) => _stepSeconds=
        CorridorSceneMotion.StepDuration(WalkingSquarePixels,WalkingPixelsPerSecond,direction>0);
}
