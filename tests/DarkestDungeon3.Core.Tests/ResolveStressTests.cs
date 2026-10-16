using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ResolveStressTests
{
    [Fact] public void OnePointIncrementsReachBothThresholdsDespiteFloatAccumulation()
    {
        var rules=Rules();var state=new ResolveState();float stress=0;
        for(int i=1;i<=100;i++)
        {
            var result=rules.Check(state,stress+.1f,false,"flagellant",1,1,0,new Rng(i));stress=result.Stress;
            Assert.Equal(i==100 ? ResolveEvent.Toxic : ResolveEvent.None,result.Event);
        }
        Assert.Equal(10,stress);
        for(int i=1;i<=100;i++)
        {
            var result=rules.Check(state,stress+.1f,false,"flagellant",1,1,0,new Rng(i));stress=result.Stress;
            Assert.Equal(i==100 ? ResolveEvent.HeartAttack : ResolveEvent.None,result.Event);
        }
        Assert.Equal(17,stress);
    }
    [Fact] public void InstalledTraitsLoadWithNativeWeightsAndClassSpecificActs()
    {
        var install=DarkestDungeon3.Core.Dd1.Dd1Install.Find();
        var rules=ResolveStress.Load(install,DarkestDungeon3.Core.Campaign.Dd1Campaign.Load(install).Rules);
        Assert.Equal(7,rules.Traits.Count(t=>t.Generated && !t.Virtuous));
        Assert.Equal(5,rules.Traits.Count(t=>t.Generated && t.Virtuous));
        Assert.False(rules.Get("test").Generated);
        Assert.Equal("BarkStressHeal",rules.Get("stalwart").Reactions["block_effect"].Effect);
        var state=new ResolveState {TraitId="abusive"};
        var attacks=Enumerable.Range(0,500).Select(seed=>rules.PickAct(state,"highwayman",new Rng(seed)))
            .Where(a=>a?.Id=="attack_friendly").ToList();
        Assert.NotEmpty(attacks);Assert.All(attacks,a=>Assert.Contains("highwayman",a.Classes));
    }
    private static ResolveStress Rules() => ResolveStress.Parse(JObject.Parse("{traits:[{id:'fearful',overstress_type:'affliction'},{id:'stalwart',overstress_type:'virtue'}]}"),null);
    [Theory]
    [InlineData(9.9f,ResolveEvent.None,9.9f)]
    [InlineData(10f,ResolveEvent.Toxic,10f)]
    [InlineData(20f,ResolveEvent.Toxic,10f)]
    public void FlagellantBreaksAtOneHundredAndCapsTheFirstHit(float stress, ResolveEvent ev, float expected)
    {
        var state=new ResolveState(); var result=Rules().Check(state,stress,false,"flagellant",1,1,0,new Rng(1));
        Assert.Equal(ev,result.Event);Assert.Equal(expected,result.Stress);
        Assert.Equal(ev==ResolveEvent.Toxic ? "toxic" : null,state.TraitId);
    }
    [Theory]
    [InlineData(false,ResolveEvent.HeartAttack)]
    [InlineData(true,ResolveEvent.HeartAttackDeath)]
    public void ToxicStillHasAHeartAttackAtTwoHundred(bool door, ResolveEvent ev)
    {
        var state=new ResolveState {TraitId="toxic",ToxicLevel=1};
        var result=Rules().Check(state,20,door,"flagellant",1,1,0,new Rng(1));
        Assert.Equal(ev,result.Event);Assert.Equal(17,result.Stress);Assert.Equal("toxic",state.TraitId);
    }
    [Fact] public void VirtueAtTwoHundredIsLostWithoutDamage()
    {
        var state=new ResolveState {TraitId="stalwart",Virtuous=true};
        var result=Rules().Check(state,20,true,"highwayman",1,1,0,new Rng(1));
        Assert.Equal(ResolveEvent.LostVirtue,result.Event);Assert.Equal(0,result.Stress);Assert.Null(state.TraitId);
    }
    [Fact] public void AfflictionSurvivesBelowOneHundredUntilZero()
    {
        var state=new ResolveState {TraitId="fearful"};
        Assert.Equal(ResolveEvent.None,Rules().Check(state,.1f,false,"vestal",1,1,0,new Rng(1)).Event);
        Assert.Equal("fearful",state.TraitId);
        Assert.Equal(ResolveEvent.Cured,Rules().Check(state,0,false,"vestal",1,1,0,new Rng(1)).Event);
        Assert.Null(state.TraitId);
    }
    [Fact] public void NormalResolveRollUsesTheNativeBaseAndDifficultyModifier()
    {
        var rules=Rules();Assert.Equal(.75f,rules.Chance(3,3,0));Assert.Equal(.9f,rules.Chance(0,3,0),5);
        int virtues=0;
        for (int seed=0;seed<10000;seed++)
        {
            var state=new ResolveState();var result=rules.Check(state,10,false,"vestal",3,3,0,new Rng(seed));
            if (result.Event==ResolveEvent.Virtue) { virtues++;Assert.Equal(4.5f,result.Stress); }
            else Assert.Equal(ResolveEvent.Affliction,result.Event);
        }
        Assert.InRange(virtues,2350,2650);
    }
    [Fact] public void ReturningHomeClearsVirtueButKeepsAnAfflictionAndHistory()
    {
        var original=new ResolveState {TraitId="stalwart",Virtuous=true,History={{"stalwart",2}}};
        var home=ResolveStress.Home(original,4.5f);
        Assert.Null(home.TraitId);Assert.Equal(2,home.History["stalwart"]);Assert.Equal("stalwart",original.TraitId);
        original.TraitId="fearful";original.Virtuous=false;
        Assert.Equal("fearful",ResolveStress.Home(original,1).TraitId);
    }
    [Fact] public void TraitAndToxicEffectsSurviveSaveAndFightRewindWithoutSharingMutableState()
    {
        var exp=new ExpeditionState {Started=true,Party={"a"},PartyStates={
            ["a"]=new ExpeditionHeroState {Hp=20,HpMax=40,Stress=12.3f,Outcome=new DarkestDungeon3.Core.Campaign.HeroOutcome {HeroId="a"},
                Resolve=new ResolveState {TraitId="toxic",ToxicLevel=1,ToxicBuffs={{"flg_toxic_dbr_penalty",1}}}}}};
        var save=new DarkestDungeon3.Core.Campaign.SaveFile {Expedition=exp};
        var loaded=DarkestDungeon3.Core.Campaign.SaveFile.FromJson(save.ToJson());
        var copied=ExpeditionParty.Copy(loaded.Expedition.PartyStates["a"]);
        copied.Resolve.ToxicBuffs.Clear();
        Assert.Equal(1,loaded.Expedition.PartyStates["a"].Resolve.ToxicBuffs["flg_toxic_dbr_penalty"]);
        var outcome=Assert.Single(ExpeditionParty.Outcomes(loaded.Expedition,save.Estate));
        Assert.Equal("toxic",outcome.Resolve.TraitId);Assert.Equal(12.3f,outcome.Stress);
        Assert.Equal(12.3f,ExpeditionParty.HeroForRestore(new(){Id="a"},loaded.Expedition.PartyStates["a"]).Stress);
    }
    [Fact] public void LegacySaveStressKeepsItsOriginalScaleAndNewSavesPreserveSmallAmounts()
    {
        var old=DarkestDungeon3.Core.Campaign.SaveFile.FromJson("{Estate:{Roster:[{Id:'a',Stress:7}]}}");
        Assert.Equal(70,ResolveStress.Dd1(old.Estate.Roster[0].Stress));Assert.Null(old.Estate.Roster[0].Resolve);
        old.Estate.Roster[0].Stress=.2f;
        Assert.Equal(2,ResolveStress.Dd1(DarkestDungeon3.Core.Campaign.SaveFile.FromJson(old.ToJson()).Estate.Roster[0].Stress));
    }
}
