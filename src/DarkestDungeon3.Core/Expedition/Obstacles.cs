using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

public sealed class ObstacleDef
{
    public string Id;
    public float HealthFraction;
    public float TorchChange;
    public bool AncestorTalk;
    public List<string> FailEffects = new();
}

/// <summary>DD1 obstacles inherit their costs from props/prop_definitions.json, with per-prop overrides.</summary>
public sealed class ObstacleLibrary
{
    private readonly Dictionary<string, ObstacleDef> _obstacles = new();

    public static ObstacleLibrary Load(Dd1Install dd1)
    {
        var lib = new ObstacleLibrary();
        var defaults = JToken.Parse(File.ReadAllText(dd1.PathOf("props", "prop_definitions.json")))["props"]
            ?.FirstOrDefault(p => (string)p["name"] == "obstacle")?["default_data"] as JObject ?? new JObject();
        foreach (var p in JToken.Parse(File.ReadAllText(dd1.PathOf("props", "obstacle_definitions.json")))["props"] ?? new JArray())
        {
            var data = (JObject)defaults.DeepClone();
            if (p["default_data"] is JObject own)
                data.Merge(own, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
            string id = (string)p["name"];
            lib._obstacles[id] = new ObstacleDef
            {
                Id = id,
                HealthFraction = (float?)data["health"] ?? 0f,
                TorchChange = (float?)data["torchlight"] ?? 0f,
                AncestorTalk = (bool?)data["ancestor_talk"] ?? false,
                FailEffects = (data["fail_effects"] as JArray ?? new JArray()).Select(e => (string)e).ToList(),
            };
        }
        return lib;
    }

    public ObstacleDef Get(string id) => id != null && _obstacles.TryGetValue(id, out var d) ? d : null;
}
