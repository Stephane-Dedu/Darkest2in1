using System;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketSharedEffectTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");

    [Fact]
    public void EarlyExperimentKeepsItsAuthoredSharedInversionAndOtherEffects()
    {
        string effects = Data.Effects("trinket_hero_pd_early_experiment", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply On CRIT: Invert 2 Positive Tokens", effects);
        Assert.Contains("+1 Blight Dealt when Noxious item is equipped", effects);
        Assert.Contains("Gain When Moved By Enemy: Bleed 1 (3 Turns) (15%) or Blight 1 (3 Turns) (15%) or Burn 1 (3 Turns) (15%)", effects);
        Assert.DoesNotContain("<color", effects);
    }

    [Fact]
    public void SharedEffectUsesNativeAuthoredLocalizationAndMalformedBodiesRemainWithheld()
    {
        string effects = Data.Effects("trinket_hero_pd_early_experiment", out bool complete,
            key => key == "effect_skill_invert_2_positive_tokens_override" ? "Inverse 2 Jetons Positifs" : null);
        Assert.True(complete);
        Assert.Contains("Apply On CRIT: Inverse 2 Jetons Positifs", effects);
        effects = Data.Effects("trinket_hero_pd_early_experiment", out complete,
            key => key == "effect_skill_invert_2_positive_tokens_override" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Apply On CRIT:", effects);
        Assert.Contains("+1 Blight Dealt", effects);
        Assert.Contains("Gain When Moved By Enemy:", effects);
    }

    [Fact]
    public void FallbackNeverOverridesPrimaryEffectsOrBypassesUnknownFieldGuards()
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_shared_effect_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"),
                "element_start,shared_item,Item\nelement_end\nelement_start,shared_item,ActorDataEffects\nturn_start_effects,primary_effect,shared_effect,unknown_effect\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Excel", "effect_data_export.Group.csv"),
                "element_start,primary_effect,Effect\nm_StressDamage,1\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Excel", "boss_blessing_data_export.Group.csv"),
                "element_start,primary_effect,Effect\nm_StressDamage,9\nelement_end\nelement_start,shared_effect,Effect\nm_StressDamage,2\nelement_end\n"
                + "element_start,unknown_effect,Effect\nm_StressDamage,3\nm_UnknownTarget,True\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_skill_effect_turn_start=Turn Start:\neffect_tooltip_stress_damage=+{0} Stress");
            string effects = TrinketDescriptions.Load(fixture).Effects("shared_item", out bool complete);
            Assert.Equal("Turn Start: +1 Stress\nTurn Start: +2 Stress", effects);
            Assert.False(complete);
            Assert.DoesNotContain("9", effects);
            Assert.DoesNotContain("3", effects);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
