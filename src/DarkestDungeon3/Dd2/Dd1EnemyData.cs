using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Library;
using Assets.Code.Resource;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Dd1;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// Independent enemy definitions/resources for the first memory boss. DD2 supplies the prefab and skill
/// presentation; installed DD1 data supplies the complete kit. No shared class/resource is modified.
/// Nothing calls this factory in normal gameplay until the encounter controller is complete.
/// </summary>
internal static class Dd1EnemyData
{
    internal sealed class Entry
    {
        public string Id;
        public Dd1EnemyKit Kit;
        public ResourceActor Resource;
        public readonly Dictionary<string, string> SkillIds = new(); // generated -> DD1
        public readonly Dictionary<string, Dd1SingleSummon> Summons = new();
    }

    private static readonly Dictionary<string, Entry> Entries = new();
    public static string ActorId(string dd1Id) => "dd3_memory_" + dd1Id + (dd1Id.StartsWith("corpse_", StringComparison.Ordinal) ? "_corpse" : "");
    public static Entry Get(string id) => id != null && Entries.TryGetValue(id, out var entry) ? entry : null;

    public static bool TryRegister(string family, char tier, out Entry entry)
    {
        entry = null;
        // One audited boss, its two summon candidates and their corpse. Broaden only after mechanic coverage.
        if (tier != 'A' || family is not ("necromancer" or "skeleton_common" or "skeleton_militia" or "corpse")) return false;
        string id = ActorId(family + "_" + tier);
        if (Entries.TryGetValue(id, out entry))
        {
            var classes = SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance;
            if (classes?.GetHasLibraryKey(id) == true && entry.Resource != null) return true;
            Entries.Remove(id); entry = null;
        }
        var session = Runtime.Session.Current;
        var kit = Dd1EnemyKit.Read(session?.Dd1, family, tier);
        if (kit == null || kit.Protection < 0 || kit.Protection > 1 || (!kit.Corpse && kit.Dodge != 0) || session?.Content?.Effects == null) return false;
        string lifeLinkId = null;
        if (!string.IsNullOrEmpty(kit.LifeLinkBaseClass))
        {
            // This audited encounter has one anchor. Register it first so native class validation can link it.
            if (kit.LifeLinkBaseClass != "necromancer" || !TryRegister(kit.LifeLinkBaseClass, tier, out var anchor)) return false;
            lifeLinkId = anchor.Id;
        }
        Entry corpse = null;
        if (!string.IsNullOrEmpty(kit.DeathClassId))
        {
            var (corpseFamily, corpseTier) = Core.Expedition.Dd1Bestiary.Split(kit.DeathClassId);
            if (!TryRegister(corpseFamily, corpseTier, out corpse)) return false;
        }

        string baseActor = kit.Corpse ? "lost_battalion_foot_soldier_corpse"
            : family == "necromancer" ? "lost_battalion_bishop" : "lost_battalion_foot_soldier";
        var database = Singleton<ResourceDatabaseActors>.Instance;
        var locationsField = AccessTools.Field(typeof(ResourceDatabaseAddressable<ResourceDatabaseActors, ResourceActor>), "m_ResourceLocationDictionary");
        if (database == null || locationsField?.GetValue(database) is not IDictionary locations || !locations.Contains(baseActor)) return false;
        var baseResource = database.GetResource(baseActor);
        if (baseResource == null) return false;

        var candidate = new Entry { Id = id, Kit = kit };
        var startingSkills = new List<ResourceSkillBase>();
        ResourceActor clone = null;
        try
        {
            foreach (var skill in kit.Skills)
            {
                // These presentations exist on the bishop, but each attack gets its own generated skill.
                if (skill.Friendly) return false;
                string baseSkill = skill.Ranged ? "bishop_smite" : "bishop_strike";
                string skillId = Dd1SkillToDd2.GeneratedId(baseSkill, kit.Id, skill.Id);
                var unmapped = new List<string>();
                var effects = Dd1SkillToDd2.Effects(skill, session.Content.Effects.Get, unmapped,
                    session.Content.Buffs != null ? session.Content.Buffs.Get : null);
                foreach (string effectName in unmapped)
                {
                    if (!Dd1SingleSummon.TryRead(session.Content.Effects.Get(effectName), out var summon, out _)
                        || summon.CanSpawnLoot || candidate.Summons.ContainsKey(skillId)) return false;
                    candidate.Summons[skillId] = summon;
                }
                var skillResource = Singleton<ResourceDatabaseSkills>.Instance.GetResource(baseSkill);
                if (skillResource == null || !Dd1SkillData.Make(skillId, baseSkill, skill, 1f, effects.Target, effects.Performer)) return false;
                var copy = UnityEngine.Object.Instantiate(skillResource);
                copy.name = skillId;
                copy.hideFlags = HideFlags.HideAndDontSave;
                AccessTools.Field(typeof(ResourceSkillBase), "m_DataSkillIdOverride")?.SetValue(copy, null);
                AccessTools.Field(typeof(ResourceSkillBase), "m_SelectSkillIdOverride")?.SetValue(copy, "");
                startingSkills.Add(copy);
                candidate.SkillIds[skillId] = skill.Id;
            }

            var stats = new ActorDataStats(id, kit.ActorStatsText());
            stats.Init(id);
            SingletonMonoBehaviour<Library<string, ActorDataStats>>.Instance.AddLibraryElement(stats, overrideCSV: true);
            var actorClass = new ActorDataClass(id, kit.ActorClassText(lifeLinkId), 0);
            actorClass.Init(id);
            actorClass.PostInit();
            SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance.AddLibraryElement(actorClass, overrideCSV: true);
            clone = UnityEngine.Object.Instantiate(baseResource);
            clone.name = id;
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.m_StartingCombatSkills = startingSkills;
            clone.m_AdditionalCombatSkills = new List<ResourceSkillBase>();
            clone.m_DeathClassResource = corpse?.Resource;
            candidate.Resource = clone;

            // Actor/art/audio lookups can use the existing addressable; creation below must use the clone's name/list.
            // Modifying only this actor database avoids a Harmony patch of the shared generic resource getter.
            locations[id] = locations[baseActor];
            Entries[id] = candidate;
            entry = candidate;
            Plugin.Log.LogInfo($"[memory enemy] registered {kit.Id}: hp {kit.Hp}, speed {kit.Speed}, {kit.Skills.Count} skills");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[memory enemy] registration failed: " + ex);
            return false;
        }
        finally
        {
            if (entry == null)
            {
                if (clone != null) UnityEngine.Object.Destroy(clone);
                foreach (var skill in startingSkills) UnityEngine.Object.Destroy(skill);
            }
        }
    }
}

/// <summary>Exact string overload only. Native creation still initializes the actor and its skills.</summary>
[HarmonyPatch(typeof(LibraryActors), nameof(LibraryActors.CreateActor), new[] { typeof(string) })]
internal static class Dd1MemoryEnemyCreation
{
    private static bool Prefix(LibraryActors __instance, string actorDataId, ref uint __result)
    {
        var resource = Dd1EnemyData.Get(actorDataId)?.Resource ?? Dd1HeroClasses.Resource(actorDataId);
        if (resource == null) return true;
        __result = __instance.CreateActor(resource);
        return false;
    }
}
