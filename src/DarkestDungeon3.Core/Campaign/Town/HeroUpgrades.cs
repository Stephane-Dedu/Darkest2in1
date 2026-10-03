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

    /// <summary>The weapon or armour levels for a class ("weapon" / "armour"), rank 1 first.</summary>
    public IReadOnlyList<HeroUpgradeLevel> Equipment(string dd2Class, string slot) =>
        Trees.TryGetValue(Dd1Class(dd2Class) + "." + slot, out var levels) ? levels : new List<HeroUpgradeLevel>();
}
