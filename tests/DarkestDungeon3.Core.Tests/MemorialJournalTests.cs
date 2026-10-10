using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class MemorialJournalTests
{
    private static readonly Dd1Lore Lore = Dd1Lore.Load(Dd1Install.Find());

    [Fact]
    public void OnlyRecoveredNativePagesAppearInOrderWithoutChangingStateOrRandomness()
    {
        var estate = new Estate { RandomCounter = 19 };
        estate.CollectedJournalPages.UnionWith(new[] { 21, 0, 7, -1, 9999 });
        var save = new SaveFile { Estate = estate };
        string before = save.ToJson();
        var pages = Memorial.Journals(estate, Lore);
        Assert.Equal(new[] { 0, 7, 21 }, pages.Select(p => p.Page));
        Assert.All(pages, p => { Assert.Equal(Lore.Text("journal_page_title_" + p.Page), p.Title); Assert.Equal(Lore.Text("journal_page_text_" + p.Page), p.Text); });
        Assert.DoesNotContain(pages, p => p.Page == 1);
        Assert.Equal(before, save.ToJson());
        var loaded = SaveFile.FromJson(before);
        Assert.Equal(pages.Select(p => p.Page), Memorial.Journals(loaded.Estate, Lore).Select(p => p.Page));
    }

    [Fact]
    public void EstatesNeverBorrowEachOthersPagesAndEmptyEstatesRevealNothing()
    {
        var first = new Estate(); first.CollectedJournalPages.Add(0);
        var second = new Estate(); second.CollectedJournalPages.Add(1);
        Assert.Equal(0, Assert.Single(Memorial.Journals(first, Lore)).Page);
        Assert.Equal(1, Assert.Single(Memorial.Journals(second, Lore)).Page);
        Assert.Empty(Memorial.Journals(new Estate(), Lore));
    }
}
