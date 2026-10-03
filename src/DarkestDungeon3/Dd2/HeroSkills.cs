using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Library;
using Assets.Code.Skill;
using Assets.Code.Source;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's combat skills for the Guild: what a class has (its ResourceActor's starting skills, unlocked from the start,
/// and additional ones that must be learned), and putting a hero's Guild progress on their fresh DD2 actor: learned
/// skills unlocked, mastered skills upgraded (DD2's "_u" variant), the chosen loadout equipped.
/// </summary>
internal static class HeroSkills
{
    public const int EquipLimit = 5;   // every DD2 hero class: m_EquippedCombatSkillLimit 5

    public sealed class Skill
    {
        public string Id;
        public Sprite Icon;
        public bool Starting;
    }

    /// <summary>The class's combat skills (starting first), or null while its resource loads.</summary>
    public static List<Skill> ForClass(string classId)
    {
        var res = ActorResources.Get(classId);
        if (res == null) return null;
        var list = new List<Skill>();
        void Add(IEnumerable<ResourceSkillBase> skills, bool starting)
        {
            foreach (var s in skills)
            {
                if (s == null) continue;
                string id = s.GetSkillId();
                if (string.IsNullOrEmpty(id) || list.Any(x => x.Id == id) || IsMoveOrPass(id)) continue;
                list.Add(new Skill { Id = id, Icon = s.m_SkillSprite, Starting = starting });
            }
        }
        Add(res.m_StartingCombatSkills, true);
        Add(res.m_AdditionalCombatSkills, false);
        return list;
    }

    // Not choosable: moving, passing (rest), act-outs and DD2's automatic ripostes. DD2's skill library isn't loaded
    // at the main menu, so the ids decide when it can't.
    private static readonly string[] NotChoosable = { "_move", "_pass", "_rest", "_act_out", "_riposte", "_corpse" };

    private static bool IsMoveOrPass(string id)
    {
        if (NotChoosable.Any(id.Contains) || id.Contains("pass") || id.Contains("skip")) return true;
        string name = Name(id);
        if (name == "Pass" || name == "Move" || name == "Rest") return true;
        var data = SingletonMonoBehaviour<Library<string, ActorDataSkill>>.Instance?.GetLibraryElement(id);
        return data != null && (data.IsMoveSkill || data.IsPassSkill);
    }

    private static readonly System.Text.RegularExpressions.Regex Tags = new("<[^>]*>");

    public static string Name(string id)
    {
        try
        {
            string name = Tags.Replace(SkillDescription.GetNameText(id) ?? "", "").Trim();
            if (!string.IsNullOrEmpty(name) && !name.Contains("_")) return name;
        }
        catch (System.Exception) { }
        string s = id.Contains("_") ? id.Substring(id.IndexOf('_') + 1) : id;
        return Ui.HamletUi.Pretty(s);
    }

    public static bool Knows(HeroRecord hero, Skill skill) => skill.Starting || hero.LearnedSkills.Contains(skill.Id);

    /// <summary>Put the hero's Guild progress on a fresh actor.</summary>
    public static void Apply(HeroRecord hero, ActorInstance actor)
    {
        foreach (var id in hero.LearnedSkills)
        {
            var skill = actor.GetCombatSkillInstance(id);
            if (skill != null && !skill.GetIsUnlocked())
            {
                skill.SetIsUnlocked();
                actor.GetCombatReplacedSkill(id)?.SetIsUnlocked();
            }
        }
        foreach (var id in hero.MasteredSkills)
        {
            // As DD2's Inn does it: the "_u" skill's unlock lists the base skill, and swaps it in.
            var unlock = SkillUtils.GetUnlockFromSkillId(id + "_u");
            if (unlock != null && unlock.RequirementIds.Contains(id) && actor.GetCombatSkillInstance(id) != null)
                actor.UnlockSkill(unlock, SourceType.INN);
        }
        if (hero.EquippedSkills.Count > 0)
        {
            var unlocked = actor.GetUnlockedCharacterSheetCombatSkillIds();
            var equip = hero.EquippedSkills.Where(id => unlocked.Contains(id) || unlocked.Contains(id + "_u")).Take(EquipLimit).ToList();
            if (equip.Count > 0)
            {
                // DD2's call only equips, and a fresh hero already has a full loadout: clear it first.
                actor.UnequipAllPossibleCombatSkills();
                actor.SetBaseCombatSkillIdsEquipped(equip);
            }
        }
        if (hero.LearnedSkills.Count + hero.MasteredSkills.Count + hero.EquippedSkills.Count > 0)
            Plugin.Log.LogInfo($"[party] {hero.Name}: equipped {string.Join(", ", actor.GetEquippedCombatSkillIds())}; upgraded {string.Join(", ", actor.GetUpgradedCombatSkillIds())}");
    }
}
