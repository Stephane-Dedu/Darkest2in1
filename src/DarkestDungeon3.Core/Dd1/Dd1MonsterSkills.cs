using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>A combat skill reduced to what matters for pairing DD1 and DD2 skills: who it targets, from where, how.</summary>
public sealed class SkillShape
{
    public string Id;
    public bool Ranged, Friendly;
    public List<int> LaunchRanks = new(), TargetRanks = new();

    // DD1's numbers (info files): damage, crit, all targets at once ("~"), its effects by name, the performer's own
    // move (.move back forward) and a heal on its target (.heal min max).
    public int DamageMin, DamageMax, MoveBack, MoveForward, HealMin, HealMax;
    public float Crit;
    public bool AllTargets, CritValid = true;
    public List<string> Effects = new();

    /// <summary>"friendly", "ranged" or "melee".</summary>
    public string Kind => Friendly ? "friendly" : Ranged ? "ranged" : "melee";

    public override string ToString() => $"{Id} ({Kind} {string.Concat(LaunchRanks)}>{string.Concat(TargetRanks)})";
}

/// <summary>DD1 monster skills from its info file: <c>skill: .id "crossbow_shot" .type "ranged" .launch 43 .target 234</c>
/// (an "@" target means its own side, an empty one itself, "~" all of them).</summary>
public static class Dd1MonsterSkills
{
    /// <summary>A DD1 monster's skills: its own tier's info file, else any tier DD1 has.</summary>
    public static List<SkillShape> Read(Dd1Install dd1, string family, char tier)
    {
        string file = InfoFile(dd1, family, tier);
        return file != null ? Parse(DarkestFile.Load(file)) : new List<SkillShape>();
    }

    /// <summary>How many ranks a DD1 monster takes (display: .size), 1 if unknown.</summary>
    public static int Size(Dd1Install dd1, string family, char tier)
    {
        string file = InfoFile(dd1, family, tier);
        var display = file != null ? DarkestFile.Load(file).FirstOrDefault(r => r.Type == "display") : null;
        return display?.Int("size", 0, 1) ?? 1;
    }

    private static string InfoFile(Dd1Install dd1, string family, char tier)
    {
        string tierName = $"{family}_{tier}";
        string file = dd1.PathOf("monsters", family, tierName, tierName + ".info.darkest");
        if (File.Exists(file)) return file;
        string dir = dd1.PathOf("monsters", family);
        file = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.info.darkest", SearchOption.AllDirectories).OrderBy(f => f).FirstOrDefault() : null;
        return file != null && File.Exists(file) ? file : null;
    }

    public static List<SkillShape> Parse(IEnumerable<DarkestRecord> records)
    {
        var skills = new List<SkillShape>();
        foreach (var r in records.Where(r => r.Type == "skill"))
        {
            string target = string.Concat(r.Values("target"));
            var dmg = r.Range("dmg");
            var heal = r.Range("heal");
            var move = r.Values("move");
            skills.Add(new SkillShape
            {
                Id = r.Str("id"),
                Ranged = r.Str("type") == "ranged",
                Friendly = target.Length == 0 || target.Contains('@'),
                LaunchRanks = Ranks(string.Concat(r.Values("launch"))),
                TargetRanks = Ranks(target),
                AllTargets = target.Contains('~'),
                DamageMin = dmg.Min,
                DamageMax = dmg.Max,
                Crit = r.Has("crit") ? r.Float("crit") : 0f,
                CritValid = !string.Equals(r.Str("is_crit_valid"), "False", StringComparison.OrdinalIgnoreCase),
                Effects = r.Values("effect").ToList(),
                MoveBack = move.Count > 0 && int.TryParse(move[0], out int mb) ? mb : 0,
                MoveForward = move.Count > 1 && int.TryParse(move[1], out int mf) ? mf : 0,
                HealMin = heal.Min,
                HealMax = heal.Max,
            });
        }
        return skills;
    }

    private static List<int> Ranks(string s) => s.Where(c => c >= '1' && c <= '4').Select(c => c - '0').Distinct().OrderBy(x => x).ToList();

    /// <summary>
    /// A monster's role from its skills: "support" (mostly helps its side), else where its attacks are launched from:
    /// "front" (mostly ranks 1-2), "back" (mostly 3-4) or "flex" (about as much from both).
    /// </summary>
    public static string Role(IReadOnlyCollection<SkillShape> skills)
    {
        if (skills == null || skills.Count == 0) return "flex";
        if (skills.Count(k => k.Friendly) * 2 > skills.Count) return "support";
        var attacks = skills.Where(k => !k.Friendly && k.LaunchRanks.Count > 0).ToList();
        if (attacks.Count == 0) return "flex";
        int front = attacks.Sum(k => k.LaunchRanks.Count(r => r <= 2)), back = attacks.Sum(k => k.LaunchRanks.Count(r => r >= 3));
        float lean = (back - front) / (float)(back + front);
        return lean > 0.25f ? "back" : lean < -0.25f ? "front" : "flex";
    }

    /// <summary>Two roles can stand for each other: the same, either flexible, or support with the back line.</summary>
    public static bool Compatible(string a, string b) =>
        a == b || a == "flex" || b == "flex" || (a == "support" && b == "back") || (a == "back" && b == "support");

    /// <summary>The DD2 skills a stand-in may use: those whose kind the DD1 monster has (a heal or buff only if it
    /// helps its side too, an attack only if it attacks).</summary>
    public static HashSet<string> Allowed(IReadOnlyList<SkillShape> dd2, IReadOnlyList<SkillShape> dd1)
    {
        var allowed = new HashSet<string>();
        if (dd2 == null || dd1 == null || dd1.Count == 0) return allowed;
        bool helps = dd1.Any(k => k.Friendly), attacks = dd1.Any(k => !k.Friendly);
        foreach (var s in dd2)
            if (s?.Id != null && (s.Friendly ? helps : attacks)) allowed.Add(s.Id);
        return allowed;
    }

    /// <summary>
    /// Pairs each DD2 skill the stand-in has with the DD1 skill that fits it best: the same kind (helping its side,
    /// ranged or melee) first, then the ranks it is used from and aimed at, spreading over the DD1 skills so each
    /// gets used. Returns DD2 skill id → index in <paramref name="dd1"/>.
    /// </summary>
    public static Dictionary<string, int> Match(IReadOnlyList<SkillShape> dd2, IReadOnlyList<SkillShape> dd1)
    {
        var result = new Dictionary<string, int>();
        if (dd2 == null || dd1 == null || dd1.Count == 0) return result;
        var uses = new int[dd1.Count];
        foreach (var s in dd2)
        {
            if (s?.Id == null || result.ContainsKey(s.Id)) continue;
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < dd1.Count; i++)
            {
                float score = Fit(s, dd1[i]) - 2.5f * uses[i];
                if (score > bestScore) { bestScore = score; best = i; }
            }
            result[s.Id] = best;
            uses[best]++;
        }
        return result;
    }

    /// <summary>How well a DD1 skill stands for a DD2 one.</summary>
    public static float Fit(SkillShape dd2, SkillShape dd1)
    {
        float score = 0f;
        if (dd2.Friendly != dd1.Friendly) score -= 20f;           // a heal or buff never plays as an attack
        else if (!dd2.Friendly && dd2.Ranged == dd1.Ranged) score += 8f;
        score += dd2.LaunchRanks.Intersect(dd1.LaunchRanks).Count();
        score += 0.5f * dd2.TargetRanks.Intersect(dd1.TargetRanks).Count();
        return score;
    }
}
