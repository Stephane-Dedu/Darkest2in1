using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// The bones every DD2 hero rig shares, used to line a corridor stage model up with the same hero in DD2's fight:
/// mesh bounds don't follow the animated pose, the skeleton does.
/// </summary>
internal static class HeroBones
{
    public static (Transform Root, Transform Head, Transform LeftAnkle, Transform RightAnkle)? Find(IEnumerable<Transform> transforms)
    {
        var all = transforms.Where(t => t != null).ToList();
        Transform Bone(string suffix) => all.FirstOrDefault(t => t.name.EndsWith(suffix, StringComparison.Ordinal));
        var root = Bone("ROOTSHJnt");
        var head = Bone("Head_TopSHJnt");
        var left = Bone("l_Leg_AnkleSHJnt");
        var right = Bone("r_Leg_AnkleSHJnt");
        return root == null || head == null || left == null || right == null ? null : (root, head, left, right);
    }
}
