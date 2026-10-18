using System;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

internal sealed partial class CorridorHeroMotion
{
    private static ExportedHeroes.Pack _walkPack;
    private static Task<HumanoidWalk> _walkReading;
    private static bool _walkFailed;
    private HumanoidWalk _authored;
    private float _authoredPhase;
    private readonly Quaternion[] _bodyBase = new Quaternion[4];
    private bool _bodyBaseReady;
    internal bool AuthoredWalkReady => _authored != null;
    internal HumanoidWalk.Pose AuthoredPose { get; private set; }

    private static HumanoidWalk AuthoredSource()
    {
        var pack=ExportedHeroes.Data();
        if (pack?.Walk==null) return null;
        if (!ReferenceEquals(pack,_walkPack))
        {
            _walkPack=pack;_walkFailed=false;
            _walkReading=Task.Run(() => new HumanoidWalk(pack.Model,pack.Walk.Clip,pack.Walk.Distances));
        }
        if (_walkFailed || !_walkReading.IsCompleted) return null;
        try { return _walkReading.GetAwaiter().GetResult(); }
        catch (Exception e)
        { _walkFailed=true;Plugin.Log.LogWarning("[stage-walk] authored source unavailable: "+e.Message);return null; }
    }

    private static Quaternion Q(HumanoidWalk.Quaternion q) => new(q.X,q.Y,q.Z,q.W);

    private void ApplyAuthored(float distancePixels,float deltaTime,float weight,bool starting)
    {
        bool moving=Mathf.Abs(distancePixels)>.000001f;
        if (starting || !_bodyBaseReady)
        {
            var body=new[]{_pelvis,_spine,_chest,_head};
            _left.AuthoredContact.Reset();_right.AuthoredContact.Reset();
            for (int i=0;i<body.Length;i++)
                _bodyBase[i]=body[i]!=null ? Quaternion.Inverse(_slot.rotation)*body[i].rotation : Quaternion.identity;
            _bodyBaseReady=true;
            float left=ObservedForward(_left),right=ObservedForward(_right),best=float.MaxValue;
            for (int i=0;i<240;i++)
            {
                float phase=i/240f;var p=_authored.Sample(phase);
                float error=(p.Left.Position.Z-left)*(p.Left.Position.Z-left)+(p.Right.Position.Z-right)*(p.Right.Position.Z-right);
                if (error<best) { best=error;_authoredPhase=phase; }
            }
        }
        float legPixels=ProjectedLegPixels;
        float distance=Mathf.Abs(legPixels)>=1 ? distancePixels/legPixels : 0;
        float advance=distance*_authored.LegLength/_authored.DistancePerCycle;
        _authoredPhase=HumanoidWalk.Wrap(_authoredPhase+advance);
        var pose=_authored.Sample(_authoredPhase);AuthoredPose=pose;
        var basis=Quaternion.LookRotation(_forward,Vector3.up);
        var gripPosition=_sword!=null ? _swordHand.InverseTransformPoint(_sword.position) : Vector3.zero;
        var gripRotation=_sword!=null ? Quaternion.Inverse(_swordHand.rotation)*_sword.rotation : Quaternion.identity;

        // One sampled motion drives pelvis, spine and legs together. Global
        // rotations avoid accumulating a parent's motion again on its children.
        var currentCentre=_slot.InverseTransformPoint((_left.Hip.position+_right.Hip.position)*.5f);
        var desired=new Vector3(_homeCenter.x,_left.AnkleY,_homeCenter.z)
            +(_side*pose.Hips.X+Vector3.up*pose.Hips.Y+_forward*pose.Hips.Z)*_legLength;
        _pelvis.position+=_slot.TransformVector((desired-currentCentre)*weight);
        void Rotate(Transform bone,HumanoidWalk.Quaternion rotation,int index)
        {
            if (bone!=null)
            {
                var target=_slot.rotation*basis*Q(rotation)*Quaternion.Inverse(basis)*_bodyBase[index];
                bone.rotation=Quaternion.Slerp(bone.rotation,target,weight);
            }
        }
        Rotate(_pelvis,pose.Pelvis,0);Rotate(_spine,pose.Spine,1);Rotate(_chest,pose.Chest,2);Rotate(_head,pose.Head,3);
        var leftRotation=Quaternion.Slerp(_left.Ankle.rotation,AuthoredFootRotation(_left,pose.Left,basis),weight);
        var rightRotation=Quaternion.Slerp(_right.Ankle.rotation,AuthoredFootRotation(_right,pose.Right,basis),weight);
        var leftTarget=AuthoredTarget(_left,pose.Left,leftRotation,weight,distance,advance);
        var rightTarget=AuthoredTarget(_right,pose.Right,rightRotation,weight,distance,advance);
        float Drop(Leg leg,Vector3 target,HumanoidWalk.Foot foot)
        {
            float reach=Vector3.Distance(leg.Hip.position,leg.Knee.position)+Vector3.Distance(leg.Knee.position,leg.Ankle.position);
            var d=leg.Hip.position-target;
            float support=foot.Planted ? 1 : Mathf.SmoothStep(1,0,Mathf.Clamp01(foot.Clearance/.06f));
            return CorridorWalkCycle.PelvisContactDrop(reach,d.x*d.x+d.z*d.z,d.y)*support;
        }
        _pelvis.position-=Vector3.up*Mathf.Max(Drop(_left,leftTarget,pose.Left),Drop(_right,rightTarget,pose.Right));
        PoseAuthoredLeg(_left,pose.Left,leftTarget,leftRotation,_authored.LeftHipSide,weight,moving);
        PoseAuthoredLeg(_right,pose.Right,rightTarget,rightRotation,_authored.RightHipSide,weight,moving);
        if (_sword!=null)
        { _sword.position=_swordHand.TransformPoint(gripPosition);_sword.rotation=_swordHand.rotation*gripRotation; }
    }

