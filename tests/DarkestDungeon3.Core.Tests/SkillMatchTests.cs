using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dd2Data;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1 monsters play their DD1 skills over DD2 stand-ins: the stand-in's role and each skill must fit.</summary>
public class SkillMatchTests
{
    private readonly ITestOutputHelper _out;
    public SkillMatchTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly string Excel = File.Exists(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel\quirk_data_export.Group.csv")
        ? @"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel"
        : @"C:\Users\Piral\dd2-decomp\data\Excel";
    private static readonly Dd2Tables Tables = Dd2Tables.Load(Excel);
    private static readonly Dd1Bestiary Bestiary = Dd1Bestiary.Load(Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "monsters.json"));

    [Fact]
    public void ReadsDd1MonsterSkills()
    {
        var arbalist = Dd1MonsterSkills.Read(Install, "skeleton_arbalist", 'A');
        var shot = arbalist.Single(s => s.Id == "crossbow_shot");
        Assert.Equal("ranged", shot.Kind);
        Assert.Equal(new[] { 3, 4 }, shot.LaunchRanks);
        Assert.Equal("melee", arbalist.Single(s => s.Id == "bayonet_jab").Kind);
        Assert.Equal("flex", Dd1MonsterSkills.Role(arbalist));   // crossbow from 3-4, bayonet from 1-2
        Assert.Equal("front", Dd1MonsterSkills.Role(Dd1MonsterSkills.Read(Install, "skeleton_defender", 'A')));
        Assert.Equal(2, Dd1MonsterSkills.Size(Install, "swinetaur", 'A'));
    }

    [Fact]
    public void Dd2SkillsAreReadPerEnemy()
    {
        var skills = Tables.SkillsOf("lost_battalion_arbalist");
        Assert.Contains(skills, s => s.Id == "arbalist_hip_shot" && s.Ranged);
        Assert.Contains(Tables.SkillsOf("lost_battalion_drummer"), s => s.Friendly);
    }

    [Fact]
    public void MatchKeepsKindsApart()
    {
        var dd2 = new List<SkillShape>
        {
            new() { Id = "hit", LaunchRanks = { 1, 2 }, TargetRanks = { 1, 2 } },
            new() { Id = "shoot", Ranged = true, LaunchRanks = { 3, 4 }, TargetRanks = { 2, 3, 4 } },
            new() { Id = "heal", Friendly = true, LaunchRanks = { 1, 2, 3, 4 } },
        };
        var dd1 = new List<SkillShape>
        {
            new() { Id = "spit", Ranged = true, LaunchRanks = { 3, 4 }, TargetRanks = { 1, 2, 3, 4 } },
            new() { Id = "rally", Friendly = true, LaunchRanks = { 1, 2, 3, 4 } },
            new() { Id = "bite", LaunchRanks = { 1, 2 }, TargetRanks = { 1, 2 } },
        };
        var m = Dd1MonsterSkills.Match(dd2, dd1);
        Assert.Equal("bite", dd1[m["hit"]].Id);
        Assert.Equal("spit", dd1[m["shoot"]].Id);
        Assert.Equal("rally", dd1[m["heal"]].Id);
    }

    /// <summary>Every stand-in in monsters.json plays the same role as its DD1 monster (front, back or support).</summary>
    [Fact]
    public void StandInsPlayTheDd1MonstersRole()
    {
        var problems = new List<string>();
        foreach (var (family, dd2, champion) in Bestiary.Entries)
        {
            var dd1 = Dd1MonsterSkills.Read(Install, Bestiary.ArtFamily(family), 'A');
            if (dd1.Count == 0) { problems.Add($"{family}: no DD1 skills found"); continue; }
            string role = Dd1MonsterSkills.Role(dd1);
            int size = Dd1MonsterSkills.Size(Install, Bestiary.ArtFamily(family), 'A');
            foreach (var standIn in dd2.Concat(champion).Distinct())
            {
                var skills = Tables.SkillsOf(standIn);
                if (skills.Count == 0) { problems.Add($"{family} -> {standIn}: no DD2 skills"); continue; }
                var match = Dd1MonsterSkills.Match(skills, dd1);
                var allowed = Dd1MonsterSkills.Allowed(skills, dd1);
                string theirs = Dd1MonsterSkills.Role(skills.Where(s => allowed.Contains(s.Id)).ToList());   // what it will use
                _out.WriteLine($"{family} ({role}) -> {standIn} ({theirs}): " +
                               string.Join(", ", skills.Select(s => $"{s.Id}={(allowed.Contains(s.Id) ? dd1[match[s.Id]].Id : "unused")}")));
                if (allowed.Count == 0) problems.Add($"{family} -> {standIn}: no usable DD2 skill");
                int theirSize = Tables.ActorSizes.TryGetValue(standIn, out var z) ? z : 1;
                if (theirSize != size) problems.Add($"{family} (size {size}) -> {standIn} (size {theirSize})");
                if (!Dd1MonsterSkills.Compatible(role, theirs)) problems.Add($"{family} ({role}: {string.Join(" ", dd1.Select(s => s.Kind))}) -> {standIn} ({theirs}: {string.Join(" ", skills.Select(s => s.Kind))})");
            }
        }
        foreach (var p in problems) _out.WriteLine("MISMATCH " + p);
        Assert.True(problems.Count == 0, problems.Count + " mismatches:\n" + string.Join("\n", problems));
    }
}
