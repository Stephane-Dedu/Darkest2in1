using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1EnemyKitTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Theory]
    [InlineData('A')]
    [InlineData('B')]
    [InlineData('C')]
    public void ExactNecromancerTierRetainsEveryAttackAndNativeNumbers(char tier)
    {
        var kit = Dd1EnemyKit.Read(Install, "necromancer", tier);
        Assert.NotNull(kit);
        Assert.True(kit.Boss);
        Assert.Equal(1, kit.Size);
        Assert.Equal(1, kit.Turns);
        Assert.Equal(new[] { "unholy_smite", "unholy_judgement", "unholy_curse" }, kit.Skills.Select(s => s.Id));
        var native = DarkestFile.Load(Install.PathOf("monsters", "necromancer", kit.Id, kit.Id + ".info.darkest")).Single(r => r.Type == "stats");
        var stats = Dd1SkillToDd2.Stats(kit.ActorStatsText());
        Assert.Equal(native.Int("hp"), stats["health_max"]);
        Assert.Equal(native.Int("spd"), stats["speed"]);
        Assert.Equal(1, stats["speed_number_of_turns"]);
        Assert.Equal(native.Float("poison_resist"), kit.Resists["blight"]);
        Assert.Equal(native.Float("stun_resist"), kit.Resists["stun"]);
        Assert.Contains("m_EquippedCombatSkillLimit,3,", kit.ActorClassText());
        Assert.DoesNotContain("Loot", kit.ActorClassText());
    }

    [Theory]
    [InlineData("skeleton_common", 'A')]
    [InlineData("skeleton_militia", 'A')]
    [InlineData("skeleton_defender", 'B')]
    [InlineData("skeleton_captain", 'C')]
    public void SummonCandidatesUseTheirOwnTierSkillsAndStats(string family, char tier)
    {
        var kit = Dd1EnemyKit.Read(Install, family, tier);
        Assert.NotNull(kit);
        Assert.False(kit.Boss);
        Assert.NotEmpty(kit.Skills);
        Assert.Contains("m_IsStallCounted,True,", kit.ActorClassText());
        Assert.DoesNotContain("spawn_effects", kit.ActorClassText());
    }

    [Theory]
    [InlineData("necromancer", 'D')]
    [InlineData("../necromancer", 'A')]
    [InlineData("absent_enemy", 'A')]
    public void MissingTierNeverBorrowsAnotherTier(string family, char tier) =>
        Assert.Null(Dd1EnemyKit.Read(Install, family, tier));

    [Fact]
    public void MilitiaProtectionAndNecromancerLifeLinkAreNotLost()
    {
        var kit = Dd1EnemyKit.Read(Install, "skeleton_militia", 'A');
        Assert.Equal("necromancer", kit.LifeLinkBaseClass);
        Assert.Equal(0.15f, kit.Protection);
        Assert.Equal(-0.15f, Dd1SkillToDd2.Stats(kit.ActorStatsText())["health_damage_received_percent"]);
        Assert.Equal("necromancer", Dd1EnemyKit.Read(Install, "skeleton_common", 'A').LifeLinkBaseClass);
    }
}
