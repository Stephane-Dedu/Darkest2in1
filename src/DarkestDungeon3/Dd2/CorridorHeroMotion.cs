using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// A corridor-only animation layer over the owner's native hero mesh. It never changes a shared mesh,
/// controller or combat actor. Foot targets and anatomical axes come from the loaded skeleton.
/// </summary>
internal sealed class CorridorHeroMotion
{
    private readonly Transform _slot, _pelvis, _spine, _chest, _head;
    private readonly Transform _sword, _swordHand;
    private readonly Transform[] _driven;
    private readonly Animator _animator;
    private readonly Leg _left, _right;
    private readonly Vector3 _forward, _side;
    private readonly Vector3 _homeCenter;
    private readonly float _legLength;
    private readonly CorridorWalkCycle _cycle;
    private readonly List<SavedBone> _saved = new();

    private readonly struct SavedBone
    {
        internal readonly Transform Bone;
        internal readonly Vector3 Position;
        internal readonly Quaternion Rotation;
        internal SavedBone(Transform bone)
        { Bone = bone; Position = bone.localPosition; Rotation = bone.localRotation; }
        internal void Restore()
        { if (Bone != null) { Bone.localPosition = Position; Bone.localRotation = Rotation; } }
    }

    private sealed class Leg
    {
        internal Transform Hip, Knee, Ankle;
        internal float Side, AnkleY;
        internal Quaternion FlatRotation;
    }

    internal static CorridorHeroMotion TryCreate(ActorBhv actor, Transform slot, int rank)
    {
        var animator = actor.GetCurrentAnimator();
        if (animator == null || !animator.isInitialized) return null;
        var transforms = animator.GetComponentsInChildren<Transform>();
        Transform Find(string suffix) => transforms.FirstOrDefault(t => t.name.EndsWith(suffix, StringComparison.Ordinal));
        var pelvis = Find("ROOTSHJnt");
        var left = new Leg { Hip = Find("l_Leg_HipSHJnt"), Knee = Find("l_Leg_KneeSHJnt"), Ankle = Find("l_Leg_AnkleSHJnt") };
        var right = new Leg { Hip = Find("r_Leg_HipSHJnt"), Knee = Find("r_Leg_KneeSHJnt"), Ankle = Find("r_Leg_AnkleSHJnt") };
        if (pelvis == null || left.Hip == null || left.Knee == null || left.Ankle == null
            || right.Hip == null || right.Knee == null || right.Ankle == null) return null;

        // Model-space bind matrices give foot orientation and hip spacing without a combat pose's wide stance.
        var body = animator.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Where(r => r.sharedMesh != null && r.bones.Contains(left.Hip) && r.bones.Contains(right.Hip))
            .OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
        if (body == null) return null;
        var bind = new Dictionary<Transform, Matrix4x4>();
        var bones = body.bones;
        var poses = body.sharedMesh.bindposes;
        for (int i = 0; i < bones.Length && i < poses.Length; i++)
            if (bones[i] != null) bind[bones[i]] = slot.worldToLocalMatrix * body.localToWorldMatrix * poses[i].inverse;
        if (!bind.ContainsKey(left.Ankle) || !bind.ContainsKey(right.Ankle)
            || !bind.ContainsKey(left.Hip) || !bind.ContainsKey(right.Hip)) return null;

        Vector3 Position(Transform t) => bind[t].MultiplyPoint3x4(Vector3.zero);
        Vector3 Horizontal(Vector3 v) => new(v.x, 0, v.z);
        var bindSide = Horizontal(Position(right.Hip) - Position(left.Hip)).normalized;
        var bindForward = Vector3.Cross(bindSide, Vector3.up).normalized;
        var ball = Find("l_Leg_BallSHJnt");
        if (ball != null && bind.ContainsKey(ball) && Vector3.Dot(Position(ball) - Position(left.Ankle), bindForward) < 0)
            bindForward = -bindForward;
        var currentSide = Horizontal(slot.InverseTransformVector(right.Hip.position - left.Hip.position)).normalized;
        var currentForward = Vector3.Cross(currentSide, Vector3.up).normalized;
        if (Vector3.Dot(Vector3.Cross(bindSide, Vector3.up), bindForward) < 0) currentForward = -currentForward;
        if (currentForward.sqrMagnitude < 0.9f || bindSide.sqrMagnitude < 0.9f) return null;

        var bindToPose = Quaternion.FromToRotation(bindForward, currentForward);
        float ground = Mathf.Min(slot.InverseTransformPoint(left.Ankle.position).y, slot.InverseTransformPoint(right.Ankle.position).y);
        var center = (Position(left.Hip) + Position(right.Hip)) * 0.5f;
        foreach (var leg in new[] { left, right })
        {
            leg.Side = Vector3.Dot(Position(leg.Hip) - center, bindSide);
            leg.AnkleY = ground;
            leg.FlatRotation = bindToPose * bind[leg.Ankle].rotation;
        }
        // The camera is at -Z. Show the front as well as the right-facing profile, irrespective of the prefab yaw.
        var unturned = slot.localRotation;
        slot.localRotation *= Quaternion.FromToRotation(currentForward, CorridorFacing);
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var result = new CorridorHeroMotion(slot, pelvis, Find("Spine_01SHJnt"), Find("Spine_TopSHJnt"), Find("Head_TopSHJnt"),
            Find("Sword_AuxSHJnt"), Find("r_Arm_WristSHJnt"), animator, left, right,
            currentForward, currentSide, rank);
        result._unturned = unturned;
        result._modelForward = currentForward;
        Plugin.Log.LogInfo($"[stage-walk] {actor.GetActorGuid()}: native rig ready, leg {result._legLength:0.00}, camera-facing corridor gait");
        return result;
    }

