using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SecretContentsTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CurioLibrary Curios = CurioLibrary.Load(Install);

    [Theory]
    [InlineData("DD_map4", "darkestdungeon", "thanks_chest", "unlocked_strongbox", 163014735u)]
    [InlineData("town_invasion_0", "town", "heirloom_chest", "heirloom_chest", 283272093u)]
    public void NativePropHashPreservesItsEffectsAndIndependentSprite(string mapName, string zone, string prop, string sprite, uint hash)
    {
        Assert.Equal(hash, Dd1Binary.Hash(prop));
        var map = PlotMap.Load(Install, mapName, zone, "kill_boss", 7, Dd1.Props(zone));
        var secret = Assert.Single(map.Rooms.Where(r => r.IsSecret));
        Assert.Equal(RoomContent.Treasure, secret.Content);
        Assert.Equal(prop, secret.CurioId);
        Assert.NotNull(Curios.Get(secret.CurioId));
        Assert.Equal(sprite, Curios.SpriteOf(secret.CurioId));
        Assert.False(secret.CurioTaken);
        secret.CurioTaken = true;
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = new ExpeditionState { Map = map } }.ToJson()).Expedition.Map;
        Assert.Equal(prop, loaded.Room(secret.Id).CurioId);
        Assert.True(loaded.Room(secret.Id).CurioTaken);
    }

    [Fact]
    public void ThanksChestUsesItsNativeJournalTableAndUnknownAliasesStayUnchanged()
    {
        var reward = Assert.Single(Curios.Get("thanks_chest").Outcomes);
        Assert.Equal("Loot", reward.Type);
        Assert.Equal("THANKS", Assert.Single(reward.Results).Name);
        Assert.Equal("unmapped_prop", Curios.SpriteOf("unmapped_prop"));
        Assert.Null(Curios.SpriteOf(null));
        Assert.Equal("ancestors_knapsack", Curios.SpriteOf("incursion_knapsack"));
    }

    [Fact]
    public void DirectBinaryImportWithoutPropNamesWithholdsTheUnknownChest()
    {
        var map = PlotMap.From(Dd1Binary.Load(PlotMap.PathOf(Install, "DD_map4")), "darkestdungeon", "kill_boss", 7, Dd1.Props("darkestdungeon"));
        Assert.Null(Assert.Single(map.Rooms.Where(r => r.IsSecret)).CurioId);
    }
}
