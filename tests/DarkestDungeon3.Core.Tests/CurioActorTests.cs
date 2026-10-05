using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CurioActorTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    [Theory]
    [InlineData(null, false)]
    [InlineData("dead", false)]
    [InlineData("unknown", false)]
    [InlineData(null, true)]
    [InlineData("dead", true)]
    [InlineData("unknown", true)]
    public void InvalidInvestigatorLeavesOrdinaryCurioAndNextOutcomeUntouched(string actor, bool hall)
    {
        var state = State("heirloom_chest", hall);
        state.Pack.Add("skeleton_key", 1);
        var party = Party();
        var crawl = Crawl(state, party);
        string before = Json(state);
        Assert.Null(crawl.InteractCurio(actor, "skeleton_key", out var rejected));
        Assert.Empty(rejected);
        Assert.Equal(before, Json(state));
        Assert.Empty(party.Log);
        Assert.Empty(party.Quirks);
        Assert.Equal(0, party.Hp["dead"]);

        // A save/reload and the subsequent real investigator retain the original draw sequence.
        var loaded = SaveFile.FromJson(before).Expedition;
        var expected = Crawl(loaded, Party()).InteractCurio("alive", "skeleton_key", out var expectedOverflow);
        var actual = crawl.InteractCurio("alive", "skeleton_key", out var overflow);
        Assert.Equal("skeleton_key", actual.ItemUsed);
        Assert.NotEmpty(actual.Loot);
        Assert.Equal(expected.Loot.Select(d => (d.Key, d.Amount)), actual.Loot.Select(d => (d.Key, d.Amount)));
        Assert.Equal(expectedOverflow.Select(d => (d.Key, d.Amount)), overflow.Select(d => (d.Key, d.Amount)));
        Assert.Equal(Json(loaded), Json(state));
        Assert.Null(crawl.CurioHere);
    }

    [Theory]
    [InlineData(null, "gather")]
    [InlineData("dead", "gather")]
    [InlineData("unknown", "gather")]
    [InlineData(null, "inventory_activate")]
    [InlineData("dead", "inventory_activate")]
    [InlineData("unknown", "inventory_activate")]
    public void InvalidInvestigatorCannotCollectOrActivateQuestObjective(string actor, string type)
    {
        var goal = Dd1.Goals.For(type, "crypts");
        var state = State(goal.CurioName, hall: type == "gather");
        state.Quest.Type = type;
        state.Goal = goal;
        state.GoalProgress = goal.Amount - 1;
        state.Map.Rooms[0].IsQuestGoal = true;
        state.Map.Corridors[0].Tiles[0].IsQuestGoal = true;
        string item = goal.NeedsItem ? ItemCatalog.QuestKey(goal.StartingItems[0].Id) : null;
        if (item != null) state.Pack.Add(item, 1);
        var crawl = Crawl(state, Party());
        string before = Json(state);
        Assert.Null(crawl.InteractCurio(actor, item, out var rejected));
        Assert.Empty(rejected);
        Assert.Equal(before, Json(state));
        Assert.False(state.QuestComplete);

        var loaded = SaveFile.FromJson(before).Expedition;
        var resumed = Crawl(loaded, Party());
        Assert.Equal("Quest", resumed.InteractCurio("alive", item, out var overflow).OutcomeType);
        Assert.Empty(overflow);
        Assert.True(loaded.QuestComplete);
        Assert.Equal(goal.Amount, loaded.GoalProgress);
        Assert.Null(resumed.CurioHere);
        if (item != null) Assert.Equal(0, loaded.Pack.Count(item));
        else Assert.Equal(1, loaded.Pack.Count(ItemCatalog.QuestKey(goal.QuestItem)));
        Assert.Equal(state.RandomCounter, loaded.RandomCounter);
    }

    private static FakeParty Party()
    {
        var party = new FakeParty("alive", "dead");
        party.Hp["dead"] = 0;
        return party;
    }

    private static Crawl Crawl(ExpeditionState state, FakeParty party) =>
        new(state, CrawlRules.FromDd1(Dd1.Rules), party, Content);

    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();

    private static ExpeditionState State(string curio, bool hall) => new()
    {
        Seed = 321, RandomCounter = 8, Party = { "alive", "dead" },
        Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1, Type = "gather" },
        RoomId = hall ? -1 : 0, CorridorId = hall ? 0 : -1, TileIndex = hall ? 0 : -1,
        Map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = curio } },
            Corridors = { new Corridor { Id = 0, Tiles = { new HallTile { Index = 0, Content = HallContent.Curio, ContentId = curio } } } }
        }
    };
}
