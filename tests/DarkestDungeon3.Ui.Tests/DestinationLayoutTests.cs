using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class DestinationLayoutTests
{
    [Fact]
    public void FourCardsFitBetweenTheQuestDetailsAndTheRoster()
    {
        var first = DestinationLayout.Card(0, 4);
        var last = DestinationLayout.Card(3, 4);
        Assert.True(first.x >= 430, $"first card at {first.x} covers the quest details");
        Assert.True(last.xMax <= 1538, $"last card ends at {last.xMax} under the roster");
        for (int i = 1; i < 4; i++)
            Assert.True(DestinationLayout.Card(i, 4).x >= DestinationLayout.Card(i - 1, 4).xMax, "cards overlap");
    }

    [Fact]
    public void FewerCardsStayCentredUnderTheMountain()
    {
        float centre = DestinationLayout.Mountain.center.x;
        Assert.Equal(centre, DestinationLayout.Card(0, 1).center.x, 3);
        Assert.Equal(centre, (DestinationLayout.Card(0, 2).x + DestinationLayout.Card(1, 2).xMax) / 2, 3);
        Assert.True(DestinationLayout.Mountain.yMax < DestinationLayout.Top, "the Mountain banner overlaps the cards");
    }

    [Fact]
    public void CardsLeaveRoomForThePartyAboveItsTray()
    {
        // The party tray's title starts 40 px above the tray at y 900.
        Assert.True(DestinationLayout.Card(0, 4).yMax + 100 < 860);
    }

    [Fact]
    public void MedallionsStayInsideTheCardAndClearOfThePageArrows()
    {
        var card = DestinationLayout.Card(2, 4);
        var left = DestinationLayout.Medallion(card, 0, DestinationLayout.PerPage);
        var right = DestinationLayout.Medallion(card, DestinationLayout.PerPage - 1, DestinationLayout.PerPage);
        Assert.True(left.x >= DestinationLayout.PreviousPage(card).xMax);
        Assert.True(right.xMax <= DestinationLayout.NextPage(card).x);
        Assert.True(left.y >= DestinationLayout.PaintingOf(card).yMax);
        Assert.True(DestinationLayout.QuestLine(card).yMax <= card.yMax);
        // A single medallion sits in the middle.
        Assert.Equal(card.center.x, DestinationLayout.Medallion(card, 0, 1).center.x, 3);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 0, 0, 3)]
    [InlineData(5, 1, 3, 2)]
    [InlineData(5, 9, 3, 2)]     // past the last page: the last page
    [InlineData(5, -1, 0, 3)]
    public void QuestsArePagedThreeAtATime(int quests, int page, int first, int count)
    {
        Assert.Equal((first, count), DestinationLayout.Page(quests, page));
    }

    [Fact]
    public void PageCountNeverDropsBelowOne()
    {
        Assert.Equal(1, DestinationLayout.Pages(0));
        Assert.Equal(1, DestinationLayout.Pages(3));
        Assert.Equal(2, DestinationLayout.Pages(4));
    }

    [Fact]
    public void ATallPaintingInAWideFrameKeepsItsTop()
    {
        // 400x800 painting into a 2:1 frame: full width, the top 200 px (texture origin is the bottom).
        var crop = DestinationLayout.Cover(new Rect(0, 0, 400, 800), 392, 196, fromTop: 0);
        Assert.Equal(0, crop.x, 3);
        Assert.Equal(400, crop.width, 3);
        Assert.Equal(200, crop.height, 3);
        Assert.Equal(800, crop.yMax, 3);
    }

    [Fact]
    public void AWidePaintingInATallFrameLosesItsSidesEvenly()
    {
        var crop = DestinationLayout.Cover(new Rect(100, 50, 1000, 500), 250, 250);
        Assert.Equal(350, crop.x, 3);
        Assert.Equal(500, crop.width, 3);
        Assert.Equal(50, crop.y, 3);
        Assert.Equal(500, crop.height, 3);
    }
}
