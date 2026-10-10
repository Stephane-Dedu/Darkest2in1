using System;
using System.Collections.Generic;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketTorchTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");

    [Fact]
    public void IconOfTheLightRetainsItsFlameRiskAndExistingBenefits()
    {
        string effects = Data.Effects("trinket_hero_ves_icon_of_the_light", out bool complete);
        Assert.Contains("Gain When CRIT: Flame -5 (66%)", effects);
        Assert.Contains("Regen Dealt", effects);
        Assert.Contains("Consecration Skills: -1 Stress", effects);
        Assert.DoesNotContain("<sprite", effects);
        Assert.True(complete);
        effects = Data.Effects("trinket_hero_ves_icon_of_the_light", out complete,
            key => key == "effect_tooltip_torch_change" ? "Flamme {0}" : null);
        Assert.True(complete);
        Assert.Contains("Gain When CRIT: Flamme -5 (66%)", effects);
        foreach (string template in new[] { "invalid {9}", "Flame" })
        {
            effects = Data.Effects("trinket_hero_ves_icon_of_the_light", out complete,
                key => key == "effect_tooltip_torch_change" ? template : null);
            Assert.DoesNotContain("Gain When CRIT:", effects);
            Assert.Contains("Regen Dealt", effects);
            Assert.False(complete);
        }
    }

    [Theory]
    [InlineData("5", "+5")]
    [InlineData("-5", "-5")]
    [InlineData("0.125", "+0.125")]
    public void FlameUsesSignedRawUnitsAndItsChance(string value, string expected)
    {
        Fixture(new Dictionary<string, string> { ["torch"] = value }, data =>
        {
            string effects = data.Effects("torch_item", out bool complete);
            Assert.True(complete);
            Assert.Equal("Gain When CRIT: Flame " + expected + " (66%)", effects);
            Assert.DoesNotContain("% Flame", effects);
        });
    }

    [Theory]
    [InlineData("torch", "NaN")]
    [InlineData("torch", "Infinity")]
    [InlineData("torch", "invalid")]
    [InlineData("torch", "0")]
    [InlineData("m_RunValuesIsSetTo", "True")]
    [InlineData("m_UnknownTorchSelector", "True")]
    [InlineData("m_IsVisible", "False")]
    public void UnsafeTorchValuesAndSelectorsAreWithheld(string field, string value)
    {
        Fixture(new Dictionary<string, string> { ["torch"] = "-5", [field] = value }, data =>
        {
            Assert.Null(data.Effects("torch_item", out bool complete));
            Assert.False(complete);
        });
    }

    private static void Fixture(Dictionary<string, string> fields, Action<TrinketDescriptions> check)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_torch_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            string csv = string.Join("\n", new[]
            {
                "element_start,torch_item,Item", "element_end", "element_start,torch_item,ActorDataEffects",
                "on_crit_as_target_to_target_effects,torch_effect", "element_end", "element_start,torch_effect,Effect", "m_Chance,0.66",
            });
            foreach (var field in fields) csv += "\n" + field.Key + "," + field.Value;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_skill_effect_on_crit_as_target_to_target=Gain When CRIT:\neffect_tooltip_torch_change=Flame {0}\neffect_tooltip_pct=({0}%)");
            check(TrinketDescriptions.Load(fixture));
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
