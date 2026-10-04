using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>One DD1 quest goal (<c>campaign/quest/quest.types.json</c>).</summary>
public sealed class QuestGoal
{
    public string Id;
    public string Type;              // explore_room, battle_room, gather, activate, kill_monster
    public string CurioName;         // gather / activate
    public string QuestItem;         // gather: what the curio yields
    public int Amount;
    public float Percentage;         // explore / battle
    public List<string> MonsterClasses = new();
    public List<(string Id, int Amount)> StartingItems = new();

    /// <summary>Inventory-activate goals hand the party the quest items to use on the curios.</summary>
    public bool NeedsItem => StartingItems.Count > 0;
}

public sealed class PlotQuest
{
    public string Id;
    public int ZoneLevel;            // DD1 dungeon_level at which it appears
    public string Dungeon, Type;
    public int Difficulty, Length;
    public List<string> GoalIds = new();
    public int ResolveXp;
    public List<Reward> Rewards = new();
    public bool Repeatable, Progression;
    /// <summary>DD1: whether the quest can be abandoned, and how many random heroes die covering the retreat
    /// (the Darkest Dungeon: 1; its last part can't be abandoned).</summary>
    public bool CanRetreat = true;
    public int RetreatKillCount;
    /// <summary>DD1: no surprise / no scouting in the quest (the Darkest Dungeon), and a win clears the roster's stress.</summary>
    public bool SurpriseEnabled = true, ScoutingEnabled = true, ClearsRosterStress;
    /// <summary>DD1: buffs the whole roster gets when a party of at least this resolve fails the quest.</summary>
    public List<string> RosterBuffsOnFailure = new();
    public int RosterBuffMinResolve;
    /// <summary>DD1's hand-made map for the quest (maps/&lt;name&gt;.dm), e.g. the Darkest Dungeon's DD_map1; null = generated.</summary>
    public string MapName;
}

public sealed class QuestGoals
{
    public Dictionary<string, QuestGoal> Goals { get; } = new();
    /// <summary>quest type → zone ("all" for any) → goal ids.</summary>
    public Dictionary<string, Dictionary<string, List<string>>> TypeGoals { get; } = new();
    public List<PlotQuest> Plot { get; } = new();

    public static QuestGoals Load(Dd1Install dd1)
    {
        var q = new QuestGoals();
        var types = JToken.Parse(File.ReadAllText(dd1.PathOf("campaign", "quest", "quest.types.json")));
        foreach (var g in types["goals"])
        {
            var data = g["data"] as JObject ?? new JObject();
            var goal = new QuestGoal
            {
                Id = (string)g["id"],
                Type = (string)g["type"],
                CurioName = (string)data["curio_name"],
                QuestItem = (string)data["item"]?["id"],
                Amount = (int?)data["amount"] ?? (int?)data["item"]?["amount"] ?? 0,
                Percentage = (float?)data["percentage"] ?? 0f,
                MonsterClasses = (data["monster_class_ids"] as JArray ?? new JArray()).Select(m => (string)m).ToList(),
                StartingItems = (g["starting_items"] as JArray ?? new JArray()).Select(i => ((string)i["id"], (int)i["amount"])).ToList(),
            };
            if (goal.Type == "gather" && goal.Amount == 0) goal.Amount = (int?)data["item"]?["amount"] ?? 3;
            q.Goals[goal.Id] = goal;
        }
        foreach (var t in types["types"])
        {
            var byZone = new Dictionary<string, List<string>>();
            foreach (var gl in t["goal_lists"])
                byZone[(string)gl["dungeon"]] = gl["goals"].SelectMany(x => x).Select(x => (string)x).ToList();
            q.TypeGoals[(string)t["id"]] = byZone;
        }

        var plot = JToken.Parse(File.ReadAllText(dd1.PathOf("campaign", "quest", "quest.plot_quests.json")));
        foreach (var p in plot["plot_quests"])
        {
            var quest = p["quest"];
            var pq = new PlotQuest
            {
                Id = (string)p["id"],
                ZoneLevel = (int?)p["dungeon_level"] ?? 0,
                Dungeon = (string)quest["dungeon"],
                Type = (string)quest["type"],
                Difficulty = (int?)quest["difficulty"] ?? 1,
                Length = (int?)quest["length"] ?? 2,
                GoalIds = (quest["goal_ids"] as JArray ?? new JArray()).Select(x => (string)x).ToList(),
                ResolveXp = (int?)quest["completion_reward"]?["resolve_xp"] ?? 0,
                Repeatable = (bool?)p["is_repeatable"] ?? false,
                Progression = (bool?)p["is_progression"] ?? false,
                CanRetreat = (bool?)p["can_retreat"] ?? true,
                RetreatKillCount = (int?)p["retreat_party_kill_count"] ?? 0,
                SurpriseEnabled = (bool?)p["is_surprise_enabled"] ?? true,
                ScoutingEnabled = (bool?)p["is_scouting_enabled"] ?? true,
                ClearsRosterStress = (bool?)p["is_roster_stress_cleared_on_completion"] ?? false,
                RosterBuffsOnFailure = (p["roster_buffs_to_apply_on_failure"] as JArray ?? new JArray()).Select(x => (string)x).ToList(),
                RosterBuffMinResolve = (int?)p["roster_buff_on_failure_minimum_party_resolve_level"] ?? 0,
                MapName = string.IsNullOrEmpty((string)quest["map_name"]) ? null : (string)quest["map_name"],
            };
            foreach (var item in (quest["completion_reward"]?["items_definition"]?["items"] as JObject)?.Properties().Select(x => x.Value) ?? Enumerable.Empty<JToken>())
            {
                string type = (string)item["type"];
                pq.Rewards.Add(new Reward(type == "heirloom" ? (string)item["id"] : type, (int)item["amount"]));
            }
            foreach (var t in p["additional_trinket_completion_rewards"] as JArray ?? new JArray())
                pq.Rewards.Add(new Reward("trinket", (int?)t["amount"] ?? 1, (string)t["rarity"]));
            q.Plot.Add(pq);
        }
        return q;
    }

    /// <summary>The goal a quest of this type uses in this zone (zone-specific first, then "all").</summary>
    public QuestGoal For(string questType, string zone)
    {
        if (!TypeGoals.TryGetValue(questType, out var byZone)) return null;
        zone = Core.Dungeon.ZoneBase.Of(zone);
        var ids = byZone.TryGetValue(zone, out var z) && z.Count > 0 ? z
                : byZone.TryGetValue("all", out var all) ? all : new List<string>();
        return ids.Select(id => Goals.TryGetValue(id, out var g) ? g : null).FirstOrDefault(g => g != null);
    }

    public QuestGoal For(QuestOffer quest) =>
        quest.GoalId != null && Goals.TryGetValue(quest.GoalId, out var g) ? g : For(quest.Type, quest.Dungeon);
}
