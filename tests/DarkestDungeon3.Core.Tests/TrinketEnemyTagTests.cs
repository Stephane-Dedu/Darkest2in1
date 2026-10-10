using System;
using System.Collections.Generic;
using System.IO;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketEnemyTagTests
{
    private static readonly TrinketDescriptions Data = TrinketDescriptions.Load(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets");

    [Fact]
    public void RatSkullKeepsItsEnemyCreaturePenaltyBesideThreeBenefits()
    {
        string effects = Data.Effects("trinket_hero_hwy_rat_skull", out bool complete);
        Assert.True(complete);
        Assert.Contains("Enemy Creature present: -66% Healing Received from Skills", effects);
        Assert.Contains("Turn Start: Crit (33%) when first in turn order", effects);
        Assert.Contains("Self: Take Aim: Skills Ignore Blind (3 Turns)", effects);
        Assert.Contains("Target: Tracking Shot: -20% Stun RES (3 Turns)", effects);
        Assert.DoesNotContain("<color", effects);
    }

    [Fact]
    public void EnemyPresenceRetainsLocalizedActorTagAndBody()
    {
        string effects = Data.Effects("trinket_hero_hwy_rat_skull", out bool complete, key => key switch
        {
            "effect_tooltip_actor_type_monsters" => "Ennemi", "tag_animal" => "Créature",
            "effect_tooltip_condition_enemy_tag_presence" => "{0} présent : {1}", _ => null,
        });
        Assert.True(complete);
        Assert.Contains("Ennemi Créature présent: -66% Healing Received from Skills", effects);
        foreach (string template in new[] { "invalid {9}", "{0} present", "{1}", "Creature: {1}" })
        {
            effects = Data.Effects("trinket_hero_hwy_rat_skull", out complete,
                key => key == "effect_tooltip_condition_enemy_tag_presence" ? template : null);
            Assert.False(complete);
            Assert.DoesNotContain("-66%", effects);
            Assert.Contains("Turn Start: Crit (33%)", effects);
        }
    }

    [Theory]
    [InlineData("m_ConditionActorType", "TARGET")]
    [InlineData("m_ConditionString", "ally")]
    [InlineData("m_ConditionNumber", "2")]
    [InlineData("m_ConditionNumber", "NaN")]
    [InlineData("m_ConditionNumberType", "EQUAL")]
    [InlineData("m_IsInverse", "True")]
    [InlineData("m_SourceConditionActorType", "PERFORMER")]
    [InlineData("m_ActorIsNotSource", "True")]
    [InlineData("m_IsVisible", "False")]
    [InlineData("m_UnknownRestriction", "True")]
    public void UnsupportedEnemyRequirementsNeverBecomeUnconditionalPenalties(string field, string value)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_enemy_tag_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            var fields = new Dictionary<string, string>
            {
                ["m_ConditionType"] = "tag", ["m_ConditionActorType"] = "MONSTERS", ["m_ConditionString"] = "animal",
                ["m_ConditionNumber"] = "1", ["m_ConditionNumberType"] = "GREATER_THAN_OR_EQUAL",
            };
            fields[field] = value;
            string csv = string.Join("\n", new[]
            {
                "element_start,enemy_item,Item", "element_end", "element_start,enemy_item,ActorDataExternalBuffs", "buffs,enemy_buff", "element_end",
                "element_start,enemy_buff,Buff", "m_ConditionId,enemy_condition", "element_end",
                "element_start,enemy_buff,ActorDataStats", "sub_stat,health_heal_received_percent,skill,-0.66", "element_end",
                "element_start,enemy_condition,Condition",
            });
            foreach (var entry in fields) csv += "\n" + entry.Key + "," + entry.Value;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_actor_type_monsters=Enemy\ntag_animal=Creature\nactor_stat_type_formatted_health_heal_received_percent_skill={0} Healing Received from Skills");
            Assert.Null(TrinketDescriptions.Load(fixture).Effects("enemy_item", out bool complete));
            Assert.False(complete);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
