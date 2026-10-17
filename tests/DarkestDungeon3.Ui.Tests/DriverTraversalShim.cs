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
    private bool _wasInRoom;
    private float _fadeInFrom = -10;
    public float RoomTransitionAt => _fadeInFrom;
    public bool TickDoorTransition() => UpdateDoorTransition();
    private const float SecondsPerSquare = CorridorSceneMotion.StepSeconds;
    public List<CrawlEvent> TraversalEvents { get; private set; }
    private void Handle(List<CrawlEvent> events) => TraversalEvents = events;
}
