using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Library;
using Assets.Code.Resource;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1 hero classes DD2 has no hero for (the Shieldbreaker) as DD2 hero classes, built in a run the way round 179
/// builds the memory bosses: her DD1 numbers (<see cref="Dd1HeroKit"/>) as a class and stats of her own, each DD1
/// skill as a DD2 skill (<see cref="Dd1SkillData"/>) with her DD1 icon, on a clone of the Hellion's resource (the
/// hidden body DD1's sprite is drawn over), with her DD1 portrait. No shared class or resource is modified.
/// </summary>
internal static class Dd1HeroClasses
{
    public const string StandIn = "hellion";

    private static readonly Dictionary<string, Dd1HeroKit> Kits = new();
    private static readonly Dictionary<string, ResourceActor> Registered = new();
    private static readonly Dictionary<string, Sprite> Pictures = new();

    public static Dd1HeroKit Kit(string classId)
    {
        if (!RecruitClasses.IsDd1Only(classId)) return null;
        if (!Kits.TryGetValue(classId, out var kit)) Kits[classId] = kit = Dd1HeroKit.Read(Session.Current?.Dd1, classId);
        return kit;
    }

    /// <summary>The class can be recruited: DD1's DLC holds its data.</summary>
    public static bool Available(string classId) => Kit(classId) != null;

    /// <summary>The class's DD1 portrait (roster, outfit A), or null.</summary>
    public static Sprite Portrait(string classId) => Picture(Kit(classId)?.PortraitFile);

    /// <summary>A DD1 skill's icon, or null.</summary>
    public static Sprite SkillIcon(string classId, string dd1Skill) => Picture(Kit(classId)?.IconFile(dd1Skill));

    private static Sprite Picture(string file)
    {
        if (file == null) return null;
        if (Pictures.TryGetValue(file, out var sprite) && sprite != null) return sprite;
        var texture = Art.Png(file);
        if (texture == null) return null;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = System.IO.Path.GetFileNameWithoutExtension(file);
        return Pictures[file] = sprite;
    }

    /// <summary>The DD2 skill her DD1 skill stands on (presentation only: her DD1 sprite plays her own attack).</summary>
    private static string BaseSkill(SkillShape s) => s.Friendly ? "hel_adrenaline_rush" : s.Ranged ? "hel_iron_swan" : "hel_wicked_hack";

    /// <summary>Her DD1 skills' DD2 ids (also what a hero record's loadout holds).</summary>
    public static List<(string Id, string Dd1Skill)> Skills(string classId)
    {
        var kit = Kit(classId);
        if (kit == null) return new List<(string, string)>();
        return kit.SkillIds.Select(id => (Dd1SkillToDd2.GeneratedId(BaseSkill(kit.Shape(id)), classId, id), id)).ToList();
    }

    public static ResourceActor Resource(string actorDataId) =>
        actorDataId != null && Registered.TryGetValue(actorDataId, out var r) && r != null ? r : null;

