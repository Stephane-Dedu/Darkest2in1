using System;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CampLootTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);
    private static string Pick(string rarity, Rng rng) => rarity + "_item_" + rng.Next(100);

    [Theory]
    [InlineData("pilfer", "S")]
    [InlineData("supply", "S")]
    [InlineData("trinket_scrounge", "T_ANTIQ_CAMP")]
    public void NativeCampLootUsesPackCapacityAndCanBeTakenAfterFreeingSpace(string skillId, string table)
    {
        var state = State(skillId);
        for (int i = 0; i < Inventory.Slots; i++) state.Pack.Add("filler" + i, 1);
        var effect = Assert.Single(Content.Camping.Get(skillId).Effects);
        Assert.Equal(("self", "loot", table, 1f), (effect.Selection, effect.Type, effect.SubType, effect.Amount));
        var rng = new Rng(state.Seed * 31 + (state.RandomCounter + 1) * 977);
        Assert.True(rng.Chance(effect.Chance));
        var expected = LootDrop.ResolveTrinket(Assert.Single(Content.Loot.Roll(table, 1, 1, "crypts", rng)), Pick, rng);
        int pickerCalls = 0;
        var crawl = Crawl(state);
        crawl.TrinketOfRarity = (rarity, random) => { pickerCalls++; return Pick(rarity, random); };
        Assert.True(crawl.UseCampSkill("a", skillId));
        var report = crawl.LastSpoils;
        Assert.Equal("camp", report.Kind);
        Assert.Empty(report.Dd1Monsters); Assert.Empty(report.Taken);
        var drop = Assert.Single(report.LeftBehind);
        Assert.Equal((expected.Key, expected.Amount), (drop.Key, drop.Amount));
        Assert.Equal(skillId == "trinket_scrounge" ? 1 : 0, pickerCalls);
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        Assert.Equal(0, state.Pack.Count(drop.Key));
        Assert.False(crawl.TakeLeftBehind(report.LeftBehind, 0, report.Taken));
        Assert.True(crawl.Discard("filler0"));
        int counter = state.RandomCounter;
        Assert.True(crawl.TakeLeftBehind(report.LeftBehind, 0, report.Taken));
        Assert.Empty(report.LeftBehind);
        Assert.Same(drop, Assert.Single(report.Taken));
        Assert.Equal(drop.Amount, state.Pack.Count(drop.Key));
        Assert.Equal(counter, state.RandomCounter);
        Assert.False(crawl.TakeLeftBehind(report.LeftBehind, 0, report.Taken));

        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        Assert.Equal(drop.Amount, loaded.Pack.Count(drop.Key));
        Assert.Equal(1, loaded.Camp.Uses["a:" + skillId]);
        if (Content.Camping.Get(skillId).UseLimit == 1)
        {
            var resumed = Crawl(loaded);
            string before = new SaveFile { Expedition = loaded }.ToJson();
            Assert.False(resumed.UseCampSkill("a", skillId));
            Assert.Equal(before, new SaveFile { Expedition = loaded }.ToJson());
        }
    }

    [Fact]
    public void CampSuppliesCanFillAnExistingStackInAFullPackWithoutExtraRolls()
    {
        var state = State("pilfer");
        var rng = new Rng(state.Seed * 31 + (state.RandomCounter + 1) * 977);
        rng.Chance(1);
        var expected = Assert.Single(Content.Loot.Roll("S", 1, 1, "crypts", rng));
        int limit = Content.Items.StackLimit(expected.Key);
        Assert.True(limit > expected.Amount);
        state.Pack.Add(expected.Key, limit - expected.Amount);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;
        var crawl = Crawl(state);
        var resumed = Crawl(loaded);
        Assert.True(crawl.UseCampSkill("a", "pilfer"));
        Assert.True(resumed.UseCampSkill("a", "pilfer"));
        Assert.Empty(crawl.LastSpoils.LeftBehind);
        Assert.Equal(expected.Key, Assert.Single(crawl.LastSpoils.Taken).Key);
        Assert.Equal(limit, state.Pack.Count(expected.Key));
        Assert.Equal(Inventory.Slots, state.Pack.SlotsUsed(Content.Items));
        Assert.Equal(8, state.RandomCounter);
        Assert.Equal(new SaveFile { Expedition = loaded }.ToJson(), new SaveFile { Expedition = state }.ToJson());
        var report = crawl.LastSpoils;
        Assert.False(crawl.UseCampSkill("a", "pilfer"));
        Assert.Same(report, crawl.LastSpoils);
    }

    private static Crawl Crawl(ExpeditionState state) => new(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a"), Content);
    private static ExpeditionState State(string skillId) => new()
    {
        Seed = 17, RandomCounter = 7, Party = { "a" }, Camp = new CampState { Ate = true, RespiteLeft = 12 },
        CampSkills = { ["a"] = new() { skillId } },
        Quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Difficulty = 1 },
        Map = new DungeonMap { Rooms = { new Room { Id = 0, Content = RoomContent.Empty } } }
    };
}
