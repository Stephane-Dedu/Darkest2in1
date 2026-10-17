using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    public void Travel(int roomId)
    {
        if (!Crawl.CanNavigate) return;
        // A different hallway arrives through the room fade. It has no previous
        // square to animate from, and must not add a second camera sweep.
        var before = (Expedition.RoomId, Expedition.CorridorId, Expedition.TileIndex);
        var events = Crawl.Travel(roomId);
        if (before != (Expedition.RoomId, Expedition.CorridorId, Expedition.TileIndex))
        {
            LastStepTime = -10f; LastStepDir = 0;
            WalkProgress = 0;
            _wasInRoom = Expedition.InRoom; _fadeInFrom = Time.unscaledTime;
        }
        Handle(events);
    }

    public void Step(bool forward) => TryStep(forward);

    private bool TryStep(bool forward)
    {
        if (!Crawl.CanNavigate) return false;
        // A second button step would reset the easing from a new square and
        // teleport the ground underneath the feet. Held movement owns its carry.
        if (!_continuousStep && Time.unscaledTime-LastStepTime < SecondsPerSquare) return false;
        if (Crawl.IsBlocked && !forward && Crawl.CurrentTile?.Content == Core.Dungeon.HallContent.Battle) return false;
        var before = (Expedition.RoomId, Expedition.CorridorId, Expedition.TileIndex);
        var events = Crawl.Step(forward);
        bool moved = before != (Expedition.RoomId, Expedition.CorridorId, Expedition.TileIndex);
        if (moved)
        {
            if (Expedition.InRoom)
            {
                LastStepTime = -10f; LastStepDir = 0;
                WalkProgress = 0;
                _wasInRoom = true; _fadeInFrom = Time.unscaledTime;
            }
            else MarkStep(forward ? +1 : -1);
        }
        Handle(events);
        return moved;
    }

    private void MarkStep(int dir)
    {
        LastStepDir = dir;
        // Held-key walking scrolls continuously (WalkProgress); only discrete steps get the slide animation.
        LastStepTime = _continuousStep ? -10f : Time.unscaledTime;
    }

}
