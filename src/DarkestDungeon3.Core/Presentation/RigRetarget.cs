using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>
/// Which DD2 bone moves which bone of an exported Dark Souls III humanoid, so DD2's own animations (a stand-in enemy's)
/// drive the export: Havok bipeds (Pelvis, Spine..Spine2, L/R_Clavicle..Hand, Thigh..Toe0, Finger0-4) onto DD2's
/// SHJnt rig. Bones without a counterpart (twists, helpers, cape, weapon) follow their parents.
/// </summary>
public static class RigRetarget
{
    public static readonly IReadOnlyDictionary<string, string> Ds3ToDd2 = Build();

    /// <summary>For each mapped bone, the mapped child its direction points to (aligns the two rest poses).</summary>
    public static readonly IReadOnlyDictionary<string, string> Aim = new Dictionary<string, string>
    {
        ["Pelvis"] = "Spine", ["Spine"] = "Spine1", ["Spine1"] = "Spine2", ["Spine2"] = "Neck", ["Neck"] = "Head",
        ["L_Clavicle"] = "L_UpperArm", ["L_UpperArm"] = "L_Forearm", ["L_Forearm"] = "L_Hand",
        ["R_Clavicle"] = "R_UpperArm", ["R_UpperArm"] = "R_Forearm", ["R_Forearm"] = "R_Hand",
        ["L_Thigh"] = "L_Calf", ["L_Calf"] = "L_Foot", ["L_Foot"] = "L_Toe0",
        ["R_Thigh"] = "R_Calf", ["R_Calf"] = "R_Foot", ["R_Foot"] = "R_Toe0",
        ["L_Finger0"] = "L_Finger01", ["L_Finger01"] = "L_Finger02", ["R_Finger0"] = "R_Finger01", ["R_Finger01"] = "R_Finger02",
        ["L_Finger1"] = "L_Finger11", ["L_Finger11"] = "L_Finger12", ["R_Finger1"] = "R_Finger11", ["R_Finger11"] = "R_Finger12",
        ["L_Finger2"] = "L_Finger21", ["L_Finger21"] = "L_Finger22", ["R_Finger2"] = "R_Finger21", ["R_Finger21"] = "R_Finger22",
        ["L_Finger3"] = "L_Finger31", ["L_Finger31"] = "L_Finger32", ["R_Finger3"] = "R_Finger31", ["R_Finger31"] = "R_Finger32",
        ["L_Finger4"] = "L_Finger41", ["L_Finger41"] = "L_Finger42", ["R_Finger4"] = "R_Finger41", ["R_Finger41"] = "R_Finger42",
    };

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>
        {
            ["Pelvis"] = "ROOTSHJnt", ["Spine"] = "Spine_01SHJnt", ["Spine1"] = "Spine_02SHJnt", ["Spine2"] = "Spine_TopSHJnt",
            ["Neck"] = "Neck_01_01SHJnt", ["Head"] = "Head_TopSHJnt",
        };
        foreach (var (ds3, dd2) in new[] { ("L", "l"), ("R", "r") })
        {
            map[$"{ds3}_Clavicle"] = $"{dd2}_Arm_ClavicleSHJnt";
            map[$"{ds3}_UpperArm"] = $"{dd2}_Arm_ShoulderSHJnt";
            map[$"{ds3}_Forearm"] = $"{dd2}_Arm_ElbowSHJnt";
            map[$"{ds3}_Hand"] = $"{dd2}_Arm_WristSHJnt";
            map[$"{ds3}_Thigh"] = $"{dd2}_Leg_HipSHJnt";
            map[$"{ds3}_Calf"] = $"{dd2}_Leg_KneeSHJnt";
            map[$"{ds3}_Foot"] = $"{dd2}_Leg_AnkleSHJnt";
            map[$"{ds3}_Toe0"] = $"{dd2}_Leg_BallSHJnt";
            // Finger0 is the thumb; Finger1-4 index to little finger. Three joints each.
            for (int j = 0; j < 3; j++)
            {
                string suffix = j == 0 ? "" : j.ToString();
                map[$"{ds3}_Finger0{suffix}"] = $"{dd2}_Thumb_01_0{j + 1}SHJnt";
                for (int f = 1; f <= 4; f++) map[$"{ds3}_Finger{f}{suffix}"] = $"{dd2}_Finger_0{f}_0{j + 1}SHJnt";
            }
        }
        return map;
    }

    /// <summary>The pairs both rigs have, as (export bone, DD2 bone).</summary>
    public static List<(string Ds3, string Dd2)> Pairs(IEnumerable<string> ds3Bones, IEnumerable<string> dd2Bones)
    {
        var have = new HashSet<string>(dd2Bones);
        return ds3Bones.Where(b => Ds3ToDd2.TryGetValue(b, out var d) && have.Contains(d)).Select(b => (b, Ds3ToDd2[b])).ToList();
    }
}
