using System;
using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.Data;
using Assets.Code.Library;
using Assets.Code.Resource;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// Puts DD1 monster skills into DD2's data: for a DD1 skill standing on a DD2 skill, a DD2 skill element of its own
/// (ActorDataSkill, ActorDataStats, ActorDataEffects; texts from <see cref="Dd1SkillToDd2"/>), added to DD2's
/// libraries as DD2 itself adds its CSV data. The DD2 skill it stands on lends its presentation
/// (<see cref="Dd1SkillPresentation"/>).
/// </summary>
internal static class Dd1SkillData
{
    private static readonly HashSet<string> Made = new();
    private static readonly Dictionary<Type, Dictionary<string, string>> Texts = new();

    /// <summary>A DD2 element's CSV text (as DD2 parsed it), or "".</summary>
    public static string Text<T>(string id) where T : class, IDataContainer, ILibraryElement<string>, new()
    {
        if (id == null) return "";
        if (!Texts.TryGetValue(typeof(T), out var byId))
        {
            byId = new Dictionary<string, string>();
            if (SingletonMonoBehaviour<Library<string, T>>.Instance is LibraryDataContainerCsv<T> library
                && library.GetResourceDatabase() is ResourceDatabaseText db)
                for (int i = 0; i < db.GetNumberOfResources(); i++)
                {
                    var r = db.GetResourceAtIndex(i);
                    if (r?.m_Name != null && !byId.ContainsKey(r.m_Name)) byId[r.m_Name] = r.m_Data ?? "";
                }
            Texts[typeof(T)] = byId;
        }
        return byId.TryGetValue(id, out var text) ? text : "";
    }

    /// <summary>Make (once) the DD2 skill <paramref name="id"/> for this DD1 skill standing on <paramref name="baseSkill"/>.</summary>
    public static bool Make(string id, string baseSkill, SkillShape dd1, float scale, IReadOnlyCollection<string> target, IReadOnlyCollection<string> performer)
    {
        if (Made.Contains(id)) return true;
        string baseText = Text<ActorDataSkill>(baseSkill);
        if (baseText.Length == 0) return false;
        var stats = new ActorDataStats(id, Dd1SkillToDd2.StatsText(Text<ActorDataStats>(baseSkill), dd1, scale));
        stats.Init(id);
        SingletonMonoBehaviour<Library<string, ActorDataStats>>.Instance.AddLibraryElement(stats, overrideCSV: true);
        if (target.Count + performer.Count > 0)
        {
            var effects = new ActorDataEffects(id, Dd1SkillToDd2.EffectsText(target, performer));
            effects.Init(id);
            SingletonMonoBehaviour<Library<string, ActorDataEffects>>.Instance.AddLibraryElement(effects, overrideCSV: true);
        }
        var skill = new ActorDataSkill(id, Dd1SkillToDd2.SkillText(baseText, dd1));
        skill.Init(id);
        SingletonMonoBehaviour<Library<string, ActorDataSkill>>.Instance.AddLibraryElement(skill, overrideCSV: true);
        Made.Add(id);
        return true;
    }
}

/// <summary>A DD1 skill's DD2 element borrows the presentation (animation, camera, sounds, icon) of the DD2 skill it
/// stands on: DD2 asks its skill resources by skill id ("dd3__&lt;base&gt;__..." → base).</summary>
[HarmonyLib.HarmonyPatch(typeof(ResourceDatabaseAddressable<ResourceDatabaseSkills, ResourceSkillBase>), "GetResource")]
internal static class Dd1SkillPresentation
{
    private static void Prefix(object __instance, ref string resourceId)
    {
        if (resourceId == null || resourceId.Length < 5 || resourceId[0] != 'd' || !(__instance is ResourceDatabaseSkills)) return;
        string baseSkill = Dd1SkillToDd2.BaseOf(resourceId);
        if (baseSkill != null) resourceId = baseSkill;
    }
}