    private CorridorHeroMotion(Transform slot, Transform pelvis, Transform spine, Transform chest, Transform head,
        Transform sword, Transform swordHand, Animator animator,
        Leg left, Leg right, Vector3 forward, Vector3 side, int rank)
    {
        _slot = slot; _pelvis = pelvis; _spine = spine; _chest = chest; _head = head; _animator = animator;
        // Leper's sword is a separate pelvis child, not a child of either hand. Preserve the native
        // grip through the procedural chest motion without reparenting or changing his combat rig.
        _sword = sword != null && swordHand != null && !sword.IsChildOf(swordHand) ? sword : null;
        _swordHand = swordHand;
        _left = left; _right = right; _forward = forward; _side = side;
        _legLength = (slot.InverseTransformVector(left.Knee.position - left.Hip.position).magnitude
                    + slot.InverseTransformVector(left.Ankle.position - left.Knee.position).magnitude);
        _homeCenter = slot.InverseTransformPoint((left.Hip.position + right.Hip.position) * 0.5f);
        _driven = new[] { pelvis, spine, chest, head, left.Hip, left.Knee, left.Ankle, right.Hip, right.Knee, right.Ankle, _sword }
            .Where(t => t != null).Distinct().ToArray();
        _cycle = new CorridorWalkCycle(rank * 0.23);
    }

    // Walking the corridor, heroes show their front as well as their right-facing profile; in a fight DD2 shows them
    // nearly in profile, facing the enemy.
    private static readonly Vector3 CorridorFacing = new Vector3(1, 0, -0.48f).normalized;
    private static readonly Vector3 BattleFacing = new Vector3(1, 0, -0.18f).normalized;
    private Quaternion _unturned;
    private Vector3 _modelForward;

    /// <summary>DD1's battle start: 0 faces the corridor camera, 1 faces the enemy as DD2's fight shows the hero.</summary>
    internal void Turn(float toBattle)
    {
        if (_slot == null) return;
        var facing = Vector3.Slerp(CorridorFacing, BattleFacing, Mathf.Clamp01(toBattle));
        _slot.localRotation = _unturned * Quaternion.FromToRotation(_modelForward, facing);
    }

    // Run before Animator evaluation. This also restores unkeyed transforms, preventing accumulated offsets.
    internal void Restore()
    {
        foreach (var saved in _saved) saved.Restore();
        _saved.Clear();
    }

