using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>A DD1 buff (shared/buffs/*.json): one stat change and how long it lasts.</summary>
public sealed class Dd1Buff
{
    public string Id;
    public string Stat;          // combat_stat_add, combat_stat_multiply, resistance, stress_dmg_received_percent ...
    public string Sub;           // attack_rating, damage_low, poison ...
    public float Amount;
    public string DurationType;  // combat_end, quest_end, activity_end ... (null: for the expedition)
    public int Duration;

    /// <summary>Battles this buff lasts in the dungeon, or 0 if it lasts the whole expedition.</summary>
    public int Battles => DurationType == "combat_end" ? System.Math.Max(1, Duration) : 0;

    /// <summary>Town-only buffs (Abbey/Tavern stress relief) have no use in the dungeon.</summary>
    public bool InDungeon => DurationType != "activity_end" && DurationType != "idle_start_town_visit";
}

public sealed class Dd1Buffs
{
    private readonly Dictionary<string, Dd1Buff> _buffs = new(System.StringComparer.OrdinalIgnoreCase);

    public int Count => _buffs.Count;

    public static Dd1Buffs Load(Dd1Install dd1)
    {
        var lib = new Dd1Buffs();
        string dir = dd1.PathOf("shared", "buffs");
        if (!Directory.Exists(dir)) return lib;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            foreach (var b in JToken.Parse(File.ReadAllText(file))["buffs"] ?? new JArray())
            {
                string id = (string)b["id"];
                if (id == null || lib._buffs.ContainsKey(id)) continue;
                lib._buffs[id] = new Dd1Buff
                {
                    Id = id,
                    Stat = (string)b["stat_type"] ?? "",
                    Sub = (string)b["stat_sub_type"] ?? "",
                    Amount = (float?)b["amount"] ?? 0f,
                    DurationType = (string)b["duration_type"],
                    Duration = (int?)b["duration"] ?? 0,
                };
            }
        return lib;
    }

    public Dd1Buff Get(string id) => id != null && _buffs.TryGetValue(id, out var b) ? b : null;
}
