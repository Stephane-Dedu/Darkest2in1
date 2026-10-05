using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Ui;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class MemorialNarrationLayoutTests
{
    [Fact]
    public void NativeLongNarrationFitsItsMeasuredCardAndNextRow()
    {
        var lore = Dd1Lore.Load(Dd1Install.Find());
        var entries = new[] { new MemorialNarration { PlotId = "plot_kill_necromancer_1", Complete = true }, new MemorialNarration { PlotId = "plot_darkest_dungeon_1" } };
        var layout = MemorialNarrationLayout.Build(entries, lore, _ => 900);
        Assert.Equal(lore.Text("str_plot_kill_necromancer_1_audio_line"), layout.Rows[0].Caption);
        Assert.Equal(900, layout.Rows[0].TextHeight);
        Assert.Equal(924, layout.Rows[0].Height);
        Assert.Equal(1868, layout.Height);
        Assert.Equal(lore.Text("str_caretaker_goal_plot_darkest_dungeon_1"), layout.Rows[1].Caption);
    }

    [Fact]
    public void ShortCardsRetainNativePortraitHeightAndEmptyCategoriesHaveNoRows()
    {
        var lore = Dd1Lore.Load(Dd1Install.Find());
        var layout = MemorialNarrationLayout.Build(new[] { new MemorialNarration { PlotId = "plot_kill_hag_1" } }, lore, _ => 20);
        Assert.Equal(120, Assert.Single(layout.Rows).Height);
        Assert.Equal(130, layout.Height);
        Assert.Empty(MemorialNarrationLayout.Build(System.Array.Empty<MemorialNarration>(), lore, _ => 20).Rows);
    }
}
