using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd2Data;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public sealed class HeroPathTests
{
    private static readonly string Assets = @"C:\Users\Piral\darkestwithdlc\game\Darkest Dungeon II_Data\StreamingAssets";
    private static readonly HeroPathCatalog Native = HeroPathCatalog.Load(Path.Combine(Assets, "Excel"));

    [Fact]
    public void EveryInstalledHeroHasItsExpeditionChoicesWithoutReservePaths()
    {
        var classes = RecruitClasses.For(true, ownsDuelist: true, ownsAbomination: true);
        Assert.Equal(15, classes.Count);
        Assert.Equal(Native.Classes.Keys.Where(c => Native.ForClass(c).Count > 0).OrderBy(c => c), classes.OrderBy(c => c));
        Assert.Equal(42, classes.Sum(c => Native.ForClass(c).Count - 1));
        foreach (string cls in classes)
        {
            var choices = Native.ForClass(cls);
            Assert.Equal(cls == RecruitClasses.BountyHunter ? 1 : 4, choices.Count);
            Assert.Equal(Native.DefaultId(cls), choices[0].Id);
            Assert.All(choices.Skip(1), p => Assert.NotNull(p.UnlockId));
            Assert.DoesNotContain(choices, p => p.Id.Contains("reserve"));
        }
        Assert.Equal("bh_professional", Native.DefaultId(RecruitClasses.BountyHunter));
        Assert.Empty(Native.ForClass(RecruitClasses.Shieldbreaker));
    }

    [Fact]
    public void NativeReplacementsResolveBothOrdinaryAndMasteredSkills()
    {
        Assert.Equal("pd_noxious_blast_p2", Native.SkillId("plague_doctor", "plg_alchemist", "pd_noxious_blast", false));
        Assert.Equal("pd_noxious_blast_p2_u", Native.SkillId("plague_doctor", "plg_alchemist", "pd_noxious_blast", true));
        Assert.Equal("pd_battlefield_medicine_u", Native.SkillId("plague_doctor", "plg_alchemist", "pd_battlefield_medicine", true));
        Assert.Equal("pd_noxious_blast", Native.SkillId("plague_doctor", "wrong", "pd_noxious_blast", false));
        Assert.False(Native.IsValid("plague_doctor", "lep_monarch"));
        Assert.Equal("wanderer", Native.ResolveId("plague_doctor", "lep_monarch"));
        Assert.True(Native.IsValid("plague_doctor", "wanderer")); // class tags, rather than explicit class IDs
        Assert.Equal("bh_collect_bounty", Native.SkillId("bounty_hunter", null, "bh_collect_bounty", true));
        Assert.Equal("dd3_shb_pierce_u", Native.SkillId("shieldbreaker", null, "dd3_shb_pierce", true));
    }

    [Fact]
    public void TrainingPreservesProgressAndNormalizesDefaultAcrossSaveAndRestore()
    {
        var estate = new Estate(); estate.Add(Currency.Gold, 5000);
        var hero = new HeroRecord { Id = "path-test", Name = "Paracelsus", ClassId = "plague_doctor", ResolveLevel = 2,
            WeaponRank = 2, ArmorRank = 1, ResolveXp = 12, LearnedSkills = { "pd_cause_of_death" },
            MasteredSkills = { "pd_noxious_blast" }, EquippedSkills = { "pd_noxious_blast", "pd_cause_of_death" } };
        estate.Roster.Add(hero);
        Assert.True(HeroPaths.Train(estate, hero, Native, "plg_alchemist", false, true, 800));
        Assert.Equal(4200, estate.Get(Currency.Gold));
        var saved = JsonConvert.DeserializeObject<SaveFile>(JsonConvert.SerializeObject(new SaveFile { Estate = estate }));
        var restored = ExpeditionParty.HeroForRestore(saved.Estate.Hero(hero.Id), null);
        Assert.Equal("plg_alchemist", restored.PathId);
        Assert.Equal(hero.LearnedSkills, restored.LearnedSkills); Assert.Equal(hero.MasteredSkills, restored.MasteredSkills);
        Assert.Equal(hero.EquippedSkills, restored.EquippedSkills); Assert.Equal(12, restored.ResolveXp);
        Assert.Equal(2, restored.WeaponRank); Assert.Equal(1, restored.ArmorRank);
        Assert.True(HeroPaths.Train(estate, hero, Native, null, false, true, 800));
        Assert.Null(hero.PathId); Assert.Equal(3400, estate.Get(Currency.Gold));
        Assert.False(HeroPaths.Train(estate, hero, Native, "wanderer", false, true, 800));
        Assert.Equal(3400, estate.Get(Currency.Gold));
        Assert.Null(JsonConvert.DeserializeObject<HeroRecord>("{\"ClassId\":\"plague_doctor\"}").PathId);
    }

    [Theory]
    [InlineData("resolve", "Needs resolve 2")]
    [InlineData("gold", "Not enough gold")]
    [InlineData("closed", "The Guild is not open")]
    [InlineData("expedition", "Retrain in the Hamlet")]
    [InlineData("missing", "Hero is unavailable")]
    [InlineData("activity", "Hero is unavailable")]
    [InlineData("dead", "Hero is unavailable")]
    [InlineData("recruit", "Choose a roster hero")]
    [InlineData("foreign", "This class has no such Path")]
    public void RefusalsNeverSpendOrMutate(string scenario, string message)
    {
        var estate = new Estate(); estate.Add(Currency.Gold, scenario == "gold" ? 999 : 1000);
        var hero = new HeroRecord { Id = "hero", ClassId = "plague_doctor", ResolveLevel = scenario == "resolve" ? 1 : 2,
            MissingWeeks = scenario == "missing" ? 1 : 0, IsDead = scenario == "dead", Activity = scenario == "activity" ? "abbey.prayer" : null };
        if (scenario != "recruit") estate.Roster.Add(hero);
        string path = scenario == "foreign" ? "lep_monarch" : "plg_alchemist";
        string before = JsonConvert.SerializeObject(estate);
        Assert.Equal(message, HeroPaths.Refusal(estate, hero, Native, path, scenario == "expedition", scenario != "closed", 1000));
        Assert.False(HeroPaths.Train(estate, hero, Native, path, scenario == "expedition", scenario != "closed", 1000));
        Assert.Equal(before, JsonConvert.SerializeObject(estate));
    }

    [Fact]
    public void ColdPreviewReadsRealTargetingDamageAndTokensForPathVariants()
    {
        var descriptions = TrinketDescriptions.Load(Assets, includeHeroes: true);
        string current = descriptions.SkillEffects("pd_noxious_blast", out _);
        string preview = descriptions.SkillEffects("pd_noxious_blast_p2_u", out _);
        Assert.Contains("From ranks 2, 3, 4", current);
        Assert.Contains("Damage 2–3", current);
        Assert.Contains("Blight", current);
        Assert.NotEqual(current, preview);
        Assert.DoesNotContain("<sprite", preview);
        Assert.DoesNotContain("_p2", preview);
        Assert.False(string.IsNullOrWhiteSpace(descriptions.PathEffects("lep_monarch", out _)));
    }

    [Fact]
    public void DlcRecruitmentIsGatedPerOwnedClassAndTheirStatsLoad()
    {
        var baseClasses = RecruitClasses.For(false);
        Assert.DoesNotContain("duelist", baseClasses); Assert.DoesNotContain("abomination", baseClasses);
        Assert.Contains("duelist", RecruitClasses.For(false, ownsDuelist: true));
        Assert.Contains("abomination", RecruitClasses.For(false, ownsAbomination: true));
        var tables = Dd2Tables.Load(Path.Combine(Assets, "Excel"));
        foreach (string cls in new[] { "crusader", "duelist", "abomination", "bounty_hunter" })
            Assert.True(tables.Classes[cls].Get("health_max") > 0);
    }

    [Fact]
    public void OathCheckpointIsCopiedBoundedAndNeverBecomesPersistentHeroProgress()
    {
        var state = new ExpeditionHeroState { Outcome = new HeroOutcome { HeroId = "monarch" }, Hp = 30, HpMax = 35,
            MonarchOath = new MonarchOathState { Stacks = 9, Conditions = 9 } };
        var copied = ExpeditionParty.Copy(state);
        state.MonarchOath.Stacks = 40;
        Assert.Equal(9, copied.MonarchOath.Stacks);
        var restored = JsonConvert.DeserializeObject<ExpeditionHeroState>(JsonConvert.SerializeObject(copied));
        Assert.Equal(9, restored!.MonarchOath.Stacks);
        Assert.Equal(66, new MonarchOathState { Stacks = int.MaxValue }.Copy().Stacks);
        Assert.Equal(0, new MonarchOathState { Conditions = -5 }.Copy().Conditions);
        var hero = new HeroRecord { Id = "monarch", ClassId = "leper", PathId = "lep_monarch" };
        Assert.DoesNotContain("MonarchOath", JsonConvert.SerializeObject(ExpeditionParty.HeroForRestore(hero, copied)));
        Assert.Null(JsonConvert.DeserializeObject<ExpeditionHeroState>("{\"Hp\":30,\"HpMax\":35}")!.MonarchOath);
    }

    [Fact]
    public void AbominationChoosesFourSkillsPerFormAlongsideAutomaticTransformAndRevert()
    {
        string[] human = { "abm_manacles", "abm_beasts_bile", "abm_absolution", "abm_cuff", "abm_spit" };
        string[] beast = { "abm_rake", "abm_rage", "abm_slam", "abm_howl", "abm_maul" };
        var known = human.Concat(beast).Concat(new[] { "abm_transform", "abm_revert" }).ToList();
        var chosen = Native.Loadout("abomination", known, known);
        Assert.Equal(human.Take(4).Concat(beast.Take(4)), chosen);
        Assert.DoesNotContain("abm_transform", chosen); Assert.DoesNotContain("abm_revert", chosen);
        Assert.Equal(chosen, Native.Loadout("abomination", chosen.Concat(chosen), known));
        Assert.Equal(4, Native.Loadout("abomination", human, known).Count);
        Assert.DoesNotContain("abm_spit", Native.Loadout("abomination", human, human.Take(4)));
    }

    [Fact]
    public void FightReplayRestoresPreFightOathRatherThanKeepingKillsFromAnInterruptedFight()
    {
        var state = new ExpeditionState { Started = true, Party = { "monarch" }, Light = 70 };
        state.PartyStates["monarch"] = new ExpeditionHeroState { Outcome = new HeroOutcome { HeroId = "monarch" }, Hp = 30, HpMax = 35,
            MonarchOath = new MonarchOathState { Stacks = 3, Conditions = 3 } };
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "path-test-battle", null, false, false));
        state.PartyStates["monarch"].MonarchOath.Stacks = 12;
        Assert.True(ExpeditionFight.RestoreParty(state));
        Assert.Equal(3, state.PartyStates["monarch"].MonarchOath.Stacks);
        state.PartyStates["monarch"].MonarchOath.Stacks = 15;
        Assert.Equal(3, state.FightCheckpoint.PartyStates["monarch"].MonarchOath.Stacks);
    }
}
