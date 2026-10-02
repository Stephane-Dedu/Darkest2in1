using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

public sealed class TrapDef
{
    public string Id;
    public float HealthFraction;            // negative = damage as a fraction of max HP
    public List<string> FailEffects = new();
    public List<string> SuccessEffects = new();
}

/// <summary>DD1 traps (<c>props/trap_definitions.json</c>), with per-difficulty variations.</summary>
public sealed class TrapLibrary
{
    private readonly Dictionary<string, JObject> _traps = new();

    public static TrapLibrary Load(Dd1Install dd1)
    {
        var lib = new TrapLibrary();
        var path = dd1.PathOf("props", "trap_definitions.json");
        if (!File.Exists(path)) return lib;
        foreach (JObject p in JToken.Parse(File.ReadAllText(path))["props"] ?? new JArray())
            lib._traps[(string)p["name"]] = p;
        return lib;
    }

    /// <summary>The trap at a difficulty: the default data, overridden by the highest variation at or below it.</summary>
    public TrapDef Get(string id, int difficulty)
    {
        if (id == null || !_traps.TryGetValue(id, out var t)) return null;
        var data = (JObject)t["default_data"]?.DeepClone() ?? new JObject();
        var variation = (t["difficulty_variations"] as JArray ?? new JArray())
            .Where(v => (int?)v["level"] <= difficulty)
            .OrderBy(v => (int?)v["level"])
            .LastOrDefault();
        if (variation is JObject v) data.Merge(v, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });

        return new TrapDef
        {
            Id = id,
            HealthFraction = (float?)data["health"] ?? 0f,
            FailEffects = (data["fail_effects"] as JArray ?? new JArray()).Select(e => (string)e).ToList(),
            SuccessEffects = (data["success_effects"] as JArray ?? new JArray()).Select(e => (string)e).ToList(),
        };
    }
}

/// <summary>The DD1 content libraries the crawl draws on.</summary>
public sealed class CrawlContent
{
    public CurioResolver Curios;
    public TrapLibrary Traps;
    public EffectLibrary Effects;
    public LootTables Loot;
    public ItemCatalog Items;
    public CampingSkills Camping;

    public static CrawlContent Load(Dd1Install dd1)
    {
        var effects = EffectLibrary.Load(dd1);
        var loot = LootTables.Load(dd1);
        return new CrawlContent
        {
            Effects = effects,
            Loot = loot,
            Traps = TrapLibrary.Load(dd1),
            Items = ItemCatalog.Load(dd1),
            Camping = CampingSkills.Load(dd1),
            Curios = new CurioResolver(CurioLibrary.Load(dd1), effects, loot),
        };
    }
}
