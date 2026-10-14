using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExportedMovesetTests
{
    private const string Donor = "lost_battalion_knight";

    private const string Json = @"{
      ""model"": ""boss.gltf"",
      ""phases"": [
        { ""weapon"": ""#01#"", ""idle"": { ""clip"": ""idle1"", ""duration"": 2.0 },
          ""moves"": { ""attackslash"": { ""clip"": ""slash1"", ""split"": [0.7, 1.3], ""duration"": 2.8 },
                       ""impact"": { ""clip"": ""hit1"", ""split"": [0.05, 0.5], ""duration"": 1.6 } } },
        { ""weapon"": ""#05#"", ""idle"": { ""clip"": ""idle2"", ""duration"": 2.0 },
          ""moves"": { ""attackslash"": { ""clip"": ""slash2"", ""split"": [0.6, 1.2], ""duration"": 2.3 } } }
      ],
      ""phase_change"": { ""on"": ""deathsdoor"", ""clip"": ""kneel"", ""duration"": 3.0, ""next"": 1 }
    }";

    [Theory]
    [InlineData("lost_battalion_knight_attackslash_antic", "attackslash", "antic")]
    [InlineData("lost_battalion_knight_riposte_action_v2_downpunch", "riposte", "action")]
    [InlineData("lost_battalion_knight_buff_impact", "buff", "impact")]
    [InlineData("lost_battalion_knight_impact_recover", "impact", "recover")]
    [InlineData("lost_battalion_knight_downswing_idle", "downswing", "idle")]
    [InlineData("lost_battalion_knight_idle_B", "idle", "idle")]
    [InlineData("lost_battalion_knight_deathsdoor_antic", "deathsdoor", "antic")]
    public void DonorStatesSplitIntoMoveAndPart(string clip, string move, string part)
    {
        Assert.Equal((move, part), ExportedMoveset.Split(clip, Donor));
    }

    [Fact]
    public void HeroRecoverySuffixTakesPrecedenceOverActionInsideTheMotionName()
    {
        var split=ExportedMoveset.Split("crusader_attack_scroll_action_scrolloutfar_recover","crusader");
        Assert.Equal("recover",split.Part);
        Assert.Equal("attack_scroll_action_scrolloutfar",split.Move);
    }

    [Fact]
    public void AttacksPlayAtTheirOwnSpeedAndHoldBetweenParts()
    {
        var set = ExportedMoveset.Parse(Json);
        // Wind-up plays from 0 and holds at its end however long DD2 lingers in the antic.
        Assert.Equal(("slash1", 0.3f, "#01#"), set.Resolve(Donor + "_attackslash_antic", Donor, 0.3f, 10f));
        Assert.Equal(0.7f, set.Resolve(Donor + "_attackslash_antic", Donor, 4f, 10f).Time);
        // The strike starts from the wind-up's end when DD2's action begins, then holds at the strike's end.
        Assert.Equal(0.7f, set.Resolve(Donor + "_attackslash_action", Donor, 0f, 10f).Time, 3);
        Assert.Equal(1.3f, set.Resolve(Donor + "_attackslash_action", Donor, 3f, 10f).Time, 3);
        // Recovery runs from the strike's end to the clip's end.
        Assert.Equal(1.5f, set.Resolve(Donor + "_attackslash_recover", Donor, 0.2f, 10f).Time, 3);
        Assert.Equal(2.8f, set.Resolve(Donor + "_attackslash_recover", Donor, 9f, 10f).Time, 3);
    }

    [Fact]
    public void StanceLoopsAndUnknownMovesFallBackToIt()
    {
        var set = ExportedMoveset.Parse(Json);
        Assert.Equal(("idle1", 1.5f, "#01#"), set.Resolve(Donor + "_idle_A", Donor, 0f, 5.5f));
        Assert.Equal("idle1", set.Resolve(Donor + "_buff_antic", Donor, 0f, 1f).Clip);   // no buff in this moveset
        Assert.Equal("hit1", set.Resolve(Donor + "_impact_action", Donor, 0.1f, 1f).Clip);
    }

    [Fact]
    public void DeathsDoorPlaysTheChangeThenTheSecondPhase()
    {
        var set = ExportedMoveset.Parse(Json);
        Assert.Equal(("kneel", 1f, "#01#"), set.Resolve(Donor + "_deathsdoor_antic", Donor, 1f, 0f));
        Assert.Equal(("kneel", 3f, "#01#"), set.Resolve(Donor + "_deathsdoor_antic", Donor, 8f, 0f));
        Assert.Equal(("idle2", 0.5f, "#05#"), set.Resolve(Donor + "_deathsdoor_idle", Donor, 0f, 2.5f));
        Assert.Equal("slash2", set.Resolve(Donor + "_attackslash_antic", Donor, 0f, 0f).Clip);
        Assert.Equal(new[] { "#01#", "#05#" }, set.Weapons.ToArray());
        set.Reset();
        Assert.Equal("slash1", set.Resolve(Donor + "_attackslash_antic", Donor, 0f, 0f).Clip);
    }

    [Fact]
    public void ChannelsSampleLinearlyAndRotationsTheShortWay()
    {
        var t = new GltfModel.Channel { Path = 't', Times = new[] { 0f, 1f }, Values = new[] { 0f, 0f, 0f, 2f, 4f, 6f } };
        var v = new float[3];
        GltfModel.Sample(t, 0.5f, v);
        Assert.Equal(new[] { 1f, 2f, 3f }, v);
        GltfModel.Sample(t, 5f, v);
        Assert.Equal(new[] { 2f, 4f, 6f }, v);                   // clamped past the last key
        // The same rotation written with both signs must not swing through the long way round.
        var r = new GltfModel.Channel { Path = 'r', Times = new[] { 0f, 1f }, Values = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, -1f } };
        var q = new float[4];
        GltfModel.Sample(r, 0.5f, q);
        Assert.Equal(1f, Math.Abs(q[3]), 3);
    }

    /// <summary>The owner's private animated Soul of Cinder export and its moveset (ds3-extract, make_moveset.py).</summary>
    [Fact]
    public void ThePrivateExportCarriesEveryClipItsMovesetNames()
    {
        const string dir = @"C:\Users\Piral\ds3-extract\soul_of_cinder\dd2";
        string gltf = Path.Combine(dir, "soul_of_cinder.gltf"), moves = Path.Combine(dir, "soul_of_cinder.moveset.json");
        if (!File.Exists(gltf) || !File.Exists(moves)) return;
        var model = GltfModel.Load(gltf);
        var set = ExportedMoveset.Parse(File.ReadAllText(moves));
        var names = model.Animations.Select(a => a.Name).ToHashSet();
        foreach (var phase in set.Phases)
        {
            Assert.Contains(phase.IdleClip, names);
            foreach (var m in phase.Moves.Values)
            {
                Assert.Contains(m.Clip, names);
                Assert.InRange(m.WindupEnd, 0f, m.StrikeEnd);
                Assert.InRange(m.StrikeEnd, m.WindupEnd, m.Duration + 0.001f);
            }
        }
        Assert.Contains(set.ChangeClip, names);
        var idle = model.Animations.First(a => a.Name == set.Phases[0].IdleClip);
        Assert.InRange(idle.Duration, 1.9f, 2.1f);
        Assert.All(idle.Channels, c => Assert.InRange(c.Node, 0, model.Nodes.Count - 1));
        Assert.Contains(idle.Channels, c => c.Path == 'r' && model.Nodes[c.Node].Name == "Pelvis");
    }
}
