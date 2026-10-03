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
