using System;
using System.IO;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class NativeCurioCatalogTests
{
    private static JObject Manifest()
    {
        var mask = new byte[128];
        // A tiny painted island at the centre. The canvas and interior holes
        // remain transparent, including the common foot padding.
        int bit = 20 * 32 + 16; mask[bit / 8] = (byte)(1 << (bit % 8));
        return JObject.FromObject(new { Version = 2,
            Artifacts = new[] { new { Id = "coffer", Width = 200f, PixelWidth = 128, PixelHeight = 128,
                Padding = 16, MaskCellSize = 4, MaskWidth = 32, MaskHeight = 32, Mask = mask,
                ClosedSha256 = new string('a',64), UsedSha256 = new string('b',64) } },
            Entries = new[] { new { Id = "unlocked_strongbox", ArtId = "coffer" }, new { Id = "thanks_chest", ArtId = "coffer" } } });
    }

    [Fact]
    public void ExactIdentitiesCanSharePaintWithoutInventingFamilyFallbacks()
    {
        var catalog = NativeCurioCatalog.Parse(Manifest().ToString());
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Same(catalog.Get("unlocked_strongbox").Art, catalog.Get("thanks_chest").Art);
        Assert.Null(catalog.Get("giant_oyster"));
        Assert.Null(catalog.Get("../unlocked_strongbox"));
        Assert.Null(catalog.Get("faded_memory"));
        Assert.Null(NativeCurioCatalog.Empty.Get("thanks_chest"));
    }

    [Fact]
    public void HitTestingUsesTopDownAlphaAndRejectsBlankCanvasAndHoles()
    {
        var art = NativeCurioCatalog.Parse(Manifest().ToString()).Get("thanks_chest").Art;
        Assert.True(art.Hit(66f/128,82f/128));
        Assert.False(art.Hit(.02f,.02f));
        Assert.False(art.Hit(62f/128,82f/128));
        Assert.False(art.Hit(66f/128,46f/128)); // not vertically flipped
        Assert.False(art.Hit(1,.5f)); Assert.False(art.Hit(-.01f,.5f));
        Assert.False(art.Hit(float.NaN,.5f));
    }

    [Fact]
    public void PlacementKeepsTheRegisteredFootlineForBothStatesAtAnyViewportPosition()
    {
        var art = NativeCurioCatalog.Parse(Manifest().ToString()).Get("thanks_chest").Art;
        var placement = art.At(1040,690);
        Assert.Equal(940f,placement.Left);
        Assert.Equal(690,placement.Top+placement.Height-art.Padding*placement.Width/art.PixelWidth);
        Assert.True(placement.Hit(placement.Left+200*66f/128,placement.Top+200*82f/128));
        Assert.False(placement.Hit(placement.Left+1,placement.Top+1));
        Assert.EndsWith("coffer-closed.png",art.File(false));
        Assert.EndsWith("coffer-used.png",art.File(true));
        Assert.Equal(placement.Height,art.At(600,690).Height);
    }

    [Theory]
    [InlineData("Id", "../coffer")]
    [InlineData("Id", "faded_memory")]
    [InlineData("Width", 401)]
    [InlineData("PixelHeight", 3000)]
    [InlineData("Padding", 0)]
    [InlineData("MaskCellSize", 0)]
    [InlineData("MaskWidth", 33)]
    [InlineData("Mask", "")]
    [InlineData("ClosedSha256", "missing")]
    public void InvalidArtifactsCannotEnterTheNativeLoader(string field, object value)
    {
        var document=Manifest(); document["Artifacts"]![0]![field]=JToken.FromObject(value);
        Assert.Throws<InvalidDataException>(()=>NativeCurioCatalog.Parse(document.ToString()));
    }

    [Fact]
    public void UnknownReferencesDuplicateIdsAndOwnerExceptionAreRejected()
    {
        foreach(string id in new[]{"unlocked_strongbox","faded_memory","../../estate"})
        {
            var document=Manifest(); document["Entries"]![1]!["Id"]=id;
            Assert.Throws<InvalidDataException>(()=>NativeCurioCatalog.Parse(document.ToString()));
        }
        var missing=Manifest(); missing["Entries"]![1]!["ArtId"]="missing";
        Assert.Throws<InvalidDataException>(()=>NativeCurioCatalog.Parse(missing.ToString()));
    }
}
