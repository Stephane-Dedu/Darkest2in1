using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

public sealed class UpgradeLevel
{
    public string TreeId;
    public string Code;
    public List<Reward> Cost = new();
    public List<string> Prerequisites = new();   // "treeId:code"

    public string Key => TreeId + ":" + Code;
}

/// <summary>DD1's building upgrade trees (<c>upgrades/building/*.upgrades.json</c>).</summary>
public sealed class UpgradeTrees
{
    public Dictionary<string, List<UpgradeLevel>> Trees { get; } = new();

    public static UpgradeTrees Load(Dd1Install dd1)
    {
        var trees = new UpgradeTrees();
        foreach (var file in Directory.GetFiles(dd1.PathOf("upgrades", "building"), "*.upgrades.json"))
            foreach (var tree in Json.Read(file)["trees"])
            {
                string id = (string)tree["id"];
                trees.Trees[id] = tree["requirements"].Select(r => new UpgradeLevel
                {
                    TreeId = id,
                    Code = (string)r["code"],
                    Cost = r["currency_cost"].Select(c => new Reward((string)c["type"], (int)c["amount"]))
                                             .Where(c => c.Amount > 0).ToList(),
                    Prerequisites = r["prerequisite_requirements"]
                        .Select(p => (string)p["tree_id"] + ":" + (string)p["requirement_code"]).ToList(),
                }).ToList();
            }
        return trees;
    }

    public UpgradeLevel Find(string treeId, string code) =>
        Trees.TryGetValue(treeId, out var levels) ? levels.FirstOrDefault(l => l.Code == code) : null;

    /// <summary>DD1's building completion ratio, rounded as the Unity port's upgrade window does.</summary>
    public int Percent(Estate estate, string building)
    {
        var levels = Trees.Where(t => t.Key.StartsWith(building + ".", StringComparison.Ordinal)).SelectMany(t => t.Value).ToList();
        return levels.Count == 0 ? 0 : (int)Math.Round(100f * levels.Count(l => estate.Upgrades.Contains(l.Key)) / levels.Count);
    }

    /// <summary>The next level of a tree the estate hasn't bought yet.</summary>
    public UpgradeLevel Next(Estate estate, string treeId) =>
        Trees.TryGetValue(treeId, out var levels) ? levels.FirstOrDefault(l => !estate.Upgrades.Contains(l.Key)) : null;

    public bool CanBuy(Estate estate, UpgradeLevel level) =>
        level != null
        && !estate.Upgrades.Contains(level.Key)
        && level.Prerequisites.All(estate.Upgrades.Contains)
        && estate.CanAfford(level.Cost);

    public bool TryBuy(Estate estate, string treeId, string code)
    {
        var level = Find(treeId, code);
        if (!CanBuy(estate, level)) return false;
        estate.TrySpend(level.Cost);
        estate.Upgrades.Add(level.Key);
        return true;
    }
}

/// <summary>
/// DD1 building values come as tier lists: <c>[{value}, {value, upgrade_tree_id?, upgrade_requirement_code}]</c>.
/// The current value is the last tier whose upgrade the estate owns. Activities leave the tree id out; it's
/// implied by the activity ("abbey.prayer").
/// </summary>
public static class Tiers
{
    public static JObject Current(JToken tiers, Estate estate, string impliedTreeId = null)
    {
        JObject current = null;
        if (tiers is not JArray list) return null;
        foreach (JObject tier in list)
        {
            string code = (string)tier["upgrade_requirement_code"];
            string tree = (string)tier["upgrade_tree_id"] ?? impliedTreeId;
            if (code == null || (tree != null && estate.Upgrades.Contains(tree + ":" + code)))
                current = tier;
        }
        return current;
    }

    /// <summary>Discount tiers stack: every owned tier adds its percent (the Nomad Wagon, Guild, Blacksmith).</summary>
    public static float TotalDiscount(JToken tiers, Estate estate)
    {
        if (tiers is not JArray list) return 0f;
        float total = 0f;
        foreach (JObject tier in list)
        {
            string code = (string)tier["upgrade_requirement_code"];
            string tree = (string)tier["upgrade_tree_id"];
            if (code != null && tree != null && estate.Upgrades.Contains(tree + ":" + code))
                total += (float?)tier["discount_percent"] ?? 0f;
        }
        return total;
    }

    public static Reward Cost(JObject tier) =>
        tier?["cost_currency"] is JObject c ? new Reward((string)c["type"], (int)c["amount"]) : null;
}

internal static class Json
{
    /// <summary>DD1's JSON sometimes has a BOM or // comments; Newtonsoft copes with both.</summary>
    public static JToken Read(string path) => JToken.Parse(File.ReadAllText(path));
}
