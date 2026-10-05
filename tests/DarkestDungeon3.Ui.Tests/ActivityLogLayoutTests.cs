using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Ui;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ActivityLogLayoutTests
{
    private static float Measure(string text, float width) => MathF.Max(22, MathF.Ceiling((text?.Length ?? 0) * 11 / width) * 22);

    [Fact]
    public void WeekOrderMatchesDd1ReturnTownThenEmbarkAndSkipsEmptyWeeks()
    {
        var first = new ActivityWeek { Week = 0, Town = { "Older week" } };
        var current = new ActivityWeek
        {
            Week = 2, Town = { "Town outcome" },
            Raids = { new() { Result = "embark", Quest = "short explore in dd2_sprawl", Region = "dd2_sprawl" }, new() { Result = "complete", Quest = "short explore in dd2_sprawl", Region = "dd2_sprawl", Messages = { "Reward: 100 gold" } } }
        };
        var layout = ActivityLogLayout.Build(new[] { first, new ActivityWeek { Week = 1 }, current }, Measure, _ => "The Sprawl");
        Assert.Equal(new[] { "Week 2", "Quest complete: short explore in The Sprawl", "Reward: 100 gold", "Town outcome", "Embarked: short explore in The Sprawl", "Week 0", "Older week" }, layout.Rows.Select(r => r.Text));
        Assert.Equal(new[] { ActivityLogLayout.Kind.Week, ActivityLogLayout.Kind.Raid, ActivityLogLayout.Kind.Town, ActivityLogLayout.Kind.Town, ActivityLogLayout.Kind.Raid, ActivityLogLayout.Kind.Week, ActivityLogLayout.Kind.Town }, layout.Rows.Select(r => r.Type));
    }

    [Fact]
    public void LongTextAndSavedNamesIncreaseHeightWithoutOverlappingFollowingRows()
    {
        var raid = new ActivityRaid { Result = "retreat", Quest = new string('Q', 300), Heroes = { new() { Name = new string('N', 80) } } };
        var layout = ActivityLogLayout.Build(new[] { new ActivityWeek { Week = 6, Town = { new string('T', 600) }, Raids = { raid } } }, Measure);
        var party = layout.Rows.Single(r => r.Type == ActivityLogLayout.Kind.Raid);
        Assert.True(party.NameHeight > 22);
        Assert.True(party.Height >= 60 + 125 + party.NameHeight + Measure(party.Text, 550) + 16);
        Assert.True(layout.Rows.Single(r => r.Type == ActivityLogLayout.Kind.Town).Height > 120);
        for (int i = 1; i < layout.Rows.Count; i++) Assert.Equal(layout.Rows[i - 1].Y + layout.Rows[i - 1].Height + 20, layout.Rows[i].Y);
        Assert.Equal(layout.Rows.Last().Y + layout.Rows.Last().Height + 20, layout.Height);
    }

    [Fact]
    public void ThousandWeekHistoryOnlyExposesVisibleRowsAndCanReachTheOldestEntry()
    {
        var weeks = Enumerable.Range(0, 1000).Select(w => new ActivityWeek { Week = w, Town = { $"Outcome {w}" } });
        var layout = ActivityLogLayout.Build(weeks, Measure);
        Assert.Equal(2000, layout.Rows.Count);
        Assert.Equal("Week 999", layout.Rows[0].Text);
        var newest = layout.Visible(0, ActivityLogLayout.ViewHeight).ToList();
        Assert.InRange(newest.Count, 1, 6);
        var oldest = layout.Visible(layout.Height - ActivityLogLayout.ViewHeight, ActivityLogLayout.ViewHeight).ToList();
        Assert.InRange(oldest.Count, 1, 6);
        Assert.Equal("Outcome 0", oldest.Last().Text);
        Assert.All(oldest, r => Assert.True(r.Y + r.Height > layout.Height - ActivityLogLayout.ViewHeight && r.Y < layout.Height));
        Assert.Empty(layout.Visible(layout.Height, ActivityLogLayout.ViewHeight));
    }

    [Fact]
    public void NullEmptyAndOpeningHistoriesKeepTheDd1ViewportBounds()
    {
        Assert.Empty(ActivityLogLayout.Build(null, Measure).Rows);
        Assert.Equal(0, ActivityLogLayout.Build(new[] { new ActivityWeek() }, Measure).Height);
        Assert.Equal(620, ActivityLogLayout.ViewWidth);
        Assert.Equal(550, ActivityLogLayout.ViewHeight);
        Assert.True(ActivityLogLayout.Width < ActivityLogLayout.ViewWidth);
        var opening = ActivityLogLayout.Build(new[] { new ActivityWeek { Week = 0, Raids = { new() { Result = "embark", Quest = "Opening raid" } } } }, Measure);
        Assert.Equal("Week 0", opening.Rows[0].Text);
        Assert.All(opening.Visible(0, ActivityLogLayout.ViewHeight), r => Assert.InRange(r.Y, 0, ActivityLogLayout.ViewHeight));
    }

    [Fact]
    public void SavedTownActorsUseDd1PortraitAndTextWidthsWhileLegacyEntriesKeepFullWidth()
    {
        var widths = new List<float>();
        var week = new ActivityWeek
        {
            Town = { "Legacy message", new string('T', 600) },
            TownActors = { new() { MessageIndex = 1, HeroName = "Dismas", HeroClass = "highwayman" } }
        };
        var layout = ActivityLogLayout.Build(new[] { week }, (text, width) => { widths.Add(width); return Measure(text, width); });
        Assert.Equal(new[] { 550f, 440f }, widths);
        var entries = layout.Rows.Where(r => r.Type == ActivityLogLayout.Kind.Town).ToList();
        Assert.Null(entries[0].Actor);
        Assert.Equal("highwayman", entries[1].Actor.HeroClass);
        Assert.Equal("Dismas", entries[1].Actor.HeroName);
        Assert.True(entries[1].Height >= Measure(entries[1].Text, 440) + 32);
        Assert.True(20 + 90 < 135); // portrait clears the text's left edge
        Assert.True(135 + 440 <= ActivityLogLayout.Width);
    }
}
