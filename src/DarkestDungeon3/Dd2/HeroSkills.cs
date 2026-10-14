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
        public bool MasteredByDefault;
        public bool AlwaysEquipped;
        public string ModeId;
    }

    /// <summary>The class's combat skills (starting first), or null while its resource loads.</summary>
    public static List<Skill> ForClass(string classId)
    {
        if (classId == Core.Campaign.Town.ExperimentalHeroes.Vilhelm)
        {
            var donor=ForClass(Core.Campaign.Town.ExperimentalHeroes.VilhelmDonor);
            return donor == null ? null : Core.Campaign.Town.VilhelmKit.Skills.Select(s => new Skill
                { Id=s.Id,Starting=true,Icon=donor.FirstOrDefault(d => d.Id==s.Donor)?.Icon }).ToList();
        }
        // A DD1-only class (the Shieldbreaker): her DD1 skills as their DD2 ids, all known from the start.
        if (Core.Campaign.Town.RecruitClasses.IsDd1Only(classId))
            return Dd1HeroClasses.Skills(classId).Select(s => new Skill { Id = s.Id, Icon = Dd1HeroClasses.SkillIcon(classId, s.Dd1Skill), Starting = true }).ToList();
        var res = ActorResources.Get(classId);
        if (res == null) return null;
        var list = new List<Skill>();
        void Add(IEnumerable<ResourceSkillBase> skills, bool starting)
        {
            foreach (var s in skills)
            {
                if (s == null) continue;
                string id = s.GetSkillId();
                bool mastered = id?.EndsWith("_u", System.StringComparison.Ordinal) == true;
                if (mastered) id = id.Substring(0, id.Length - 2);
                if (string.IsNullOrEmpty(id) || list.Any(x => x.Id == id) || IsMoveOrPass(id)) continue;
                Core.Dd2Data.HeroPathCatalog.SkillDefinition metadata = null;
                HeroPathBridge.Catalog?.Skills.TryGetValue(id, out metadata);
                list.Add(new Skill { Id = id, Icon = s.m_SkillSprite, Starting = starting,
                    MasteredByDefault = mastered || HeroPathBridge.Catalog?.CanMaster(id) == false,
                    AlwaysEquipped = metadata?.AlwaysEquipped == true, ModeId = metadata?.ModeId });
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
        string visitorName=Core.Campaign.Town.VilhelmKit.Find(id)?.Name;
        if (visitorName!=null) return visitorName;
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

    public static List<string> Loadout(HeroRecord hero, IEnumerable<string> requested = null)
    {
        var skills = ForClass(hero.ClassId) ?? new();
        var known = skills.Where(s => Knows(hero, s)).Select(s => s.Id).ToList();
        var choose = requested ?? (hero.EquippedSkills.Count > 0 ? hero.EquippedSkills : skills.Where(s => s.Starting).Select(s => s.Id));
        return HeroPathBridge.Catalog?.Loadout(hero.ClassId, choose, known) ?? choose.Where(known.Contains).Distinct().Take(EquipLimit).ToList();
    }

    public static string ModeName(Skill skill) => skill.ModeId == null ? null : Ui.HamletUi.Pretty(skill.ModeId.Substring(skill.ModeId.LastIndexOf('_') + 1));

    public static IReadOnlyList<string> EquippedForCurrentMode(ActorInstance actor) => actor.GetEquippedCombatSkillIds(null,
        getBaseSkillIds: false, isModeValid: true, includeMoveSkills: false, includePassSkills: false,
        includeCombatSkillInventory: false, includeTokenSkills: false);

    /// <summary>Put the hero's Guild progress on a fresh actor.</summary>
    public static void Apply(HeroRecord hero, ActorInstance actor)
    {
        // Work on the ordinary skills first. A Path replaces these IDs, including mastered variants.
        // Explicitly select the class default so native editor/test overrides cannot choose a Path for us.
        actor.SetActorPath(actor.ActorDataClass.DefualtActorDataPath, refundUnlockedSkills: false);
        var classSkills = ForClass(hero.ClassId) ?? new();
        foreach (var id in hero.LearnedSkills.Concat(classSkills.Where(s => s.Starting).Select(s => s.Id)).Distinct())
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
        HeroPathBridge.Apply(hero, actor);
        var chosen = Loadout(hero);
        if (chosen.Count > 0)
        {
            var unlocked = actor.GetUnlockedCharacterSheetCombatSkillIds();
            var baseIds = unlocked.Select(id => actor.GetCombatSkillInstance(id)).Where(s => s != null)
                .Select(actor.GetCombatBaseSkillId).ToList();
            var equip = chosen.Where(baseIds.Contains).ToList();
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
