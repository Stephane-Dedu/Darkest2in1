using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class StageHeroFormTests
{
    private static readonly string[] Abomination = { "abm_mode_human", "abm_mode_beast" };

    [Fact]
    public void BeastArtReturnsToHumanOnceTheFightHasResetTheDataMode()
    {
        // In the fight the transform event switched the stage model too; DD2 owns the art until the fight ends.
        Assert.Null(StageHeroForm.Resync(true, false, "abm_mode_beast", "abm_mode_human", Abomination));
        // Battle end reset the data mode without an event: travel must show the human again.
        Assert.Equal("abm_mode_human", StageHeroForm.Resync(false, false, "abm_mode_beast", "abm_mode_human", Abomination));
        // A finishing skill timeline still holds the pose; DD2 defers its own mode change the same way.
        Assert.Null(StageHeroForm.Resync(false, true, "abm_mode_beast", "abm_mode_human", Abomination));
    }

    [Fact]
    public void MatchingOrUnknownFormsLeaveTheModelAlone()
    {
        Assert.Null(StageHeroForm.Resync(false, false, "abm_mode_human", "abm_mode_human", Abomination));
        Assert.Null(StageHeroForm.Resync(false, false, null, "abm_mode_human", Abomination));
        Assert.Null(StageHeroForm.Resync(false, false, "abm_mode_beast", null, Abomination));
        Assert.Null(StageHeroForm.Resync(false, false, "abm_mode_beast", "abm_mode_other", Abomination));
        // Classes with one art root (every other hero) never switch.
        Assert.Null(StageHeroForm.Resync(false, false, "default", "crusader_mode", new[] { "default" }));
        Assert.Null(StageHeroForm.Resync(false, false, "abm_mode_beast", "abm_mode_human", null));
    }
}
