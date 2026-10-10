using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dd2Data;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1 monster skills become DD2 skill data (ranks, damage, crit, effects) on their stand-in's skills.</summary>
public class SkillConversionTests
{
    private readonly ITestOutputHelper _out;
    public SkillConversionTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly string Excel = File.Exists(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel\quirk_data_export.Group.csv")
        ? @"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel"
        : @"C:\Users\Piral\dd2-decomp\data\Excel";
    private static readonly Dd2Tables Tables = Dd2Tables.Load(Excel);
    private static readonly EffectLibrary Dd1Effects = EffectLibrary.Load(Install);
    private static readonly Dd1Buffs Buffs = Dd1Buffs.Load(Install);
    private static readonly Dd1Bestiary Bestiary = Dd1Bestiary.Load(Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "monsters.json"));

    // DD2's element texts (inside element_start/element_end), by type and id.
    private static readonly Dictionary<(string, string), string> Elements = LoadElements();

    private static Dictionary<(string, string), string> LoadElements()
    {
        var all = new Dictionary<(string, string), string>();
        foreach (var file in Directory.GetFiles(Excel, "*_data_export.Group.csv").OrderBy(f => Path.GetFileName(f).StartsWith("herostory") ? 1 : 0))
        {
            string text = File.ReadAllText(file);
            int at = 0;
            while ((at = text.IndexOf("element_start", at, System.StringComparison.Ordinal)) >= 0)
            {
                int end = text.IndexOf("element_end", at, System.StringComparison.Ordinal);
                if (end < 0) break;
                string block = text.Substring(at, end - at);
                int nl = block.IndexOf('\n');
                var head = block.Substring(0, nl < 0 ? block.Length : nl).Split(',');
                if (head.Length >= 3 && nl > 0) all.TryAdd((head[2].Trim(), head[1]), block.Substring(nl + 1));
                at = end;
            }
        }
        return all;
    }

    private static string Element(string type, string id) => Elements.TryGetValue((type, id), out var t) ? t : "";

    [Fact]
    public void ArbalistCrossbowBecomesADd2Skill()
    {
        var dd1 = Dd1MonsterSkills.Read(Install, "skeleton_arbalist", 'A');
        var shot = dd1.Single(s => s.Id == "crossbow_shot");
        Assert.Equal(3, shot.DamageMin);
        Assert.Equal(7, shot.DamageMax);
        Assert.Equal(0.12f, shot.Crit, 3);

        var dd2 = Tables.SkillsOf("lost_battalion_arbalist");
        var pairs = Dd1SkillToDd2.Pair(dd2, dd1);
        Assert.Equal(2, pairs.Count);                                     // DD1's two skills, one DD2 skill each
        Assert.NotEqual(pairs["crossbow_shot"], pairs["bayonet_jab"]);
        string baseId = pairs["crossbow_shot"];

        string id = Dd1SkillToDd2.GeneratedId(baseId, "skeleton_arbalist_A", "crossbow_shot");
        Assert.Equal(baseId, Dd1SkillToDd2.BaseOf(id));
        Assert.Null(Dd1SkillToDd2.BaseOf("arbalist_hip_shot"));

        var skill = Dd1SkillToDd2.Lines(Dd1SkillToDd2.SkillText(Element("ActorDataSkill", baseId), shot)).ToDictionary(l => l.Key, l => l.Values);
        Assert.Equal(new[] { "3", "4" }, skill["launch_ranks"]);
        Assert.Equal(new[] { "2", "3", "4" }, skill["target_ranks"]);
        Assert.Equal("False", skill["m_IsFriendly"][0]);
        Assert.False(skill.ContainsKey("m_AllConditionIds"));

        var stats = Dd1SkillToDd2.Stats(Dd1SkillToDd2.StatsText(Element("ActorDataStats", baseId), shot, 0.5f));
        Assert.Equal(2f, stats["health_damage"]);         // 3 x 0.5, rounded
        Assert.Equal(2f, stats["health_damage_range"]);   // 7 x 0.5 = 4 (rounded) - 2
        Assert.Equal(0.12f, stats["crit_chance"], 3);
    }

