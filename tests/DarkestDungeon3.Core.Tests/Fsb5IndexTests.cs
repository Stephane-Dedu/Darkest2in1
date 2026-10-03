using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Fsb5IndexTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void FindsDd1SamplesByName()
    {
        string dir = Install.PathOf("audio", "secondary_banks");
        var index = Fsb5Index.Load(new[] { "music", "ambience", "general", "town" }.Select(b => Path.Combine(dir, b + ".bank")));
        Assert.True(index.Count > 800);
        Assert.True(index.TryGet("Town_Stereo_Mix_LOOP_1", out var chunk, out int i));
        Assert.EndsWith("music.bank", chunk.File);
        Assert.Equal("Town_Stereo_Mix_LOOP_1", chunk.Names[i]);
        Assert.True(index.Has("amb_dun_ruins_base"));
        Assert.True(index.Has("gen_party_foot_stone_01"));
        Assert.True(index.Has("town_enter_tavern"));
        // The FSB5 sits whole inside its bank file.
        Assert.All(index.Chunks, c => Assert.True(c.Offset + c.Length <= new FileInfo(c.File).Length));
    }
}

public class Dd1AudioNameTests
{
    /// <summary>Every DD1 sample the plugin's audio asks for by name is in DD1's banks.</summary>
    [Fact]
    public void EverySampleWeAskForExists()
    {
        string dir = Dd1Install.Find().PathOf("audio", "secondary_banks");
        var index = Fsb5Index.Load(new[] { "music", "ambience", "general", "ui_dungeon", "ui_shared", "ui_town", "raid_screen", "town" }
            .Select(b => Path.Combine(dir, b + ".bank")));
        var names = new[]
        {
            "Town_Stereo_Mix_LOOP_1", "Camping_Stereo_Mix_LOOP_1", "Mournweald_LEVEL1_LOOP1_V11", "Mournweald_LEVEL2_LOOP1_V13b",
            "Explore_Vaults_Level_1_Loop", "Explore_Vaults_Level_2_Loop", "Explore_Vaults_Level_3_Loop", "Explore_Vaults_Level_4_Loop",
            "mus_combat_weald_hallway", "Combat_Level1_Loop1", "mus_combat_warrens_hallway", "WARRENS_Combat_LOOP1_LEVEL1_V06b",
            "mus_combat_cove_hallway_a", "mus_combat_cove_lvl1_loop1", "mus_combat_dd_hallway_a", "mus_combat_dd_lvl1_loop1", "mus_combat_hallway_part_a",
            "amb_dun_ruins_base", "amb_dun_ruins_dark", "amb_dun_weald_base", "amb_dun_weald_dark", "amb_dun_warrens_base", "amb_dun_warrens_dark",
            "amb_dun_cove_base", "amb_dun_cove_dark", "amb_dun_darkest_1_base", "amb_town_gen_base", "amb_local_campfire",
            "gen_map_door_open", "gen_map_campstart", "gen_map_campend", "gen_map_torchout", "gen_combat_ambush", "Combat_Level2_Victory",
            "town_enter_tavern", "town_enter_abbey", "town_enter_blacksmith", "town_enter_guild", "town_enter_sanitarium", "town_enter_graveyard", "town_enter_coach",
        };
        var missing = names.Where(n => !index.Has(n)).ToList();
        foreach (var prefix in new[] { "gen_party_foot_stone_", "gen_party_foot_dirt_" })
            if (!index.Names.Any(n => n.StartsWith(prefix))) missing.Add(prefix + "*");
        Assert.True(missing.Count == 0, string.Join(", ", missing));
        // What the button and retreat sounds resolve to (prefix families or single names).
        Assert.Contains(index.Names, n => n.StartsWith("ui_town_button_click") || n.StartsWith("ui_shared_button_click"));
    }
}
