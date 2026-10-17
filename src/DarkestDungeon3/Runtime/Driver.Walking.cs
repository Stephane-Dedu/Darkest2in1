using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    // Shared by held keys, held mouse and map routes. Input selection stays in Walk.
    private void AdvanceWalk(bool forward, float deltaTime)
    {
        if (forward && Crawl.IsBlocked)   // an obstacle or a fight in this square: no further than its middle
        {
            StopWalking();
            return;
        }
        var hall=Crawl.CurrentCorridor;
        int outwardRoom=forward ? Expedition.HeadingRoomId : hall.Other(Expedition.HeadingRoomId);
        bool atDoor=Crawl.CanEnterDoor(outwardRoom);
        if (atDoor && (forward ? WalkProgress>=.5f : WalkProgress<=-.5f)) { StopWalking();return; }
        float movement = Core.Dungeon.CorridorSceneMotion.WalkingDelta(deltaTime, WalkingSquarePixels, WalkingPixelsPerSecond, forward);
        _walkVelocity = deltaTime > 0 ? movement / deltaTime : 0;
        if (movement == 0) return;
        if (Time.unscaledTime >= _nextFootstep) { Dd1Audio.Play("/general/party/hero_step"); _nextFootstep = Time.unscaledTime + (forward ? 0.55f : 0.8f); }
        float beforeProgress = WalkProgress;
        WalkProgress += movement;
        if (Mathf.Abs(WalkProgress) < 0.5f) return;
        if (atDoor)
        {
            WalkProgress=forward ? .5f : -.5f;
            _walkVelocity=0;StopWalking();return;
        }
        float carry = WalkProgress - Mathf.Sign(WalkProgress);   // -0.5 + overshoot in the next square
        _continuousStep = true;
        bool moved;
        try { moved = TryStep(forward); }
        finally { _continuousStep = false; }
        if (Expedition == null) { WalkProgress = 0; StopWalking(); return; }
        if (!moved)
        {
            WalkProgress = beforeProgress; _walkVelocity = 0;
            StopWalking(); return;
        }
        WalkProgress = Expedition.InRoom ? 0 : carry;
        if (Expedition.InRoom) Dd1Audio.Play("/general/map/room_transition");
        // Something happened (a fight, a curio, a trap, a room): stop and let the player look.
        if (_interruptsThisStep || (IsWalking && Crawl.CurioHere != null)) { _interruptsThisStep = false; StopWalking(); }
        if (_routeTargetTile.corridor >= 0 && !Expedition.InRoom && Expedition.TileIndex == _routeTargetTile.tile) StopWalking();
    }
}
