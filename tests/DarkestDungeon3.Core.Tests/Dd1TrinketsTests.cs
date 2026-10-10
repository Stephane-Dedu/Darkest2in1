using System;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1TrinketsTests
{
    private static readonly Dd1Trinkets Base = Dd1Trinkets.LoadBase(Dd1Install.Find());

    [Theory]
    [InlineData("very_rare", 24)]
    [InlineData("ancestral", 9)]
    [InlineData("ancestral_shambler", 5)]
    [InlineData("legendary", 0)]
    public void ExactBaseRarityPoolsStaySeparate(string rarity, int count)
    {
        var pool = Base.ForRarity(rarity);
        Assert.Equal(count, pool.Count);
        Assert.Equal(pool.Count, pool.Select(t => t.Id).Distinct().Count());
        Assert.All(pool, item => Assert.Equal(rarity, item.Rarity));
    }

    [Fact]
    public void AncestorsPistolRetainsItsActualEffectsAndUniqueLimit()
    {
        var item = Base.Get("ancestors_pistol");
        Assert.Equal("ancestral", item.Rarity);
        Assert.Equal(1, item.Limit);
        Assert.Equal(50000, item.Price);
        Assert.Empty(item.HeroClasses);
        Assert.Equal(new[] { "TRINKET_rangedonly_ACC_B3", "TRINKET_SPD_B3", "TRINKET_ANCESTOR_STRESSDMG" }, item.BuffIds);
    }

    [Fact]
    public void LegendaryNameDoesNotDefineRarityOrDiscardClassRestrictions()
    {
        Assert.Equal("very_rare", Base.Get("legendary_bracer").Rarity);
        Assert.Equal(0, Base.Get("legendary_bracer").Limit);
        Assert.Equal(new[] { "vestal" }, Base.Get("sacred_scroll").HeroClasses);
        Assert.Equal(490, Base.Count);
        Assert.Null(Base.Get("lost_battalion_general_trinket"));
    }

    private const string Item = "{\"id\":\"test\",\"rarity\":\"ancestral\",\"origin_dungeon\":\"crypts\",\"price\":1,\"limit\":1,\"buffs\":[\"effect\"],\"hero_class_requirements\":[]}";

    [Fact]
    public void DuplicateIdentityCannotSilentlyReplaceAnEarlierDefinition() =>
        Assert.Throws<FormatException>(() => Dd1Trinkets.Parse("{\"entries\":[" + Item + "," + Item + "]}"));

    [Theory]
    [InlineData("\"id\":\"test\"", "\"id\":\"\"")]
    [InlineData("\"buffs\":[\"effect\"]", "\"buffs\":null")]
    [InlineData("\"limit\":1", "\"limit\":-1")]
    public void IncompleteDefinitionsCannotBecomeRewardCandidates(string oldValue, string newValue) =>
        Assert.Throws<FormatException>(() => Dd1Trinkets.Parse("{\"entries\":[" + Item.Replace(oldValue, newValue) + "]}"));
}