    internal void Apply(float speed, float deltaTime, bool visible)
    {
        if (_slot == null || _animator == null || !_animator.gameObject.activeInHierarchy || !visible) return;
        _cycle.Advance(speed, deltaTime, true);
        float weight = _cycle.Weight;
        if (weight <= 0) return;
        foreach (var bone in _driven) _saved.Add(new SavedBone(bone));
        var gripPosition = _sword != null ? _swordHand.InverseTransformPoint(_sword.position) : Vector3.zero;
        var gripRotation = _sword != null ? Quaternion.Inverse(_swordHand.rotation) * _sword.rotation : Quaternion.identity;

        var leftFoot = CorridorWalkCycle.Sample(_cycle.Phase);
        var rightFoot = CorridorWalkCycle.Sample(_cycle.Phase + 0.5);
        var worldForward = _slot.TransformDirection(_forward);
        var worldSide = _slot.TransformDirection(_side);
        // Hip compression and weight shift happen above fixed foot contacts, not by bouncing the whole model.
        float shift = CorridorWalkCycle.SupportShift(_cycle.Phase);
        // Hip yaw follows the separation of the feet, while sway follows the supporting foot.
        // Using the same sine for both twisted the shoulders most when the feet were passing.
        float twist = Mathf.Clamp((rightFoot.Forward - leftFoot.Forward) / CorridorWalkCycle.Stride, -1f, 1f);
        var displacement = Vector3.up * CorridorWalkCycle.PelvisLift(_legLength, _homeCenter.y - _left.AnkleY, _cycle.Phase)
                         - _side * (_legLength * 0.024f * shift);
        _pelvis.localPosition += _pelvis.parent.InverseTransformVector(_slot.TransformVector(displacement * weight));
        _pelvis.rotation = Quaternion.AngleAxis(3.5f * twist * weight, Vector3.up)
                         * Quaternion.AngleAxis(2f * shift * weight, worldForward) * _pelvis.rotation;
        if (_spine != null)
            _spine.rotation = Quaternion.AngleAxis(-0.25f * shift * weight, worldForward)
                            * Quaternion.AngleAxis(3f * weight, worldSide) * _spine.rotation;
        // A small shoulder counter-motion leaves 2.25 degrees of torso yaw and 1.5 degrees of sway,
        // following the hips. Carry arms and held weapons together; keep the gaze steady above the chest.
        if (_chest != null)
            _chest.rotation = Quaternion.AngleAxis(-1.25f * twist * weight, Vector3.up)
                            * Quaternion.AngleAxis(-0.25f * shift * weight, worldForward) * _chest.rotation;
        if (_head != null)
            _head.rotation = Quaternion.AngleAxis(-2.25f * twist * weight, Vector3.up)
                           * Quaternion.AngleAxis(-1.5f * weight, worldSide) * _head.rotation;

        PoseLeg(_left, leftFoot, weight, worldForward, worldSide);
        PoseLeg(_right, rightFoot, weight, worldForward, worldSide);
        if (_sword != null)
        {
            _sword.position = _swordHand.TransformPoint(gripPosition);
            _sword.rotation = _swordHand.rotation * gripRotation;
        }
    }

    private void PoseLeg(Leg leg, CorridorWalkCycle.Foot foot, float weight, Vector3 forward, Vector3 side)
    {
        var neutral = new Vector3(_homeCenter.x, leg.AnkleY, _homeCenter.z) + _side * leg.Side;
        var goal = neutral + _forward * (foot.Forward * _legLength) + Vector3.up * (foot.Lift * _legLength);
        var target = Vector3.Lerp(leg.Ankle.position, _slot.TransformPoint(goal), weight);
        SolveLeg(leg.Hip, leg.Knee, leg.Ankle, target, forward);
        var flat = _slot.rotation * leg.FlatRotation;
        leg.Ankle.rotation = Quaternion.Slerp(leg.Ankle.rotation, Quaternion.AngleAxis(-foot.Pitch, side) * flat, weight);
    }

    private static void SolveLeg(Transform hip, Transform knee, Transform ankle, Vector3 target, Vector3 pole)
    {
        float upper = Vector3.Distance(hip.position, knee.position), lower = Vector3.Distance(knee.position, ankle.position);
        var delta = target - hip.position;
        if (upper < 0.001f || lower < 0.001f || delta.sqrMagnitude < 0.000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        var axis = delta.normalized;
        var bend = Vector3.ProjectOnPlane(pole, axis).normalized;
        if (bend.sqrMagnitude < 0.5f) bend = Vector3.ProjectOnPlane(knee.position - hip.position, axis).normalized;
        float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        var kneeGoal = hip.position + axis * along + bend * height;
        hip.rotation = Quaternion.FromToRotation(knee.position - hip.position, kneeGoal - hip.position) * hip.rotation;
        knee.rotation = Quaternion.FromToRotation(ankle.position - knee.position, hip.position + axis * distance - knee.position) * knee.rotation;
    }
}
