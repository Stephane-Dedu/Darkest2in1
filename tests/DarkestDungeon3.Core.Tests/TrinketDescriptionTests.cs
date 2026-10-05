using System;
using System.IO;
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
    public void InitiativeConditionsKeepFirstTurnOrderPenaltiesAndLastTurnOrderBonuses()
    {
        string effects = Data.Effects("trinket_forest_blistering_bugle", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Taunt (50%)", effects);
        Assert.Contains("Turn Start: Vulnerable (33%) when first in turn order", effects);
        effects = Data.Effects("trinket_hero_lep_inevitable_end", out _);
        Assert.Contains("Turn End: +5% DMG (1 Battle) when last in turn order", effects);
        Assert.Contains("When Moving: -1 Speed (1 Battle)", effects);
        Assert.Contains("Turn End: -6 Speed (1 Battle) when last in turn order", Data.Effects("trinket_cultist_snap_judgement", out _));
    }

    [Fact]
    public void InitiativeConditionUsesLiveLocalization()
    {
        string effects = Data.Effects("trinket_forest_blistering_bugle", out bool complete,
            key => key == "effect_tooltip_condition_first_initiative" ? "{0} en premier" : null);
        Assert.True(complete);
        Assert.Contains("Vulnerable (33%) en premier", effects);
    }

    [Fact]
    public void MalformedInitiativeConditionDoesNotMakeTheBuffUnconditional()
    {
        string effects = Data.Effects("trinket_cultist_snap_judgement", out bool complete,
            key => key == "effect_tooltip_condition_last_initiative" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("-6 Speed", effects);
        Assert.Contains("Turn Start: +1 Speed (1 Battle)", effects);
        Assert.Contains("Add 1 Positive Token when Speed is 8 or more", effects);
    }

    [Fact]
    public void ExtraActionsRetainTheirTriggerChanceAndCombatStateRequirement()
    {
        string effects = Data.Effects("trinket_city_boss_smoldering_hymnal", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Killing Blow: Extra Action (20%)", effects);
        Assert.Contains("+6 Speed when self Burn", effects);
        effects = Data.Effects("trinket_coast_boss_carved_bodkin", out complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Extra Action (20%) when self Bleed", effects);
        Assert.Contains("-2 Bleed Received", effects);
        Assert.Contains("Extra Action (10%) when Speed is 12 or more", Data.Effects("trinket_cultist_snap_judgement", out _));
    }

    [Fact]
    public void ExtraActionChoicesRetainTheStunDamageAndResistancePenalties()
    {
        string effects = Data.Effects("trinket_curio_oversprung_pocketwatch", out bool complete);
        Assert.True(complete);
        Assert.Equal("Turn End: Extra Action (50%) or Stun (50%)", effects);
        effects = Data.Effects("trinket_cultist_temptation", out complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Extra Action or 1 DMG", effects);
        Assert.Contains("-100% Deathblow RES", effects);
    }

    [Fact]
    public void MalformedExtraActionTemplateWithholdsItsEntireChoiceButKeepsOtherStats()
    {
        string effects = Data.Effects("trinket_curio_oversprung_pocketwatch", out bool complete,
            key => key == "effect_tooltip_add_turn" ? "invalid {9}" : null);
        Assert.Null(effects);
        Assert.False(complete);
        effects = Data.Effects("trinket_cultist_temptation", out complete,
            key => key == "effect_tooltip_add_turn" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("1 DMG", effects);
        Assert.Contains("-100% Deathblow RES", effects);
    }

    [Fact]
    public void SelfMovementRetainsDirectionDistanceAndOtherTriggeredPenalties()
    {
        string effects = Data.Effects("trinket_hero_lep_inevitable_end", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Forward 1", effects);
        Assert.Contains("Turn End: +5% DMG (1 Battle) when last in turn order", effects);
        Assert.Contains("When Moving: -1 Speed (1 Battle)", effects);
    }

    [Fact]
    public void ShuffleAndKnockbackRetainTheirTargetAndSkillRequirement()
    {
        string effects = Data.Effects("trinket_hero_run_pile_of_ash", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply to Attacker When Missed: Shuffle", effects);
        Assert.Contains("Gain When Hit: Knockback 1", effects);
        Assert.Contains("When Moving: Dodge (50%) or Dodge+ (15%)", effects);
        effects = Data.Effects("trinket_collector_barristans_head", out _);
        Assert.Contains("Apply On Hit: Melee Skills: Knockback 1", effects);
        Assert.Contains("Turn Start: +1 Stress (15%)", effects);
    }

    [Fact]
    public void MovementUsesLiveNativeTemplatesAndWithholdsMalformedMovementOnly()
    {
        string effects = Data.Effects("trinket_hero_run_pile_of_ash", out bool complete,
            key => key == "effect_tooltip_target_backward" ? "Recul {0}" : null);
        Assert.True(complete);
        Assert.Contains("Gain When Hit: Recul 1", effects);
        effects = Data.Effects("trinket_hero_lep_inevitable_end", out complete,
            key => key == "effect_tooltip_move_forward" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Turn Start:", effects);
        Assert.Contains("+5% DMG (1 Battle) when last in turn order", effects);
        Assert.Contains("-1 Speed (1 Battle)", effects);
    }

    [Fact]
    public void SeparateTrinketTableProvidesBothStorageRoomKeySkillEffects()
    {
        string effects = Data.Effects("trinket_hero_pd_storage_room_key", out bool complete);
        Assert.True(complete);
        Assert.Contains("Target: Ounce of Prevention: Block", effects);
        Assert.Contains("Target: Emboldening Vapours: Regen 2 (3 Turns)", effects);
        Assert.Contains("+1 Speed per Medical Gear item equipped", effects);
        Assert.Contains("Gain On Miss: -1 Speed (3 Turns)", effects);
    }

    [Fact]
    public void SeparateTrinketTableRetainsSkillRequirementsAndExistingPenalties()
    {
        string effects = Data.Effects("trinket_hero_flg_his_prison", out bool complete);
        Assert.True(complete);
        Assert.Contains("Self: More! MORE!: Strength", effects);
        Assert.Contains("-20% Blight RES", effects);
        effects = Data.Effects("trinket_collector_barristans_head", out complete);
        Assert.True(complete);
        Assert.Contains("Self: Melee Skills: Block", effects);
        Assert.Contains("Turn Start: +1 Stress (15%)", effects);
        effects = Data.Effects("trinket_hero_gr_foreclosure_notice", out complete);
        Assert.True(complete);
        Assert.Contains("Self: Shadow Fade: Remove All Negative Tokens", effects);
        Assert.Contains("-25% Healing Received from Skills when self has no Stealth", effects);
    }

    [Fact]
    public void SeparateTrinketTableRetainsKnownTeamAndFlameEffects()
    {
        string effects = Data.Effects("trinket_curio_grim_mask", out bool complete);
        Assert.True(complete);
        Assert.Contains("Each Ally On Turn End: +1 Stress (33%) when Flame is below 50", effects);
        Assert.Contains("+40% DMG when Flame is below 50", effects);
    }

    [Fact]
    public void TeamTriggersRetainRandomTargetsActorCountsAndHarmfulOutcomes()
    {
        string effects = Data.Effects("trinket_cave_rousing_recorder", out bool complete);
        Assert.True(complete);
        Assert.Contains("Random Ally on Turn Start: Add 1 Positive Token (33%)", effects);
        Assert.Contains("-10% Debuff RES when Flame is above 75", effects);
        effects = Data.Effects("trinket_curio_obsidian_dronepipe", out complete);
        Assert.True(complete);
        Assert.Contains("Random Enemy on Turn Start: Stun (33%)", effects);
        Assert.Contains("Random Ally on Turn Start: Stun (10%)", effects);
        effects = Data.Effects("trinket_hero_maa_standard_of_the_ninth", out complete);
        Assert.True(complete);
        Assert.Contains("Each Hero on Combat Start: Block+", effects);
        Assert.Contains("Apply to Attacker When Hit: Remove 1 Positive Token", effects);
        Assert.Contains("Gain When CRIT: Weak", effects);
    }

    [Fact]
    public void TriggerChoicesKeepBothStressOutcomesAndUseLiveTargetTitles()
    {
        string effects = Data.Effects("trinket_curio_astroglass_flute", out bool complete);
        Assert.True(complete);
        Assert.Equal("Random Ally on Turn Start: -1 Stress (70%) or +1 Stress (30%)", effects);
        effects = Data.Effects("trinket_curio_astroglass_flute", out complete,
            key => key == "effect_tooltip_stress_damage" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.Null(effects);
        effects = Data.Effects("trinket_cave_rousing_recorder", out complete,
            key => key == "effect_tooltip_skill_effect_turn_start_friendly_team_1" ? "Allie aleatoire:" : null);
        Assert.True(complete);
        Assert.Contains("Allie aleatoire: Add 1 Positive Token (33%)", effects);
    }

    [Fact]
    public void TeamTriggerFallbackRetainsEventConditionAndSeparateNeighborEffects()
    {
        string effects = Data.Effects("trinket_curio_grim_mask", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On CRIT: Each Ally: -1 Stress (33%) when Flame is above 50", effects);
        Assert.Contains("Each Ally On Turn End: +1 Stress (33%) when Flame is below 50", effects);
        effects = Data.Effects("trinket_curio_faceless_visage", out complete);
        Assert.True(complete);
        Assert.Contains("Each Enemy on Round Start: Shuffle", effects);
        effects = Data.Effects("trinket_cave_sneakers_standard", out complete);
        Assert.True(complete);
        Assert.Contains("Combat Start: Taunt (33%) when Flame is above 75", effects);
        Assert.Contains("Combat Start: All Allies Behind: Stealth", effects);
    }

    [Fact]
    public void NeighborTriggersDistinguishAllAlliesBehindAndTheSingleAllyAhead()
    {
        string effects = Data.Effects("trinket_cave_sneakers_standard", out bool complete);
        Assert.True(complete);
        Assert.Contains("Combat Start: All Allies Behind: Stealth", effects);
        effects = Data.Effects("trinket_forest_unwavering_standard", out complete);
        Assert.True(complete);
        Assert.Contains("Combat Start: All Allies Behind: Block", effects);
        Assert.Contains("Gain On Miss: Weak when first in turn order", effects);
        effects = Data.Effects("trinket_city_hastening_history", out complete);
        Assert.True(complete);
        Assert.Contains("Turn End: Ally Ahead: Speed (33%)", effects);
        Assert.Contains("Turn Start: Stun (10%) when Speed is 2 or less", effects);
        effects = Data.Effects("trinket_forest_insulating_insignia", out complete);
        Assert.True(complete);
        Assert.Contains("Turn End: Ally Ahead: Block (33%)", effects);
        Assert.Contains("Gain On Miss: Combo when first in turn order", effects);
    }

    [Fact]
    public void NeighborTriggersRetainCritMissAndNamedSkillRequirements()
    {
        string effects = Data.Effects("trinket_hero_jes_royal_summons", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On CRIT: All Allies Behind: Add 1 Positive Token", effects);
        effects = Data.Effects("trinket_hero_maa_undeserved_commendation", out _);
        Assert.Contains("Apply On Miss: All Allies Behind: +1 Stress", effects);
        effects = Data.Effects("trinket_hero_ves_profane_scroll", out complete);
        Assert.True(complete);
        Assert.Contains("Self: Ally Behind: Hand of Light: Guardedx2", effects);
        Assert.Contains("Gain On Miss: +1 Stress (25%)", effects);
    }

    [Fact]
    public void NeighborDirectionLabelsUseNativeLocalizationAndKeepOtherEffects()
    {
        string effects = Data.Effects("trinket_hero_lep_a_simple_flower", out bool complete,
            key => key == "actor_trigger_target_type_neighbor_back_1" ? "Allie derriere:" : null);
        Assert.True(complete);
        Assert.Contains("Turn End: Allie derriere: Add 1 Positive Token", effects);
        Assert.Contains("Gain On CRIT: Melee Skills: +1 Stress (25%)", effects);
        effects = Data.Effects("trinket_city_hastening_history", out complete,
            key => key == "actor_trigger_target_type_neighbor_front_1" ? "Allie devant:" : null);
        Assert.True(complete);
        Assert.Contains("Turn End: Allie devant: Speed (33%)", effects);
        Assert.Contains("Stun (10%) when Speed is 2 or less", effects);
    }

    [Fact]
    public void TokenConversionsRetainBothTokensAndTheWholeTeamTrigger()
    {
        string effects = Data.Effects("trinket_forest_boss_footmans_grog", out bool complete);
        Assert.True(complete);
        Assert.Equal("Each Hero on Turn Start: Convert Vulnerable to Block\nEach Hero on Turn Start: Convert Weak to Strength", effects);
        Assert.DoesNotContain("99", effects);
    }

    [Fact]
    public void TokenConversionsKeepTurnPhasesAndBleedPenaltyWithoutResolvingBlockedVisibility()
    {
        string effects = Data.Effects("trinket_curio_blood_smeared_calculations", out bool complete);
        Assert.False(complete);
        Assert.Contains("Turn Start: Convert Dodge to Crit", effects);
        Assert.Contains("Turn Start: Convert Block to Strength", effects);
        Assert.Contains("Turn End: Convert Crit to Dodge+", effects);
        Assert.Contains("Turn End: Convert Strength to Block+", effects);
        Assert.Contains("Turn End: Bleed 1 (3 Turns)", effects);
        effects = Data.Effects("trinket_cave_goading_gargoyle", out complete);
        Assert.False(complete);
        Assert.DoesNotContain("Convert", effects);
        Assert.Contains("+4 Speed when Flame is below 50", effects);
    }

    [Fact]
    public void TokenConversionsUseNativeLocalizationAndWithholdMalformedConversions()
    {
        string effects = Data.Effects("trinket_forest_boss_footmans_grog", out bool complete,
            key => key == "effect_tooltip_token_convert_amount" ? "Transformer {0} en {1}" : null);
        Assert.True(complete);
        Assert.Contains("Each Hero on Turn Start: Transformer Weak en Strength", effects);
        effects = Data.Effects("trinket_curio_blood_smeared_calculations", out complete,
            key => key == "effect_tooltip_token_convert_amount" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Convert", effects);
        Assert.Equal("Turn End: Bleed 1 (3 Turns)", effects);
    }

    [Fact]
    public void ResistanceTriggersRetainBurnAndBlightRequirementsAndTheirRewards()
    {
        string effects = Data.Effects("trinket_city_boss_charred_litany", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Burn: -2 Stress", effects);
        Assert.Contains("Apply to Attacker When Hit: Burn 1 (3 Turns)", effects);
        effects = Data.Effects("trinket_farm_boss_kitchen_knives", out _);
        Assert.Contains("Gain On Resist: Blight: Extra Action (20%)", effects);
        Assert.Contains("+15% CRIT when target Blight", effects);
        effects = Data.Effects("trinket_hero_run_knitted_blanket", out _);
        Assert.Contains("Gain On Resist: Burn: Stealth", effects);
        Assert.Contains("When Stress Damaged: Burn 1 (3 Turns) (15%)", effects);
    }

    [Fact]
    public void ResistanceTriggersRetainEveryDotDamagePenaltyAndMoveResistanceUnits()
    {
        string effects = Data.Effects("trinket_cultist_hardened_heart", out bool complete);
        Assert.True(complete);
        Assert.Contains("+200% Bleed RES", effects);
        Assert.Contains("Gain On Resist: Bleed: 2 DMG", effects);
        Assert.Contains("Gain On Resist: Blight: 2 DMG", effects);
        Assert.Contains("Gain On Resist: Burn: 2 DMG", effects);
        effects = Data.Effects("trinket_hoarder_skeletons_sight", out complete);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Bleed: -2 Stress", effects);
        Assert.Contains("Gain On Resist: Blight: -2 Stress", effects);
        Assert.Contains("Gain On Resist: Burn: -2 Stress", effects);
        effects = Data.Effects("trinket_coast_boss_sodden_sweater", out complete);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Move RES: -1 Stress", effects);
        Assert.Contains("+50% Bleed RES", effects);
    }

    [Fact]
    public void ResistanceQualifiersUseNativeLocalizationAndWithholdMalformedConditions()
    {
        string effects = Data.Effects("trinket_city_boss_charred_litany", out bool complete,
            key => key == "resist_tag_burn" ? "Brulure" : null);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Brulure: -2 Stress", effects);
        effects = Data.Effects("trinket_city_boss_charred_litany", out complete,
            key => key == "effect_tooltip_condition_resist_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Gain On Resist:", effects);
        Assert.Contains("Apply to Attacker When Hit: Burn 1 (3 Turns)", effects);
        Assert.Contains("+20% Stun RES Piercing when target Burn", effects);
    }

    [Fact]
    public void HealCritMetadataRetainsEveryResistanceHealAndExistingStats()
    {
        string effects = Data.Effects("trinket_hoarder_fates_foreteller", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Bleed: Heal 20%", effects);
        Assert.Contains("Gain On Resist: Blight: Heal 20%", effects);
        Assert.Contains("Gain On Resist: Burn: Heal 20%", effects);
        Assert.DoesNotContain("5%", effects);
        effects = Data.Effects("trinket_coast_boss_sodden_sweater", out complete);
        Assert.True(complete);
        Assert.Contains("Gain On Resist: Bleed: Heal 10%", effects);
        Assert.Contains("Gain On Resist: Move RES: -1 Stress", effects);
        Assert.Contains("+50% Bleed RES", effects);
        Assert.Contains("+50% Move RES", effects);
    }

    [Fact]
    public void HealCritMetadataKeepsBaseHealProcChanceAndNativeLocalization()
    {
        string effects = Data.Effects("trinket_farm_boss_ghastly_gruel", out bool complete);
        Assert.False(complete);
        Assert.Equal("Each Hero on Round End: Heal 2 (33%)", effects);
        effects = Data.Effects("trinket_farm_boss_ghastly_gruel", out complete,
            key => key == "effect_tooltip_health_heal_amount" ? "Heal {0} HP" : null);
        Assert.False(complete);
        Assert.Equal("Each Hero on Round End: Heal 2 HP (33%)", effects);
        Assert.DoesNotContain("5%", effects);
        Assert.DoesNotContain("Heal 3", effects);
    }

    [Fact]
    public void MalformedHealingTemplateWithholdsHealsAndRetainsOtherResistEffects()
    {
        string effects = Data.Effects("trinket_hoarder_fates_foreteller", out bool complete,
            key => key == "effect_tooltip_health_heal_percent" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.Null(effects);
        effects = Data.Effects("trinket_coast_boss_sodden_sweater", out complete,
            key => key == "effect_tooltip_health_heal_percent" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Gain On Resist: Bleed", effects);
        Assert.Contains("Gain On Resist: Move RES: -1 Stress", effects);
        Assert.Contains("+50% Bleed RES", effects);
    }

    [Fact]
    public void AuthoredTrinketIconsUseCanonicalTokenAndHealingNames()
    {
        string effects = Data.Effects("trinket_hero_lep_a_simple_flower", out bool complete);
        Assert.True(complete);
        Assert.Contains("Gain On Stun/Daze/Move Resist: Block+, Heal 10%", effects);
        Assert.DoesNotContain("Deflect", effects);
        Assert.DoesNotContain("gold", effects);
        effects = Data.Effects("trinket_hoarder_inert_indicia", out _);
        Assert.Contains("Gain On Stun/Daze Resist: Add 2 Positive Tokens, Regen 2", effects);
    }

    [Fact]
    public void BothColdAndNativePlainDescriptionsResolveLocalizedTokenNames()
    {
        string effects = Data.Effects("trinket_hero_lep_a_simple_flower", out bool complete,
            key => key == "token_name_block_plus" ? "Parade+" : key == "token_name_daze" ? "Etourdi" : null);
        Assert.True(complete);
        Assert.Contains("Gain On Stun/Etourdi/Move Resist: Parade+, Heal 10%", effects);
        Assert.Equal("Gain Parade+, Etourdi", TrinketDescriptions.Plain(
            "Gain <sprite name={q}token_deflect{q}>, <sprite name={q}token_daze_gold{q}>",
            key => key == "token_name_block_plus" ? "Parade+" : key == "token_name_daze" ? "Etourdi" : null));
    }

    [Fact]
    public void PlainGlyphIdsWithPlusAndHyphenRetainTokensQuantitiesAndNumericRanges()
    {
        Assert.Equal("Gain Dodge+ x2, Blind; Daze/Block+: Heal 10%, 0.5-1.5", TrinketDescriptions.Plain(
            "Gain<sprite name={q}token_dodge+{q}>x2, <sprite name={q}token_blind-line{q}>; " +
            "<sprite name={q}token_daze_gold{q}>/<sprite name={q}token_deflect{q}>: " +
            "<sprite name={q}icon_healthup{q}>10%, 0.5-1.5"));
    }

    [Fact]
    public void NestedTrinketBuffRetainsItsSkillTriggerMovementAndLifetime()
    {
        string effects = Data.Effects("trinket_hero_jes_buskers_haul", out bool complete);
        Assert.True(complete);
        Assert.Contains("Target: Battle Ballad: Turn Start: Forward 1 (1 Turn)", effects);
        Assert.Contains("Target: Play Out: Remove 1 Negative Token", effects);
        Assert.DoesNotContain("Encore", effects);
    }

    [Fact]
    public void MalformedNestedBuffDurationWithholdsThatBuffAndRetainsOtherSkillEffects()
    {
        string effects = Data.Effects("trinket_hero_jes_buskers_haul", out bool complete,
            key => key == "duration_display_type_turn" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Battle Ballad", effects);
        Assert.Contains("Target: Play Out: Remove 1 Negative Token", effects);
        Assert.Contains("Turn End: +1 Stress (25%) when Relics in inventory is below 25", effects);
    }

    [Fact]
    public void CyclicNestedBuffsAreWithheldWithoutUnboundedDescriptionRecursion()
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_cycle_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), string.Join("\n", new[]
            {
                "element_start,cyclic_item,Item", "element_end",
                "element_start,cyclic_item,ActorDataEffects", "target_effects,apply_cycle", "element_end",
                "element_start,apply_cycle,Effect", "m_Chance,1", "buffs,cyclic_buff", "element_end",
                "element_start,cyclic_buff,Buff", "m_DurationType,every_turn_end", "m_DurationAmount,1", "element_end",
                "element_start,cyclic_buff,ActorDataEffects", "turn_start_effects,apply_cycle", "element_end",
            }));
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"), string.Join("\n", new[]
            {
                "effect_tooltip_skill_effect_target=Target:", "effect_tooltip_skill_effect_turn_start=Turn Start:",
                "effect_tooltip_buff={0}", "duration_display_type_turn=1 Turn", "skill_effect_duration_label={0} ({1})",
            }));
            string effects = TrinketDescriptions.Load(fixture).Effects("cyclic_item", out bool complete);
            Assert.False(complete);
            Assert.Null(effects);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    [Fact]
    public void InventoryAmountRequirementRetainsLowRelicsStressChanceAndOtherSkillEffects()
    {
        string effects = Data.Effects("trinket_hero_jes_buskers_haul", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn End: +1 Stress (25%) when Relics in inventory is below 25", effects);
        Assert.Contains("Target: Battle Ballad: Turn Start: Forward 1 (1 Turn)", effects);
        Assert.Contains("Target: Play Out: Remove 1 Negative Token", effects);
        Assert.DoesNotContain("Gold", effects);
    }

    [Fact]
    public void InventoryCategoryThresholdRestoresCleansingClaspWithoutDroppingItsRequirement()
    {
        string effects = Data.Effects("trinket_antiq_cleansing_clasp", out bool complete);
        Assert.True(complete);
        Assert.Contains("Turn Start: Remove 1 Negative Token when Baubles in inventory is above 25", effects);
        Assert.DoesNotContain("above 25%", effects);
        Assert.DoesNotContain("Relics", effects);
    }

    [Fact]
    public void InventoryCategoryThresholdsKeepSeparateCurrencyBonuses()
    {
        string effects = Data.Effects("trinket_antiq_celebrated_chalice", out bool complete);
        Assert.True(complete);
        Assert.Contains("+4 Speed when Relics in inventory is above 50", effects);
        Assert.Contains("+10% CRIT when Baubles in inventory is above 50", effects);
        effects = Data.Effects("trinket_antiq_clarifying_carcanet", out complete);
        Assert.True(complete);
        Assert.Contains("+20% Max HP when Relics in inventory is above 75", effects);
        Assert.Contains("+20% DMG when Baubles in inventory is above 75", effects);
        effects = Data.Effects("trinket_antiq_shimmering_crown", out complete);
        Assert.True(complete);
        Assert.Contains("Combat Start: Heal 100% when Relics in inventory is above 100", effects);
        Assert.Contains("Combat Start: -10 Stress when Baubles in inventory is above 100", effects);
    }

    [Fact]
    public void InventoryCategoryUsesNativeNamesAndWithholdsMalformedRequirements()
    {
        string effects = Data.Effects("trinket_antiq_cleansing_clasp", out bool complete,
            key => key == "item_tag_faction" ? "Babioles" : null);
        Assert.True(complete);
        Assert.Contains("Turn Start: Remove 1 Negative Token when Babioles in inventory is above 25", effects);
        foreach (string template in new[] { "invalid {9}", "{0} {1} {2}" })
        {
            effects = Data.Effects("trinket_antiq_cleansing_clasp", out complete,
                key => key == "effect_tooltip_condition_item_tag_amount" ? template : null);
            Assert.False(complete);
            Assert.Null(effects);
            effects = Data.Effects("trinket_antiq_celebrated_chalice", out complete,
                key => key == "effect_tooltip_condition_item_tag_amount" ? template : null);
            Assert.False(complete);
            Assert.DoesNotContain("CRIT", effects);
            Assert.Contains("+4 Speed when Relics in inventory is above 50", effects);
        }
    }

    [Fact]
    public void InventoryAmountThresholdsUseNativeRelicsUnitsForStatsAndHealing()
    {
        string effects = Data.Effects("trinket_antiq_celebrated_chalice", out _);
        Assert.Contains("+4 Speed when Relics in inventory is above 50", effects);
        effects = Data.Effects("trinket_antiq_clarifying_carcanet", out _);
        Assert.Contains("+20% Max HP when Relics in inventory is above 75", effects);
        effects = Data.Effects("trinket_antiq_shimmering_crown", out _);
        Assert.Contains("Combat Start: Heal 100% when Relics in inventory is above 100", effects);
        Assert.DoesNotContain("above 100%", effects);
    }

    [Fact]
    public void InventoryAmountRequirementUsesNativeNamesAndWithholdsMalformedThresholdText()
    {
        string effects = Data.Effects("trinket_hero_jes_buskers_haul", out _,
            key => key == "item_name_gold" ? "Reliques" : null);
        Assert.Contains("Turn End: +1 Stress (25%) when Reliques in inventory is below 25", effects);
        effects = Data.Effects("trinket_hero_jes_buskers_haul", out bool complete,
            key => key == "effect_tooltip_condition_item_amount" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Stress", effects);
        Assert.Contains("Battle Ballad: Turn Start: Forward 1 (1 Turn)", effects);
        Assert.Contains("Play Out: Remove 1 Negative Token", effects);
    }

    [Fact]
    public void StealEffectsRetainNamedSkillsBonusesAndPenalties()
    {
        string effects = Data.Effects("trinket_hero_flg_emancipation", out bool complete);
        Assert.True(complete);
        Assert.Contains("Target: Punish: Steal Positive Token", effects);
        Assert.Contains("+25% DMG when self HP is below 20%", effects);
        Assert.Contains("-20% Bleed RES", effects);
        effects = Data.Effects("trinket_hero_hwy_cursed_coin", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Highway Robbery: Steal Regen", effects);
        Assert.Contains("-10% CRIT when Relics in inventory is above 50", effects);
        effects = Data.Effects("trinket_hero_flg_searing_scripture", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Other Ally: Steal Negative Token", effects);
        Assert.Contains("Target: Other Ally: Steal Combo Token", effects);
        Assert.Contains("-20% Burn RES", effects);
    }

    [Fact]
    public void TokenCopyRetainsCakedPalettePositiveGainAndNegativeRisk()
    {
        string effects = Data.Effects("trinket_curio_caked_palette", out bool complete);
        Assert.True(complete);
        Assert.Contains("Apply On Hit: Copy Positive Token", effects);
        Assert.Contains("Apply On Hit: Copy All Negative Tokens (5%)", effects);
        Assert.DoesNotContain("Steal", effects);
    }

    [Fact]
    public void TokenCopyUsesNativeCategoryAndActionLocalization()
    {
        string effects = Data.Effects("trinket_curio_caked_palette", out bool complete,
            key => key == "effect_tooltip_token_copy_tag" ? "Copier {0}"
                : key == "effect_tooltip_token_copy_all_tag" ? "Copier tous {0}"
                : key == "token_tag_pos_copy_steal" ? "Positif" : key == "token_tag_negative" ? "Negatif" : null);
        Assert.True(complete);
        Assert.Contains("Apply On Hit: Copier Positif", effects);
        Assert.Contains("Apply On Hit: Copier tous Negatif (5%)", effects);
    }

    [Fact]
    public void MalformedTokenCopyTemplatesWithholdOnlyTheirUnknownOutcome()
    {
        string effects = Data.Effects("trinket_curio_caked_palette", out bool complete,
            key => key == "effect_tooltip_token_copy_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Positive", effects);
        Assert.Contains("Copy All Negative Tokens (5%)", effects);
        effects = Data.Effects("trinket_curio_caked_palette", out complete,
            key => key == "effect_tooltip_token_copy_all_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Negative", effects);
        Assert.Contains("Copy Positive Token", effects);
    }

    [Theory]
    [InlineData("m_TokenCopyAmount", "0", null)]
    [InlineData("m_TokenCopyAmount", "1.5", null)]
    [InlineData("m_TokenCopyAmountRange", "1", null)]
    [InlineData("m_TokenCopyTags", "unknown_category", null)]
    [InlineData("m_UnknownSelector", "TARGET", null)]
    [InlineData("m_TokenCopyAmount", "2", "Target: Copy 2 Positive Tokens")]
    public void TokenCopyValidatesQuantitiesAndSelectors(string field, string value, string expected)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_copy_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            var fields = new System.Collections.Generic.Dictionary<string, string>
            { ["m_TokenCopyTags"] = "positive", ["m_TokenCopyAmount"] = "1" };
            fields[field] = value;
            string csv = string.Join("\n", new[]
            {
                "element_start,copy_item,Item", "element_end",
                "element_start,copy_item,ActorDataEffects", "target_effects,copy_effect", "element_end",
                "element_start,copy_effect,Effect", "m_Chance,1",
            });
            foreach (var row in fields) csv += "\n" + row.Key + "," + row.Value;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_skill_effect_target=Target:\neffect_tooltip_token_copy_tag=Copy {0} Token\neffect_tooltip_token_copy_tag+plural=Copy {0} {1} Tokens\ntoken_tag_positive=Positive");
            Assert.Equal(expected, TrinketDescriptions.Load(fixture).Effects("copy_item", out bool complete));
            Assert.Equal(expected != null, complete);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    [Fact]
    public void StealEffectsUseNativeCategoryAndDotLocalization()
    {
        string effects = Data.Effects("trinket_hero_flg_emancipation", out bool complete,
            key => key == "effect_tooltip_token_steal_tag" ? "Voler {0}" : key == "token_tag_pos_copy_steal" ? "Positif" : null);
        Assert.True(complete);
        Assert.Contains("Target: Punish: Voler Positif", effects);
        effects = Data.Effects("trinket_hero_hwy_cursed_coin", out _,
            key => key == "effect_tooltip_dot_steal_tag" ? "Voler {0}" : key == "dot_hot" ? "Regeneration" : null);
        Assert.Contains("Target: Highway Robbery: Voler Regeneration", effects);
    }

    [Fact]
    public void MalformedStealTemplatesWithholdTransfersAndKeepOtherEffects()
    {
        string effects = Data.Effects("trinket_hero_flg_emancipation", out bool complete,
            key => key == "effect_tooltip_token_steal_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Punish", effects);
        Assert.Contains("+25% DMG when self HP is below 20%", effects);
        Assert.Contains("-20% Bleed RES", effects);
        effects = Data.Effects("trinket_hero_hwy_cursed_coin", out complete,
            key => key == "effect_tooltip_dot_steal_tag" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.Contains("-10% CRIT when Relics in inventory is above 50", effects);
        Assert.Contains("+5% DMG per Positive Token", effects);
    }

    [Fact]
    public void NativeAuthoredEffectBodiesRetainRestrictedChoicesAndTheirOwnChance()
    {
        string effects = Data.Effects("trinket_hero_pd_annotated_textbook", out bool complete);
        Assert.True(complete);
        Assert.Contains("Other Ally: Invert 1 Negative Token (65%) or Other Ally: +1 Stress (5%)", effects);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(effects, @"\(65%\)").Count);
        Assert.Contains("+50% Healing Given from Skills when Restorative item is equipped", effects);
        Assert.Contains("Turn Start: Vulnerable (33%) when first in turn order", effects);
        effects = Data.Effects("trinket_cave_rousing_ringer", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Other Ally: Remove Daze Stun", effects);
        Assert.Contains("Apply to Attacker When Hit: Daze (66%) when Flame is below 50", effects);
        // The earlier visibility gap is still withheld, even when other authored bodies are recovered.
        Assert.Null(Data.Effects("trinket_curio_anatomical_map", out complete));
        Assert.False(complete);
    }

    [Fact]
    public void NativeAuthoredEffectBodyUsesLiveTextWithoutAppendingItsChanceAgain()
    {
        string effects = Data.Effects("trinket_hero_pd_annotated_textbook", out bool complete,
            key => key == "effect_skill_invert_negative_tokens_annotated_textbook_override"
                ? "Inverser 1 jeton negatif (65%)" : null);
        Assert.True(complete);
        Assert.Contains("Other Ally: Inverser 1 jeton negatif (65%) or Other Ally: +1 Stress (5%)", effects);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(effects, @"\(65%\)").Count);
    }

    [Fact]
    public void MalformedAuthoredBodyWithholdsWholeChoiceAndKeepsSeparateRequirements()
    {
        string effects = Data.Effects("trinket_hero_pd_annotated_textbook", out bool complete,
            key => key == "effect_skill_invert_negative_tokens_annotated_textbook_override" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Invert", effects);
        Assert.DoesNotContain("+1 Stress", effects);
        Assert.Contains("Restorative item is equipped", effects);
        Assert.Contains("Vulnerable (33%) when first in turn order", effects);
        effects = Data.Effects("trinket_cave_rousing_ringer", out complete,
            key => key == "effect_tooltip_condition_other_ally" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Remove", effects);
        Assert.Contains("Flame is below 50", effects);
    }

    [Fact]
    public void OtherAllyRequirementsKeepCleansingTransfersChancesAndPenalties()
    {
        string effects = Data.Effects("trinket_collector_junias_head", out bool complete);
        Assert.True(complete);
        Assert.Contains("Target: Other Ally: Remove 1 Negative Token", effects);
        Assert.Contains("Target: Other Ally: Remove Combo", effects);
        Assert.Contains("Turn Start: +1 Stress (15%)", effects);
        effects = Data.Effects("trinket_hero_jes_buskers_haul", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Other Ally: Dodge (33%)", effects);
        Assert.Contains("+1 Stress (25%) when Relics in inventory is below 25", effects);
        Assert.Contains("Battle Ballad: Turn Start: Forward 1 (1 Turn)", effects);
        effects = Data.Effects("trinket_ancestors_mustache_cream", out complete);
        Assert.True(complete);
        Assert.Contains("Target: Other Ally: Extra Action (50%)", effects);
        Assert.Contains("Ineffective against Bosses", effects);
        Assert.Contains("+100% Horror Received", effects);
    }

    [Fact]
    public void OtherAllyRequirementsUseLocalizedTemplateAndWithholdMalformedQualifiers()
    {
        string effects = Data.Effects("trinket_collector_junias_head", out bool complete,
            key => key == "effect_tooltip_condition_other_ally" ? "Autre allie: {0}" : null);
        Assert.True(complete);
        Assert.Contains("Target: Autre allie: Remove 1 Negative Token", effects);
        effects = Data.Effects("trinket_collector_junias_head", out complete,
            key => key == "effect_tooltip_condition_other_ally" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Remove", effects);
        Assert.Contains("Turn Start: +1 Stress (15%)", effects);
    }

    [Fact]
    public void MissingPartyScalingRetainsBothThrillingTabletBonuses()
    {
        string effects = Data.Effects("trinket_general_thrilling_tablet", out bool complete);
        Assert.True(complete);
        Assert.Contains("+100% DMG per Missing Ally", effects);
        Assert.Contains("+100% Max HP per Missing Ally", effects);
        Assert.Equal(2, effects.Split('\n').Length);
    }

    [Fact]
    public void MissingPartyScalingUsesNativeLocalizationAndRequiresItsEffectBody()
    {
        string effects = Data.Effects("trinket_general_thrilling_tablet", out bool complete,
            key => key == "tag_ally" ? "Allie" : key == "effect_tooltip_condition_tag_inverse" ? "{1} par {0} absent" : null);
        Assert.True(complete);
        Assert.Contains("+100% DMG par Allie absent", effects);
        Assert.Contains("+100% Max HP par Allie absent", effects);
        foreach (string template in new[] { "invalid {9}", "Missing {0}" })
        {
            effects = Data.Effects("trinket_general_thrilling_tablet", out complete,
                key => key == "effect_tooltip_condition_tag_inverse" ? template : null);
            Assert.False(complete);
            Assert.Null(effects);
        }
    }

    [Theory]
    [InlineData("m_ConditionActorType", "TARGET")]
    [InlineData("m_ConditionNumber", "2")]
    [InlineData("m_ConditionNumberType", "EQUAL")]
    [InlineData("m_IsInverse", "False")]
    [InlineData("m_SourceConditionActorType", "PERFORMER")]
    [InlineData("m_ActorIsNotSource", "True")]
    [InlineData("m_UnknownRestriction", "True")]
    public void UnsupportedPartyCountsNeverBecomeUnconditionalBonuses(string field, string value)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_party_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            var condition = new System.Collections.Generic.Dictionary<string, string>
            {
                ["m_ConditionType"] = "tag", ["m_ConditionString"] = "ally", ["m_ConditionActorType"] = "PARTY",
                ["m_ConditionNumberType"] = "MULTIPLE", ["m_ConditionNumber"] = "1", ["m_IsInverse"] = "True",
            };
            condition[field] = value;
            string csv = string.Join("\n", new[]
            {
                "element_start,party_item,Item", "element_end",
                "element_start,party_item,ActorDataExternalBuffs", "buffs,party_buff", "element_end",
                "element_start,party_buff,Buff", "m_ConditionId,party_condition", "element_end",
                "element_start,party_buff,ActorDataStats", "key_map,crit_chance", "add_stats,0.1", "element_end",
                "element_start,party_condition,Condition",
            });
            foreach (var row in condition) csv += "\n" + row.Key + "," + row.Value;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "tag_ally=Ally\neffect_tooltip_condition_tag_inverse={1} per Missing {0}\nactor_stat_type_formatted_crit_chance={0} CRIT");
            Assert.Null(TrinketDescriptions.Load(fixture).Effects("party_item", out bool complete));
            Assert.False(complete);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    [Theory]
    [InlineData("m_SourceConditionActorType", "TARGET")]
    [InlineData("m_ConditionActorType", "PERFORMER")]
    [InlineData("m_ActorIsNotSource", "False")]
    [InlineData("m_ConditionNumber", "2")]
    [InlineData("m_IsInverse", "True")]
    [InlineData("m_IsVisible", "False")]
    [InlineData("m_UnknownRestriction", "True")]
    public void UnsupportedAllyRestrictionsNeverBecomeUnconditionalExtraActions(string field, string value)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_trinket_ally_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "Excel"));
            Directory.CreateDirectory(Path.Combine(fixture, "Localization", "Sources"));
            var condition = new System.Collections.Generic.Dictionary<string, string>
            {
                ["m_ConditionType"] = "tag", ["m_ConditionString"] = "ally", ["m_ConditionActorType"] = "TARGET",
                ["m_ConditionNumberType"] = "GREATER_THAN_OR_EQUAL", ["m_ConditionNumber"] = "1",
                ["m_SourceConditionActorType"] = "PERFORMER", ["m_ActorIsNotSource"] = "True",
            };
            condition[field] = value;
            string csv = string.Join("\n", new[]
            {
                "element_start,ally_item,Item", "element_end",
                "element_start,ally_item,ActorDataEffects", "target_effects,ally_action", "element_end",
                "element_start,ally_action,Effect", "m_Chance,1", "m_AddTurn,1", "all_conditions,ally_condition", "element_end",
                "element_start,ally_condition,Condition",
            });
            foreach (var row in condition) csv += "\n" + row.Key + "," + row.Value;
            File.WriteAllText(Path.Combine(fixture, "Excel", "trinkets_data_export.Group.csv"), csv + "\nelement_end");
            File.WriteAllText(Path.Combine(fixture, "Localization", "Sources", "combat.txt"),
                "effect_tooltip_skill_effect_target=Target:\neffect_tooltip_add_turn=Extra Action");
            Assert.Null(TrinketDescriptions.Load(fixture).Effects("ally_item", out bool complete));
            Assert.False(complete);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    [Fact]
    public void TokenCategoryMultipliersRetainPositiveAndNegativeStacksAndPenalties()
    {
        string effects = Data.Effects("trinket_hero_hwy_cursed_coin", out bool complete);
        Assert.True(complete);
        Assert.Contains("+5% DMG per Positive Token", effects);
        Assert.Contains("Target: Highway Robbery: Steal Regen", effects);
        Assert.Contains("-10% CRIT when Relics in inventory is above 50", effects);
        effects = Data.Effects("trinket_cultist_selfish_motivation", out complete);
        Assert.True(complete);
        Assert.Contains("+50% DMG per Negative Token", effects);
        Assert.Contains("Turn Start: Blind or Immobilize or Taunt or Vulnerable", effects);
    }

    [Fact]
    public void TokenCategoryConditionsRetainTargetPresenceAndSelfThreshold()
    {
        string effects = Data.Effects("trinket_hero_run_knitted_blanket", out bool complete);
        Assert.True(complete);
        Assert.Contains("+1 Burn Dealt when target has Negative Token", effects);
        Assert.Contains("Gain On Resist: Burn: Stealth", effects);
        Assert.Contains("When Stress Damaged: Burn 1 (3 Turns) (15%)", effects);
        effects = Data.Effects("trinket_hero_occ_scalded_skull", out complete);
        Assert.True(complete);
        Assert.Contains("Random Ally on Turn Start: Burn 1 (3 Turns) (33%)", effects);
        Assert.Contains("2 or more", effects);
    }

    [Fact]
    public void TokenCategoryConditionsUseNativeLocalizationAndWithholdMalformedMultipliers()
    {
        string effects = Data.Effects("trinket_hero_hwy_cursed_coin", out bool complete,
            key => key == "token_positive" ? "Positive Stack" : null);
        Assert.True(complete);
        Assert.Contains("+5% DMG per Positive Stack", effects);
        effects = Data.Effects("trinket_hero_hwy_cursed_coin", out complete,
            key => key == "effect_tooltip_condition_token_tag_amount_multiple" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("+5% DMG", effects);
        Assert.Contains("Target: Highway Robbery: Steal Regen", effects);
        Assert.Contains("-10% CRIT when Relics in inventory is above 50", effects);
    }

    [Fact]
    public void CategoryThresholdTextUsesUncheckedPowerNameAndOneComparisonVerb()
    {
        string effects = Data.Effects("trinket_hero_occ_scalded_skull", out bool complete);
        Assert.True(complete);
        Assert.Contains("Random Ally on Turn Start: Burn 1 (3 Turns) (33%) when Unchecked Power is 2 or more", effects);
        Assert.DoesNotContain("has is", effects);
        Assert.DoesNotContain("Uc power", effects);
        Assert.Contains("Target: Burn 1 (3 Turns) when target has Combo", effects);
    }

    [Fact]
    public void NativePlainThresholdTextUsesTheSameCanonicalNameAndKeepsQuantities()
    {
        Assert.Equal("Unchecked Power is 2 or more", TrinketDescriptions.Plain(
            "<sprite name={q}token_uc_power{q}> has is 2 or more"));
        Assert.Equal("Puissance is 2 or more", TrinketDescriptions.Plain(
            "<sprite name={q}token_uc_power{q}> has is 2 or more",
            key => key == "token_name_unchecked_power" ? "<color=#{notable}>Puissance</color>" : null));
        Assert.Equal("target has Combo", TrinketDescriptions.Plain("target has <sprite name={q}token_combo{q}>"));
    }

    [Fact]
    public void CategoryThresholdCleanupPreservesExplicitLocalizedTemplateAndWithholding()
    {
        string effects = Data.Effects("trinket_hero_occ_scalded_skull", out bool complete,
            key => key == "effect_tooltip_condition_token_tag_amount" ? "Bonus {3} si {0}{1}{2}"
                : key == "comparison_greater_than_or_equal_label" ? " au moins {0}"
                : key == "token_name_unchecked_power" ? "Puissance" : null);
        Assert.True(complete);
        Assert.Contains("Bonus Burn 1 (3 Turns) (33%) si Puissance au moins 2", effects);
        effects = Data.Effects("trinket_hero_occ_scalded_skull", out complete,
            key => key == "effect_tooltip_condition_token_tag_amount" ? "invalid {9}" : null);
        Assert.False(complete);
        Assert.DoesNotContain("Random Ally on Turn Start:", effects);
        Assert.Contains("Adjacent Allies on Turn Start: Add 1 Positive Token when self Burn", effects);
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
