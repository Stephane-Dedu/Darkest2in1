using System;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketComboTests
{
    [Fact]
    public void SnapJudgementKeepsComboRemovalAtItsActualSpeedThreshold()
    {
        var data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");
        string effects = data.Effects("trinket_cultist_snap_judgement", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Remove Combo when Speed is 12 or more", effects);
        Assert.Contains("Turn Start: Extra Action (10%) when Speed is 12 or more", effects);
        Assert.Contains("Turn Start: Add 1 Positive Token when Speed is 8 or more", effects);
        Assert.Contains("Turn End: -6 Speed (1 Battle) when last in turn order", effects);
        Assert.DoesNotContain("Combo!", effects);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", true)]
    [InlineData("invalid", false)]
    [InlineData("1", false)]
    public void ComboPresentationMetadataRequiresANativeBoolean(string marker, bool supported)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_combo_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"),
                "element_start,combo_item,Item\nelement_end\nelement_start,combo_item,ActorDataEffects\nturn_start_effects,combo_effect\nelement_end\n"
                + "element_start,combo_effect,Effect\nm_IsCombo," + marker + "\nm_TokenRemoveId,combo\nm_TokenRemoveAmount,1\nm_ShowValue,False\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_skill_effect_turn_start=Turn Start:\ntoken_name_combo=Combo\ntoken_amount_format_singular={0}\neffect_tooltip_token_remove_amount=Remove {0}");
            string effects = TrinketDescriptions.Load(fixture).Effects("combo_item", out bool complete);
            Assert.Equal(supported, complete);
            Assert.Equal(supported ? "Turn Start: Remove Combo" : null, effects);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
