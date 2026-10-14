using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>Independent test skills. Native combat rules are retained while model/animation work is evaluated.</summary>
public static class VilhelmKit
{
    public sealed class Skill
    {
        public string Key { get; }
        public string Name { get; }
        public string Donor { get; }
        public string Id => Dd1SkillToDd2.GeneratedId(Donor, ExperimentalHeroes.Vilhelm, Key);
        internal Skill(string key, string name, string donor) { Key=key; Name=name; Donor=donor; }
    }
    public static IReadOnlyList<Skill> Skills { get; } = new[]
    {
        new Skill("slash", "Onyx Slash", "cru_smite"),
        new Skill("sweep", "Onyx Sweep", "cru_reap"),
        new Skill("heal", "Great Heal", "cru_battle_heal"),
        new Skill("blackflame", "Elfriede's Blackflame", "cru_radiance"),
        new Skill("advance", "Advancing Strike", "cru_holy_lance")
    };
    public static Skill Find(string id) => Skills.FirstOrDefault(s => id == s.Id || id == s.Id+"_u");
    public static string[] Loadout => Skills.Select(s => s.Id).ToArray();

    /// <summary>Retain donor targeting/conditions, but own history, cooldowns and Guild mastery.</summary>
    public static string SkillText(string donorText, Skill skill, bool mastered)
    {
        var lines = donorText.Replace("\r", "").Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)
            && !l.StartsWith("m_ProfileUnlockId,", StringComparison.Ordinal)
            && !l.StartsWith("m_SkillHistoryIdOverride,", StringComparison.Ordinal)
            && !l.StartsWith("m_SkillCooldownIdOverride,", StringComparison.Ordinal)).ToList();
        lines.Add("m_SkillHistoryIdOverride,"+skill.Id+",");
        lines.Add("m_SkillCooldownIdOverride,"+skill.Id+",");
        return string.Join("\n",lines)+"\n";
    }
}
