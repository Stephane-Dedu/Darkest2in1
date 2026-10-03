using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Library;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1's camp and town buffs (+20% damage, +7% crit, +25% blight resist, a hangover's -10% accuracy ...) as DD2's
/// own single-stat buffs, put on the hero when a fight starts and taken off when it ends. DD1 counts how many battles
/// each lasts (see Crawl.FightBuffs). DD2 has no accuracy or dodge: accuracy becomes a little crit, dodge and
/// protection become damage taken.
/// </summary>
internal static class FightBuffs
{
    private const string Source = "dd3_fight";

    /// <summary>DD2 has no "stress taken" stat: during our fights, stress on these actors is scaled (see the patch below).</summary>
    internal static readonly Dictionary<uint, float> StressTakenMultiplier = new();

    /// <summary>The DD2 buff ids standing in for one DD1 buff (empty when DD2 has nothing like it).</summary>
    public static IEnumerable<string> Dd2For(Dd1Buff b)
    {
        float a = b.Amount;
        switch (b.Stat, b.Sub)
        {
            case ("combat_stat_multiply", "damage_low"):      // DD1 always pairs low and high: count it once
                if (a >= 0.22f) yield return "story_combat_dmg_25pct_buff";
                else if (a >= 0.17f) yield return "trinket_tiered_greater_sharpness_charm_01";
                else if (a >= 0.12f) yield return "trinket_tiered_sharpness_charm_01";
                else if (a > 0) yield return "memory_buff_04";
                else if (a <= -0.18f) yield return "quirk_creeping_cough_dmg_debuff";
                else if (a < 0) yield return "quirk_sprained_wrist_dmg_debuff";
                break;
            case ("combat_stat_add", "crit_chance"):
                if (a >= 0.09f) yield return "trinket_tiered_greater_heartseeker_01";
                else if (a >= 0.06f) { yield return "memory_buff_03"; yield return "trinket_tiered_minor_heartseeker_01"; }
                else if (a >= 0.04f) yield return "memory_buff_03";
                else if (a > 0) yield return "trinket_tiered_minor_heartseeker_01";
                else if (a <= -0.04f) yield return "quirk_flawed_release_crit_debuff";
                else if (a < 0) yield return "trinket_tiered_minor_hale_draught_02";
                break;
            case ("combat_stat_add", "attack_rating"):
                if (a > 0) yield return "trinket_tiered_minor_heartseeker_01";
                else if (a < 0) yield return "trinket_tiered_minor_hale_draught_02";
                break;
            case ("combat_stat_add", "defense_rating"):
            case ("combat_stat_add", "protection_rating"):
                if (a > 0) yield return "trinket_ancestors_coat_01";             // -15% damage taken
                else if (a < 0) yield return "occ_unchecked_power_p2_hp_debuff"; // +10% damage taken
                break;
            case ("combat_stat_add", "speed_rating"):
                if (a >= 4) { yield return "trinket_tiered_wolfsblood_01"; yield return "quirk_lightning_reflexes"; }
                else if (a >= 2.5f) yield return "trinket_tiered_wolfsblood_01";
                else if (a >= 1.5f) yield return "quirk_lightning_reflexes";
                else if (a > 0) yield return "memory_buff_02";
                else if (a <= -2.5f) yield return "quirk_stiff_knees";
                else if (a <= -1.5f) yield return "quirk_blundering_neg_spd";
                else if (a < 0) yield return "spd_debuff_inevitable_end";
                break;
            case ("combat_stat_multiply", "max_hp"):
                if (a >= 0.18f) yield return "quirk_iron_constitution_buff";
                else if (a >= 0.12f) yield return "trinket_tiered_hale_draught_01";
                else if (a > 0) yield return "memory_buff_01";
                else if (a <= -0.18f) yield return "quirk_broken_max_hp_20pct_penalty";
                else if (a <= -0.12f) yield return "quirk_fragile_maxhp_debuff";
                else if (a < 0) yield return "max_health_debuff_10pct";
                break;
            case ("resistance", var sub):
                bool up = a > 0;
                string id = sub switch
                {
                    "poison" => up ? "banter_buff_blightresist" : "banter_debuff_blightresist",
                    "bleed" => up ? "banter_buff_tactician_cave" : "banter_debuff_bleedresist",
                    "stun" => up ? "banter_buff_tactician_forest" : null,
                    "move" => up ? "banter_buff_tactician_shroud" : null,
                    "debuff" => up ? "quirk_brasstacks_debuff_resist" : "eyes_debuff_res_debuff",
                    _ => null,
                };
                if (id != null) yield return id;
                break;
            case ("hp_heal_received_percent", _):
                yield return a > 0 ? "lep_withstand_p2_healing_given" : "eyes_healing_received_debuff";
                break;
        }
    }

