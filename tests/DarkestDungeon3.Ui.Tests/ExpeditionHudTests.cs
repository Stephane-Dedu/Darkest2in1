using System.Reflection;
using DarkestDungeon3.Ui;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ExpeditionHudTests
{
    [Theory]
    [InlineData(4, 0, "hero-3")]
    [InlineData(4, 3, "hero-0")]
    [InlineData(2, 0, null)]
    [InlineData(2, 1, null)]
    [InlineData(2, 2, "hero-1")]
    [InlineData(2, 3, "hero-0")]
    [InlineData(0, 3, null)]
    public void InspectTargetsTheHeroDisplayedInTheBackToFrontLineup(int count, int cell, string expected)
    {
        var ui = new EmbarkUi();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var party = (List<string>)typeof(EmbarkUi).GetField("_party", flags).GetValue(ui);
        party.AddRange(Enumerable.Range(0, count).Select(i => "hero-" + i));
        Assert.Equal(expected, typeof(EmbarkUi).GetMethod("PartyHeroAtCell", flags).Invoke(ui, new object[] { cell }));
    }
}
