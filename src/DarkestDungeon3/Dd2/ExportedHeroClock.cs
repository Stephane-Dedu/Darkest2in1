using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Animation.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace DarkestDungeon3.Dd2;

/// <summary>Read the presentation DD2 is actually evaluating, including Timeline takeover.</summary>
internal sealed class ExportedHeroClock
{
    private TimelineAsset _timeline;
    private TimelineClip[] _clips;

    internal (string Name,float Progress,bool Timeline) Read(ActorBhv actor,Animator animator)
    {
        if (actor.GetAnimatorWeightFromPlayableDirector()>.5f)
        {
            var timeline=actor.GetCurrentTimelineAsset();
            if (timeline!=_timeline)
            {
                _timeline=timeline;
                _clips=timeline?.GetOutputTracks().OfType<AnimationTrack>().Where(t => !t.mutedInHierarchy)
                    .SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
            }
            double time=actor.GetPlayableDirectorTime();
            var clip=_clips?.LastOrDefault(c => time>=c.start && time<=c.end+.001);
            if (clip?.asset is AnimationPlayableAsset animation)
            {
                string name=animation.clip?.name ?? (animation as AnimationOverridePlayableAsset)?.DefaultClip?.SubObjectName;
                if (!string.IsNullOrEmpty(name)) return (name,Mathf.Clamp01((float)((time-clip.start)/clip.duration)),true);
            }
        }
        bool next=animator.IsInTransition(0) && animator.GetNextAnimatorClipInfoCount(0)>0;
        var info=next ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        var clips=next ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
        string current=clips.OrderByDescending(c => c.weight).Select(c => c.clip?.name).FirstOrDefault() ?? "crusader_idle_A";
        return (current,Mathf.Clamp01(info.normalizedTime),false);
    }
}
