using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    public float LastStepTime { get; private set; } = -10;
    public int LastStepDir { get; private set; }
    public float WalkProgress { get; private set; }
    public void SeedWalkProgress(float progress) => WalkProgress = progress;
    private bool _continuousStep;
    private float _walkVelocity, _nextFootstep;
    private bool _interruptsThisStep;
    public int SceneryWidth = 4096, SceneryHeight = 768;
    public float ProjectedLegPixels = 102.6f;
    private float WalkingPixelsPerSecond => CorridorSceneMotion.WalkingSpeedForLeg(ProjectedLegPixels);
    private float WalkingSquarePixels => CorridorSceneMotion.PropOffset(SceneryWidth, SceneryHeight, Crawl.CurrentCorridor.Tiles.Count, 1, 0);
    public void AdvanceHeldTick(bool forward, float deltaTime) => AdvanceWalk(forward, deltaTime);
    private bool _wasInRoom;
    private float _fadeInFrom = -10;
    public float RoomTransitionAt => _fadeInFrom;
    public bool TickDoorTransition() => UpdateDoorTransition();
    private const float SecondsPerSquare = CorridorSceneMotion.StepSeconds;
    public List<CrawlEvent> TraversalEvents { get; private set; }
    private void Handle(List<CrawlEvent> events) => TraversalEvents = events;
}
