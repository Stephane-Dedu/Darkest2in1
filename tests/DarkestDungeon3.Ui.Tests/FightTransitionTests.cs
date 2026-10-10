using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class FightTransitionTests
{
    [Fact]
    public void TheSceneStaysWholeUntilHeroesHaveSettled()
    {
        // While DD2 loads and while the heroes glide, nothing of the fight shows through.
        Assert.Equal(1f, FightTransition.CoverAtStart(-0.6f));
        Assert.Equal(1f, FightTransition.CoverAtStart(0f));
        Assert.Equal(1f, FightTransition.CoverAtStart(FightTransition.Move - 0.01f));
        Assert.Equal(1f, FightTransition.MoveAtStart(FightTransition.Move), 4);
        Assert.Equal(0f, FightTransition.CoverAtStart(FightTransition.Move + FightTransition.Fade), 4);
    }

    [Fact]
    public void NothingMovesBeforeDd2IsReady()
    {
        Assert.Equal(0f, FightTransition.MoveAtStart(-0.4f));
        Assert.Equal(0f, FightTransition.MoveAtStart(0f));
        Assert.True(FightTransition.MoveAtStart(FightTransition.Move / 2) is > 0.4f and < 0.6f);
    }

    [Fact]
    public void HeroesTurnWhileDd2Loads()
    {
        Assert.Equal(0f, FightTransition.TurnAtStart(0f));
        Assert.Equal(1f, FightTransition.TurnAtStart(FightTransition.Turn), 4);
        Assert.Equal(1f, FightTransition.TurnAtStart(5f));
    }

    [Fact]
    public void TheReturnCoversTheFightBeforeAnythingMovesBack()
    {
        Assert.Equal(0f, FightTransition.CoverAtReturn(0f));
        Assert.Equal(1f, FightTransition.MoveAtReturn(FightTransition.ReturnFade / 2));   // still in the fight's layout
        Assert.Equal(1f, FightTransition.CoverAtReturn(FightTransition.ReturnFade), 4);
        Assert.Equal(0f, FightTransition.MoveAtReturn(FightTransition.ReturnFade + FightTransition.ReturnMove), 4);
        Assert.False(FightTransition.ReturnDone(FightTransition.ReturnFade + FightTransition.ReturnMove - 0.01f));
        Assert.True(FightTransition.ReturnDone(FightTransition.ReturnFade + FightTransition.ReturnMove));
    }

    [Fact]
    public void AHeroAlreadyInPlaceStaysPut()
    {
        var (x, feet, grow) = FightTransition.Land(788, 680, 790, 652, 330, 790, 652, 330);
        Assert.Equal(788f, x, 3);
        Assert.Equal(680f, feet, 3);
        Assert.Equal(1f, grow, 3);
    }

    [Fact]
    public void AHeroLandsOnDd2sBones()
    {
        // Stage: slot at x 620 on the 680 feet line; pelvis 6 px to its right, ankle 28 px above it, 330 px tall.
        // DD2's fight: pelvis at 480, lower ankle at 690, 400 px tall (bigger: the hero must grow, never shrink).
        var (x, feet, grow) = FightTransition.Land(620, 680, 626, 652, 330, 480, 690, 400);
        Assert.True(grow > 1f);
        // Scaling around the slot origin by `grow` puts each measured bone exactly on DD2's.
        Assert.Equal(480f, x + grow * (626 - 620), 3);
        Assert.Equal(690f, feet + grow * (652 - 680), 3);
        Assert.Equal(400f, grow * 330, 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(0.2f)]
    [InlineData(0.31f)]
    [InlineData(0.44f)]
    public void EveryStepOnlyGoesOneWay(float t)
    {
        const float dt = 0.01f;
        Assert.True(FightTransition.MoveAtStart(t + dt) >= FightTransition.MoveAtStart(t));
        Assert.True(FightTransition.CoverAtStart(t + dt) <= FightTransition.CoverAtStart(t));
        Assert.True(FightTransition.CoverAtReturn(t + dt) >= FightTransition.CoverAtReturn(t));
        Assert.True(FightTransition.MoveAtReturn(t + dt) <= FightTransition.MoveAtReturn(t));
    }
}
