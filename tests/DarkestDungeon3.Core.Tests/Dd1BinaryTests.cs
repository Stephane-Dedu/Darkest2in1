using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's hand-made plot maps (maps/*.dm) read with the binary-file reader.</summary>
public class Dd1BinaryTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    private static Dd1Binary Map(string name) => Dd1Binary.Load(Install.PathOf("maps", name + ".dm"));

    [Fact]
    public void ReadsTheDarkestDungeonsFirstMap()
    {
        var map = Map("DD_map1");
        Assert.Equal("base_root", map.Root.Name);
        Assert.Equal(5, map.Root["version"].Int);

        // The dynamic part: each area's tiles with their content and battle table.
        var areas = map.Root.At("map", "static_dynamic", "areas");
        var room = areas["rooA"];
        Assert.False(room["reversed"].Bool);
        var tile = room.At("tiles", "tile0");
        Assert.Equal(1, tile["content"].Int);
        Assert.Equal("dd_quest_1_mash_07", tile["mash_name"].String);
        Assert.Equal(5, areas["corA"]["tiles"].Children.Count);

        // The static part is a nested file: area kinds, doors, tile positions.
        var layout = map.Root.At("map", "static_dynamic", "static_save").Nested.Root["areas"];
        var rooA = layout["rooA"];
        var corA = layout["corA"];
        Assert.Equal(0, rooA["kind"].Int);   // room
        Assert.Equal(1, corA["kind"].Int);   // corridor
        Assert.Equal(rooA["id"].Int, corA.At("tiles", "tile0", "door_to", "area_to").Int);   // the corridor starts at room A
        Assert.Equal(corA["id"].Int, rooA.At("door0", "area_to").Int);
        Assert.Equal((28f, 1f), rooA.At("tiles", "tile0", "mappos").Vector);
        Assert.Equal(areas.Children.Select(a => a.Name), layout.Children.Select(a => a.Name));   // the same areas in both
    }

    [Theory]
    [InlineData("DD_map2")]
    [InlineData("DD_map3")]
    [InlineData("DD_map4")]
    [InlineData("town_invasion_0")]
    [InlineData("crow_map1")]
    public void ReadsEveryPlotMap(string name)
    {
        var map = Map(name);
        var areas = map.Root.At("map", "static_dynamic", "areas");
        var layout = map.Root.At("map", "static_dynamic", "static_save").Nested.Root["areas"];
        Assert.NotEmpty(areas.Children);
        Assert.Equal(areas.Children.Count, layout.Children.Count);
        Assert.Contains(layout.Children, a => a["kind"].Int == 0);                    // rooms (the crow's lair is a single room)
        Assert.All(layout.Children, a => Assert.InRange(a["kind"].Int, 0, 1));         // and corridors
    }
}
