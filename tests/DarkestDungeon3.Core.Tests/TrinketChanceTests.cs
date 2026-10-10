using System;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketChanceTests
{
    [Theory]
    [InlineData("NaN", false)]
    [InlineData("NaN", true)]
    [InlineData("Infinity", false)]
    [InlineData("Infinity", true)]
    [InlineData("-Infinity", false)]
    [InlineData("-Infinity", true)]
    [InlineData("invalid", false)]
    [InlineData("invalid", true)]
    [InlineData("0", false)]
    [InlineData("0", true)]
    [InlineData("-0.05", false)]
    [InlineData("-0.05", true)]
    [InlineData("1.01", false)]
    [InlineData("1.01", true)]
    [InlineData("100", false)]
    [InlineData("100", true)]
    public void InvalidChancesNeverReadAsGuaranteedEvenWithAuthoredBodies(string chance, bool authored)
    {
        Fixture(chance, authored, data =>
        {
            Assert.Null(data.Effects("chance_item", out bool complete));
            Assert.False(complete);
        });
    }

    [Theory]
    [InlineData(null, "Turn Start: +1 Stress")]
    [InlineData("1", "Turn Start: +1 Stress")]
    [InlineData("0.05", "Turn Start: +1 Stress (5%)")]
    [InlineData("0.66", "Turn Start: +1 Stress (66%)")]
    public void NativeDefaultAndFractionalChancesRemainAccurate(string chance, string expected)
    {
        Fixture(chance, false, data =>
        {
            Assert.Equal(expected, data.Effects("chance_item", out bool complete));
            Assert.True(complete);
        });
    }

    [Fact]
    public void ValidAuthoredBodyKeepsItsChanceWithoutDuplicatingTheSuffix()
    {
        Fixture("0.05", true, data =>
        {
            Assert.Equal("Turn Start: Authored Stress (5%)", data.Effects("chance_item", out bool complete));
            Assert.True(complete);
        });
    }

    private static void Fixture(string chance, bool authored, Action<TrinketDescriptions> check)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_chance_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            string csv = string.Join("\n", new[]
            {
                "element_start,chance_item,Item", "element_end", "element_start,chance_item,ActorDataEffects",
                "turn_start_effects,chance_effect", "element_end", "element_start,chance_effect,Effect", "m_StressDamage,1",
            });
            if (chance != null) csv += "\nm_Chance," + chance;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            string localization = "effect_tooltip_skill_effect_turn_start=Turn Start:\neffect_tooltip_stress_damage=+{0} Stress\neffect_tooltip_pct=({0}%)";
            if (authored) localization += "\neffect_skill_chance_effect_override=Authored Stress (5%)";
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"), localization);
            check(TrinketDescriptions.Load(fixture));
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
