using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>DD1 campaign hero presentation, with skeletons in anim and pages in the chosen outfit.</summary>
public sealed class Dd1HeroArt
{
    public string ClassId { get; }
    public string Root { get; }
    public string AnimationDirectory => Path.Combine(Root, "anim");
    public string TextureDirectory => Path.Combine(Root, ClassId + "_A", "anim");
    public IReadOnlyDictionary<string, string> SkillAnimations { get; }
    private readonly Dictionary<string, bool> _available = new(StringComparer.Ordinal);

    private Dd1HeroArt(string classId, string root, Dictionary<string, string> animations)
    {
        ClassId = classId;
        Root = root;
        SkillAnimations = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(animations);
    }

    /// <summary>No alternate-class or multiplayer fallback. Missing campaign art is an explicit capability gap.</summary>
    public static Dd1HeroArt Find(Dd1Install dd1, string classId)
    {
        if (dd1 == null || string.IsNullOrEmpty(classId)
            || classId.Any(c => !(c >= 'a' && c <= 'z') && c != '_')) return null;
        foreach (string root in Candidates(dd1, classId))
        {
            string artFile = Path.Combine(root, classId + ".art.darkest");
            if (!File.Exists(artFile)) continue;
            var animations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var record in DarkestFile.Load(artFile).Where(r => r.Type == "combat_skill" || r.Type == "riposte_skill"))
                if (record.Str("id") is { } id && record.Str("anim") is { } anim)
                    animations[id] = anim;
            var art = new Dd1HeroArt(classId, root, animations);
            if (art.HasAnimation("combat") && art.HasAnimation("defend")) return art;
        }
        return null;
    }

    internal static IEnumerable<string> Candidates(Dd1Install dd1, string classId)
    {
        yield return dd1.PathOf("heroes", classId);
        string dlcs = dd1.PathOf("dlc");
        if (!Directory.Exists(dlcs)) yield break;
        foreach (var dlc in Directory.GetDirectories(dlcs).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (Path.GetFileName(dlc).IndexOf("arena", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            yield return Path.Combine(dlc, "heroes", classId);
            string features = Path.Combine(dlc, "features");
            if (!Directory.Exists(features)) continue;
            foreach (var feature in Directory.GetDirectories(features).OrderBy(p => p, StringComparer.Ordinal))
                yield return Path.Combine(feature, "heroes", classId);
        }
    }

    public string Stem(string animation) => ClassId + ".sprite." + animation;

    public bool HasAnimation(string animation)
    {
        if (string.IsNullOrEmpty(animation) || animation.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return false;
        if (_available.TryGetValue(animation, out bool available)) return available;
        string stem = Path.Combine(AnimationDirectory, Stem(animation));
        if (!File.Exists(stem + ".skel") || !File.Exists(stem + ".atlas")) return _available[animation] = false;
        var atlas = SpineAtlas.Parse(File.ReadAllText(stem + ".atlas"));
        return _available[animation] = atlas.Pages.Count > 0 && atlas.Pages.All(p => p.File == Path.GetFileName(p.File)
            && File.Exists(Path.Combine(TextureDirectory, p.File)));
    }

    /// <summary>Match shared skill names, allowing DD2's class prefix and mastery/path suffixes.</summary>
    public string AnimationFor(string dd2SkillId)
    {
        if (string.IsNullOrEmpty(dd2SkillId)) return "combat";
        foreach (var pair in SkillAnimations.OrderByDescending(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal))
            if (dd2SkillId == pair.Key || dd2SkillId.EndsWith("_" + pair.Key, StringComparison.Ordinal)
                || dd2SkillId.Contains("_" + pair.Key + "_"))
                return HasAnimation(pair.Value) ? pair.Value : "combat";
        return "combat";
    }
}
