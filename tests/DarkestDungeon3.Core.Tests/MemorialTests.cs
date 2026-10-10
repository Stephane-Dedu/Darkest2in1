using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class MemorialTests
{
    [Fact]
    public void Dd1sPrologueCanBeReplayedAndTheEpilogueRequiresTheFinalQuest()
    {
        var media = Memorial.Load(Dd1Install.Find());
        var completed = new List<string>();
        Assert.Equal(new[] { "prologue", "boss_entries", "dd_entries", "epilog", "backerjournal" }, media.Categories.Select(c => c.Name));
        Assert.Equal(3, media.Videos.Count);
        Assert.All(media.Videos, v => Assert.True(v.Visible()));
        Assert.All(media.Videos.Where(v => v.Category == "prologue"), v => Assert.True(v.CanPlay(completed)));
        var epilogue = Assert.Single(media.Videos, v => v.Name == "epilog");
        Assert.False(epilogue.CanPlay(completed));
        completed.Add("plot_darkest_dungeon_3");
        Assert.False(epilogue.CanPlay(completed));
        completed.Add("plot_darkest_dungeon_4");
        Assert.True(epilogue.CanPlay(completed));
    }

    [Fact]
    public void PreviouslyViewedMediaUsesTheVisibilityAndAccessFlags()
    {
        var video = new MemorialVideo { Name = "example", ShowOnlyIfViewed = true, AccessOnlyIfViewed = true };
        Assert.False(video.Visible());
        Assert.False(video.CanPlay(new List<string>()));
        var viewed = new List<string> { "example" };
        Assert.True(video.Visible(viewed));
        Assert.True(video.CanPlay(new List<string>(), viewed));
    }
}
