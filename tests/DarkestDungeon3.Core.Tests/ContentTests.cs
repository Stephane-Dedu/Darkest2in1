using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Curios, loot, traps and effects, all read from the user's DD1 install.</summary>
public class ContentTests
{
    private readonly ITestOutputHelper _out;
    public ContentTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly CrawlContent Content = CrawlContent.Load(Install);
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);

    [Fact]
    public void ParsesTheCurioLibrary()
    {
        var lib = CurioLibrary.Load(Install);
        _out.WriteLine($"{lib.Curios.Count} curios");
        Assert.True(lib.Curios.Count >= 50);

        var box = lib.Get("unlocked_strongbox");
        var loot = box.Outcomes.Single(o => o.Type == "Loot");
        Assert.Equal(3f, loot.Weight);
        Assert.Equal("A", loot.Results[0].Name);
        Assert.Equal(2f, loot.Results[0].Weight);
        Assert.Contains(box.Outcomes, o => o.Type == "Effect" && o.Results[0].Name == "Blight 1");

        var locked = lib.Get("locked_strongbox");
        Assert.Contains(locked.Items, i => i.Item == "skeleton_key" && i.Outcome.Type == "Loot");

        // Every curio the zones can place exists in the library.
        foreach (var zone in new[] { "crypts", "weald", "warrens", "cove" })
            foreach (var table in new[] { ZoneProps.HallCurios, ZoneProps.RoomCurios, ZoneProps.RoomTreasures })
                foreach (var (_, id) in Dd1.Props(zone).Table(table))
                    Assert.True(lib.Get(id) != null, $"{zone} {table}: {id} missing from the curio library");
    }

    [Fact]
    public void LootTableADrawsDd1Treasure()
    {
        var rng = new Rng(9);
        var all = Enumerable.Range(0, 200).SelectMany(_ => Content.Loot.Roll("A", 1, 1, "crypts", rng)).ToList();
        Assert.Contains(all, d => d.Type == "gold");
        Assert.Contains(all, d => d.Type == "gem");
        Assert.Contains(all, d => d.Type == "heirloom");
        _out.WriteLine(string.Join(", ", all.Take(12)));
    }

    [Fact]
    public void TrapsComeFromDd1Definitions()
    {
        var spikes = Content.Traps.Get("spikes", 1);
        Assert.Equal(-0.25f, spikes.HealthFraction, 3);
        Assert.Equal(-0.3f, Content.Traps.Get("spikes", 5).HealthFraction, 3);
        Assert.Contains("Stress 2", spikes.FailEffects);
        Assert.Contains("Blight 3", Content.Traps.Get("poison_cloud", 5).FailEffects);
    }

    [Fact]
    public void EffectsChangeStressAndHealth()
    {
        var party = new FakeParty("a");
        var rng = new Rng(3);
        for (int i = 0; i < 20; i++) Content.Curios.ApplyEffect("Stress 2", "a", party, rng); // 15 DD1 = 1.5 points each
        Assert.InRange(party.Stress["a"], 22, 38);
        Content.Curios.ApplyEffect("Blight 1", "a", party, rng);                             // 2 x 3 turns = 6 of ~30 HP
        Assert.Equal(0.8f, party.Hp["a"], 2);
    }

    [Fact]
    public void InvestigatingACurioFillsThePack()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1 };
            var map = MapGenerator.Generate(Dd1.MapGen.Find("crypts", "short", "explore"), seed, Dd1.Props("crypts"));
            var state = new ExpeditionState { Quest = quest, Map = map, Seed = seed, Party = { "a", "b", "c", "d" } };
            var party = new FakeParty("a", "b", "c", "d");
            var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);
            crawl.Begin();
            var c = map.Corridors.FirstOrDefault(k => k.RoomA == map.EntranceRoomId && k.Tiles[0].Content == HallContent.Curio);
            if (c == null) continue;
            crawl.Travel(c.RoomB);
            Assert.NotNull(crawl.CurioHere);
            var report = crawl.InteractCurio("a", null, out var overflow);
            _out.WriteLine($"seed {seed}: {report.CurioId} → {report.OutcomeType}: {report.Text} {string.Join(", ", report.Loot)}");
            Assert.Null(crawl.CurioHere);
            Assert.Empty(overflow);
            if (report.OutcomeType == "Loot") Assert.True(state.Pack.Items.Count > 0);
            return;
        }
        Assert.Fail("no curio next to the entrance in 200 seeds");
    }

    [Fact]
    public void ItemsGuaranteeTheirInteraction()
    {
        var state = new ExpeditionState { Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 }, Map = new DungeonMap() };
        state.Pack.Add(Supply.Key, 1);
        var report = Content.Curios.Resolve("locked_strongbox", "a", Supply.Key, state, new FakeParty("a"), new Rng(1));
        Assert.Equal(Supply.Key, report.ItemUsed);
        Assert.Equal("Loot", report.OutcomeType);
        Assert.Equal(0, state.Pack.Count(Supply.Key));
        Assert.NotEmpty(report.Loot);
    }
}
