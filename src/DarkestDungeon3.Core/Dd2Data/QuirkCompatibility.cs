using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd2Data;

/// <summary>Limits new campaign quirks to payloads the expedition can use. Native road scouting, coach banter,
/// inn item restrictions and relationship rolls have no DD1 campaign consumer. Mixed combat quirks stay eligible;
/// unresolved references and unfamiliar payloads stay eligible too, rather than being guessed incompatible.</summary>
public static class QuirkCompatibility
{
    /// <summary>DD1 town rules can consume an otherwise inn-only native quirk (Resolution bars drinking).
    /// Restore exact cross-game IDs only; no arbitrary substitute gains a campaign restriction here.</summary>
    public static void AllowCampaignEffects(Dd2Tables tables, IEnumerable<string> dd1QuirkIds)
    {
        foreach (string id in dd1QuirkIds?.Distinct() ?? Enumerable.Empty<string>())
            foreach (string candidate in new[] { id, "quirk_" + id, "quirk_" + id + "_pos", "quirk_" + id + "_neg" })
                if (tables.Quirks.TryGetValue(candidate, out var quirk)) quirk.HasExpeditionEffect = true;
    }

    [Flags]
    private enum Payload { Inactive = 0, Active = 1, Unknown = 2 }
    private sealed class Record
    {
        public Dictionary<string, List<string>> Fields;
        public List<string[]> Lines;
        public IReadOnlyList<string> Values(string key) => Fields.TryGetValue(key, out var value) ? value : Array.Empty<string>();
        public string Value(string key) => Values(key).FirstOrDefault();
    }

    private static readonly HashSet<string> InactiveActorStats = new(StringComparer.Ordinal)
    {
        "affinity_relationship_tag_chance_modifier", "affinity_relationship_tag_extra_duration",
        "health_heal_percent_between_nodes",
    };
    private static readonly string[] InactiveEffectContexts =
    {
        "enter_biome_", "node_", "inn_start_", "embark_", "kingdom_contagion_",
    };