    [Fact]
    public void Dd1EffectsMapToDd2Effects()
    {
        Assert.Equal(new[] { "skill_dot_medium_blight" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Blight 1")).Target);
        Assert.Equal(new[] { "add_1_stun" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Stun 1")).Target);
        Assert.Equal(new[] { "move_knockback_1" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Push 1A")).Target);
        Assert.Equal(new[] { "stress_damage_1" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Drum Stress 1")).Target);
        Assert.Equal(new[] { "add_1_vulnerable" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Vulnerability Curse 1")).Target);
        Assert.Empty(Dd1SkillToDd2.Map(Dd1Effects.Get("Arbalist Dmg Marked Target")).Performer);   // conditional: none
        Assert.Equal(new[] { "dot_horror_medium" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Madman Horror 1")).Target);
        Assert.Equal(new[] { "move_shuffle_50pct" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Gargoyle Flurry Shuffle 3")).Target);
        Assert.Equal(10, Dd1SkillToDd2.TorchDecrease(new SkillShape { Effects = { "Darkness 2" } }, Dd1Effects.Get));
    }

    [Fact]
    public void GuardStealthAndBlockEffectsMapToRealDd2Tokens()
    {
        Assert.Equal(new[] { "remove_all_stealth" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Destealth")).Target);
        Assert.Equal(new[] { "remove_all_guard" }, Dd1SkillToDd2.Map(Dd1Effects.Get("Clear Guarded Target")).Target);
        foreach (string id in new[] { "add_1_daze", "remove_all_stealth", "remove_all_guard", "add_1_block", "add_2_block", "add_3_block", "add_1_vulnerable" })
            Assert.True(Element("Effect", id).Length > 0, id + " is not a DD2 effect");
        foreach (string id in new[] { "til_ignore_block_buff", "til_ignore_guard", "til_ignore_stealth" })
            Assert.True(Element("TokenIgnore", id).Length > 0, id + " is not a DD2 token ignore");
    }

    [Fact]
    public void SkillFlagsBecomeDd2TokenIgnoresAndAPerBattleLimit()
    {
        var shape = new SkillShape { Id = "x", LaunchRanks = { 1 }, TargetRanks = { 1 }, IgnoreProtection = true, IgnoreGuard = true, PerBattleLimit = 2 };
        var lines = Dd1SkillToDd2.Lines(Dd1SkillToDd2.SkillText("token_ignores,til_ignore_crit,\nm_Limit,3,\nm_Tags,melee,\n", shape)).ToDictionary(l => l.Key, l => l.Values);
        Assert.Equal(new[] { "til_ignore_crit", "til_ignore_block_buff", "til_ignore_guard" }, lines["token_ignores"]);
        Assert.Equal(new[] { "2" }, lines["m_Limit"]);
        var plain = Dd1SkillToDd2.Lines(Dd1SkillToDd2.SkillText("m_Limit,3,\n", new SkillShape { Id = "y", LaunchRanks = { 1 } })).ToDictionary(l => l.Key, l => l.Values);
        Assert.False(plain.ContainsKey("token_ignores"));
        Assert.Equal(new[] { "3" }, plain["m_Limit"]);      // no DD1 limit: the base skill's stays
    }

    /// <summary>Every DD1 monster we stand in for converts: each generated effect is a real DD2 effect; the DD1
    /// effects with no DD2 counterpart are listed.</summary>
    [Fact]
    public void EveryStandInConverts()
    {
        var dd2Effects = new HashSet<string>(Elements.Keys.Where(k => k.Item1 == "Effect").Select(k => k.Item2));
        Assert.Contains("add_1_stun", dd2Effects);
        var unmapped = new SortedSet<string>();
        var problems = new List<string>();
        int converted = 0;
        foreach (var (family, standIns, champions) in Bestiary.Entries)
            foreach (char tier in new[] { 'A', 'B', 'C' })
            {
                var dd1 = Dd1MonsterSkills.Read(Install, Bestiary.ArtFamily(family), tier);
                foreach (var standIn in standIns.Concat(champions).Distinct())
                {
                    var dd2 = Tables.SkillsOf(standIn);
                    var pairs = Dd1SkillToDd2.Pair(dd2, dd1);
                    if (pairs.Count == 0) { problems.Add($"{family}_{tier} -> {standIn}: no skill pairs"); continue; }
                    var dd2Attacks = pairs.Values.Select(b => Dd1SkillToDd2.Stats(Element("ActorDataStats", b)))
                                          .Where(s => s.ContainsKey("health_damage"))
                                          .Select(s => (s["health_damage"], s["health_damage"] + (s.TryGetValue("health_damage_range", out var r) ? r : 0f)));
                    float scale = Dd1SkillToDd2.DamageScale(dd2Attacks, dd1.Select(s => ((float)s.DamageMin, (float)s.DamageMax)));
                    foreach (var s in dd1.Where(s => pairs.ContainsKey(s.Id)))
                    {
                        var (t, p) = Dd1SkillToDd2.Effects(s, Dd1Effects.Get, unmapped, Buffs.Get);
                        foreach (var e in t.Concat(p).Where(e => !dd2Effects.Contains(e))) problems.Add($"{family}_{tier} {s.Id}: no DD2 effect {e}");
                        Dd1SkillToDd2.SkillText(Element("ActorDataSkill", pairs[s.Id]), s);
                        Dd1SkillToDd2.StatsText(Element("ActorDataStats", pairs[s.Id]), s, scale);
                        converted++;
                    }
                }
            }
        _out.WriteLine($"{converted} skills converted; DD1 effects without a DD2 counterpart: {string.Join(" | ", unmapped)}");
        Assert.True(converted > 150, converted.ToString());
        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct().Take(30)));
    }
}