    /// <summary>Make the DD2 class for a party about to be created (in a run, where DD2's libraries exist).</summary>
    public static bool TryRegister(string classId)
    {
        var kit = Kit(classId);
        var session = Session.Current;
        var classes = SingletonMonoBehaviour<Library<string, ActorDataClass>>.Instance;
        if (kit == null || classes == null || session?.Content?.Effects == null) return false;
        if (Registered.TryGetValue(classId, out var done) && done != null && classes.GetHasLibraryKey(classId)) return true;

        var database = Singleton<ResourceDatabaseActors>.Instance;
        var locationsField = AccessTools.Field(typeof(ResourceDatabaseAddressable<ResourceDatabaseActors, ResourceActor>), "m_ResourceLocationDictionary");
        if (database == null || locationsField?.GetValue(database) is not IDictionary locations || !locations.Contains(StandIn)) return false;
        var standIn = database.GetResource(StandIn);
        if (standIn == null) return false;

        var made = new List<ResourceSkillBase>();
        var unmapped = new List<string>();
        ResourceActor clone = null;
        bool ok = false;
        try
        {
            foreach (var (skillId, dd1Skill) in Skills(classId))
            {
                var shape = kit.Shape(dd1Skill);
                string baseSkill = Dd1SkillToDd2.BaseOf(skillId);
                var missing = new List<string>();
                var effects = Dd1SkillToDd2.Effects(shape, session.Content.Effects.Get, missing,
                    session.Content.Buffs != null ? session.Content.Buffs.Get : null);
                unmapped.AddRange(missing.Select(m => dd1Skill + ": " + m));
                var baseResource = Singleton<ResourceDatabaseSkills>.Instance.GetResource(baseSkill);
                if (baseResource == null || !Dd1SkillData.Make(skillId, baseSkill, shape, 1f, effects.Target, effects.Performer))
                {
                    Plugin.Log.LogWarning($"[dd1 hero] {classId}: skill {dd1Skill} on {baseSkill} could not be made");
                    return false;
                }
                var copy = UnityEngine.Object.Instantiate(baseResource);
                copy.name = skillId;
                copy.hideFlags = HideFlags.HideAndDontSave;
                AccessTools.Field(typeof(ResourceSkillBase), "m_DataSkillIdOverride")?.SetValue(copy, null);
                AccessTools.Field(typeof(ResourceSkillBase), "m_SelectSkillIdOverride")?.SetValue(copy, "");
                copy.m_SkillSprite = SkillIcon(classId, dd1Skill) ?? copy.m_SkillSprite;
                made.Add(copy);
            }

            // The stand-in's moving and passing stay hers; its own attacks do not.
            var keep = standIn.m_StartingCombatSkills.Where(s => s != null && IsMoveOrPass(s.GetSkillId())).ToList();
            float hpScale = HpScale(session.Dd1);
            var stats = new ActorDataStats(classId, kit.StatsText(Dd1SkillData.Text<ActorDataStats>(StandIn), hpScale));
            stats.Init(classId);
            SingletonMonoBehaviour<Library<string, ActorDataStats>>.Instance.AddLibraryElement(stats, overrideCSV: true);
            var actorClass = new ActorDataClass(classId, ClassText(Dd1SkillData.Text<ActorDataClass>(StandIn), kit.SelectedMax), 0);
            actorClass.Init(classId);
            actorClass.PostInit();
            classes.AddLibraryElement(actorClass, overrideCSV: true);

            clone = UnityEngine.Object.Instantiate(standIn);
            clone.name = classId;
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.m_StartingCombatSkills = keep.Concat(made).ToList();
            clone.m_AdditionalCombatSkills = new List<ResourceSkillBase>();
            var portrait = Portrait(classId);
            if (portrait != null)
                foreach (string field in new[] { "m_TurnOrderIcon", "m_CombatBarPortrait", "m_BWPortrait", "m_ColorPortrait", "m_MapPortrait", "m_StoryPortrait" })
                    AccessTools.Field(typeof(ResourceActor), field)?.SetValue(clone, portrait);

            // Lookups of the actor's art and sounds use the stand-in's addressable; creation uses the clone.
            locations[classId] = locations[StandIn];
            Registered[classId] = clone;
            ok = true;
            Plugin.Log.LogInfo($"[dd1 hero] registered {classId} on {StandIn}: {made.Count} DD1 skills, hp x{hpScale:0.##}, {kit.SelectedMax} to equip"
                + (unmapped.Count > 0 ? "; DD1 effects without a DD2 counterpart: " + string.Join(", ", unmapped) : ""));
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[dd1 hero] {classId}: registration failed: {e}");
            return false;
        }
        finally
        {
            if (!ok)
            {
                if (clone != null) UnityEngine.Object.Destroy(clone);
                foreach (var skill in made) UnityEngine.Object.Destroy(skill);
            }
        }
    }

    /// <summary>DD2's like-for-like HP: the stand-in's DD2 health over its DD1 namesake's (the Hellion: 35 / 26).</summary>
    private static float HpScale(Dd1Install dd1)
    {
        var dd1StandIn = Dd1HeroKit.Read(dd1, StandIn);
        var keys = Dd1SkillToDd2.Lines(Dd1SkillData.Text<ActorDataStats>(StandIn));
        var map = keys.FirstOrDefault(l => l.Key == "key_map").Values;
        var values = keys.FirstOrDefault(l => l.Key == "add_stats").Values;
        int at = map?.IndexOf("health_max") ?? -1;
        if (dd1StandIn == null || at < 0 || values == null || at >= values.Count
            || !float.TryParse(values[at], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dd2Hp)
            || dd1StandIn.Armours[0].Hp <= 0) return 1f;
        return dd2Hp / dd1StandIn.Armours[0].Hp;
    }

    /// <summary>The stand-in's class lines (hero tags, paths, quirks, overstress) with DD1's loadout size and no DD2 kits.</summary>
    private static string ClassText(string standInText, int equipLimit)
    {
        var lines = standInText.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0 && !l.StartsWith("skill_sets,", StringComparison.Ordinal))
            .Select(l => l.StartsWith("m_EquippedCombatSkillLimit,", StringComparison.Ordinal) ? $"m_EquippedCombatSkillLimit,{equipLimit}," : l);
        return string.Join("\n", lines) + "\n";
    }

    private static bool IsMoveOrPass(string id) =>
        id != null && (id.Contains("_move") || id.Contains("_pass") || id.Contains("_rest") || id.Contains("skip"));

    /// <summary>The fight's DD1-drawn heroes: party actors of a DD1-only class, by actor guid.</summary>
    public static Dictionary<uint, Dd1HeroArt> Dd1Art(IEnumerable<string> heroIds, Func<string, uint> guidOf)
    {
        var result = new Dictionary<uint, Dd1HeroArt>();
        var estate = Session.Current?.Save?.Estate;
        foreach (string id in heroIds ?? Enumerable.Empty<string>())
        {
            string cls = estate?.Hero(id)?.ClassId;
            if (!RecruitClasses.IsDd1Only(cls)) continue;
            uint guid = guidOf(id);
            var art = guid != 0 ? Dd1HeroArt.Find(Session.Current.Dd1, cls) : null;
            if (art != null) result[guid] = art;
        }
        return result;
    }
}
