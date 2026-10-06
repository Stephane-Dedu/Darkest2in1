using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class LootGridTests : IDisposable
{
    private static readonly CrawlContent Content = CrawlContent.Load(Dd1Install.Find());
    private readonly LootGridUi _grid = new();

    private static void Send(EventType type, float x = 0, float y = 0)
    {
        ItemArt.Cards.Clear();
        Event.current = new Event { type = type, rawType = type, mousePosition = new Vector2(x, y) };
        GUI.enabled = true;
    }

    public void Dispose() { ItemArt.Cards.Clear(); Gui.Tips.Clear(); Gui.Texts.Clear(); GUI.enabled = true; }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuestLootBeyondTheOriginalCardCapCanBeReachedAndCollected(bool curio)
    {
        var questDrop = new LootDrop { Type = "quest_item", Id = "holy_relic", Amount = 1 };
        var spoils = new BattleSpoils { LeftBehind = { questDrop } };
        var report = new CurioReport { LeftBehind = { questDrop } };
        int cap = curio ? 5 : 10;
        for (int i = 0; i < cap; i++)
        {
            var collected = new LootDrop { Type = "gold", Amount = 1 };
            spoils.Taken.Add(collected);
            report.Loot.Add(collected);
        }
        report.Loot.Add(questDrop);
        var area = new Rect(0, 0, 456, curio ? 144 : 294);
        int Draw() => curio ? _grid.DrawCurio(report, area, Content.Items) : _grid.DrawBattle(spoils, area, Content.Items);
        Send(EventType.Repaint); Draw();
        Assert.False(Crawl.CanLeave(curio ? report.LeftBehind : spoils.LeftBehind));
        Send(EventType.MouseUp, 442, area.height / 2); Draw(); // next page, at the right edge of the card area
        Send(EventType.Repaint); Draw();
        Assert.Contains(ItemArt.Cards, c => c.Key == questDrop.Key && c.Dim);
        Send(EventType.MouseUp, 228, 70);
        int waiting = Draw();
        Assert.Equal(0, waiting);
        var state = new ExpeditionState();
        var crawl = new Crawl(state, new CrawlRules(), null, Content);
        var left = curio ? report.LeftBehind : spoils.LeftBehind;
        Assert.True(crawl.TakeLeftBehind(left, waiting, curio ? null : spoils.Taken));
        Assert.True(Crawl.CanLeave(left));
        Assert.Equal(1, state.Pack.Count(questDrop.Key));
    }

    [Fact]
    public void PartialPickupOnLaterPageRetainsClickableRemainderAfterTakenListGrows()
    {
        var spoils = new BattleSpoils();
        for (int i = 0; i < 10; i++) spoils.Taken.Add(new LootDrop { Type = "gold", Amount = 1 });
        var gold = new LootDrop { Type = "gold", Amount = 200 };
        var quest = new LootDrop { Type = "quest_item", Id = "holy_relic", Amount = 1 };
        spoils.LeftBehind.AddRange(new[] { gold, quest });
        var state = new ExpeditionState { RandomCounter = 31 };
        int limit = Content.Items.StackLimit(gold.Key);
        state.Pack.Add(gold.Key, limit - 50);
        for (int i = 0; i < Inventory.Slots - 1; i++) state.Pack.Add("filler" + i, 1);
        var crawl = new Crawl(state, new CrawlRules(), null, Content);
        var area = new Rect(0, 0, 456, 294);
        int Draw() => _grid.DrawBattle(spoils, area, Content.Items);
        Send(EventType.Repaint); Draw();
        Send(EventType.MouseUp, 442, 147); Assert.Equal(-1, Draw());
        Assert.Equal(EventType.Used, Event.current.type);
        Send(EventType.MouseUp, 188, 70);
        Assert.True(crawl.TakeLeftBehind(spoils.LeftBehind, Draw(), spoils.Taken));
        Assert.Equal(150, gold.Amount);
        Send(EventType.Repaint); Draw();
        Assert.Contains(ItemArt.Cards, c => c.Amount == 150 && c.Dim);
        Send(EventType.MouseUp, 228, 70);
        int waiting = Draw();
        Assert.Equal(0, waiting);
        Assert.False(crawl.TakeLeftBehind(spoils.LeftBehind, waiting, spoils.Taken));
        Assert.True(crawl.Discard("filler0"));
        Assert.True(crawl.TakeLeftBehind(spoils.LeftBehind, waiting, spoils.Taken));
        Assert.Same(quest, Assert.Single(spoils.LeftBehind));
        Assert.False(Crawl.CanLeave(spoils.LeftBehind));
        Assert.True(crawl.Discard("filler1"));
        Send(EventType.MouseUp, 308, 70);
        Assert.True(crawl.TakeLeftBehind(spoils.LeftBehind, Draw(), spoils.Taken));
        Assert.True(Crawl.CanLeave(spoils.LeftBehind));
        Assert.Equal(limit + 150, state.Pack.Count(gold.Key));
        Assert.Equal(1, state.Pack.Count(quest.Key));
        Assert.Equal(31, state.RandomCounter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurioPageClicksMapToWaitingListNotDisplayIndexIncludingAfterReload(bool reload)
    {
        var report = new CurioReport();
        for (int i = 0; i < 12; i++) report.Loot.Add(new LootDrop { Type = "gold", Amount = i + 1 });
        report.LeftBehind.Add(report.Loot[8]);
        report.LeftBehind.Add(report.Loot[11]);
        if (reload)
        {
            var state = SaveFile.FromJson(new SaveFile { Expedition = new ExpeditionState { PendingCurio = report } }.ToJson()).Expedition;
            var crawl = new Crawl(state, new CrawlRules(), null, Content);
            report = crawl.LastCurio;
        }
        var area = new Rect(0, 0, 456, 144);
        int Draw() => _grid.DrawCurio(report, area, Content.Items);
        Send(EventType.Repaint); Draw();
        Send(EventType.MouseUp, 442, 72); Draw();
        Send(EventType.MouseUp, 308, 70); // display index 8 is waiting index 0
        Assert.Equal(0, Draw());
        Send(EventType.MouseUp, 442, 72); Draw();
        Send(EventType.MouseUp, 268, 70); // display index 11 is waiting index 1
        Assert.Equal(1, Draw());
        Send(EventType.MouseUp, 188, 70); // collected display index 10
        Assert.Equal(-1, Draw());
    }

    [Fact]
    public void PagesClampAfterRemovalAndResetForANewReportWithoutChangingReportData()
    {
        var spoils = new BattleSpoils();
        for (int i = 0; i < 21; i++) spoils.LeftBehind.Add(new LootDrop { Type = "gold", Amount = i + 1 });
        var area = new Rect(0, 0, 456, 294);
        int Draw(BattleSpoils report) => _grid.DrawBattle(report, area, Content.Items);
        string before = new SaveFile { Expedition = new ExpeditionState { PendingSpoils = spoils } }.ToJson();
        Send(EventType.Repaint); Draw(spoils);
        Send(EventType.MouseUp, 442, 147); Draw(spoils);
        Send(EventType.MouseUp, 442, 147); Draw(spoils);
        Send(EventType.Repaint); Draw(spoils);
        Assert.Equal(21, Assert.Single(ItemArt.Cards).Amount);
        Assert.Equal(before, new SaveFile { Expedition = new ExpeditionState { PendingSpoils = spoils } }.ToJson());
        spoils.LeftBehind.RemoveAt(20);
        Send(EventType.Repaint); Draw(spoils);
        Assert.Equal(11, ItemArt.Cards[0].Amount); // former page 3 clamps to page 2
        Send(EventType.MouseUp, 14, 147); Draw(spoils);
        Send(EventType.Repaint); Draw(spoils);
        Assert.Equal(1, ItemArt.Cards[0].Amount);
        Send(EventType.MouseUp, 442, 147); Draw(spoils);
        var next = new BattleSpoils();
        next.LeftBehind.AddRange(spoils.LeftBehind);
        Send(EventType.Repaint); Draw(next);
        Assert.Equal(1, ItemArt.Cards[0].Amount);
    }

    [Fact]
    public void DisabledPageCannotNavigateOrPickUpAndSmallLootKeepsItsOriginalPosition()
    {
        var spoils = new BattleSpoils();
        for (int i = 0; i < 11; i++) spoils.LeftBehind.Add(new LootDrop { Type = "gold", Amount = i + 1 });
        var area = new Rect(0, 0, 456, 294);
        Send(EventType.Repaint); _grid.DrawBattle(spoils, area, Content.Items);
        Send(EventType.MouseUp, 442, 147); GUI.enabled = false;
        Assert.Equal(-1, _grid.DrawBattle(spoils, area, Content.Items));
        Send(EventType.Repaint); _grid.DrawBattle(spoils, area, Content.Items);
        Assert.Equal(1, ItemArt.Cards[0].Amount);
        var small = new BattleSpoils { LeftBehind = { new LootDrop { Type = "gold", Amount = 25 } } };
        Send(EventType.Repaint); _grid.DrawBattle(small, area, Content.Items);
        var card = Assert.Single(ItemArt.Cards);
        Assert.Equal(192, card.Rect.x); Assert.Equal(0, card.Rect.y);
        Send(EventType.MouseUp, 228, 70); GUI.enabled = false;
        Assert.Equal(-1, _grid.DrawBattle(small, area, Content.Items));
    }
}
