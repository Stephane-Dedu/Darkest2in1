using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's additive slots (building lights) brighten what is drawn instead of painting white shapes.</summary>
public class SpineAdditiveTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void HoveredBuildingIsItsOutlineBehindTheBuilding()
    {
        // DD1's town skeletons: "active" (a flat silhouette a few pixels larger) comes before "idle" in slot order.
        string dir = Install.PathOf("fx", "town_nomad_wagon_level01");
        var skel = SpineSkeleton.Load(Path.Combine(dir, "town_nomad_wagon_level01.sprite.skel"));
        var names = skel.Slots.Select(s => s.Name).ToList();
        Assert.True(names.IndexOf("active") < names.IndexOf("idle"));                // drawn behind the building
        Assert.True(Campaign.Town.TownLayout.HoverSlot("idle"));                       // the hover keeps the building
        Assert.True(Campaign.Town.TownLayout.HoverSlot("active"));
        Assert.False(Campaign.Town.TownLayout.HoverSlot("smoke_1"));
        Assert.False(Campaign.Town.TownLayout.IdleSlot("active"));
        Assert.True(Campaign.Town.TownLayout.IdleSlot("light"));
    }

    [Fact]
    public void AdditivePiecesBrightenWithoutAddingCoverage()
    {
        // A grey 1x1 page and a white one; the white quad overlaps the right half of the grey one and sticks out.
        var grey = new RgbaImage(4, 4, Enumerable.Repeat(new byte[] { 100, 100, 100, 255 }, 16).SelectMany(b => b).ToArray());
        var white = new RgbaImage(4, 4, Enumerable.Repeat(new byte[] { 255, 255, 255, 128 }, 16).SelectMany(b => b).ToArray());
        var greyPage = new SpineAtlas.Page { File = "grey", Width = 4, Height = 4 };
        var whitePage = new SpineAtlas.Page { File = "white", Width = 4, Height = 4 };
        SpineSkeleton.Piece Quad(SpineAtlas.Page page, float x0, float x1, bool additive) => new()
        {
            Slot = additive ? "light" : "idle", Page = page, Additive = additive, Color = 0xFFFFFFFF,
            Positions = new[] { x0, 0f, x1, 0f, x1, 2f, x0, 2f },
            PagePixels = new[] { 0f, 4f, 4f, 4f, 4f, 0f, 0f, 0f },
            Triangles = new[] { 0, 1, 2, 2, 3, 0 },
        };
        var pieces = new[] { Quad(greyPage, 0, 4, false), Quad(whitePage, 2, 6, true) };
        var result = SpineRaster.Render(pieces, p => p.File == "grey" ? grey : white, pixelsPerUnit: 4, pad: 0);
        var img = result.Image;
        byte[] At(int x, int y) => img.Pixels.Skip((y * img.Width + x) * 4).Take(4).ToArray();
        int mid = img.Height / 2;
        Assert.Equal(100, At(2, mid)[0]);                // grey alone: unchanged
        Assert.True(At(12, mid)[0] > 150);               // grey under the light: brighter
        Assert.Equal(255, At(12, mid)[3]);               // still opaque
        Assert.Equal(0, At(20, mid)[3]);                 // light over nothing: no white shape
    }
}
