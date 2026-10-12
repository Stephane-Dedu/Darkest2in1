using DarkestDungeon3.Core.Campaign;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionFormationTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    public void DroppingASelectedHeroSwapsOnlyTheTwoRanks(int from, int to)
    {
        var party = new List<string> { "front", "second", "third", "back" };
        var expected = party.ToArray();
        (expected[from], expected[to]) = (expected[to], expected[from]);
        Assert.True(ExpeditionFormation.Drop(party, party[from], to));
        Assert.Equal(expected, party);
        Assert.Equal(4, party.Distinct().Count());
    }

    [Fact]
    public void ANewRosterHeroReplacesAnOccupiedRankInAFullParty()
    {
        var party = new List<string> { "a", "b", "c", "d" };
        Assert.True(ExpeditionFormation.Drop(party, "e", 2));
        Assert.Equal(new[] { "a", "b", "e", "d" }, party);
    }

    [Fact]
    public void EmptyRanksStayPackedAndExistingHeroesAreNeverDuplicated()
    {
        var party = new List<string> { "a", "b" };
        Assert.True(ExpeditionFormation.Drop(party, "c", 3));
        Assert.Equal(new[] { "a", "b", "c" }, party);
        Assert.True(ExpeditionFormation.Drop(party, "a", 3));
        Assert.Equal(new[] { "c", "b", "a" }, party);
        Assert.True(ExpeditionFormation.Drop(party, "a", 2));
        Assert.Equal(new[] { "c", "b", "a" }, party);
    }

    [Theory]
    [InlineData(-1, "new")]
    [InlineData(4, "new")]
    [InlineData(0, null)]
    [InlineData(0, "")]
    public void InvalidDropsPreserveTheFormation(int rank, string id)
    {
        var party = new List<string> { "a", "b" };
        Assert.False(ExpeditionFormation.Drop(party, id, rank));
        Assert.Equal(new[] { "a", "b" }, party);
    }
}
