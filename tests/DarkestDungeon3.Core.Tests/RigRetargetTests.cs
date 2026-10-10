using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>The owner's private exports and the knight's rig dump; without them the pairing cases have nothing to read.</summary>
public class RigRetargetTests
{
    private const string Cinder = @"C:\Users\Piral\ds3-extract\soul_of_cinder\soul_of_cinder.gltf";
    private const string KnightRig = @"C:\Users\Piral\.universal-modder\inspection\exported-boss-20261010\knight_rig.json";

    [Fact]
    public void EveryLimbOfTheBipedHasADd2Bone()
    {
        foreach (string side in new[] { "L", "R" })
            foreach (string bone in new[] { "Clavicle", "UpperArm", "Forearm", "Hand", "Thigh", "Calf", "Foot", "Toe0", "Finger0", "Finger42" })
                Assert.True(RigRetarget.Ds3ToDd2.ContainsKey($"{side}_{bone}"), $"{side}_{bone}");
        Assert.Equal("ROOTSHJnt", RigRetarget.Ds3ToDd2["Pelvis"]);
        Assert.Equal("l_Arm_ShoulderSHJnt", RigRetarget.Ds3ToDd2["L_UpperArm"]);
        Assert.Equal("r_Thumb_01_03SHJnt", RigRetarget.Ds3ToDd2["R_Finger02"]);
        Assert.Equal("r_Finger_04_02SHJnt", RigRetarget.Ds3ToDd2["R_Finger41"]);
        Assert.All(RigRetarget.Aim, pair => Assert.True(RigRetarget.Ds3ToDd2.ContainsKey(pair.Key) && RigRetarget.Ds3ToDd2.ContainsKey(pair.Value), pair.Key));
    }

    [Fact]
    public void TheSoulOfCinderPairsWithTheKnightLimbForLimb()
    {
        if (!File.Exists(Cinder) || !File.Exists(KnightRig)) return;
        var export = GltfModel.Load(Cinder);
        var ds3 = export.Joints.Select(j => export.Nodes[j].Name).ToList();
        var dd2 = JObject.Parse(File.ReadAllText(KnightRig))["bones"]!.Select(b => (string)b).ToList();
        var pairs = RigRetarget.Pairs(ds3, dd2);
        Assert.Equal(RigRetarget.Ds3ToDd2.Count, pairs.Count);          // every mapped bone exists on both sides
        Assert.Equal(pairs.Count, pairs.Select(p => p.Dd2).Distinct().Count());
    }
}