    private Quaternion AuthoredFootRotation(Leg leg,HumanoidWalk.Foot foot,Quaternion basis) =>
        _slot.rotation*basis*Q(foot.Rotation)*Quaternion.Inverse(basis)*leg.FlatRotation;

    private Vector3 AuthoredTarget(Leg leg,HumanoidWalk.Foot foot,Quaternion rotation,float weight,float distance,float advance)
    {
        var toe=Quaternion.Inverse(_slot.rotation)*rotation*leg.ToeLocal;
        float toeOffset=-_slot.TransformVector(toe).x*HeroStage.PixelsPerUnit/ProjectedLegPixels;
        float forward=leg.AuthoredContact.Forward(foot,toeOffset,ObservedForward(leg),distance,advance);
        float lift=foot.Clearance*_legLength+(foot.ToePivot ? leg.FlatToeY-toe.y : 0);
        var target=_slot.TransformPoint(Neutral(leg)+_forward*(forward*_legLength)+Vector3.up*lift);
        return Vector3.Lerp(leg.Ankle.position,target,Mathf.Abs(distance)>.000001f ? 1 : weight);
    }

    private void PoseAuthoredLeg(Leg leg,HumanoidWalk.Foot foot,Vector3 target,Quaternion rotation,
        float sourceHipSide,float weight,bool moving)
    {
        var knee=Neutral(leg)+(_forward*foot.Knee.Z+_side*(foot.Knee.X-sourceHipSide)+Vector3.up*foot.Knee.Y)*_legLength;
        var pole=(_slot.TransformPoint(knee)-leg.Hip.position).normalized;
        SolveLeg(leg.Hip,leg.Knee,leg.Ankle,target,pole,null,foot.Planted,0);
        // Contact prediction and the rendered foot must use the same blended
        // rotation, including the first frames out of the native combat stance.
        leg.Ankle.rotation=rotation;
        if (moving && foot.Planted) ContactErrorPixels=Mathf.Max(ContactErrorPixels,Mathf.Abs(leg.Ankle.position.x-target.x)*100);
    }
}
