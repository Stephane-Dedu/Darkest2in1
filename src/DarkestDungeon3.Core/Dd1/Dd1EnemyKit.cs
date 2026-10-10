using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>Installed enemy numbers and complete skill list, separate from DD2's borrowed presentation.</summary>
public sealed class Dd1EnemyKit
{
    public string Id { get; private set; }
    public string Family { get; private set; }
    public char Tier { get; private set; }
    public int Size { get; private set; }
    public int Hp { get; private set; }
    public int Speed { get; private set; }
    public int Turns { get; private set; }
    public bool Boss { get; private set; }
    public bool Corpse { get; private set; }
    public bool CanBeSummonRank { get; private set; }
    public int AliveRoundLimit { get; private set; }
    public string DeathClassId { get; private set; }
    public string Brain { get; private set; }
    public string LifeLinkBaseClass { get; private set; }
    public IReadOnlyList<SkillShape> Skills { get; private set; }
    public IReadOnlyDictionary<string, float> Resists { get; private set; }
    public float Dodge { get; private set; }
    public float Protection { get; private set; }

    /// <summary>Exact tier only. Another tier's stats/skills must never silently stand in for a missing boss.</summary>
    public static Dd1EnemyKit Read(Dd1Install dd1, string family, char tier)
    {
        if (dd1 == null || string.IsNullOrEmpty(family) || family.Any(c => !(c >= 'a' && c <= 'z') && c != '_')
            || tier < 'A' || tier > 'D') return null;
        string id = family + "_" + tier;
        string file = dd1.PathOf("monsters", family, id, id + ".info.darkest");
        if (!File.Exists(file)) return null;
        var records = DarkestFile.Load(file);
        var stats = records.FirstOrDefault(r => r.Type == "stats");
        if (stats == null) return null;
        var skills = Dd1MonsterSkills.Parse(records);
        int size = records.FirstOrDefault(r => r.Type == "display")?.Int("size") ?? 0;
        int hp = stats.Int("hp"), turns = records.FirstOrDefault(r => r.Type == "initiative")?.Int("number_of_turns_per_round", fallback: 1) ?? 1;
        bool corpse = records.Any(r => r.Type == "enemy_type" && r.Str("id") == "corpse");
        if (hp <= 0 || size < 1 || size > 4 || turns < 0 || (!corpse && (turns == 0 || skills.Count == 0))
            || skills.Any(s => string.IsNullOrEmpty(s.Id)) || skills.Select(s => s.Id).Distinct().Count() != skills.Count) return null;
        var resists = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var pair in new[] { ("stun", "stun"), ("poison", "blight"), ("bleed", "bleed"), ("debuff", "debuff"), ("move", "move") })
            resists[pair.Item2] = stats.Float(pair.Item1 + "_resist");
        return new Dd1EnemyKit
        {
            Id = id, Family = family, Tier = tier, Size = size, Hp = hp, Speed = stats.Int("spd"), Turns = turns,
            Dodge = stats.Float("def"), Protection = stats.Float("prot"),
            Boss = records.Any(r => r.Type == "tag" && r.Str("id") == "boss"),
            Corpse = corpse,
            CanBeSummonRank = string.Equals(records.FirstOrDefault(r => r.Type == "battle_modifier")?.Str("can_be_summon_rank"), "True", StringComparison.OrdinalIgnoreCase),
            AliveRoundLimit = records.FirstOrDefault(r => r.Type == "life_time")?.Int("alive_round_limit") ?? 0,
            DeathClassId = records.FirstOrDefault(r => r.Type == "death_class" && r.Str("type") == "corpse")?.Str("monster_class_id"),
            Brain = records.FirstOrDefault(r => r.Type == "monster_brain")?.Str("id"),
            LifeLinkBaseClass = records.FirstOrDefault(r => r.Type == "life_link")?.Str("base_class"),
            Skills = skills.AsReadOnly(),
            Resists = new System.Collections.ObjectModel.ReadOnlyDictionary<string, float>(resists),
        };
    }

    public string ActorClassText(string lifeLinkActorId = null) => Corpse
        ? $"m_Size,{Size},\nm_Tags,monster,corpse,\nm_ActorControllerType,RANDOM,\nm_EquippedCombatSkillLimit,0,\n"
          + $"m_IsBattleComplete,True,\nm_IsSummonReplacable,{(CanBeSummonRank ? "True" : "False")},\nm_DeathRound,{AliveRoundLimit},\n"
          + "m_ClearContainerTypes,BuffContainer,TokenContainer,DotContainer,\nm_SkillBlockId,corpse,\n"
          + "m_IsTickTriggerValid,False,\nm_IsStressTriggerValid,False,\nm_IsStallCounted,False,\n"
        : $"m_Size,{Size},\nm_Tags,monster,{(Boss ? "boss," : "")}\n"
        + $"m_ActorControllerType,RANDOM,\nm_EquippedCombatSkillLimit,{Skills.Count},\n"
        + "m_IsTickTriggerValid,True,\nm_IsStressTriggerValid,True,\n"
        + $"m_IsStallCounted,{(!Boss ? "True" : "False")},\n"
        + (!string.IsNullOrEmpty(LifeLinkBaseClass) && !string.IsNullOrEmpty(lifeLinkActorId)
            ? $"m_DeathChainIds,{lifeLinkActorId},\nm_IsDeathChainDeathClassValid,False,\n" : "");

    /// <summary>DD2 retains its combat engine; it has no accuracy/dodge stat, and DD1 has no burn resistance.</summary>
    public string ActorStatsText()
    {
        string text = $"key_map,health_max,speed,speed_number_of_turns,health_damage_received_percent,\n"
            + $"add_stats,{Hp},{Speed},{Turns},{(-Protection).ToString("R", CultureInfo.InvariantCulture)},\n";
        foreach (var pair in Resists) text += "sub_stat,resistance," + pair.Key + "," + pair.Value.ToString("R", CultureInfo.InvariantCulture) + ",\n";
        return text + "sub_stat,resistance,death,0,\n";
    }
}
