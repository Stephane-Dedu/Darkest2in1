using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;

namespace DarkestDungeon3.Core.Tests;

public class ExperimentalHeroesTests
{
    [Fact]
    public void VilhelmSkillsOwnTheirIdsWithoutChangingNativeTargetingOrRequirements()
    {
        Assert.Equal(5,VilhelmKit.Loadout.Distinct().Count());
        foreach (var skill in VilhelmKit.Skills)
        {
            Assert.Equal(skill,VilhelmKit.Find(skill.Id+"_u"));
            Assert.Equal(skill.Donor,DarkestDungeon3.Core.Dd1.Dd1SkillToDd2.BaseOf(skill.Id));
            string text=VilhelmKit.SkillText("launch_ranks,1,2,\nm_AllConditionIds,health_threshold,\nm_ProfileUnlockId,donor_story,\nm_SkillHistoryIdOverride,donor,\n",skill,true);
            Assert.Contains("launch_ranks,1,2,",text);
            Assert.Contains("m_AllConditionIds,health_threshold,",text);
            Assert.DoesNotContain("donor_story",text);
            Assert.DoesNotContain("m_SkillHistoryIdOverride,donor,",text);
            Assert.Contains("m_SkillCooldownIdOverride,"+skill.Id+",",text);
        }
        Assert.Null(VilhelmKit.Find("cru_radiance"));
    }
    [Fact]
    public void VisitorInviteDoesNotDuplicateALivingRecruitOrOverflowVisibleCoachRows()
    {
        var estate = new Estate();
        Assert.True(ExperimentalHeroes.CanInviteVilhelm(estate));
        var hero = new HeroRecord { ClassId = ExperimentalHeroes.Vilhelm };
        estate.Recruits.Add(hero);
        Assert.False(ExperimentalHeroes.CanInviteVilhelm(estate));
        estate.Recruits.Remove(hero); estate.Roster.Add(hero);
        Assert.False(ExperimentalHeroes.CanInviteVilhelm(estate));
        hero.IsDead = true;
        Assert.True(ExperimentalHeroes.CanInviteVilhelm(estate));
        estate.Recruits.AddRange(Enumerable.Range(0,7).Select(_ => new HeroRecord { ClassId = "crusader" }));
        Assert.False(ExperimentalHeroes.CanInviteVilhelm(estate));
    }

    [Fact]
    public void VisitorUsesOrdinaryRecruitmentAndCrusaderProgressionCosts()
    {
        var dd1=DarkestDungeon3.Core.Dd1.Dd1Install.Find();
        var estate=new Estate();
        var hamlet=new Hamlet(estate,Dd1Campaign.Load(dd1),Buildings.Load(dd1),new FakeCatalog());
        var visitor=ExperimentalHeroes.InviteVilhelm(hamlet,new DarkestDungeon3.Core.Rng(42));
        Assert.NotNull(visitor);
        Assert.Equal(VilhelmKit.Loadout,visitor.EquippedSkills);
        Assert.Null(ExperimentalHeroes.InviteVilhelm(hamlet,new DarkestDungeon3.Core.Rng(42)));
        Assert.True(hamlet.Recruit(visitor.Id));
        Assert.Contains(visitor,estate.Roster);
        var upgrades=HeroUpgrades.Load(dd1);
        Assert.Equal("crusader",upgrades.Dd1Class(visitor.ClassId));
        var slash=VilhelmKit.Skills.Single(s => s.Key=="slash");
        Assert.NotNull(upgrades.SkillTree("crusader",slash.Donor));
        Assert.Same(upgrades.SkillTree("crusader",slash.Donor),upgrades.SkillTree(visitor.ClassId,slash.Id));
    }
}
