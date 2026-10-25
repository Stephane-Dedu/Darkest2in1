using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class TrinketShopTests
{
    [Fact]
    public void BuyingAnEarlierItemDoesNotChangeTheItemBeingRead()
    {
        var stock = new List<string> { "ash", "book", "belt" };
        var selection = new TrinketSelection();
        selection.Reconcile(stock);
        selection.Select("belt", stock);
        stock.RemoveAt(0);
        selection.Reconcile(stock);
        Assert.Equal("belt", selection.Id);
        selection.Select("not in stock", stock);
        Assert.Equal("belt", selection.Id);
    }

    [Fact]
    public void DuplicatePurchasesKeepTheDescriptionUntilTheLastCopyIsGone()
    {
        var stock = new List<string> { "book", "book", "belt" };
        var selection = new TrinketSelection();
        selection.Reconcile(stock);
        stock.Remove("book");
        selection.Reconcile(stock);
        Assert.Equal("book", selection.Id);
        stock.Remove("book");
        selection.Reconcile(stock);
        Assert.Equal("belt", selection.Id);
        stock.Clear();
        selection.Reconcile(stock);
        Assert.Null(selection.Id);
        stock.Add("ash");
        selection.Reconcile(stock);
        Assert.Equal("ash", selection.Id);
    }

    [Fact]
    public void StockPricesReadingAreaAndPurchaseDoNotOverlap()
    {
        var area = new Rect(740, 202, 759, 676);
        var layout = new TrinketShopLayout(area);
        Assert.True(layout.Description.yMax < layout.Status.y);
        Assert.True(layout.Status.yMax < layout.Purchase.y);
        Assert.True(layout.Purchase.yMax < area.yMax);
        for (int i = 0; i < TrinketShopLayout.Capacity; i++)
        {
            var cell = layout.Cell(i);
            Assert.InRange(cell.x, area.x, layout.Details.x - cell.width);
            Assert.InRange(cell.y, layout.Header.yMax, area.yMax - cell.height);
            Assert.True(cell.yMax < layout.Price(i).y);
            Assert.True(layout.Price(i).yMax < area.yMax - 54);
            for (int j = 0; j < i; j++)
            {
                var other = layout.Cell(j);
                Assert.True(cell.xMax <= other.x || other.xMax <= cell.x || cell.yMax <= other.y || other.yMax <= cell.y);
            }
        }
    }

    [Fact]
    public void TriggerAndEffectWordingStayInOrderWithoutInventingPositiveNegativeLabels()
    {
        var lines = ItemEffectText.Lines("Turn Start: Strength (15%)\r\nGain On Miss: Weak (20%)\n-10% Move RES").ToArray();
        Assert.Equal(new[] { "Turn Start:", "Strength (15%)", "Gain On Miss:", "Weak (20%)", "-10% Move RES" }, lines.Select(l => l.Text));
        Assert.True(lines[0].Condition);
        Assert.True(lines[1].Indent);
        Assert.False(lines[4].Condition);
        Assert.False(lines[4].Indent);
        Assert.Equal("Ratio 1:2", Assert.Single(ItemEffectText.Lines("Ratio 1:2")).Text);
        Assert.Empty(ItemEffectText.Lines(null));
    }
}
