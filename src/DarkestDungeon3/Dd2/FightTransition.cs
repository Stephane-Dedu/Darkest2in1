namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1's battle start where the party stands: the corridor heroes turn to the enemy while DD2 sets the fight up,
/// then heroes and scene glide to where the fight shows them, and only then the fight shows through. A fight's end
/// runs the same steps backwards. Times in seconds; every value is 0..1.
/// </summary>
internal static class FightTransition
{
    public const float Turn = 0.35f;        // heroes face the enemy, from the moment the fight starts
    public const float Move = 0.3f;         // heroes and scene settle into the fight's layout once DD2 is ready
    public const float Fade = 0.15f;        // then the fight shows through
    public const float ReturnFade = 0.15f;  // after the fight: the party's scene comes back over it
    public const float ReturnMove = 0.3f;   // and heroes and scene glide back to the corridor layout

    public static float Smooth(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>How far the heroes have turned to the enemy, <paramref name="sinceRequest"/> after the fight began.</summary>
    public static float TurnAtStart(float sinceRequest) => Smooth(sinceRequest / Turn);

    /// <summary>How far heroes and scene have moved into the fight's layout; negative times: DD2 isn't ready yet.</summary>
    public static float MoveAtStart(float sinceReady) => sinceReady < 0f ? 0f : Smooth(sinceReady / Move);

    /// <summary>The DD1 scene's opacity over the fight: whole until the move is done, then fading out.</summary>
    public static float CoverAtStart(float sinceReady) => sinceReady < Move ? 1f : 1f - Clamp01((sinceReady - Move) / Fade);

    public static float CoverAtReturn(float sinceEnd) => Clamp01(sinceEnd / ReturnFade);

    public static float MoveAtReturn(float sinceEnd) => 1f - Smooth((sinceEnd - ReturnFade) / ReturnMove);

    /// <summary>The scene covers the fight and the corridor layout is back: DD2 may return to the road.</summary>
    public static bool ReturnDone(float sinceEnd) => sinceEnd >= ReturnFade + ReturnMove;

    /// <summary>
    /// Where a stage hero's slot must stand (virtual x, feet line y) and how much it must grow so that its pelvis
    /// (x), lower ankle (y) and ankle-to-head height land on the same hero's in DD2's fight. The slot scales around
    /// its own origin: its x on the feet line.
    /// </summary>
    public static (float X, float Feet, float Grow) Land(float slotX, float slotFeet, float rootX, float ankleY, float height,
        float targetX, float targetAnkle, float targetHeight)
    {
        float grow = targetHeight / height;
        return (targetX - grow * (rootX - slotX), targetAnkle - grow * (ankleY - slotFeet), grow);
    }

    private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
}
