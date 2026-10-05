using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Ui;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class MemorialJournalLayoutTests
{
    [Fact]
    public void LongTextCardsKeepTheirFullHeightAndNativeSpacing()
    {
        var pages = new[] { new MemorialJournal { Page = 0, Title = "short", Text = "long" }, new MemorialJournal { Page = 1, Title = "wrapped", Text = "small" } };
        var layout = MemorialJournalLayout.Build(pages, t => t == "wrapped" ? 90 : 20, t => t == "long" ? 1200 : 30);
        Assert.Equal(1282, layout.Rows[0].Height);
        Assert.Equal(1292, layout.Rows[1].Y);
        Assert.Equal(152, layout.Rows[1].Height);
        Assert.Equal(1454, layout.Height);
        Assert.Same(pages[0], layout.Rows[0].Journal);
        Assert.Equal(1200, layout.Rows[0].BodyHeight);
    }

    [Fact]
    public void EmptyCollectionHasNoInventedCards()
    {
        var layout = MemorialJournalLayout.Build(new MemorialJournal[0], _ => throw new System.Exception(), _ => throw new System.Exception());
        Assert.Empty(layout.Rows); Assert.Equal(0, layout.Height);
    }
}
