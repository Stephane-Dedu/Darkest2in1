using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class MemorialNarrationTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Campaign = Dd1Campaign.Load(Install);
    private static readonly Dd1Lore Lore = Dd1Lore.Load(Install);
    private static readonly Memorial Media = Memorial.Load(Install);

    [Fact]
    public void NativeNarrationHasExactSamplesPortraitsAndBothLocalizedCaptions()
    {
        var index = Fsb5Index.Load(new[] { Install.PathOf("audio", "secondary_banks", "voiceover.bank") });
        Assert.Equal(27, Media.Narration.Count);
        Assert.Equal(24, Media.Narration.Count(n => n.Category == "boss_entries"));
        Assert.Equal(3, Media.Narration.Count(n => n.Category == "dd_entries"));
        Assert.DoesNotContain(Media.Narration, n => n.PlotId == "plot_darkest_dungeon_4");
        Assert.All(Media.Narration, n =>
        {
            Assert.True(index.Has(n.Sample), n.Sample);
            Assert.True(File.Exists(Install.PathOf("campaign", "town", "buildings", "statue", n.Portrait)), n.Portrait);
            Assert.False(string.IsNullOrWhiteSpace(n.Caption(Lore)), n.PlotId);
            var complete = new MemorialNarration { PlotId = n.PlotId, Complete = true };
            Assert.False(string.IsNullOrWhiteSpace(complete.Caption(Lore)), n.PlotId);
        });
        Assert.Equal("vo_narr_load_crypts_01", Media.Narration.Single(n => n.PlotId == "plot_kill_necromancer_1").Sample);
        Assert.Equal("vo_narr_neut_darkest03_load", Media.Narration.Single(n => n.PlotId == "plot_darkest_dungeon_3").Sample);
        Assert.Null(MemorialNarration.SampleFor("/unknown"));
    }

    [Fact]
    public void NativeLairCompletionsNeverUnlockUnrelatedDd1Narration()
    {
        var estate = new Estate();
        estate.CompletedPlotQuests.Add("region_dd2_city_boss_1");
        var entries = Media.Narrations(estate, Campaign);
        Assert.Equal(3, entries.Count);
        Assert.All(entries, n => { Assert.Equal("dd_entries", n.Category); Assert.False(n.Complete); });
        estate.Toggles["zone.crypts"] = true;
        entries = Media.Narrations(estate, Campaign);
        Assert.Equal(9, entries.Count);
        Assert.All(entries, n => Assert.False(n.Complete));
    }

    [Fact]
    public void CompletedDisabledHistoryAndTierUnlocksAreReadOnlyAndEstateSpecific()
    {
        var estate = new Estate { RandomCounter = 71 };
        estate.CompletedPlotQuests.UnionWith(new[] { "plot_kill_necromancer_2", "plot_darkest_dungeon_1" });
        var save = new SaveFile { Estate = estate };
        string before = save.ToJson();
        var entries = Media.Narrations(estate, Campaign);
        Assert.Equal(4, entries.Count);
        Assert.True(entries.Single(n => n.PlotId == "plot_kill_necromancer_2").Complete);
        Assert.True(entries.Single(n => n.PlotId == "plot_darkest_dungeon_1").Complete);
        Assert.False(entries.Single(n => n.PlotId == "plot_darkest_dungeon_2").Complete);
        Assert.Equal(before, save.ToJson());
        Assert.Equal(entries.Select(n => (n.PlotId, n.Complete)), Media.Narrations(SaveFile.FromJson(before).Estate, Campaign).Select(n => (n.PlotId, n.Complete)));
        Assert.All(Media.Narrations(new Estate(), Campaign), n => Assert.False(n.Complete));
        estate.Toggles["zone.crypts"] = true;
        Assert.Equal(6, Media.Narrations(estate, Campaign).Count(n => n.Category == "boss_entries"));
        estate.Toggles["zone.crypts"] = false;
        Assert.Single(Media.Narrations(estate, Campaign).Where(n => n.Category == "boss_entries"));
        Assert.False(Media.Narration.Any(n => n.Complete));
    }
}
