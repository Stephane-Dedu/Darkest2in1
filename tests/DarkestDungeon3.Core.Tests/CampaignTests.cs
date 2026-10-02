using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

public class CampaignTests
{
    private readonly ITestOutputHelper _out;
    public CampaignTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    [Fact]
    public void LoadsDd1CampaignTables()
    {
        Assert.Equal(new[] { "crypts", "weald", "warrens", "cove" }, Dd1.ZoneUnlocks.Keys.ToArray());
        Assert.Equal(1, Dd1.ZoneUnlocks["crypts"]);
        Assert.Equal(8, Dd1.QuestTables["crypts"].Count);
        Assert.Equal(new[] { 2, 6, 8, 9, 10, 11, 12, 13 }, Dd1.QuestsPerVisit.ToArray());
        Assert.Equal(3000, Dd1.Gold(1, 1));
        Assert.Equal(15000, Dd1.Gold(5, 3));
        Assert.Equal(4, Dd1.HeirloomAmount("bust", 1, 3));
        Assert.Equal(18, Dd1.HeirloomAmount("crest", 5, 3));
    }

    [Fact]
    public void ZoneLevelsFollowDd1Thresholds()
    {
        Assert.Equal(0, Dd1.ZoneLevel(0));
        Assert.Equal(1, Dd1.ZoneLevel(2));
        Assert.Equal(2, Dd1.ZoneLevel(9));
        Assert.Equal(3, Dd1.ZoneLevel(10));
        Assert.Equal(1, Dd1Campaign.MaxDifficultyForZoneLevel(2));
        Assert.Equal(3, Dd1Campaign.MaxDifficultyForZoneLevel(3));
        Assert.Equal(5, Dd1Campaign.MaxDifficultyForZoneLevel(6));
    }

    [Fact]
    public void NewEstateOffersOnlyRuinsApprenticeShortQuests()
    {
        var estate = new Estate { Seed = 42 };
        var board = QuestBoard.Generate(estate, Dd1);
        Assert.Equal(6, board.Count); // QuestsCompleted starts at 1 (DD1 counts the tutorial) → table index 1
        Assert.All(board, q => Assert.Equal("crypts", q.Dungeon));
        Assert.All(board, q => Assert.Equal(1, q.Difficulty));
        Assert.All(board, q => Assert.Contains(q.Rewards, r => r.Type == Currency.Gold && r.Amount > 0));
        foreach (var q in board) _out.WriteLine($"{q}: {string.Join(", ", q.Rewards)}");
    }

    [Fact]
    public void ZonesOpenAsQuestsAreFinished()
    {
        var estate = new Estate { Seed = 7, QuestsCompleted = 4 };
        var zones = QuestBoard.Generate(estate, Dd1).Select(q => q.Dungeon).Distinct().OrderBy(z => z).ToArray();
        Assert.Equal(new[] { "cove", "crypts", "warrens", "weald" }, zones);
    }

    [Fact]
    public void VeteranZoneCanOfferVeteranQuests()
    {
        var estate = new Estate { Seed = 3, QuestsCompleted = 6 };
        estate.ZoneXp["weald"] = 12; // zone level 3
        var weald = Enumerable.Range(0, 20).SelectMany(_ => QuestBoard.Generate(estate, Dd1)).Where(q => q.Dungeon == "weald").ToList();
        Assert.Contains(weald, q => q.Difficulty == 3);
        Assert.DoesNotContain(weald, q => q.Difficulty == 5);
    }

    [Fact]
    public void EveryOfferedQuestHasAMapConfig()
    {
        var estate = new Estate { Seed = 11, QuestsCompleted = 7 };
        foreach (var q in Enumerable.Range(0, 30).SelectMany(_ => QuestBoard.Generate(estate, Dd1)))
        {
            var p = Dd1.MapGen.Find(q.Dungeon, q.Size, q.Type);
            Assert.Equal(q.Dungeon, p.Dungeon);
            Assert.Equal(q.Type, p.QuestType);
        }
    }
}
