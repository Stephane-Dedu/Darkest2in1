using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd2Data;

/// <summary>DD2's expedition Paths and skill replacements, read without starting a native run.</summary>
public sealed class HeroPathCatalog
{
    public sealed class HeroClass
    {
        public string Id, DefaultPathId, Gender;
        public List<string> Tags = new();
        public List<string> Modes = new();
    }

    public sealed class SkillDefinition
    {
        public string Id, ModeId;
        public bool AlwaysEquipped;
    }

    public sealed class PathDefinition
    {
        public string Id, UnlockId, SkillReplacementId;
        public int Order;
        public List<string> ClassIds = new(), ClassTags = new();
        public Dictionary<string, string> Replacements = new();
        public HashSet<string> ComparisonSkills = new();
        public bool Fits(HeroClass hero) => hero != null && (ClassIds.Contains(hero.Id) || ClassTags.Intersect(hero.Tags).Any());
    }

    public Dictionary<string, HeroClass> Classes { get; } = new();
    public Dictionary<string, PathDefinition> Paths { get; } = new();
    private readonly HashSet<string> _skills = new();
    public Dictionary<string, SkillDefinition> Skills { get; } = new();
    public bool CanMaster(string baseId) => baseId != null && _skills.Contains(baseId + "_u");

    // Kingdom overrides are a different ruleset. DLC files are only offered by the bridge when the
    // corresponding owned ResourceActor exists. Merely having a DLC CSV on disk grants nothing.
    public static IEnumerable<string> Files(string excel) => Directory.Exists(excel)
        ? Directory.GetFiles(excel, "*.csv", SearchOption.AllDirectories).Where(file =>
            !file.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Contains("kingdom") &&
            (System.IO.Path.GetFileName(file).StartsWith("hero_", StringComparison.Ordinal) ||
             System.IO.Path.GetFileName(file).StartsWith("actor_paths_", StringComparison.Ordinal))).OrderBy(f => f, StringComparer.Ordinal)
        : Enumerable.Empty<string>();

    public static HeroPathCatalog Load(string excel)
    {
        var data = new HeroPathCatalog();
        var replacements = new Dictionary<string, (string From, string To, bool Compare)>();
        var sets = new Dictionary<string, List<string>>();
        foreach (string file in Files(excel))
            foreach (var (id, type, fields) in Dd2Tables.Blocks(file))
            {
                List<string> Values(string key) => fields.TryGetValue(key, out var value) ? value : new List<string>();
                string Field(string key) => Values(key).FirstOrDefault();
                if (type == "ActorDataClass" && Values("m_Tags").Contains("hero"))
                    data.Classes[id] = new HeroClass { Id = id, DefaultPathId = Field("m_DefaultActorDataPathId") ?? id,
                        Gender = Field("m_LocalizationGender"), Tags = Values("m_Tags"), Modes = Values("modes") };
                else if (type == "ActorDataPath")
                    data.Paths[id] = new PathDefinition { Id = id, UnlockId = Field("m_UnlockId"),
                        SkillReplacementId = Field("m_ActorDataSkillReplacementId") ?? id,
                        Order = int.TryParse(Field("m_OrderPriority"), out int order) ? order : 0,
                        ClassIds = Values("m_ActorClassIds"), ClassTags = Values("m_ActorClassTags") };
                else if (type == "ActorDataSkillReplacement") sets[id] = Values("skill_replacements");
                else if (type == "SkillReplacement") replacements[id] = (Field("m_FromActorDataSkillId"), Field("m_ToActorDataSkillId"), Field("m_IsPathComparisonValid") != "False");
                else if (type == "ActorDataSkill")
                {
                    data._skills.Add(id);
                    data.Skills[id] = new SkillDefinition { Id = id, ModeId = Field("m_ActorDataModeId"), AlwaysEquipped = Field("m_IsAlwaysEquipped") == "True" };
                }
            }
        foreach (var path in data.Paths.Values)
            if (sets.TryGetValue(path.SkillReplacementId, out var ids))
                foreach (string id in ids)
                    if (replacements.TryGetValue(id, out var pair) && pair.From != null && pair.To != null)
                    {
                        path.Replacements[pair.From] = pair.To;
                        if (pair.Compare && !pair.From.EndsWith("_u", StringComparison.Ordinal)) path.ComparisonSkills.Add(pair.From);
                    }
        return data;
    }

    /// <summary>DD2 counts always-equipped skills inside each mode's limit. Abomination has two independent kits.</summary>
    public List<string> Loadout(string classId, IEnumerable<string> requested, IEnumerable<string> known)
    {
        var available = new HashSet<string>(known);
        if (classId == null || !Classes.TryGetValue(classId, out var hero)) return requested.Where(available.Contains).Distinct().Take(5).ToList();
        var modes = hero.Modes.Count > 0 ? hero.Modes : new List<string> { "" };
        bool Valid(SkillDefinition skill, string mode) => skill.ModeId == null || mode == "" || skill.ModeId == mode;
        var counts = modes.ToDictionary(mode => mode, mode => available.Count(id => Skills.TryGetValue(id, out var skill) && skill.AlwaysEquipped && Valid(skill, mode)));
        var result = new List<string>();
        foreach (string id in requested.Distinct())
        {
            if (!available.Contains(id) || !Skills.TryGetValue(id, out var skill) || skill.AlwaysEquipped) continue;
            var validModes = modes.Where(mode => Valid(skill, mode)).ToList();
            if (validModes.Any(mode => counts[mode] >= 5)) continue;
            result.Add(id);
            foreach (string mode in validModes) counts[mode]++;
        }
        return result;
    }

    /// <summary>The class default plus native selectable specialisations. Reserve/story Paths stay hidden.</summary>
    public List<PathDefinition> ForClass(string classId)
    {
        if (classId == null || !Classes.TryGetValue(classId, out var hero)) return new();
        return Paths.Values.Where(p => p.Id == hero.DefaultPathId || (p.UnlockId != null && p.Fits(hero)))
            .OrderBy(p => p.Id == hero.DefaultPathId ? int.MinValue : p.Order).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    public string DefaultId(string classId) => classId != null && Classes.TryGetValue(classId, out var hero) && Paths.ContainsKey(hero.DefaultPathId)
        ? hero.DefaultPathId : null;

    public bool IsValid(string classId, string pathId) => pathId != null && ForClass(classId).Any(p => p.Id == pathId);
    public string ResolveId(string classId, string savedPathId) => IsValid(classId, savedPathId) ? savedPathId : DefaultId(classId);

    /// <summary>Progress/loadouts store the ordinary base ID; the active Path picks its real combat variant.</summary>
    public string SkillId(string classId, string pathId, string baseId, bool mastered)
    {
        if (string.IsNullOrEmpty(baseId)) return baseId;
        // Classes registered by another bridge (DD1 Shieldbreaker) keep their own mastery contract.
        string id = mastered && (classId == null || !Classes.ContainsKey(classId) || CanMaster(baseId)) ? baseId + "_u" : baseId;
        string resolved = ResolveId(classId, pathId);
        return resolved != null && Paths.TryGetValue(resolved, out var path) && path.Replacements.TryGetValue(id, out var replacement)
            ? replacement : id;
    }
}