    public static void Apply(Dd2Tables tables, string excelDir)
    {
        // An incomplete exported kit is not enough evidence to exclude a quirk.
        if (new[] { "buff_data_export.Group.csv", "quirk_act_out_data_export.Group.csv", "item_data_export.Group.csv" }
            .Any(name => !File.Exists(Path.Combine(excelDir, name)))) return;
        var records = new Dictionary<(string Id, string Type), Record>();
        foreach (string file in Directory.GetFiles(excelDir, "*.csv", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("temp_", StringComparison.Ordinal) || !(name.Contains("buff_data_export")
                || name.Contains("quirk_data_export") || name.Contains("act_out_data_export") || name.Contains("item_data_export"))) continue;
            foreach (var block in Dd2Tables.BlockLines(file))
                records[(block.Id, block.Type)] = new Record { Fields = block.Fields, Lines = block.Lines };
        }

        Record Get(string id, string type) => id != null && records.TryGetValue((id, type), out var record) ? record : null;

        Payload Stats(Record record)
        {
            if (record == null) return Payload.Inactive;
            var keys = record.Lines.Where(l => l[0] == "key_map").SelectMany(l => l.Skip(1))
                .Concat(record.Lines.Where(l => l[0] == "sub_stat" && l.Length > 1).Select(l => l[1])).ToList();
            if (keys.Count == 0) return record.Fields.Count > 0 ? Payload.Unknown : Payload.Inactive;
            return keys.Any(key => !InactiveActorStats.Contains(key)) ? Payload.Active : Payload.Inactive;
        }

        Payload Effects(Record record)
        {
            if (record == null) return Payload.Inactive;
            var contexts = record.Fields.Where(kv => kv.Key.EndsWith("_effects", StringComparison.Ordinal) && kv.Value.Count > 0).ToList();
            if (contexts.Count == 0) return record.Fields.Count > 0 ? Payload.Unknown : Payload.Inactive;
            return contexts.Any(kv => !InactiveEffectContexts.Any(prefix => kv.Key.StartsWith(prefix, StringComparison.Ordinal)))
                ? Payload.Active : Payload.Inactive;
        }

        Payload RunStats(Record record)
        {
            if (record == null) return Payload.Inactive;
            var keys = record.Lines.Where(l => l[0] == "key_map").SelectMany(l => l.Skip(1))
                .Concat(record.Lines.Where(l => l[0] == "sub_stat" && l.Length > 1).Select(l => l[1])).ToList();
            // These exact native scouting stats have no consumer in Core's own map/scouting system.
            // Other/future run stats might affect native combat, so retain them conservatively.
            return keys.Count > 0 && keys.All(key => key is "scout_route_chance" or "scout_node_chance")
                ? Payload.Inactive : Payload.Unknown;
        }

        Payload Blocks(Record owner, string id)
        {
            Payload result = Payload.Inactive;
            if (owner?.Values("token_ignores").Count > 0) result |= Payload.Active;
            string skillBlock = owner?.Value("m_SkillBlockId");
            if (Get(skillBlock ?? id, "SkillBlock") != null) result |= Payload.Active;
            else if (skillBlock != null) result |= Payload.Unknown;
            string itemBlock = owner?.Value("m_ItemBlockId");
            var block = Get(itemBlock ?? id, "ItemBlock");
            if (block == null) return itemBlock != null ? result | Payload.Unknown : result;
            if (block.Fields.Keys.Any(key => key != "m_ItemIds")) result |= Payload.Unknown;
            foreach (string itemId in block.Values("m_ItemIds"))
            {
                string type = Get(itemId, "Item")?.Value("m_type");
                if (type == "combat") result |= Payload.Active;
                else if (type != "rest") result |= Payload.Unknown;
            }
            return result;
        }

        Payload Buff(string id)
        {
            var buff = Get(id, "Buff");
            if (buff == null) return Payload.Unknown;
            var actorStats = Get(id, "ActorDataStats");
            var actorEffects = Get(buff.Value("m_ActorDataEffectId") ?? id, "ActorDataEffects");
            Payload result = Stats(actorStats) | Effects(actorEffects) | Blocks(buff, id) | RunStats(Get(id, "RunDataStats"));
            if (actorStats == null && actorEffects == null && Get(id, "RunDataStats") == null && Get(id, "InnDataStats") == null
                && result == Payload.Inactive) return Payload.Unknown;
            return result;
        }

        foreach (var quirk in tables.Quirks.Values)
        {
            var definition = Get(quirk.Id, "Quirk");
            if (definition == null) continue;
            Payload payload = Blocks(definition, quirk.Id);
            string externalId = definition.Value("m_ExternalBuffsId");
            var external = Get(externalId ?? quirk.Id, "ActorDataExternalBuffs");
            if (external != null)
            {
                foreach (string buff in external.Values("buffs")) payload |= Buff(buff);
                if (external.Fields.Keys.Any(key => key != "buffs")) payload |= Payload.Unknown;
            }
            else if (externalId != null) payload |= Payload.Unknown;
            string effectsId = definition.Value("m_ActorDataEffectsId");
            var effects = Get(effectsId ?? quirk.Id, "ActorDataEffects");
            payload |= effects != null ? Effects(effects) : effectsId != null ? Payload.Unknown : Payload.Inactive;
            string actOutId = definition.Value("m_ActOutId");
            var actOuts = Get(actOutId ?? quirk.Id, "ActorDataActOut");
            if (actOuts != null)
                foreach (string id in actOuts.Values("act_outs"))
                {
                    string type = Get(id, "ActOut")?.Value("m_Type");
                    if (type == null) payload |= Payload.Unknown;
                    else if (type is not ("banter" or "story_choice" or "rest_item_block")) payload |= Payload.Active;
                }
            else if (actOutId != null) payload |= Payload.Unknown;
            quirk.HasExpeditionEffect = payload != Payload.Inactive;
        }
    }
}
