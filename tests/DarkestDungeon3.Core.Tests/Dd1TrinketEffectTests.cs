using System.Linq;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1TrinketEffectTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Trinkets Items = Dd1Trinkets.LoadBase(Install);
    private static readonly Dd1Buffs Buffs = Dd1Buffs.Load(Install);

    [Fact]
    public void PistolKeepsRangedConditionSpeedAndStressPenalty()
    {
        var effects = Items.Get("ancestors_pistol").BuffIds.Select(id => Dd1TrinketEffect.Adapt(Buffs.Get(id))).ToArray();
        Assert.Contains("m_ConditionString,ranged,", effects[0].Condition);
        Assert.Contains("crit_chance", effects[0].Stats);
        Assert.Contains("DD1 ACC adaptation", effects[0].Text);
        Assert.Contains("add_stats,3,", effects[1].Stats);
        Assert.Equal(0.1f, effects[2].Stress);
    }

    [Fact]
    public void BracerCountsPairedDamageBoundsOnceAndRetainsExactAmounts()
    {
        var effects = Items.Get("legendary_bracer").BuffIds.Select(id => Dd1TrinketEffect.Adapt(Buffs.Get(id))).Where(b => b != null).ToArray();
        Assert.Equal(3, effects.Length);
        Assert.Contains("add_stats,0.2,", effects[0].Stats);
        Assert.Contains("add_stats,-1,", effects[1].Stats);
        Assert.Equal(0.1f, effects[2].Stress);
    }

    [Fact]
    public void UnknownConditionalRulesNeverBecomeUnconditionalBonuses()
    {
        var effect = Dd1TrinketEffect.Adapt(new Dd1Buff { Stat = "combat_stat_add", Sub = "crit_chance", Amount = 0.5f, Rule = "future_rule" });
        Assert.Null(effect.Stats);
        Assert.Contains("inactive", effect.Text);
    }

    [Fact]
    public void MaxHealthIsMultiplicativeAndDeathDoorBonusRemainsConditional()
    {
        Assert.Contains("multiply_stats,0.15,", Dd1TrinketEffect.Adapt(Buffs.Get(Items.Get("martyrs_seal").BuffIds.Last())).Stats);
        var death = Items.Get("martyrs_seal").BuffIds.Select(Buffs.Get).First(b => b.Rule == "at_deaths_door");
        Assert.Contains("health_percent", Dd1TrinketEffect.Adapt(death).Condition);
    }

    [Fact]
    public void RewardPoolsRollOnceExcludeSpecialPoolAndPreferUnownedLimitedItems()
    {
        var memory = new FadedMemoryEncounter();
        string[] owned = Items.ForRarity("ancestral").Where(t => t.Id != "ancestors_pistol").Select(t => FadedMemory.TrinketPrefix + t.Id).ToArray();
        Assert.True(FadedMemory.PrepareRewards(memory, Items, owned, new Rng(20)));
        Assert.Equal("dd3_dd1_ancestors_pistol", memory.Rewards[1]);
        var before = memory.Rewards.ToArray();
        Assert.True(FadedMemory.PrepareRewards(memory, Items, owned, new Rng(400)));
        Assert.Equal(before, memory.Rewards);
        Assert.Equal("very_rare", Items.Get(memory.Rewards[0].Substring(FadedMemory.TrinketPrefix.Length)).Rarity);
        Assert.True(FadedMemory.PrepareRewards(new FadedMemoryEncounter(), Items, Items.ForRarity("ancestral").Select(t => FadedMemory.TrinketPrefix + t.Id), new Rng(10)));
    }
}
