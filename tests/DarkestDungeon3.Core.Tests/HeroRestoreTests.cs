using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class HeroRestoreTests
{
    [Fact]
    public void RecordedLoadoutOverridesConditionAndKeepsIndependentProgressionAndSlots()
    {
        var hero = new HeroRecord
        {
            Id = "a", ClassId = "highwayman", Name = "Dismas", PathId = "sharpshot",
            ResolveLevel = 3, ResolveXp = 18, WeaponRank = 2, ArmorRank = 3, Stress = 1,
            EquippedSkills = { "pistol_shot" }, LearnedSkills = { "double_tap" },
            MasteredSkills = { "pistol_shot" }, CampingSkills = { "pilfer" },
            SkillRanks = { ["pistol_shot"] = 2 }, LockedQuirks = { "locked" },
            Quirks = { "old" }, Trinkets = { null, "ring" }, PendingBuffs = { "town_buff" }
        };
        var saved = Saved();
        string before = JsonConvert.SerializeObject(hero);
        var restored = ExpeditionParty.HeroForRestore(hero, saved);
        Assert.Equal(7, restored.Stress);
        Assert.Equal(new[] { "changed" }, restored.Quirks);
        Assert.Equal(new string[] { null, "ring" }, restored.Trinkets);
        Assert.Equal("sharpshot", restored.PathId);
        Assert.Equal(3, restored.ResolveLevel); Assert.Equal(18, restored.ResolveXp);
        Assert.Equal(2, restored.WeaponRank); Assert.Equal(3, restored.ArmorRank);
        Assert.Equal(hero.EquippedSkills, restored.EquippedSkills);
        Assert.Equal(hero.LearnedSkills, restored.LearnedSkills);
        Assert.Equal(hero.MasteredSkills, restored.MasteredSkills);
        Assert.Equal(hero.CampingSkills, restored.CampingSkills);
        Assert.Equal(hero.LockedQuirks, restored.LockedQuirks);
        Assert.Equal(hero.PendingBuffs, restored.PendingBuffs);
        restored.EquippedSkills.Clear(); restored.LearnedSkills.Clear(); restored.MasteredSkills.Clear();
        restored.CampingSkills.Clear(); restored.SkillRanks.Clear(); restored.LockedQuirks.Clear();
        restored.PendingBuffs.Clear(); restored.Quirks.Clear(); restored.Trinkets.Clear();
        Assert.Equal(before, JsonConvert.SerializeObject(hero));
        Assert.Equal(new[] { "changed" }, saved.Outcome.Quirks);
        Assert.Equal(new[] { "ring" }, saved.Outcome.Trinkets);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("foreign")]
    [InlineData("invalid")]
    [InlineData("unknown-loadout")]
    public void MissingOrInvalidDataRetainsEstateEvidenceInACopy(string kind)
    {
        var hero = new HeroRecord { Id = "a", Stress = 3, Quirks = { "old" }, Trinkets = { "old_ring" } };
        var saved = Saved();
        if (kind == "legacy") saved = null;
        else if (kind == "foreign") saved.Outcome.HeroId = "other";
        else if (kind == "invalid") saved.Stress = float.MaxValue;
        else { saved.Outcome.Quirks = null; saved.Outcome.Trinkets = null; }
        var restored = ExpeditionParty.HeroForRestore(hero, saved);
        Assert.NotSame(hero, restored);
        Assert.Equal(kind == "unknown-loadout" ? 7 : 3, restored.Stress);
        Assert.Equal(hero.Quirks, restored.Quirks); Assert.Equal(hero.Trinkets, restored.Trinkets);
        restored.Quirks.Clear(); restored.Trinkets.Clear();
        Assert.Single(hero.Quirks); Assert.Single(hero.Trinkets);
    }

    [Fact]
    public void ConfirmedDeathsNeverRespawnEvenIfOtherRecordedFieldsAreUnavailable()
    {
        var hero = new HeroRecord { Id = "a" };
        var saved = Saved(); saved.Outcome.Died = true; saved.HpMax = 0;
        Assert.Null(ExpeditionParty.HeroForRestore(hero, saved));
        saved.Outcome.HeroId = "other";
        Assert.NotNull(ExpeditionParty.HeroForRestore(hero, saved));
        hero.IsDead = true;
        Assert.Null(ExpeditionParty.HeroForRestore(hero, null));
        Assert.Null(ExpeditionParty.HeroForRestore(null, saved));
    }

    private static ExpeditionHeroState Saved() => new()
    {
        Hp = -2, HpMax = 30, Stress = 6.75f, WoundPercent = .2f,
        Outcome = new HeroOutcome { HeroId = "a", Quirks = new() { "changed" }, Trinkets = new() { "ring" } }
    };
}
