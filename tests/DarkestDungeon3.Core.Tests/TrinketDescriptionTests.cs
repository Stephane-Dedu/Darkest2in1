using System;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketDescriptionTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");
    [Theory]
    [InlineData("trinket_general_gnarly_knuckles", "Melee Skills: +20% DMG")]
    [InlineData("trinket_general_ravens_reach", "Ranged Skills: +20% DMG")]
    public void ConditionalSkillBonusesKeepTheirSkillRequirement(string id, string expected)
    {
        string effects = Data.Effects(id, out bool complete);
        Assert.Contains(expected, effects);
        Assert.Contains("Gain On Miss:", effects);
        Assert.Contains("+1 Stress", effects);
        Assert.True(complete);
    }

    [Fact]
    public void ColdMenuTriggeredTokensKeepBothTheirEventAndTheirChance()
    {
        string effects = Data.Effects("trinket_general_adrenalizing_ash", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn End: Speed (15%)", effects);
        Assert.Contains("Gain On Miss: Daze (20%)", effects);
        effects = Data.Effects("trinket_general_bulwark_band", out complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Dodge (15%)", effects);
        Assert.Contains("Gain On Miss: Blind (15%)", effects);
        Assert.DoesNotContain("{q}", effects);
        Assert.DoesNotContain("<sprite", effects);
    }

    [Fact]
    public void MalformedLocalizedEffectDoesNotInventATokenOrChance()
    {
        string effects = Data.Effects("trinket_general_adrenalizing_ash", out bool complete,
            key => key == "effect_tooltip_token_add_amount" ? "invalid {9}" : null);
        Assert.Null(effects);
        Assert.False(complete);
    }

    [Fact]
    public void ConditionalHealthAndFlameThresholdsKeepNativeUnitsAndQualifierSpacing()
    {
        string health = Data.Effects("trinket_hero_hel_bloodied_branch", out _);
        Assert.Contains("+2 Bleed Dealt when self HP is below 33%", health);
        string torch = Data.Effects("trinket_cave_peculiar_pods", out _);
        Assert.Contains("-2 Speed when Flame is above 75", torch);
        Assert.Contains("+25% Debuff RES Piercing", torch);
    }

    [Fact]
    public void UnsupportedLocalizedConditionNeverTurnsItsBonusIntoAnUnconditionalClaim()
    {
        string effects = Data.Effects("trinket_cave_peculiar_pods", out bool complete,
            key => key == "effect_tooltip_condition_run_value" ? "invalid {9}" : null);
        Assert.DoesNotContain("Speed", effects);
        Assert.Contains("+25% Debuff RES Piercing", effects);
        Assert.False(complete);
    }

    [Fact]
    public void ColdMenuReadsBothBenefitsAndPenaltiesFromTheActualBuffTable()
    {
        string text = Data.Effects("trinket_tiered_anchoring_charm_minor", out bool complete);
        Assert.True(complete);
        Assert.Contains("+15% Move RES", text);
        Assert.Contains("+10% Stun RES", text);
        Assert.Contains("-10% Deathblow RES", text);
        Assert.DoesNotContain("<sprite", text);
    }

    [Fact]
    public void PercentMultipliersAndValueStatsKeepTheirNativeUnits()
    {
        string text = Data.Effects("trinket_tiered_hale_draught_minor", out bool complete);
        Assert.True(complete);
        Assert.Contains("+10% Max HP", text);
        Assert.Contains("-3% CRIT", text);
    }

    [Fact]
    public void LiveLocalizationOverridesEnglishFallbackAndAnUnknownItemHasNoInventedBonus()
    {
        var text = Data.Effects("trinket_tiered_anchoring_charm_minor", out _, key => key == "actor_stat_type_formatted_resistance_move" ? "{0} déplacement" : null);
        Assert.Contains("+15% déplacement", text);
        Assert.Null(Data.Effects("missing_item", out bool complete));
        Assert.False(complete);
    }

    [Fact]
    public void SourceQuotationTokensAndRichIconsBecomeReadableLines()
    {
        Assert.Equal("Gain Stun\n+10%", TrinketDescriptions.Plain("<color=#{notable}>Gain <sprite name={q}icon_stun{q}></color>\\n<b>+10%</b>"));
    }
}
