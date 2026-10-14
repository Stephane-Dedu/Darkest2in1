using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Library;
using Assets.Code.Resource;
using Assets.Code.Skill;
using Assets.Code.Unlock;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>Private test hero using an owned Crusader resource and independent class/stat entries.</summary>
internal static class SoulsHeroClasses
{
    private static ResourceActor _resource;
    private static readonly List<ResourceSkillBase> OwnedSkills=new();
    private static Sprite _portrait;
    internal static string Folder => Path.Combine(Plugin.HeroModelPath?.Value ?? "",ExperimentalHeroes.Vilhelm);
    internal static string ModelPath => Path.Combine(Folder,"sir_vilhelm.gltf");
    internal static bool Available => (Plugin.VilhelmHero?.Value == true || Environment.GetCommandLineArgs().Contains("-dd3-vilhelm-check")) && File.Exists(ModelPath);
    internal static bool IsVilhelm(ActorBhv actor) => actor != null
        && Dd2Api.Actor(actor.GetActorGuid())?.ActorDataId == ExperimentalHeroes.Vilhelm;
    internal static ResourceActor Resource(string id) => id == ExperimentalHeroes.Vilhelm ? _resource : null;

    internal static Sprite Portrait()
    {
        if (_portrait != null) return _portrait;
        var texture = Art.Png(Path.Combine(Folder,"portrait.png"));
        if (texture == null) return ActorResources.Get(ExperimentalHeroes.VilhelmDonor)?.GetPortraitIconByType(ResourceActor.PortraitIconType.Color);
        return _portrait = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
    }

    internal static bool TryRegister()
    {
        // The option controls new invitations. Existing visitors still need their
        // class and model when the owner later switches invitations off.
        if (!File.Exists(ModelPath)) return false;
        var classes = SingletonMonoBehaviour<Library<string,ActorDataClass>>.Instance;
        if (classes == null) return false;
        if (_resource != null && classes.GetHasLibraryKey(ExperimentalHeroes.Vilhelm)) return true;
        var database = Singleton<ResourceDatabaseActors>.Instance;
        var field = AccessTools.Field(typeof(ResourceDatabaseAddressable<ResourceDatabaseActors,ResourceActor>),"m_ResourceLocationDictionary");
        if (database == null || field?.GetValue(database) is not IDictionary locations
            || !locations.Contains(ExperimentalHeroes.VilhelmDonor)) return false;
        var donor = database.GetResource(ExperimentalHeroes.VilhelmDonor);
        if (donor == null) return false;
        ResourceActor clone=null;
        var kit=new List<ResourceSkillBase>();
        try
        {
            MakeSkills(kit);
            var stats = new ActorDataStats(ExperimentalHeroes.Vilhelm,Dd1SkillData.Text<ActorDataStats>(ExperimentalHeroes.VilhelmDonor));
            stats.Init(ExperimentalHeroes.Vilhelm);
            SingletonMonoBehaviour<Library<string,ActorDataStats>>.Instance.AddLibraryElement(stats,overrideCSV:true);
            var classText=string.Join("\n",Dd1SkillData.Text<ActorDataClass>(ExperimentalHeroes.VilhelmDonor)
                .Replace("\r","").Split('\n').Where(l => !l.StartsWith("skill_sets,",StringComparison.Ordinal)));
            var actorClass = new ActorDataClass(ExperimentalHeroes.Vilhelm,classText,0);
            actorClass.Init(ExperimentalHeroes.Vilhelm); actorClass.PostInit();
            classes.AddLibraryElement(actorClass,overrideCSV:true);
            clone = UnityEngine.Object.Instantiate(donor);
            clone.name = ExperimentalHeroes.Vilhelm;
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.m_StartingCombatSkills = donor.m_StartingCombatSkills.Where(s => s != null
                && (s.GetSkillId().Contains("_move") || s.GetSkillId().Contains("_pass"))).Concat(kit.Where(s => !s.name.EndsWith("_u",StringComparison.Ordinal))).ToList();
            clone.m_AdditionalCombatSkills = kit.Where(s => s.name.EndsWith("_u",StringComparison.Ordinal)).ToList();
            var portrait = Portrait();
            if (portrait != null)
                foreach (string name in new[] { "m_TurnOrderIcon","m_CombatBarPortrait","m_BWPortrait","m_ColorPortrait","m_MapPortrait","m_StoryPortrait" })
                    AccessTools.Field(typeof(ResourceActor),name)?.SetValue(clone,portrait);
            if (_resource!=null) UnityEngine.Object.Destroy(_resource);
            foreach (var old in OwnedSkills) if (old!=null) UnityEngine.Object.Destroy(old);
            OwnedSkills.Clear(); OwnedSkills.AddRange(kit); _resource=clone;
            locations[ExperimentalHeroes.Vilhelm] = locations[ExperimentalHeroes.VilhelmDonor];
            ExportedHeroes.Prepare();
            Plugin.Log.LogInfo("[vilhelm] independent test class registered; owned Crusader combat kit, private DS3 rig/animations");
            return true;
        }
        catch (Exception e)
        {
            if (clone!=null) UnityEngine.Object.Destroy(clone);
            foreach (var skill in kit) if (skill!=null) UnityEngine.Object.Destroy(skill);
            Plugin.Log.LogError("[vilhelm] class registration: "+e); return false;
        }
    }

