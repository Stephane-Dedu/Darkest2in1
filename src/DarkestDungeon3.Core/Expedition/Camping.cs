using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

public sealed class CampEffect
{
    public string Selection;   // self, individual, party, party_other
    public string Type;        // stress_heal_amount, health_heal_max_health_percent, buff, remove_poison...
    public string SubType;     // buff id for "buff"
    public float Amount;
    public float Chance = 1f;
}

public sealed class CampSkill
{
    public string Id;
    public int Cost;
    public int UseLimit;
    public List<CampEffect> Effects = new();
    public List<string> Classes = new();
    public int TeachCost;      // gold at the Survivalist
    public bool NeedsTarget => Effects.Any(e => e.Selection == "individual");
}

/// <summary>DD1 camping skills (<c>raid/camping/*.camping_skills.json</c>, including DLC classes).</summary>
public sealed class CampingSkills
{
    /// <summary>DD2 classes with no DD1 namesake borrow a DD1 class's camp skills.</summary>
    public static readonly Dictionary<string, string> Dd1ClassFor = new()
    {
        ["runaway"] = "arbalest",
    };

    public Dictionary<string, CampSkill> Skills { get; } = new();

    /// <summary>DD1: a skill listed for this many classes or more counts as shared by everyone.</summary>
    public int SharedThreshold { get; private set; } = 4;

    public static CampingSkills Load(Dd1Install dd1)
    {
        var lib = new CampingSkills();
        var files = new List<string> { dd1.PathOf("raid", "camping", "default.camping_skills.json") };
        var dlc = dd1.PathOf("dlc");
        if (Directory.Exists(dlc)) files.AddRange(Directory.GetFiles(dlc, "*.camping_skills.json", SearchOption.AllDirectories));

        foreach (var file in files.Where(File.Exists))
        {
            var root = JToken.Parse(File.ReadAllText(file));
            if (root["configuration"]?["class_specific_number_of_classes_threshold"] is JValue t) lib.SharedThreshold = (int)t;
            foreach (var s in root["skills"] ?? new JArray())
            {
                string id = (string)s["id"];
                if (!lib.Skills.TryGetValue(id, out var skill))
                {
                    skill = new CampSkill
                    {
                        Id = id,
                        Cost = (int?)s["cost"] ?? 0,
                        UseLimit = (int?)s["use_limit"] ?? 1,
                        TeachCost = (int?)s["upgrade_requirements"]?.First?["currency_cost"]?.First?["amount"] ?? 1750,
                        Effects = s["effects"].Select(e => new CampEffect
                        {
                            Selection = (string)e["selection"],
                            Type = (string)e["type"],
                            SubType = (string)e["sub_type"],
                            Amount = (float?)e["amount"] ?? 0f,
                            Chance = (float?)e["chance"]?["amount"] ?? 1f,
                        }).ToList(),
                    };
                    lib.Skills[id] = skill;
                }
                // DLC files add classes to shared skills (e.g. the Flagellant to "encourage").
                foreach (var c in s["hero_classes"] ?? new JArray())
                    if (!skill.Classes.Contains((string)c)) skill.Classes.Add((string)c);
            }
        }
        return lib;
    }

    public CampSkill Get(string id) => id != null && Skills.TryGetValue(id, out var s) ? s : null;

    /// <summary>The camp skills a DD2 class can learn, via its DD1 namesake.</summary>
    public List<string> ForClass(string dd2ClassId)
    {
        string dd1Class = Dd1ClassFor.TryGetValue(dd2ClassId, out var mapped) ? mapped : dd2ClassId;
        return Skills.Values
            .Where(s => s.Classes.Contains(dd1Class) || s.Classes.Count >= SharedThreshold)
            .Select(s => s.Id).ToList();
    }

    /// <summary>DD1 heroes start knowing four of their class's camp skills.</summary>
    public List<string> Starting(string dd2ClassId, Rng rng)
    {
        var all = ForClass(dd2ClassId);
        rng.Shuffle(all);
        return all.Take(4).ToList();
    }
}

public enum Meal { None, Half, Full, Feast }

/// <summary>A camp in progress (DD1: firewood in a cleared room, 12 respite points).</summary>
public sealed class CampState
{
    public int RespiteLeft;
    public bool Ate;
    public float AmbushReduction;
    public Dictionary<string, int> Uses = new();      // "heroId:skillId" -> times used
    /// <summary>DD1 camp buffs per hero, applied as DD2 tokens at the next fights.</summary>
    public Dictionary<string, List<string>> Buffs = new();
}
