using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using Newtonsoft.Json;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TrinketEquipmentTests
{
    private readonly Estate E = new();
    private readonly FakeCatalog C = new();
    private HeroRecord Hero(string id, string cls = "highwayman", params string[] worn)
    {
        var h = new HeroRecord { Id = id, ClassId = cls, Trinkets = worn.ToList() };
        E.Roster.Add(h); return h;
    }
    private bool Move(string id, HeroRecord source, HeroRecord target, int slot, int fromSlot = -1) =>
        TrinketEquipment.Transfer(E, C, id, source?.Id, fromSlot, target, slot);

    [Fact]
    public void DuplicateRefusalDoesNotConsumeTheStashAndUnlimitedItemsStillFit()
    {
        var h = Hero("a", "highwayman", "ring"); E.Trinkets.Add("ring");
        Assert.False(Move("ring", null, h, 1)); Assert.Equal(new[] { "ring" }, E.Trinkets); Assert.Single(h.WornTrinkets);
        h.Trinkets[0] = "unlimited"; E.Trinkets[0] = "unlimited";
        Assert.True(Move("unlimited", null, h, 1)); Assert.Equal(2, h.WornTrinkets.Count());
    }
    [Fact]
    public void StaleSourceAndRestrictedSwapLeaveBothHeroesAndStashUnchanged()
    {
        var a = Hero("a", "jester", "jester_only"); var b = Hero("b", "highwayman", "ring");
        Assert.False(Move("missing", b, a, 0));
        Assert.False(Move("ring", b, a, 0)); // Jester's displaced item cannot move to highwayman.
        Assert.Equal("jester_only", a.TrinketAt(0)); Assert.Equal("ring", b.TrinketAt(0)); Assert.Empty(E.Trinkets);
    }
    [Fact]
    public void SwapReturnsTheDisplacedItemToItsSourceAndMovingWithinAHeroKeepsSlots()
    {
        var a = Hero("a", "highwayman", "ring", "charm"); var b = Hero("b", "highwayman", "belt");
        Assert.True(Move("ring", a, b, 0)); Assert.Equal("belt", a.TrinketAt(0)); Assert.Equal("ring", b.TrinketAt(0));
        Assert.True(Move("belt", a, a, 1)); Assert.Equal(new[] { "charm", "belt" }, a.Trinkets);
        E.Trinkets.Add("scroll"); Assert.True(Move("scroll", null, a, 1)); Assert.Equal(new[] { "belt" }, E.Trinkets);
    }
    [Fact]
    public void RightOnlyEquipmentSurvivesSerializationAndUnequippingTheLeftDoesNotShiftIt()
    {
        var h = Hero("a", "highwayman", "ring", "charm");
        Assert.True(TrinketEquipment.Unequip(E, h, 0)); Assert.Null(h.TrinketAt(0)); Assert.Equal("charm", h.TrinketAt(1));
        var restored = JsonConvert.DeserializeObject<HeroRecord>(JsonConvert.SerializeObject(h));
        Assert.Null(restored.TrinketAt(0)); Assert.Equal("charm", restored.TrinketAt(1)); Assert.Single(restored.WornTrinkets);
        Assert.True(TrinketEquipment.Unequip(E, h, 1)); Assert.Empty(h.Trinkets); Assert.Equal(new[] { "ring", "charm" }, E.Trinkets);
    }
    [Fact]
    public void MissingBusyDeadAndUnrecruitedHeroesCannotTransferEquipment()
    {
        var h = Hero("a"); E.Trinkets.Add("ring");
        h.Activity = "abbey.prayer"; Assert.False(Move("ring", null, h, 0));
        h.Activity = null; h.MissingWeeks = 1; Assert.False(Move("ring", null, h, 0));
        h.MissingWeeks = 0; h.IsDead = true; Assert.False(Move("ring", null, h, 0));
        h.IsDead = false; E.Roster.Clear(); Assert.False(Move("ring", null, h, 0)); Assert.Single(E.Trinkets);
    }

    [Fact]
    public void HomecomingPreservesARightOnlyTrinketFromDd2sCompactInventory()
    {
        var h = Hero("a"); h.SetTrinket(1, "charm");
        var expedition = new Core.Expedition.ExpeditionState { Quest = new QuestOffer { Dungeon = "dd2_city", Difficulty = 1, Length = 1 } };
        Homecoming.Apply(E, Dd1Campaign.Load(Core.Dd1.Dd1Install.Find()), expedition,
            new[] { new HeroOutcome { HeroId = h.Id, Trinkets = new() { "charm" } } });
        Assert.Null(h.TrinketAt(0)); Assert.Equal("charm", h.TrinketAt(1));
        Assert.Empty(E.Trinkets);
    }

    [Fact]
    public void ReturnedItemCountsAccountForLossesDuplicatesAndNewItems()
    {
        Assert.Equal(new string[] { null, "charm" }, TrinketEquipment.RestoreSlots(new[] { "ring", "charm" }, new[] { "charm" }));
        Assert.Equal(new[] { "belt", "charm" }, TrinketEquipment.RestoreSlots(new[] { "ring", "charm" }, new[] { "charm", "belt" }));
        Assert.Equal(new[] { "ring" }, TrinketEquipment.RestoreSlots(new[] { "ring", "ring" }, new[] { "ring" }));
        Assert.Equal(new[] { "ring", "ring" }, TrinketEquipment.RestoreSlots(new[] { "ring" }, new[] { "ring", "ring" }));
        Assert.Empty(TrinketEquipment.RestoreSlots(new string[] { null, "charm" }, new string[0]));
    }
}
