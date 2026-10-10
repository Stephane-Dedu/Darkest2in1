using System;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketLootTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");

    [Fact]
    public void HisRingsKeepsBothDeadOfNightRewardsAlongsideBenefitsAndRisk()
    {
        string effects = Data.Effects("trinket_hero_gr_his_rings", out bool complete);
        Assert.True(complete);
        Assert.Contains("Self: Dead of Night: +2 Relics", effects);
        Assert.Contains("Self: Dead of Night: +1 Bauble", effects);
        Assert.Contains("Apply to Attacker When Missed: Combo", effects);
        Assert.Contains("Pick to the Face: +10% CRIT", effects);
        Assert.Contains("Gain When Hit: +1 Stress (15%)", effects);
        Assert.DoesNotContain("<color", effects);
    }

    [Fact]
    public void RewardsUseNativeLocalizationAndWithholdMalformedLabelsIndividually()
    {
        string effects = Data.Effects("trinket_hero_gr_his_rings", out bool complete, key => key switch
        {
            "effect_tooltip_loot_id_RELICS_TINY" => "+2 Reliques",
            "effect_tooltip_loot_id_BAUBLES_MINUSCULE" => "+1 Babiole",
            "skill_name_gr_dead_of_night" => "Nuit noire",
            _ => null,
        });
        Assert.True(complete);
        Assert.Contains("Self: Nuit noire: +2 Reliques", effects);
        Assert.Contains("Self: Nuit noire: +1 Babiole", effects);
        effects = Data.Effects("trinket_hero_gr_his_rings", out complete,
            key => key == "effect_tooltip_loot_id_RELICS_TINY" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Relics", effects);
        Assert.Contains("Self: Dead of Night: +1 Bauble", effects);
        Assert.Contains("Gain When Hit: +1 Stress (15%)", effects);
    }

    [Theory]
    [InlineData("", "RELICS_TINY,BAUBLES_MINUSCULE", true)]
    [InlineData("m_Chance,1\n", "RELICS_TINY", true)]
    [InlineData("m_Chance,0.5\n", "RELICS_TINY", false)]
    [InlineData("m_Chance,NaN\n", "RELICS_TINY", false)]
    [InlineData("m_IsVisible,False\n", "RELICS_TINY", false)]
    [InlineData("m_LootReasonOverride,OTHER\n", "RELICS_TINY", false)]
    [InlineData("m_UnknownTarget,True\n", "RELICS_TINY", false)]
    [InlineData("all_conditions,hidden_skill\n", "RELICS_TINY", false)]
    [InlineData("", "RELICS_TINY,UNKNOWN", false)]
    [InlineData("", "MALFORMED", false)]
    [InlineData("", "BLANK", false)]
    public void LootRequiresCompleteAuthoredLabelsAndSupportedVisibleMetadata(string fields, string ids, bool supported)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_loot_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"),
                "element_start,loot_item,Item\nelement_end\nelement_start,loot_item,ActorDataEffects\nperformer_effects,loot_effect\nelement_end\n"
                + "element_start,loot_effect,Effect\nm_LootIds," + ids + "\n" + fields + "element_end\n"
                + "element_start,hidden_skill,Condition\nm_ConditionType,skill\nm_ConditionActorType,NONE\nm_ConditionNumberType,BOOL\n"
                + "m_ConditionString,gr_dead_of_night\nm_IsVisible,False\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "items.txt"),
                "effect_tooltip_skill_effect_performer=Self:\neffect_tooltip_loot_id_RELICS_TINY=+2 Relics\n"
                + "effect_tooltip_loot_id_BAUBLES_MINUSCULE=+1 Bauble\neffect_tooltip_loot_id_MALFORMED=invalid {9}\n"
                + "effect_tooltip_loot_id_BLANK=<color=#fff></color>\n"
                + "effect_tooltip_condition_skill={0}: {1}\nskill_name_gr_dead_of_night=Dead of Night");
            string effects = TrinketDescriptions.Load(fixture).Effects("loot_item", out bool complete);
            Assert.Equal(supported, complete);
            Assert.Equal(supported ? "Self: +2 Relics" + (ids.Contains(",") ? " +1 Bauble" : "") : null, effects);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
