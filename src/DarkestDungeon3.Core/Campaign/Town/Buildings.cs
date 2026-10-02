using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>A stress-relief activity: Abbey (meditation, prayer, flagellation) or Tavern (bar, gambling, brothel).</summary>
public sealed class ActivityDef
{
    public string Building;
    public string Id;
    public JObject Data;

    public string Key => Building + "." + Id;   // also the activity's upgrade tree id

    public Reward Cost(Estate e) => Tiers.Cost(Tiers.Current(Data["cost_upgrades"], e, Key));
    public int Slots(Estate e) => (int?)Tiers.Current(Data["slot_upgrades"], e, Key)?["number_of_slots"] ?? 1;

    public (int Low, int High) StressHeal(Estate e)
    {
        var t = Tiers.Current(Data["stress_upgrades"], e, Key);
        return ((int?)t?["heal_low"] ?? 0, (int?)t?["heal_high"] ?? 0);
    }

    public float SideEffectChance => (float?)Data["side_effects"]?["chance"] ?? 0f;
    public JArray SideEffects => Data["side_effects"]?["results"] as JArray ?? new JArray();

    /// <summary>DD1 quirk ids that bar a hero from this activity (e.g. no prayer for the faithless).</summary>
    public IEnumerable<string> ForbiddenQuirks =>
        (Data["requirements"] as JArray ?? new JArray())
            .Where(r => (string)r["type"] == "not_have_quirks")
            .SelectMany(r => r["data"]["quirk_library_names"].Select(q => (string)q));
}

/// <summary>DD1's Hamlet buildings, read from <c>campaign/town/buildings/*/*.building.json</c>.</summary>
public sealed class Buildings
{
    public const string StageCoach = "stage_coach";
    public const string Abbey = "abbey";
    public const string Tavern = "tavern";
    public const string Sanitarium = "sanitarium";
    public const string Guild = "guild";
    public const string Blacksmith = "blacksmith";
    public const string NomadWagon = "nomad_wagon";
    public const string Survivalist = "camping_trainer";
    public const string Graveyard = "graveyard";
    public const string Memorial = "statue";

    public static readonly string[] All = { StageCoach, Abbey, Tavern, Sanitarium, Guild, Blacksmith, NomadWagon, Survivalist, Graveyard, Memorial };

    private readonly Dictionary<string, JObject> _defs = new();
    public UpgradeTrees Trees { get; private set; }
    public List<ActivityDef> Activities { get; } = new();

    public static Buildings Load(Dd1Install dd1)
    {
        var b = new Buildings { Trees = UpgradeTrees.Load(dd1) };
        foreach (var id in All)
        {
            var path = dd1.PathOf("campaign", "town", "buildings", id, id + ".building.json");
            if (!File.Exists(path)) continue;
            var def = (JObject)Json.Read(path);
            b._defs[id] = def;
            // Only the Abbey and Tavern are stress relief; the Sanitarium's "activities" are treatments.
            if (id != Abbey && id != Tavern) continue;
            foreach (var act in def["data"]?["activities"] as JArray ?? new JArray())
                b.Activities.Add(new ActivityDef { Building = id, Id = (string)act["id"], Data = (JObject)act["data"] });
        }
        return b;
    }

    public JObject Data(string building) => _defs.TryGetValue(building, out var d) ? (JObject)d["data"] : new JObject();

    /// <summary>DD1 opens buildings as quests are finished (Abbey and Tavern after 2, Guild and Blacksmith after 3...).</summary>
    public bool IsOpen(string building, Estate e) =>
        !_defs.TryGetValue(building, out var d) || e.QuestsCompleted >= ((int?)d["requirements"]?["number_of_quests_finished"] ?? 0);

    public ActivityDef Activity(string key) => Activities.FirstOrDefault(a => a.Key == key);

    // ---- Stagecoach ----
    private JObject StageCoachStore => (JObject)Data(StageCoach)["stores"]?.First?["data"];

    public int RecruitsPerWeek(Estate e) => (int?)Tiers.Current(StageCoachStore?["number_of_recruits_upgrades"], e)?["amount"] ?? 2;
    public int RosterSize(Estate e) => (int?)Tiers.Current(StageCoachStore?["roster_size_upgrades"], e)?["amount"] ?? 9;

    /// <summary>Owned "experienced recruit" tiers: (resolve level, chance).</summary>
    public IEnumerable<(int Level, float Chance)> ExperiencedRecruits(Estate e) =>
        (StageCoachStore?["upgraded_recruits_upgrades"] as JArray ?? new JArray())
            .Where(t => e.Upgrades.Contains((string)t["upgrade_tree_id"] + ":" + (string)t["upgrade_requirement_code"]))
            .Select(t => ((int)t["level"], (float)t["chance"]));

    // ---- Nomad Wagon ----
    private JObject WagonStore => (JObject)Data(NomadWagon)["stores"]?.First?["data"];

    public int WagonStock(Estate e) => (int?)Tiers.Current(WagonStore?["number_of_trinkets_upgrades"], e)?["amount"] ?? 2;
    public float WagonDiscount(Estate e) => Tiers.TotalDiscount(WagonStore?["trinket_cost_discount_upgrades"], e);

    // ---- Discounts (Guild, Blacksmith, Survivalist) ----
    public float GuildDiscount(Estate e) => Tiers.TotalDiscount(Data(Guild)["combat_skill_cost_discount_upgrades"], e);
    public float BlacksmithDiscount(Estate e) => Tiers.TotalDiscount(Data(Blacksmith)["equipment_cost_discount_upgrades"], e);
    public float SurvivalistDiscount(Estate e) => Tiers.TotalDiscount(Data(Survivalist)["camping_skill_cost_discount_upgrades"], e);

    // ---- Sanitarium ----
    private JObject SanitariumActivity(string id) =>
        (JObject)(Data(Sanitarium)["activities"] as JArray ?? new JArray()).FirstOrDefault(a => (string)a["id"] == id)?["data"];

    public Reward QuirkTreatmentCost(Estate e, string kind) =>
        Tiers.Cost(Tiers.Current(SanitariumActivity("treatment")?[kind + "_quirk_cost_upgrades"], e));

    public Reward DiseaseTreatmentCost(Estate e) =>
        Tiers.Cost(Tiers.Current(SanitariumActivity("disease_treatment")?["disease_quirk_cost_upgrades"], e));

    public float DiseaseCureAllChance(Estate e) =>
        (float?)Tiers.Current(SanitariumActivity("disease_treatment")?["disease_quirk_cure_all_chance_upgrades"], e)?["chance"] ?? 0.33f;

    public int SanitariumSlots(Estate e, string activity) =>
        (int?)Tiers.Current(SanitariumActivity(activity)?["slot_upgrades"], e)?["number_of_slots"] ?? 1;
}
