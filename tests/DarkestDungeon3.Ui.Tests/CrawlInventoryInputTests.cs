using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CrawlInventoryInputTests : IDisposable
{
    private static readonly Rect Item = new(980, 748, 72, 144);
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);
    private sealed class Party : IParty
    {
        public IReadOnlyList<string> Alive => new[] { "hero" };
        public float HpFraction(string id) => 1;
        public void Damage(string id, float hp, string cause) { }
        public void Heal(string id, float hp) { }
        public void AddStress(string id, int points, string cause) { }
        public string AddDd1Quirk(string id, string quirk) => quirk;
        public string PurgeNegative(string id) => null;
        public string CureDisease(string id) => null;
    }
    private static Crawl Ready(bool hall = false)
    {
        var state = new ExpeditionState
        {
            Started = true, Seed = 42, Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 },
            Party = { "hero" }, RoomId = hall ? -1 : 0, CorridorId = hall ? 0 : -1, TileIndex = hall ? 0 : -1,
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, Content = RoomContent.Curio, CurioId = "heirloom_chest" } },
                Corridors = { new Corridor { Id = 0, Tiles = { new HallTile { Index = 0, Content = HallContent.Curio, ContentId = "heirloom_chest" } } } }
            }
        };
        state.Pack.Add(Supply.Key, 1);
        return new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new Party(), Content);
    }
    private static void Click(EventType type = EventType.MouseDown, int button = 1)
    {
        Time.frameCount++;
        Event.current = new Event { type = type, rawType = type, button = button, mousePosition = Item.center };
        Drag.Begin();
    }
    public void Dispose() { Drag.Cancel(); GUI.enabled = true; }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RightClickUsesKeyOnOpenCurioOnceAndPreservesItsSavedReport(bool hall)
    {
        var crawl = Ready(hall);
        string panel = CrawlInventoryInput.CurioPanelKey(crawl);
        int calls = 0;
        Click();
        Assert.True(CrawlInventoryInput.RightClick(Item, crawl, panel, curio =>
        {
            calls++;
            Assert.Equal("heirloom_chest", curio);
            Assert.Equal(Supply.Key, crawl.InteractCurio("hero", Supply.Key, out _).ItemUsed);
        }, () => Assert.Fail("Must not use this on the hero")));
        Assert.Equal(0, crawl.State.Pack.Count(Supply.Key));
        Assert.NotNull(crawl.LastCurio);
        Assert.False(CrawlInventoryInput.RightClick(Item, crawl, panel, _ => calls++, () => calls++));
        Assert.Equal(1, calls);
        var saved = SaveFile.FromJson(new SaveFile { Expedition = crawl.State }.ToJson()).Expedition;
        Assert.Equal(Supply.Key, saved.PendingCurio.ItemUsed);
        Assert.Equal(0, saved.Pack.Count(Supply.Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("r9:heirloom_chest")]
    public void ClosedOrStaleCurioPanelRoutesToTheHero(string panel)
    {
        var crawl = Ready(); int heroUses = 0;
        Click();
        Assert.True(CrawlInventoryInput.RightClick(Item, crawl, panel,
            _ => Assert.Fail("Closed or different curio"), () => heroUses++));
        Assert.Equal(1, heroUses);
        Assert.Equal(1, crawl.State.Pack.Count(Supply.Key));
        Assert.Null(crawl.LastCurio);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("drag")]
    [InlineData("loot")]
    [InlineData("camp")]
    [InlineData("outside")]
    [InlineData("left")]
    [InlineData("release")]
    public void BlockedOrUnrelatedInputCannotUseAnything(string reason)
    {
        var crawl = Ready();
        string panel = CrawlInventoryInput.CurioPanelKey(crawl);
        if (reason == "loot") crawl.State.PendingSpoils = new BattleSpoils();
        if (reason == "camp") crawl.State.Camp = new CampState();
        Click(reason == "release" ? EventType.MouseUp : EventType.MouseDown, reason == "left" ? 0 : 1);
        if (reason == "disabled") GUI.enabled = false;
        if (reason == "outside") Event.current.mousePosition = Vector2.zero;
        if (reason == "drag")
        {
            Click(EventType.MouseDown, 0); Drag.Source(Item, "held", _ => { });
            Event.current = new Event { type = EventType.MouseDrag, rawType = EventType.MouseDrag, mousePosition = new Vector2(50,50) };
            Drag.Begin(); Click();
        }
        Assert.False(CrawlInventoryInput.RightClick(Item, crawl, panel,
            _ => Assert.Fail("Unexpected curio use"), () => Assert.Fail("Unexpected hero use")));
        Assert.Equal(1, crawl.State.Pack.Count(Supply.Key));
    }
}