    /// <summary>Put each hero's DD1 buffs on their DD2 actor for this fight.</summary>
    public static void Apply(IEnumerable<(uint Guid, Dd1Buff Buff)> buffs)
    {
        var library = SingletonMonoBehaviour<Library<string, BuffDefinition>>.Instance;
        if (library == null) return;
        StressTakenMultiplier.Clear();
        foreach (var group in buffs.GroupBy(b => b.Guid))
        {
            float stress = 1f + group.Where(b => b.Buff.Stat == "stress_dmg_received_percent").Sum(b => b.Buff.Amount);
            if (stress != 1f) StressTakenMultiplier[group.Key] = System.Math.Max(0f, stress);
            var actor = Dd2Api.Actor(group.Key);
            if (actor?.BuffContainer == null) continue;
            var applied = new List<string>();
            foreach (var (_, buff) in group)
            {
                var ids = Dd2For(buff).ToList();
                if (ids.Count == 0)
                {
                    if (buff.Sub != "damage_high" && buff.Stat != "stress_dmg_received_percent") Plugin.Log.LogInfo($"[buffs] {buff.Id}: no DD2 equivalent");
                    continue;
                }
                foreach (var id in ids)
                    if (library.TryGetLibraryElement(id, out var def))
                    {
                        actor.BuffContainer.TryAdd(def, isLockedTeamPosition: false, SourceType.STORY, Source, actor.ActorGuid);
                        applied.Add(id);
                    }
                    else Plugin.Log.LogWarning($"[buffs] DD2 buff {id} missing");
            }
            if (applied.Count == 0) continue;
            actor.BuffContainer.RefreshActiveBuffs();
            Plugin.Log.LogInfo($"[buffs] actor {group.Key}: {string.Join(", ", group.Select(b => b.Buff.Id))} → {string.Join(", ", applied)}");
        }
    }

    /// <summary>The side that surprised the other gets an extra turn in round one.</summary>
    public static void Surprise(IReadOnlyList<uint> party, bool heroesActFirst) =>
        Runtime.Driver.Instance.StartCoroutine(SurpriseWhenReady(party.ToList(), heroesActFirst));

    // Enemies spawn a little after DD2 enters combat: wait for them (up to ~10 s).
    private static System.Collections.IEnumerator SurpriseWhenReady(List<uint> party, bool heroesActFirst)
    {
        var library = SingletonMonoBehaviour<Library<string, BuffDefinition>>.Instance;
        if (library == null || !library.TryGetLibraryElement("extra_initiative_1_round_buff", out var buff)) yield break;
        var heroes = new HashSet<uint>(party);
        List<ActorInstance> actors = null;
        for (int tries = 0; tries < 40 && Dd2Combat.InFight; tries++)
        {
            if (heroesActFirst) actors = party.Select(Dd2Api.Actor).Where(a => a != null).ToList();
            else
            {
                var combat = UnityEngine.Object.FindObjectOfType<Assets.Code.Combat.Presentation.CombatPresentationBhv>();
                actors = combat?.AllActors.Select(a => a?.ActorInstance).Where(a => a != null && !heroes.Contains(a.ActorGuid)).ToList();
            }
            if (actors != null && actors.Count > 0) break;
            yield return new UnityEngine.WaitForSeconds(0.25f);
        }
        if (actors == null || actors.Count == 0) { Plugin.Log.LogWarning("[buffs] surprise: no actors found"); yield break; }
        foreach (var actor in actors)
        {
            actor.BuffContainer?.TryAdd(buff, isLockedTeamPosition: false, SourceType.STORY, Source, actor.ActorGuid);
            actor.BuffContainer?.RefreshActiveBuffs();
        }
        Plugin.Log.LogInfo($"[buffs] surprise: {(heroesActFirst ? "heroes" : "monsters")} act first ({actors.Count} actors)");
    }

    /// <summary>Take this fight's DD1 buffs off again (DD2 ends its own combat-length ones itself).</summary>
    public static void Remove(IEnumerable<uint> guids)
    {
        StressTakenMultiplier.Clear();
        foreach (var guid in guids)
        {
            var actor = Dd2Api.Actor(guid);
            actor?.BuffContainer?.RemoveAllInstances((BuffInstance i) => i.SourceId == Source, SourceType.STORY, Source, guid, sendEvent: false);
        }
    }
}

/// <summary>DD1's stress resistance buffs (Pep Talk ...) during our fights: scale the stress DD2 deals to that hero.
/// DD2 stress comes in whole points, so the fraction is rolled (70% of 1 point = 1 point 70% of the time).</summary>
[HarmonyLib.HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.ApplyStressDamage))]
internal static class StressTakenInOurFights
{
    private static readonly System.Random Rng = new();

    private static void Prefix(ActorInstance __instance, ref float damage)
    {
        if (damage <= 0f || !Dd2Combat.InFight || !FightBuffs.StressTakenMultiplier.TryGetValue(__instance.ActorGuid, out float k)) return;
        float scaled = damage * k;
        float whole = (float)System.Math.Floor(scaled);
        damage = whole + (Rng.NextDouble() < scaled - whole ? 1f : 0f);
    }
}
