using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>One level of a DD1 hero upgrade tree (a weapon or armour rank, a combat skill level).</summary>
public sealed class HeroUpgradeLevel
{
    public string TreeId, Code;
    public int Gold;
    public List<string> Prerequisites = new();   // "treeId:code" (building trees, or this tree's previous level)
    public int Resolve;                          // DD1 prerequisite_resolve_level
}

/// <summary>
/// DD1's per-class upgrade trees (upgrades/heroes/&lt;class&gt;.upgrades.json, DLC classes included): weapon and
/// armour ranks at the Blacksmith, combat skill levels at the Guild. DD2 classes without a DD1 namesake borrow a
/// DD1 class's costs (the same stand-ins as their camping skills).
/// </summary>
public sealed class HeroUpgrades
{
    public Dictionary<string, List<HeroUpgradeLevel>> Trees { get; } = new();

    public static HeroUpgrades Load(Dd1Install dd1)
    {
        var u = new HeroUpgrades();
        var files = new List<string>();
        string base_ = dd1.PathOf("upgrades", "heroes");
        if (Directory.Exists(base_)) files.AddRange(Directory.GetFiles(base_, "*.upgrades.json"));
        string dlc = dd1.PathOf("dlc");
        if (Directory.Exists(dlc))
            files.AddRange(Directory.GetFiles(dlc, "*.upgrades.json", SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains("/upgrades/heroes/") && !f.Replace('\\', '/').Contains("/modes/")));
        foreach (var file in files)
            foreach (var tree in JToken.Parse(File.ReadAllText(file))["trees"] ?? new JArray())
            {
                string id = (string)tree["id"];
                if (id == null || u.Trees.ContainsKey(id)) continue;
                u.Trees[id] = (tree["requirements"] ?? new JArray()).Select(r => new HeroUpgradeLevel
                {
                    TreeId = id,
                    Code = (string)r["code"],
                    Gold = (r["currency_cost"] ?? new JArray()).Where(c => (string)c["type"] == "gold").Sum(c => (int)c["amount"]),
                    Prerequisites = (r["prerequisite_requirements"] ?? new JArray())
                        .Select(p => (string)p["tree_id"] + ":" + (string)p["requirement_code"]).ToList(),
                    Resolve = (int?)r["prerequisite_resolve_level"] ?? 0,
                }).ToList();
            }
        return u;
    }

    /// <summary>The DD1 class whose upgrade costs a DD2 class uses.</summary>
    public string Dd1Class(string dd2Class)
    {
        if (dd2Class != null && Trees.ContainsKey(dd2Class + ".weapon")) return dd2Class;
        if (dd2Class != null && CampingSkills.Dd1ClassFor.TryGetValue(dd2Class, out var stand) && Trees.ContainsKey(stand + ".weapon")) return stand;
        return "highwayman";
    }

    /// <summary>
    /// The DD1 skill tree a DD2 skill prices like: DD2's "hwm_wicked_slice" is DD1's "highwayman.wicked_slice"
    /// (small spelling differences allowed: "duelists_advance" / "duelist_advance"). Null when DD1 has no match.
    /// </summary>
    public IReadOnlyList<HeroUpgradeLevel> SkillTree(string dd2Class, string dd2SkillId)
    {
        if (string.IsNullOrEmpty(dd2SkillId)) return null;
        string skill = dd2SkillId.Contains("_") ? dd2SkillId.Substring(dd2SkillId.IndexOf('_') + 1) : dd2SkillId;
        if (skill.EndsWith("_u")) skill = skill.Substring(0, skill.Length - 2);
        string want = Squash(skill), prefix = Dd1Class(dd2Class) + ".";
        foreach (var kv in Trees)
        {
            if (!kv.Key.StartsWith(prefix) || kv.Key.EndsWith(".weapon") || kv.Key.EndsWith(".armour")) continue;
            string have = Squash(kv.Key.Substring(prefix.Length));
            if (have == want || Distance(have, want) <= 2) return kv.Value;
        }
        return null;
    }

    private static string Squash(string s) => new string(s.Where(char.IsLetter).ToArray());

    private static int Distance(string a, string b)
    {
        if (System.Math.Abs(a.Length - b.Length) > 2) return 99;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    /// <summary>The weapon or armour levels for a class ("weapon" / "armour"), rank 1 first.</summary>
    public IReadOnlyList<HeroUpgradeLevel> Equipment(string dd2Class, string slot) =>
        Trees.TryGetValue(Dd1Class(dd2Class) + "." + slot, out var levels) ? levels : new List<HeroUpgradeLevel>();
}
