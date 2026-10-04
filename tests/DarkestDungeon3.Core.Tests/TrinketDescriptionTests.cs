using System;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketDescriptionTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");

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
