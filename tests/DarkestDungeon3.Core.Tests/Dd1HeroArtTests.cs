using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1HeroArtTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Theory]
    [InlineData("abomination")]
    [InlineData("bounty_hunter")]
    [InlineData("crusader")]
    [InlineData("flagellant")]
    [InlineData("grave_robber")]
    [InlineData("hellion")]
    [InlineData("highwayman")]
    [InlineData("jester")]
    [InlineData("leper")]
    [InlineData("man_at_arms")]
    [InlineData("occultist")]
    [InlineData("plague_doctor")]
    [InlineData("vestal")]
    public void CampaignIdleDefendAndSkillPosesHaveOutfitPages(string id)
    {
        var art = Dd1HeroArt.Find(Install, id);
        Assert.NotNull(art);
        Assert.DoesNotContain("arena", art.Root);
        foreach (string animation in art.SkillAnimations.Values.Append("combat").Append("defend").Distinct())
        {
            Assert.True(art.HasAnimation(animation), id + "/" + animation);
            var atlas = SpineAtlas.Parse(File.ReadAllText(Path.Combine(art.AnimationDirectory, art.Stem(animation) + ".atlas")));
            var skeleton = SpineSkeleton.Load(Path.Combine(art.AnimationDirectory, art.Stem(animation) + ".skel"));
            var pieces = skeleton.Pose(atlas, skeleton.Animations.FirstOrDefault()?.Name, 0, true);
            Assert.NotEmpty(pieces);
            Assert.All(pieces, p => Assert.NotEmpty(p.Triangles));
        }
    }

    [Theory]
    [InlineData("duelist")]
    [InlineData("runaway")]
    [InlineData("../highwayman")]
    [InlineData("highwayman/A")]
    [InlineData("")]
    public void MissingOrInvalidClassNeverBorrowsUnrelatedOrArenaArt(string id) =>
        Assert.Null(Dd1HeroArt.Find(Install, id));

    [Theory]
    [InlineData("hwm_wicked_slice", "attack_slice")]
    [InlineData("hwm_wicked_slice_u", "attack_slice")]
    [InlineData("hwm_pistol_shot_u_p1", "attack_pistol")]
    [InlineData("hwm_duelist_advance", "attack_lunge")]
    [InlineData("hwm_double_tap", "combat")]
    [InlineData("hwm_fakewicked_slice", "combat")]
    public void SharedDd2SkillUsesOnlyItsDd1Pose(string skill, string animation) =>
        Assert.Equal(animation, Dd1HeroArt.Find(Install, "highwayman").AnimationFor(skill));
}
