using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Dd1ColourTests
{
    [Fact]
    public void EquipmentTooltipColoursResolveActualInstalledAliases()
    {
        var colours = Dd1Colours.Load(Dd1Install.Find().PathOf("colours", "base.colours.darkest"));
        Assert.Equal(0xC8B46EFFu, colours.Rgba("equipment_tooltip_title"));
        Assert.Equal(0xAEACA2FFu, colours.Rgba("equipment_tooltip_body"));
        Assert.Equal(0x3B61A7FFu, colours.Rgba("rare"));
        Assert.Equal(0xE02700FFu, colours.Rgba("ancestral"));
    }

    [Fact]
    public void HexAndNumericColoursAreEquivalentAndBadAliasesFallBack()
    {
        var colours = Dd1Colours.Parse("colour: .id hex .rgba #C8B46E\ncolour: .id numeric .rgba 200 180 110 255\ncolour: .id alias .shared_id numeric\ncolour: .id cycle .shared_id cycle\ncolour: .id missing .shared_id absent");
        Assert.Equal(colours.Rgba("hex"), colours.Rgba("alias"));
        Assert.Null(colours.Rgba("cycle")); Assert.Null(colours.Rgba("missing"));
    }
}
