using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Everything the crawl and the fights draw from DD1 is where the plugin looks for it.</summary>
public class Dd1AssetTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private readonly ITestOutputHelper _out;
    public Dd1AssetTests(ITestOutputHelper output) => _out = output;

    private static readonly string[] Zones = { "crypts", "weald", "warrens", "cove", "darkestdungeon" };

    private static bool HasSkel(string dir) => Directory.Exists(dir) && Directory.GetFiles(dir, "*.skel").Length > 0;

    [Fact]
    public void EveryPropInTheZonesHasItsArt()
    {
        var missing = new SortedSet<string>();
        foreach (var zone in Zones)
        {
            var props = Dd1.Props(zone);
            foreach (var (table, kind) in new[] { (ZoneProps.HallCurios, "curios"), (ZoneProps.RoomCurios, "curios"), (ZoneProps.RoomTreasures, "curios"),
                                                   (ZoneProps.Traps, "traps"), (ZoneProps.Obstacles, "obstacles") })
                foreach (var (_, id) in props.Table(table))
                    if (!HasSkel(Install.PathOf("props", "shared", kind, id))) missing.Add($"{zone}/{kind}/{id}");
        }
        _out.WriteLine(string.Join("\n", missing));
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryZoneHasItsScenery()
    {
        var arts = Zones.Where(z => z != "darkestdungeon").Select(z => (z, ZoneArt.Load(Install, z)))
            .Concat(Enumerable.Range(1, 4).Select(q => ($"darkestdungeon q{q}", ZoneArt.Load(Install, "darkestdungeon", q))));
        foreach (var (zone, art) in arts)
        {
            Assert.True(art.Walls.Count >= 1, $"{zone}: {art.Walls.Count} walls");
            Assert.All(new[] { art.Door, art.EndHall, art.Background, art.Mid, art.ForegroundTop, art.ForegroundBottom }, f => Assert.True(f != null && File.Exists(f), zone));
            Assert.All(Enumerable.Range(0, 40), i => Assert.True(File.Exists(art.Wall(i)), $"{zone} wall {i}"));
            _out.WriteLine($"{zone}: {art.Walls.Count} walls, {art.Rooms.Count} rooms, entrance {(art.Entrance != null ? "yes" : "no")}");
        }
    }

    [Fact]
    public void EveryStandInMonsterHasDd1Animations()
    {
        var json = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "monsters.json"));
        var families = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(json))["monsters"].Children<Newtonsoft.Json.Linq.JProperty>().Select(p => p.Name);
        var problems = new List<string>();
        var bestiary = Dd1Bestiary.Load(json);
        foreach (var name in families)
        {
            string family = bestiary.ArtFamily(name);
            string anim = Install.PathOf("monsters", family, "anim");
            foreach (var a in new[] { "combat", "defend" })
                if (!File.Exists(Path.Combine(anim, $"{family}.sprite.{a}.skel"))) problems.Add($"{family}: no {a}");
            string art = Directory.Exists(Install.PathOf("monsters", family))
                ? Directory.GetFiles(Install.PathOf("monsters", family), "*.art.darkest", SearchOption.AllDirectories).OrderBy(f => f).FirstOrDefault() ?? "" : "";
            if (!File.Exists(art)) problems.Add($"{family}: no art file");
            else
                foreach (var r in DarkestFile.Load(art).Where(r => r.Type == "skill"))
                {
                    string a = r.Str("anim", null);
                    if (a != null && !File.Exists(Path.Combine(anim, $"{family}.sprite.{a}.skel"))) problems.Add($"{family}: skill anim {a} missing");
                }
        }
        _out.WriteLine(string.Join("\n", problems));
        Assert.Empty(problems);
    }
}

public class CurioItemTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    /// <summary>Every item a curio reacts to is an item the pack knows by that name (a key on a chest works).</summary>
    [Fact]
    public void CurioItemsAreRealItems()
    {
        var unknown = new SortedSet<string>();
        foreach (var zone in new[] { "crypts", "weald", "warrens", "cove", "darkestdungeon" })
        {
            var props = Dd1.Props(zone);
            foreach (var table in new[] { ZoneProps.HallCurios, ZoneProps.RoomCurios, ZoneProps.RoomTreasures })
                foreach (var (_, curio) in props.Table(table))
                    foreach (var item in Content.Curios.UsefulItems(curio))
                        if (Content.Items.Get(item) == null) unknown.Add($"{curio}: {item}");
        }
        Assert.True(unknown.Count == 0, string.Join(" | ", unknown));
    }
}