    private static void MakeSkills(List<ResourceSkillBase> made)
    {
        foreach (var skill in VilhelmKit.Skills)
        {
            var unlock=new UnlockDefinition(skill.Id+"_u","m_RequirementIds,"+skill.Id+",\nm_CostId,hero_skill_upgrade,\n");
            unlock.Init();
            SingletonMonoBehaviour<Library<string,UnlockDefinition>>.Instance.AddLibraryElement(unlock,overrideCSV:true);
            for (int rank=0;rank<2;rank++)
            {
                string source=skill.Donor+(rank==1 ? "_u" : ""), id=skill.Id+(rank==1 ? "_u" : "");
                string text=Dd1SkillData.Text<ActorDataSkill>(source);
                var resource=Singleton<ResourceDatabaseSkills>.Instance.GetResource(source);
                if (text.Length==0 || resource==null) throw new InvalidOperationException("Missing native skill "+source);
                string statsText=Dd1SkillData.Text<ActorDataStats>(source);
                if (statsText.Length>0)
                {
                    var stats=new ActorDataStats(id,statsText); stats.Init(id);
                    SingletonMonoBehaviour<Library<string,ActorDataStats>>.Instance.AddLibraryElement(stats,overrideCSV:true);
                }
                string effectsText=Dd1SkillData.Text<ActorDataEffects>(source);
                if (effectsText.Length>0)
                {
                    var effects=new ActorDataEffects(id,effectsText); effects.Init(id);
                    SingletonMonoBehaviour<Library<string,ActorDataEffects>>.Instance.AddLibraryElement(effects,overrideCSV:true);
                }
                var data=new ActorDataSkill(id,VilhelmKit.SkillText(text,skill,rank==1)); data.Init(id); data.PostInit();
                SingletonMonoBehaviour<Library<string,ActorDataSkill>>.Instance.AddLibraryElement(data,overrideCSV:true);
                var copy=UnityEngine.Object.Instantiate(resource); copy.name=id; copy.hideFlags=HideFlags.HideAndDontSave;
                AccessTools.Field(typeof(ResourceSkillBase),"m_DataSkillIdOverride")?.SetValue(copy,null);
                AccessTools.Field(typeof(ResourceSkillBase),"m_SelectSkillIdOverride")?.SetValue(copy,skill.Donor);
                made.Add(copy);
            }
        }
    }
}

[HarmonyPatch(typeof(Assets.Code.Locale.Localization),nameof(Assets.Code.Locale.Localization.GetString))]
internal static class SoulsHeroSkillNames
{
    internal static bool Name(string key,out string name)
    {
        name=key?.StartsWith("skill_name_",StringComparison.Ordinal)==true ? VilhelmKit.Find(key.Substring(11))?.Name : null;
        return name!=null;
    }
    private static bool Prefix(string key,ref string __result)
    {
        if (!Name(key,out var name)) return true;
        __result=name; return false;
    }
}
[HarmonyPatch(typeof(Assets.Code.Locale.Localization),nameof(Assets.Code.Locale.Localization.TryGetString))]
internal static class SoulsHeroSkillNamesTry
{
    private static bool Prefix(string key,ref string __result)
    {
        if (!SoulsHeroSkillNames.Name(key,out var name)) return true;
        __result=name; return false;
    }
}
