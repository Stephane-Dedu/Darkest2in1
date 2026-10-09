using System;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1SingleSummonTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly EffectLibrary Effects = EffectLibrary.Load(Install);

    [Theory]
    [InlineData('A', "NecroSummon 1", 2)]
    [InlineData('B', "NecroSummon 2", 3)]
    [InlineData('C', "NecroSummon 3", 3)]
    public void EveryInstalledNecromancerAttackHasOneSupportedSummon(char tier, string effectName, int candidates)
    {
        var skills = Dd1MonsterSkills.Read(Install, "necromancer", tier);
        Assert.Equal(3, skills.Count);
        foreach (var skill in skills)
        {
            Assert.Contains(effectName, skill.Effects);
            var effect = Effects.Get(effectName);
            Assert.True(Dd1SingleSummon.TryRead(effect, out var summon, out var reason), reason);
            Assert.Equal(candidates, summon.Monsters.Count);
            Assert.False(summon.CanSpawnLoot);
            Assert.All(summon.Monsters, id => Assert.EndsWith("_" + tier, id));
            Assert.True(summon.TryPlan(4, Size, () => 0, out var choice));
            Assert.Equal(1, choice.Rank);
            Assert.False(choice.CanSpawnLoot);
            Assert.Equal(summon.Monsters[0], choice.Monster);
        }
    }

    private static int Size(string id)
    {
        int split = id.LastIndexOf('_');
        return Dd1MonsterSkills.Size(Install, id.Substring(0, split), id[split + 1]);
    }

    private static Dd1SingleSummon Read(string extra = "", string weights = "1 3", string count = "1")
    {
        var effect = Synthetic(extra, weights, count);
        Assert.True(Dd1SingleSummon.TryRead(effect, out var summon, out var reason), reason);
        return summon;
    }

    private static DarkestRecord Synthetic(string extra = "", string weights = "1 3", string count = "1") =>
        DarkestFile.Parse($"effect: .name test .target performer .chance 100% .summon_count {count} "
            + $".summon_can_spawn_loot false .summon_monsters small large .summon_chances {weights} "
            + ".on_hit true .on_miss true .apply_once true " + extra).Single();

    [Theory]
    [InlineData(0, "small")]
    [InlineData(0.249999, "small")]
    [InlineData(0.25, "large")]
    [InlineData(0.999999, "large")]
    public void WeightedSelectionUsesNativeCumulativeBoundary(double roll, string expected)
    {
        Assert.True(Read().TryPlan(4, _ => 1, () => roll, out var choice));
        Assert.Equal(expected, choice.Monster);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0.5)]
    public void NoSpaceSpendsOneRollAndNeverRerollsASmallerCandidate(int room, double value)
    {
        int rolls = 0;
        Assert.False(Read().TryPlan(room, id => id == "small" ? 1 : 2, () => { rolls++; return value; }, out var choice));
        Assert.Null(choice);
        Assert.Equal(1, rolls);
    }

    [Fact]
    public void EveryAttemptStartsWithTheCompletePool()
    {
        var summon = Read();
        Assert.False(summon.TryPlan(0, _ => 1, () => 0.9, out _));
        Assert.True(summon.TryPlan(1, _ => 1, () => 0.9, out var choice));
        Assert.Equal("large", choice.Monster);
    }

    [Fact]
    public void MissingCoverageCannotConsumeRngOrPartiallyUseThePool()
    {
        Assert.False(Read().TryPlan(4, id => id == "large" ? 0 : 1,
            () => throw new Exception("Incomplete adapter consumed RNG"), out _));
    }

    [Theory]
    [InlineData(".summon_limits 1 1")]
    [InlineData(".summon_ranks 2 3")]
    [InlineData(".summon_does_roll_initiatives true")]
    [InlineData(".summon_erase_data_on_roll true")]
    [InlineData(".summon_rank_is_previous_monster_class true")]
    [InlineData(".capture true")]
    [InlineData(".dotBleed 2")]
    public void UnsupportedMechanicsRejectInsteadOfPretendingToTranslate(string extra)
    {
        Assert.False(Dd1SingleSummon.TryRead(Synthetic(extra), out var summon, out var reason));
        Assert.Null(summon);
        Assert.StartsWith("Unsupported summon fields", reason);
    }

    [Theory]
    [InlineData("1", "2")]
    [InlineData("1", "1 3 4")]
    [InlineData("1", "1 0")]
    [InlineData("1", "1 -1")]
    [InlineData("1", "1 NaN")]
    [InlineData("1", "1 Infinity")]
    [InlineData("1", "1e308 1e308")]
    [InlineData("2", "1 3")]
    public void InvalidOrMultipleAttemptsReject(string count, string weights)
    {
        Assert.False(Dd1SingleSummon.TryRead(Synthetic(weights: weights, count: count), out _, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData(".on_miss false")]
    [InlineData(".on_hit false")]
    [InlineData(".apply_once false")]
    [InlineData(".chance 50%")]
    public void ConflictingOrConditionalFlagsReject(string extra) =>
        Assert.False(Dd1SingleSummon.TryRead(Synthetic(extra), out _, out _));
}
