using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd2Data;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD2's data tables (the cached copy of the game's StreamingAssets/Excel).</summary>
public class Dd2TablesTests
{
    private static readonly string Excel = File.Exists(@"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel\quirk_data_export.Group.csv")
        ? @"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets\Excel"
        : @"C:\Users\Piral\dd2-decomp\data\Excel";

    [Fact]
    public void Quirks_and_trinkets_read_with_their_kinds()
    {
        var t = Dd2Tables.Load(Excel);
        Assert.True(t.Quirks.Count > 100);
        Assert.True(t.Quirks["quirk_amateur_armorsmith_pos"].IsPositive);
        Assert.True(t.Quirks["quirk_anemic_neg"].IsNegative);
        Assert.Contains(t.Quirks.Values, q => q.IsDisease);
        Assert.True(t.Quirks.Values.Count(q => q.IsStarting && q.IsPositive) > 10);
        Assert.True(t.Trinkets.Count > 150);
        var haul = t.Trinkets["trinket_hero_jes_buskers_haul"];
        Assert.Equal("jester", haul.HeroClass);
        Assert.False(haul.IsForHero("vestal"));
        Assert.Equal("common", t.Trinkets["trinket_tiered_anchoring_charm_minor"].Rarity);
        var hwm = t.Classes["highwayman"];
        Assert.Equal(35, hwm.Get("health_max"));
        Assert.Equal(5, hwm.Get("speed"));
        Assert.Equal(0.3f, hwm.Resistances["bleed"], 3);
        Assert.True(t.Classes.Count >= 11);
    }
}
