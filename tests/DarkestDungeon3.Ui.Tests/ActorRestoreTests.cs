using Assets.Code.Actor;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ActorRestoreTests
{
    [Theory]
    [InlineData(-2f, true)]
    [InlineData(0f, true)]
    [InlineData(7.5f, false)]
    [InlineData(40f, false)]
    public void ActualBridgeRestoresRawConditionWithoutReplayingEventsOrReplacingIdentity(float hp, bool deathsDoor)
    {
        var actor = new ActorInstance();
        var inventory = actor.Inventory; var quirks = actor.Quirks; var buffs = actor.Buffs;
        var saved = Saved(); saved.Hp = hp;
        for (int load = 0; load < 2; load++)
        {
            actor.Calls.Clear();
            Assert.True(ExpeditionActorRestore.Apply(actor, saved));
            Assert.Equal(Math.Min(hp, 24), actor.HpRaw);
            Assert.Equal(10, actor.Stress); Assert.Equal(.2f, actor.WoundPercent);
            Assert.Equal(24, actor.PreviousHpMax); Assert.Equal(deathsDoor, actor.DeathsDoor);
            Assert.True(actor.LoadedStatus); Assert.Equal(0, actor.ConditionEvents);
            Assert.Equal(new[] { "serialize", "clamp", "status", "previous" }, actor.Calls);
            Assert.Equal(123u, actor.ActorGuid);
            Assert.Same(inventory, actor.Inventory); Assert.Same(quirks, actor.Quirks); Assert.Same(buffs, actor.Buffs);
        }
        Assert.Equal(hp, saved.Hp); Assert.Equal(30, saved.HpMax);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("missing")]
    [InlineData("invalid")]
    public void RejectedStateDoesNotTouchTheNativeActor(string reason)
    {
        var actor = new ActorInstance(); var saved = Saved();
        if (reason == "dead") saved.Outcome.Died = true;
        else if (reason == "missing") saved = null;
        else saved.Hp = float.NaN;
        Assert.False(ExpeditionActorRestore.Apply(actor, saved));
        Assert.Equal(30, actor.HpRaw); Assert.Equal(0, actor.Stress); Assert.Equal(0, actor.WoundPercent);
        Assert.Empty(actor.Calls);
        Assert.False(ExpeditionActorRestore.Apply(null, Saved()));
    }

    private static ExpeditionHeroState Saved() => new()
    {
        Hp = 7.5f, HpMax = 30, Stress = 10, WoundPercent = .2f,
        Outcome = new HeroOutcome { HeroId = "a" }
    };
}
