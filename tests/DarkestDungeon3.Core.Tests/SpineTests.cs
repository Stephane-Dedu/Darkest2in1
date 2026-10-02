using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Spine 2.1 setup poses from the user's DD1 install (town buildings, curios, heroes).</summary>
public class SpineTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    private static (SpineSkeleton, SpineAtlas) Load(params string[] dir)
    {
        string folder = Install.PathOf(dir);
        string name = Path.GetFileName(folder);
        var skel = SpineSkeleton.Load(Path.Combine(folder, name + ".sprite.skel"));
        var atlas = SpineAtlas.Parse(File.ReadAllText(Path.Combine(folder, name + ".sprite.atlas")));
        return (skel, atlas);
    }

    [Fact]
    public void Tavern_setup_pose_is_the_idle_building_standing_on_its_origin()
    {
        var (skel, atlas) = Load("fx", "town_tavern_level01");
        Assert.Equal("2.1.27", skel.Version);
        Assert.Contains(skel.Slots, s => s.Name == "idle");
        Assert.True(atlas.Regions["idle"].Rotate);

        var pieces = skel.SetupPose(atlas, s => s.Name == "idle");
        var piece = Assert.Single(pieces);
        var (minX, minY, maxX, maxY) = SpineSkeleton.Bounds(pieces);
        // The picture is drawn at half its atlas size, centred on the origin, feet on the ground.
        Assert.InRange(maxX - minX, 380, 390);
        Assert.InRange(maxY - minY, 398, 408);
        Assert.InRange((minX + maxX) / 2, -10, 10);
        Assert.InRange(minY, -10, 10);
        Assert.Equal(6, piece.Triangles.Length);
    }

    [Fact]
    public void Every_town_building_and_curio_reads()
    {
        var folders = Directory.GetDirectories(Install.PathOf("fx"), "town_*")
            .Concat(Directory.GetDirectories(Install.PathOf("props", "shared", "curios")))
            .Where(d => Dd1Install.SpineIn(d) != null)
            .ToList();
        Assert.True(folders.Count > 100, $"only {folders.Count} animations found");
        foreach (var dir in folders)
        {
            var (skelPath, atlasPath) = Dd1Install.SpineIn(dir).Value;
            var skel = SpineSkeleton.Load(skelPath);
            var atlas = SpineAtlas.Parse(File.ReadAllText(atlasPath));
            var pieces = skel.SetupPose(atlas);
            Assert.True(pieces.Count > 0, Path.GetFileName(dir) + " has no visible pieces");
            Assert.All(pieces, p => Assert.Equal(p.Positions.Length, p.PagePixels.Length));
        }
    }
}

public class SpineRasterTests
{
    private static readonly byte[] Red = { 255, 0, 0, 255 }, Blue = { 0, 0, 255, 255 };

    private static RgbaImage Page(int w, int h, System.Func<int, int, byte[]> colour)
    {
        var img = new RgbaImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                System.Array.Copy(colour(x, y), 0, img.Pixels, (y * w + x) * 4, 4);
        return img;
    }

    private static byte[] At(RgbaImage img, int x, int y) => img.Pixels.Skip((y * img.Width + x) * 4).Take(4).ToArray();

    private static SpineRaster.Result Draw(SpineAtlas.Region region, RgbaImage page)
    {
        // A 4x2 picture (red left half, blue right half) centred on the origin, drawn at 8 pixels per unit.
        var a = new SpineSkeleton.Attachment { Type = SpineSkeleton.AttachmentType.Region, ScaleX = 1, ScaleY = 1, Width = 4, Height = 2 };
        var quad = SpineSkeleton.RegionQuad(a, region, SpineSkeleton.IdentityBone());
        return SpineRaster.Render(new[] { quad }, _ => page, pixelsPerUnit: 8, pad: 0);
    }

    [Fact]
    public void Plain_region_draws_upright_around_its_pivot()
    {
        var pagedef = new SpineAtlas.Page { Width = 4, Height = 2 };
        var region = new SpineAtlas.Region { Page = pagedef, Width = 4, Height = 2, OrigWidth = 4, OrigHeight = 2 };
        var r = Draw(region, Page(4, 2, (x, y) => x < 2 ? Red : Blue));
        Assert.Equal(32, r.Image.Width);
        Assert.Equal(16, r.Image.Height);
        Assert.Equal(16, r.PivotX, 3);
        Assert.Equal(8, r.PivotY, 3);
        Assert.Equal(Red, At(r.Image, 4, 8));
        Assert.Equal(Blue, At(r.Image, 28, 8));
    }

    [Fact]
    public void Rotated_region_is_turned_back_upright()
    {
        // The atlas stores the 4x2 picture turned 90 degrees counter-clockwise as a 2x4 block: its left (red)
        // half ends up at the bottom of the block.
        var pagedef = new SpineAtlas.Page { Width = 2, Height = 4 };
        var region = new SpineAtlas.Region { Page = pagedef, Rotate = true, Width = 4, Height = 2, OrigWidth = 4, OrigHeight = 2 };
        var r = Draw(region, Page(2, 4, (x, y) => y >= 2 ? Red : Blue));
        Assert.Equal(Red, At(r.Image, 4, 8));
        Assert.Equal(Blue, At(r.Image, 28, 8));
    }
}

public class TownLayoutTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void Buildings_stand_where_dd1_puts_them_back_to_front()
    {
        var layout = DarkestDungeon3.Core.Campaign.Town.TownLayout.Load(Install);
        var tavern = layout["tavern"];
        Assert.Equal(550, tavern.X);
        Assert.Equal(852, tavern.Y);
        Assert.Equal(1082, layout.Ground.Y);
        Assert.Equal("ground", layout.Spots[0].Id);           // farthest, drawn first
        Assert.True(layout.Spots.IndexOf(layout["abbey"]) < layout.Spots.IndexOf(tavern));
        Assert.Equal(-75, layout["blacksmith"].NameOffsetY);
        foreach (var id in new[] { "stage_coach", "abbey", "tavern", "sanitarium", "guild", "blacksmith", "nomad_wagon", "camping_trainer", "graveyard", "statue" })
        {
            Assert.NotNull(layout[id]);
            string folder = DarkestDungeon3.Core.Campaign.Town.TownLayout.ArtFolder(Install, id, open: true, 0.5f);
            Assert.True(Dd1Install.SpineIn(folder) != null, id + " art missing: " + folder);
        }
    }
}
