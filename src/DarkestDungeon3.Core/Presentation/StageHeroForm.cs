using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>
/// Which art mode a travel (corridor, room, camp) hero model must show. DD2 returns a transformed hero to its default
/// mode at battle end (ActorInstance.OnBattleEnd sets the data mode directly), but a model only switches its art
/// when the mode-change event fires. The stage's own model therefore follows the hero's data mode outside fights.
/// </summary>
public static class StageHeroForm
{
    /// <summary>The art mode to switch to, or null to leave the model as drawn.</summary>
    /// <param name="inFight">A fight is on: DD2's own mode events and timelines own the art.</param>
    /// <param name="timelinePlaying">A skill timeline still poses the model (DD2 defers mode changes the same way).</param>
    /// <param name="drawnMode">The art mode the model shows now, or null before it has loaded.</param>
    /// <param name="dataMode">The hero's data mode, or null for a class without modes.</param>
    /// <param name="artModes">The art modes the model carries.</param>
    public static string Resync(bool inFight, bool timelinePlaying, string drawnMode, string dataMode,
        IReadOnlyCollection<string> artModes)
    {
        if (inFight || timelinePlaying || string.IsNullOrEmpty(drawnMode) || string.IsNullOrEmpty(dataMode)
            || drawnMode == dataMode || artModes == null || artModes.Count < 2)
            return null;
        foreach (string mode in artModes)
            if (mode == dataMode) return dataMode;
        return null;
    }
}
