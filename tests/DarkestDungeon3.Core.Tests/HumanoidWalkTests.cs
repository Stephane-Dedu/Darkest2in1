using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class HumanoidWalkTests
{
    private const string Folder=@"C:\Users\Piral\darkestwithdlc\game\PrivateHeroModels\sir_vilhelm";
    private static HumanoidWalk PrivateWalk()
    {
        if (!File.Exists(Path.Combine(Folder,"motion.json"))) return null;
        var motion=JObject.Parse(File.ReadAllText(Path.Combine(Folder,"motion.json")));
        return new HumanoidWalk(GltfModel.Load(Path.Combine(Folder,"sir_vilhelm.gltf")),
            (string)motion["clip"],motion["distances"].Values<float>().ToArray());
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void SupportingToeTravelsWithTheFloorWhileTheAnkleRolls(float direction)
    {
        var contact=new HumanoidFootContact();
        var foot=new HumanoidWalk.Foot { Planted=true,ContactLength=.6f,ContactProgress=.5f,
            Position=new HumanoidWalk.Vector3(0,0,.2f) };
        float first=contact.Forward(foot,-.10f,.3f,.01f*direction,.005f*direction);
        Assert.Equal(.3f-.01f*direction,first,5);
        foot.ToePivot=true;foot.ContactProgress+=.005f*direction;
        float next=contact.Forward(foot,-.07f,0,.01f*direction,.005f*direction);
        Assert.Equal(-.01f*direction+.03f,next-first,5);
        // Switching pivot on an unchanged pose cannot teleport the ankle.
        foot.ToePivot=false;
        float flat=contact.Forward(foot,-.07f,0,.01f*direction,.005f*direction);
        Assert.Equal(-.01f*direction,flat-next,5);
    }

    [Fact]
    public void ANewContactDoesNotRetainThePreviousSupportAfterASkippedSwing()
    {
        var contact=new HumanoidFootContact();
        var foot=new HumanoidWalk.Foot { Planted=true,ContactLength=.55f,ContactProgress=.9f,
            Position=new HumanoidWalk.Vector3(0,0,.4f) };
        contact.Forward(foot,0,-.5f,.01f,.005f);
        foot.ContactProgress=.1f;
        Assert.Equal(.4f,contact.Forward(foot,0,0,1.2f,.6f));
    }

    [Fact]
    public void OwnedWalkRetainsBodySwayAndRollingSupportBeforeRecovery()
    {
        var walk=PrivateWalk();if(walk==null)return;
        var poses=Enumerable.Range(0,1000).Select(i => walk.Sample(i/1000f)).ToArray();
        Assert.InRange(walk.LegLength,.8f,.95f);
        Assert.InRange(walk.DistancePerCycle,1.4f,2.1f);
        Assert.True(poses.Max(p => p.Hips.X)-poses.Min(p => p.Hips.X)>.07f);
        Assert.True(poses.Any(p => p.Left.Planted && p.Left.ToePivot && p.Left.Position.Y>.04f));
        Assert.True(poses.Any(p => p.Right.Planted && p.Right.ToePivot && p.Right.Position.Y>.04f));
        Assert.False(walk.Sample(.2f).Right.Planted); // Near the floor, but still reaching forward.
        Assert.All(poses,p => Assert.True(p.Left.Planted || p.Right.Planted));
        var start=walk.Sample(0);var end=walk.Sample(.999999f);
        Assert.InRange(HumanoidWalk.Vector3.Distance(start.Hips,end.Hips),0,.001f);
        Assert.InRange(HumanoidWalk.Vector3.Distance(start.Left.Position,end.Left.Position),0,.001f);
        Assert.InRange(Math.Abs(HumanoidWalk.Quaternion.Dot(start.Pelvis,end.Pelvis)),.999f,1.001f);
    }
}
