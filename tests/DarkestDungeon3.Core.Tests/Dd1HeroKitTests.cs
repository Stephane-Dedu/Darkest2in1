using System.Linq;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>The Shieldbreaker comes with DD1's DLC (the owner's combined install); without it these cases have nothing to read.</summary>
public class Dd1HeroKitTests
{
    private static readonly Dd1Install Install = Dd1Install.Find(@"C:\Users\Piral\darkestwithdlc\game");
    private static readonly Dd1HeroKit Shieldbreaker = Dd1HeroKit.Read(Install, RecruitClasses.Shieldbreaker);

    [Fact]
    public void ReadsTheShieldbreakersGearAndSkills()
    {
        if (Shieldbreaker == null) return;
        Assert.Equal(new[] { "pierce", "break_guard", "adders_kiss", "spearing", "expose", "single_out", "serpents_sway" }, Shieldbreaker.SkillIds);
        Assert.Equal(4, Shieldbreaker.SelectedMax);
        Assert.Equal(5, Shieldbreaker.Weapons.Count);
        Assert.Equal((5, 10, 5), (Shieldbreaker.Weapons[0].DamageMin, Shieldbreaker.Weapons[0].DamageMax, Shieldbreaker.Weapons[0].Speed));
        Assert.Equal(0.06f, Shieldbreaker.Weapons[0].Crit, 4);
        Assert.Equal(20, Shieldbreaker.Armours[0].Hp);
        Assert.Equal(0.5f, Shieldbreaker.Resists["stun"], 4);
        Assert.Equal(0.2f, Shieldbreaker.Resists["blight"], 4);      // DD1 "poison"
        Assert.Equal(0.67f, Shieldbreaker.Resists["death"], 4);      // DD1 "death_blow"
        Assert.True(System.IO.File.Exists(Shieldbreaker.IconFile("pierce")));         // shieldbreaker.ability.one.png
        Assert.EndsWith("shieldbreaker.ability.seven.png", Shieldbreaker.IconFile("serpents_sway"));
        Assert.True(System.IO.File.Exists(Shieldbreaker.PortraitFile));
    }

    [Fact]
    public void ASkillHitsWithItsWeaponTimesItsModifier()
    {
        if (Shieldbreaker == null) return;
        var pierce = Shieldbreaker.Shape("pierce");
        Assert.Equal((5, 9), (pierce.DamageMin, pierce.DamageMax));    // 5-10 at -10%
        Assert.Equal(0.11f, pierce.Crit, 4);                            // weapon 6% + skill 5%
        Assert.Equal(new[] { 1, 2, 3 }, pierce.LaunchRanks);
        Assert.Equal(new[] { 1, 2, 3, 4 }, pierce.TargetRanks);
        Assert.Equal((0, 1), (pierce.MoveBack, pierce.MoveForward));
        Assert.False(pierce.Friendly);

        var stronger = Shieldbreaker.Shape("pierce", level: 0, weaponRank: 4);   // 9-18 at -10%
        Assert.Equal((8, 16), (stronger.DamageMin, stronger.DamageMax));

        var impale = Shieldbreaker.Shape("spearing");
        Assert.True(impale.AllTargets);
        Assert.True(impale.Ranged);
    }

    [Fact]
    public void SerpentSwayIsForHerselfAndDealsNoDamage()
    {
        if (Shieldbreaker == null) return;
        var sway = Shieldbreaker.Shape("serpents_sway");
        Assert.True(sway.Friendly);
        Assert.Equal(0, sway.DamageMax);
        Assert.Contains("SB Aegis", sway.Effects);
    }

    [Fact]
    public void HerDlcEffectsAreRead()
    {
        if (Shieldbreaker == null) return;
        var effects = EffectLibrary.Load(Install);
        Assert.NotNull(effects.Get("SB Adder Blight 1"));
        Assert.NotNull(effects.Get("SB Aegis"));
    }

    [Fact]
    public void HerSkillFlagsAreKept()
    {
        if (Shieldbreaker == null) return;
        Assert.True(Shieldbreaker.Shape("pierce").IgnoreProtection);
        Assert.True(Shieldbreaker.Shape("break_guard").IgnoreGuard);
        Assert.True(Shieldbreaker.Shape("expose").IgnoreStealth);
        Assert.Equal(2, Shieldbreaker.Shape("serpents_sway").PerBattleLimit);
        Assert.Equal(0, Shieldbreaker.Shape("pierce").PerBattleLimit);
    }

    [Fact]
    public void HerEffectsBecomeDd2Tokens()
    {
        if (Shieldbreaker == null) return;
        var effects = EffectLibrary.Load(Install);
        var buffs = Dd1Buffs.Load(Install);
        (System.Collections.Generic.List<string> Target, System.Collections.Generic.List<string> Performer) Of(string skill) =>
            Dd1SkillToDd2.Effects(Shieldbreaker.Shape(skill), effects.Get, new System.Collections.Generic.List<string>(), buffs.Get);

        var puncture = Of("break_guard");
        Assert.Contains("remove_all_guard", puncture.Target);       // Clear Guarded Target
        Assert.Contains("move_pull_2", puncture.Target);            // Pull 2A
        Assert.Contains("add_1_daze", puncture.Target);             // Daze Slowdown 1
        Assert.Contains("skill_dot_large_blight", Of("adders_kiss").Target);
        var expose = Of("expose");
        Assert.Contains("add_1_vulnerable", expose.Target);         // more crits taken
        Assert.Contains("remove_all_stealth", expose.Target);
        Assert.Contains("add_1_daze", expose.Target);               // SB Slow 1
        var sway = Of("serpents_sway");
        Assert.Contains("add_2_block", sway.Performer);             // SB Aegis: two damage blocks
        Assert.Contains("add_1_speed", sway.Performer);
        Assert.Contains("move_forward_1", sway.Performer);
        Assert.Contains("move_backward_1", Of("adders_kiss").Performer);
    }

    [Fact]
    public void HerStatsStandOnTheStandInsBlock()
    {
        if (Shieldbreaker == null) return;
        const string hellion = "key_map,health_max,speed,stress_max,\nadd_stats,35,4,10,\nsub_stat,resistance,stun,0.2,\nsub_stat,resistance,burn,0.3,\n";
        string text = Shieldbreaker.StatsText(hellion, hpScale: 1.5f);
        Assert.Contains("add_stats,30,5,10", text);              // 20 HP x 1.5, speed 5 + 0, stress kept
        Assert.Contains("sub_stat,resistance,stun,0.5,", text);
        Assert.Contains("sub_stat,resistance,burn,0.3,", text);  // DD1 has no burn: the stand-in's
    }

    [Fact]
    public void TheStagecoachOffersHerOnlyWithDd1sDlc()
    {
        Assert.DoesNotContain(RecruitClasses.Shieldbreaker, RecruitClasses.For(ownsCrusader: true));
        Assert.Contains(RecruitClasses.Shieldbreaker, RecruitClasses.For(ownsCrusader: false, hasShieldbreaker: true));
        Assert.True(RecruitClasses.IsDd1Only(RecruitClasses.Shieldbreaker));
        Assert.False(RecruitClasses.IsDd1Only(RecruitClasses.Crusader));
    }
}
