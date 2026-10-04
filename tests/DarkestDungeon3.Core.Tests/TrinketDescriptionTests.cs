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
    public void LimitedEffectsRetainEveryAlternativeIncludingThePenalty()
    {
        string effects = Data.Effects("trinket_coast_nautical_compass", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn End:", effects);
        Assert.Contains("Block", effects);
        Assert.Contains("Dodge", effects);
        Assert.Contains("Strength", effects);
        Assert.Contains("+1 Stress", effects);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(effects, " or ").Count);
    }

    [Fact]
    public void LimitedHitEffectsKeepTheAttackerTargetAndEachCandidatesChance()
    {
        string effects = Data.Effects("trinket_forest_clenching_claws", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply to Attacker When Hit:", effects);
        Assert.Contains("Weak (20%) or Vulnerable (20%) or Stun (10%)", effects);
        Assert.Contains("Round Start: Immobilize (66%) when Speed is 2 or less", effects);
    }

    [Fact]
    public void StatThresholdsKeepTheirNativeNumbersAndBothComparisonDirections()
    {
        string effects = Data.Effects("trinket_coast_seamens_boots", out bool complete);
        Assert.True(complete);
        Assert.Contains("When Moving: Block (66%) when Speed is 2 or less", effects);
        Assert.Contains("When Moving: Dodge (66%) when Speed is 6 or more", effects);
        Assert.DoesNotContain("200%", effects);
        string localized = Data.Effects("trinket_coast_seamens_boots", out _,
            key => key == "actor_stat_type_speed" ? "Vitesse" : null);
        Assert.Contains("Vitesse is 6 or more", localized);
    }

    [Fact]
    public void MalformedStatConditionWithholdsItsEffectButKeepsUnconditionalBonuses()
    {
        string effects = Data.Effects("trinket_city_laden_lantern", out bool complete,
            key => key == "effect_tooltip_condition_actor_stat_value" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Blind", effects);
        Assert.Contains("+25% Burn RES Piercing", effects);
    }

    [Fact]
    public void AnUnknownCandidateWithholdsTheWholeChoiceRatherThanClaimingOnlyItsPenalty()
    {
        string effects = Data.Effects("trinket_coast_nautical_compass", out bool complete,
            key => key == "effect_tooltip_token_add_amount" ? "invalid {9}" : null);
        Assert.Null(effects);
        Assert.False(complete);
    }

    [Fact]
    public void DamageOverTimeChoicesKeepTheirMagnitudeAndDurationOnBothTargets()
    {
        string effects = Data.Effects("trinket_curio_corrupted_bile_gland", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply On Hit:", effects);
        Assert.Contains("Bleed 3 (3 Turns) or Blight 3 (3 Turns) or Burn 3 (3 Turns)", effects);
        Assert.Contains("Gain When Hit:", effects);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(effects, " or ").Count);
    }

    [Fact]
    public void HealingOverTimeChoicesKeepTheirChanceAndTheAlternativeBleedPenalty()
    {
        string effects = Data.Effects("trinket_curio_pulsing_heart", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Regen 2 (3 Turns) (33%) or Bleed 1 (3 Turns) (66%)", effects);
        string localized = Data.Effects("trinket_curio_pulsing_heart", out _,
            key => key == "duration_display_type_turn+plural" ? "{0} tours" : null);
        Assert.Contains("Regen 2 (3 tours)", localized);
    }

    [Fact]
    public void MalformedDotDurationWithholdsTheWholeChoiceIncludingItsPenalty()
    {
        string effects = Data.Effects("trinket_curio_pulsing_heart", out bool complete,
            key => key == "duration_display_type_turn+plural" ? "invalid {9}" : null);
        Assert.Null(effects);
        Assert.False(complete);
    }

    [Fact]
    public void TokenRequirementsKeepTheirActorAndPresenceOrAbsence()
    {
        string stealth = Data.Effects("trinket_hero_gr_foreclosure_notice", out _);
        Assert.Contains("+20% DMG when self has Stealth", stealth);
        Assert.Contains("-25% Healing Received from Skills when self has no Stealth", stealth);
        string target = Data.Effects("trinket_cave_bone_mallet", out bool complete);
        Assert.True(complete);
        Assert.Contains("+25% DMG when target has Daze", target);
        Assert.Contains("+50% DMG when target has Stun", target);
        Assert.Contains("+10% CRIT when target has Combo", Data.Effects("trinket_cave_pig_sticker", out _));
    }

    [Fact]
    public void DotRequirementsWrapBothStatBonusesAndTriggeredPenalties()
    {
        string effects = Data.Effects("trinket_hero_run_carved_toy", out bool complete);
        Assert.True(complete);
        Assert.Contains("+25% Debuff RES Piercing when target Burn", effects);
        effects = Data.Effects("trinket_cultist_key_dark_impulse_bleed_res", out complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: +1 Stress when self Bleed", effects);
        Assert.Contains("+50% Bleed RES", effects);
        string localized = Data.Effects("trinket_cultist_key_dark_impulse_bleed_res", out _,
            key => key == "effect_tooltip_actor_type_performer" ? "soi" : null);
        Assert.Contains("when soi Bleed", localized);
    }

    [Fact]
    public void MalformedCombatStateConditionsWithholdOnlyTheirConditionalLines()
    {
        string token = Data.Effects("trinket_cave_pig_sticker", out bool complete,
            key => key == "effect_tooltip_condition_token_amount_nonzero" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("CRIT when", token);
        Assert.Contains("Stealth when Flame is below 50", token);
        string dot = Data.Effects("trinket_cultist_key_dark_impulse_bleed_res", out complete,
            key => key == "effect_tooltip_condition_dot_tag_amount_nonzero" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Stress", dot);
        Assert.Contains("+50% Bleed RES", dot);
    }

    [Fact]
    public void TokenCategoriesKeepAddRemoveAndAllTokenSemanticsWithTheirPenalty()
    {
        string effects = Data.Effects("trinket_cultist_jealous_whisper", out bool complete);
        Assert.True(complete);
        Assert.Contains("When Moving: Add 1 Positive Token", effects);
        Assert.Contains("Gain When Moved By Ally Skill: Add 1 Positive Token", effects);
        Assert.Contains("Gain When Moved By Enemy: Remove All Positive Tokens", effects);
        Assert.Contains("Gain When Moved By Enemy: +1 Stress", effects);
        Assert.DoesNotContain("99", effects);
        Assert.DoesNotContain("<color", effects);
        Assert.Contains("Random Ally When Healed: Add 1 Positive Token (33%)",
            Data.Effects("trinket_curio_heart-shaped_padlock", out _));
    }

    [Fact]
    public void NamedAndCategoryRemovalsKeepNativeQuantityVisibilityAndConditions()
    {
        string effects = Data.Effects("trinket_hero_flg_searing_scripture", out _);
        Assert.Contains("Round End: Remove 1 Negative Token", effects);
        Assert.Contains("Round End: Remove Combo", effects);
        Assert.DoesNotContain("99", effects);
        Assert.Contains("-20% Burn RES", effects);
        effects = Data.Effects("trinket_hero_jes_severed_finger", out bool complete);
        Assert.True(complete);
        Assert.Contains("Round Start: Add 1 Positive Token when self Bleed", effects);
    }

    [Fact]
    public void MalformedTokenCategoryTemplateWithholdsTheMutationButKeepsItsPenalty()
    {
        string effects = Data.Effects("trinket_cultist_jealous_whisper", out bool complete,
            key => key == "effect_tooltip_token_remove_all_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Remove", effects);
        Assert.Contains("Gain When Moved By Enemy: +1 Stress", effects);
        Assert.Contains("When Moving: Add 1 Positive Token", effects);
    }

    [Fact]
    public void TriggeredStatBuffsKeepTheirEventAndNativeLifetime()
    {
        string effects = Data.Effects("trinket_cultist_cruel_intent", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Miss: +10% CRIT (1 Battle)", effects);
        Assert.Contains("Combat Start: Blindx2", effects);
        effects = Data.Effects("trinket_hero_pd_storage_room_key", out _);
        Assert.Contains("Gain On Miss: -1 Speed (3 Turns)", effects);
        Assert.Contains("+1 Speed per Medical Gear item equipped", effects);
        Assert.Contains("When Stress Healed: +1% CRIT (1 Battle)", Data.Effects("trinket_hero_hel_empty_stein", out _));
    }

    [Fact]
    public void TriggeredSubStatDebuffKeepsItsQuantityDurationChanceAndResistancePenalty()
    {
        string effects = Data.Effects("trinket_hero_flg_his_prison", out _);
        Assert.Contains("Apply to Attacker When Hit: +1 Blight Received (3 Turns) (33%)", effects);
        Assert.Contains("-20% Blight RES", effects);
        string localized = Data.Effects("trinket_hero_flg_his_prison", out _,
            key => key == "duration_display_type_turn+plural" ? "{0} tours" : null);
        Assert.Contains("+1 Blight Received (3 tours) (33%)", localized);
        Assert.DoesNotContain("<color", effects);
    }

    [Fact]
    public void MalformedBuffDurationOrStatTemplateWithholdsTheBuffRatherThanInventingALifetime()
    {
        string effects = Data.Effects("trinket_hero_pd_storage_room_key", out bool complete,
            key => key == "skill_effect_duration_label" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("-1 Speed", effects);
        Assert.Contains("+1 Speed per Medical Gear item equipped", effects);
        effects = Data.Effects("trinket_cultist_cruel_intent", out complete,
            key => key == "actor_stat_type_formatted_crit_chance" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("CRIT (1 Battle)", effects);
        Assert.Contains("Gain On Non-CRIT: Blind", effects);
    }

    [Fact]
    public void NamedSkillRequirementsKeepEachStatAndTriggeredEffectWithItsSkill()
    {
        string effects = Data.Effects("trinket_hero_bh_utility_belt", out bool complete);
        Assert.True(complete);
        Assert.Contains("Caltrops: +25% Debuff RES Piercing", effects);
        Assert.Contains("Apply On Hit: Flashbang: Blind", effects);
        Assert.Contains("Apply On Hit: Hurlbat: Weak", effects);
        Assert.DoesNotContain("Upgraded skill", effects);
        Assert.Contains("Self: Raucous Revelry: Strength (95%) or Raucous Revelry: Daze (5%)",
            Data.Effects("trinket_hero_hel_empty_stein", out _));
        Assert.Contains("Target: Play Out: Remove 1 Negative Token", Data.Effects("trinket_hero_jes_buskers_haul", out _));
    }

    [Fact]
    public void NamedSkillLocalizationAndNativeSpecificOverridesAreUsed()
    {
        string effects = Data.Effects("trinket_hero_bh_utility_belt", out _,
            key => key == "skill_name_bh_caltrops" ? "Chausse-trappes" : null);
        Assert.Contains("Chausse-trappes: +25% Debuff RES Piercing", effects);
        effects = Data.Effects("trinket_hero_bh_utility_belt", out _,
            key => key == "effect_tooltip_condition_skill_bh_flashbang" ? "Flashbang only: {0}" : null);
        Assert.Contains("Apply On Hit: Flashbang only: Blind", effects);
    }

    [Fact]
    public void MalformedNamedSkillConditionWithholdsItsEffectsAndPreservesOtherTriggers()
    {
        string effects = Data.Effects("trinket_hero_hel_empty_stein", out bool complete,
            key => key == "effect_tooltip_condition_skill" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Strength", effects);
        Assert.DoesNotContain("Daze", effects);
        Assert.Contains("When Stress Healed: +1% CRIT (1 Battle)", effects);
    }

    [Fact]
    public void ResistanceBypassDoesNotHideNativeSelfEffectsOrSkillDebuffs()
    {
        string effects = Data.Effects("trinket_general_strong_shackles", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply On Hit: Immobilize", effects);
        Assert.Contains("Gain On Hit: Immobilize", effects);
        effects = Data.Effects("trinket_hero_hel_empty_stein", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Barbaric YAWP!: -20% Debuff RES (3 Turns)", effects);
        Assert.Contains("Raucous Revelry: Strength (95%) or Raucous Revelry: Daze (5%)", effects);
    }

    [Fact]
    public void ResistanceBypassChoicesKeepSeparateQuantitiesAndTheirMissPenalty()
    {
        string effects = Data.Effects("trinket_coast_pristine_lure", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Hit: Taunt (50%) or Tauntx2 (25%)", effects);
        Assert.Contains("Gain On Miss: Bleed 2 (3 Turns)", effects);
    }

    [Fact]
    public void ResistanceBypassStatPenaltyRetainsLifetimeAndSafeMalformedFallback()
    {
        string effects = Data.Effects("trinket_cultist_idle_thought", out bool complete);
        Assert.True(complete);
        Assert.Contains("+100% DMG", effects);
        Assert.Contains("Round End: -30% DMG (1 Battle)", effects);
        effects = Data.Effects("trinket_cultist_idle_thought", out complete,
            key => key == "buff_combat_end_single_duration_label" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("-30%", effects);
        Assert.Contains("+100% DMG", effects);
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
