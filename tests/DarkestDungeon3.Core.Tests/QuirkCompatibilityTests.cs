using DarkestDungeon3.Core.Dd2Data;

namespace DarkestDungeon3.Core.Tests;

public sealed class QuirkCompatibilityTests
{
    [Fact]
    public void Resolved_road_and_inn_only_quirks_do_not_occupy_campaign_quirk_slots()
    {
        WithTables(t =>
        {
            Assert.False(t.Quirks["road"].CanGenerate("crusader"));
            Assert.False(t.Quirks["banter"].CanGenerate("crusader"));
            Assert.False(t.Quirks["inn_block"].CanGenerate("crusader"));
            Assert.False(t.Quirks["relationship"].CanGenerate("crusader"));
            Assert.False(t.Quirks["node"].CanGenerate("crusader"));
        });
    }

    [Fact]
    public void Mixed_combat_quirks_combat_item_blocks_and_unresolved_future_payloads_are_preserved()
    {
        WithTables(t =>
        {
            Assert.True(t.Quirks["combat"].CanGenerate("crusader"));
            Assert.True(t.Quirks["mixed"].CanGenerate("crusader"));
            Assert.True(t.Quirks["combat_block"].CanGenerate("crusader"));
            Assert.True(t.Quirks["unknown_reference"].CanGenerate("crusader"));
            Assert.True(t.Quirks["future_stat"].CanGenerate("crusader"));
        });
    }

    [Fact]
    public void Exact_DD1_town_consumers_retain_their_native_quirk_without_enabling_unrelated_quirks()
    {
        WithTables(t =>
        {
            Assert.False(t.Quirks["inn_block"].CanGenerate("crusader"));
            QuirkCompatibility.AllowCampaignEffects(t, new[] { "inn_block", "unknown_dd1_quirk" });
            Assert.True(t.Quirks["inn_block"].CanGenerate("crusader"));
            Assert.False(t.Quirks["road"].CanGenerate("crusader"));
            Assert.False(t.Quirks["relationship"].CanGenerate("crusader"));
        });
    }

    private static void WithTables(Action<Dd2Tables> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), "dd3-quirk-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string Quirk(string id, string body = "") => $"element_start,{id},Quirk\nm_Tags,positive,pos_start,\n{body}element_end\n";
            string Buffs(string id, string buffs) => $"element_start,{id},ActorDataExternalBuffs\nbuffs,{buffs},\nelement_end\n";
            File.WriteAllText(Path.Combine(dir, "quirk_data_export.Group.csv"),
                Quirk("road") + Buffs("road", "scout") + Quirk("combat") + Buffs("combat", "damage")
                + Quirk("mixed") + Buffs("mixed", "scout,damage") + Quirk("relationship") + Buffs("relationship", "affinity")
                + Quirk("node") + Buffs("node", "node_only") + Quirk("future_stat") + Buffs("future_stat", "unknown_stat")
                + Quirk("banter") + "element_start,banter,ActorDataActOut\nact_outs,road_chat,\nelement_end\n"
                + Quirk("inn_block") + "element_start,inn_block,ItemBlock\nm_ItemIds,inn_food,\nelement_end\n"
                + Quirk("combat_block") + "element_start,combat_block,ItemBlock\nm_ItemIds,bandages,\nelement_end\n"
                + Quirk("unknown_reference", "m_ExternalBuffsId,new_dlc_payload,\n"));
            File.WriteAllText(Path.Combine(dir, "buff_data_export.Group.csv"),
                "element_start,scout,Buff\nelement_end\nelement_start,scout,RunDataStats\nkey_map,scout_route_chance,\nadd_stats,1,\nelement_end\n"
                + "element_start,damage,Buff\nelement_end\nelement_start,damage,ActorDataStats\nkey_map,health_damage_dealt_percent,\nadd_stats,0.1,\nelement_end\n"
                + "element_start,affinity,Buff\nelement_end\nelement_start,affinity,ActorDataStats\nsub_stat,affinity_relationship_tag_chance_modifier,positive,0.1,\nelement_end\n"
                + "element_start,node_only,Buff\nelement_end\nelement_start,node_only,ActorDataEffects\nenter_biome_effects,some_effect,\nelement_end\n"
                + "element_start,unknown_stat,Buff\nelement_end\nelement_start,unknown_stat,ActorDataStats\nkey_map,new_combat_stat,\nadd_stats,1,\nelement_end\n");
            File.WriteAllText(Path.Combine(dir, "quirk_act_out_data_export.Group.csv"),
                "element_start,road_chat,ActOut\nm_Type,banter,\nelement_end\n");
            File.WriteAllText(Path.Combine(dir, "item_data_export.Group.csv"),
                "element_start,inn_food,Item\nm_type,rest,\nelement_end\nelement_start,bandages,Item\nm_type,combat,\nelement_end\n");
            check(Dd2Tables.Load(dir));
        }
        finally { Directory.Delete(dir, true); }
    }
}
