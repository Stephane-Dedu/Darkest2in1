using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Code.Actor;
using Assets.Code.Library;
using Assets.Code.Locale;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd2Data;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>Native Path effects in combat, menu-safe descriptions and previews in the Guild.</summary>
internal static class HeroPathBridge
{
    public static HeroPathCatalog Catalog => Session.Current?.HeroPaths;
    private static Task<TrinketDescriptions> _descriptions;
    private static bool _warned;

    public static List<HeroPathCatalog.PathDefinition> ForClass(string classId) =>
        ActorResources.Has(classId) == true ? Catalog?.ForClass(classId) ?? new() : new();

    public static void Apply(HeroRecord hero, ActorInstance actor)
    {
        string id = Catalog?.ResolveId(hero.ClassId, hero.PathId);
        var library = SingletonMonoBehaviour<Library<string, ActorDataPath>>.Instance;
        ActorDataPath selected = actor.ActorDataClass.DefualtActorDataPath;
        if (id != null && library != null && library.TryGetLibraryElement(id, out var path)
            && (path == selected || path.GetIsValidForActorDataClass(actor.ActorDataClass))) selected = path;
        actor.SetActorPath(selected, refundUnlockedSkills: false);
        if (hero.PathId != null && selected?.Id != hero.PathId)
            Plugin.Log.LogWarning($"[paths] {hero.Name}: unavailable {hero.PathId}; using {selected?.Id ?? "class default"}");
    }

    private static TrinketDescriptions Descriptions
    {
        get
        {
            if (_descriptions == null)
            {
                string assets = Application.streamingAssetsPath;
                _descriptions = Task.Run(() => TrinketDescriptions.Load(assets, includeHeroes: true));
            }
            if (!_descriptions.IsCompleted) return null;
            try { return _descriptions.GetAwaiter().GetResult(); }
            catch (Exception e)
            {
                if (!_warned) Plugin.Log.LogWarning("[paths] descriptions: " + e.Message);
                _warned = true;
                return null;
            }
        }
    }

    private static string Localize(string key) => Singleton<Localization>.Instance?.TryGetString(key);
    private static string Plain(string text) => TrinketDescriptions.Plain(text, Localize);

    public static string Name(string classId, string pathId)
    {
        string id = Catalog?.ResolveId(classId, pathId);
        if (id == null) return "Class default";
        string gender = Catalog.Classes.TryGetValue(classId, out var hero) ? hero.Gender : null;
        string text = Localize("hero_path_name_" + id + (gender == null ? "" : "_" + gender))
            ?? Localize("hero_path_name_" + id) ?? Descriptions?.Text("hero_path_name_" + id);
        return Plain(text) ?? Ui.HamletUi.Pretty(id.Contains("_") ? id.Substring(id.IndexOf('_') + 1) : id);
    }

    public static string Flavour(string classId, string pathId)
    {
        string id = Catalog?.ResolveId(classId, pathId);
        return id == null ? null : Plain(Localize("hero_path_flavour_" + id) ?? Descriptions?.Text("hero_path_flavour_" + id));
    }

    public static string Effects(string classId, string pathId)
    {
        string id = Catalog?.ResolveId(classId, pathId);
        if (id == null) return null;
        var library = SingletonMonoBehaviour<Library<string, ActorDataPath>>.Instance;
        if (library != null && library.IsInitializationFinished() && library.TryGetLibraryElement(id, out var path))
            return Plain(ActorPathDescription.GetEffectStrings(path));
        return Descriptions?.PathEffects(id, out _, Localize);
    }

    public static string SkillId(HeroRecord hero, string baseId, string previewPath = null, bool preview = false) =>
        Catalog?.SkillId(hero.ClassId, preview ? previewPath : hero.PathId, baseId, IsMastered(hero, baseId))
        ?? (hero.MasteredSkills.Contains(baseId) ? baseId + "_u" : baseId);

    public static bool IsMastered(HeroRecord hero, string baseId) => hero.MasteredSkills.Contains(baseId)
        || HeroSkills.ForClass(hero.ClassId)?.Any(s => s.Id == baseId && s.MasteredByDefault) == true;

    public static Sprite Icon(HeroRecord hero, HeroSkills.Skill skill)
    {
        string id = SkillId(hero, skill.Id);
        var resource = ActorResources.Get(hero.ClassId);
        return resource?.m_StartingCombatSkills.Concat(resource.m_AdditionalCombatSkills)
            .FirstOrDefault(s => s != null && s.GetSkillId() == id)?.m_SkillSprite ?? skill.Icon;
    }

    public static string SkillDescription(string id)
    {
        var library = SingletonMonoBehaviour<Library<string, ActorDataSkill>>.Instance;
        if (library != null && library.IsInitializationFinished() && library.TryGetLibraryElement(id, out var skill))
            return Plain(Assets.Code.Skill.SkillDescription.GetDebugDetailText(skill, true, true, true, true));
        return Descriptions?.SkillEffects(id, out _, Localize) ?? "Preparing skill details…";
    }
}
