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

    [Theory]
    [InlineData("dd2_city")]
    [InlineData("dd2_forest")]
    [InlineData("dd2_farm")]
    [InlineData("dd2_coast")]
    public void RegionalPresentationKeepsTheCurioIdentityAndClassicDefault(string region)
    {
        var document = Manifest(); document["Version"] = 3;
        var cabinet = document["Artifacts"]![0]!.DeepClone(); cabinet["Id"] = "cabinet";
        ((JArray)document["Artifacts"]!).Add(cabinet);
        var entry = document["Entries"]![0]!;
        entry["Id"] = "locked_display_cabinet"; entry["ArtId"] = "cabinet";
        entry["Regions"] = new JObject { [region] = new JObject { ["ArtId"] = "coffer", ["DisplayName"] = "Locked field cache" } };
        var catalog = NativeCurioCatalog.Parse(document.ToString());
        var regional = catalog.Get("locked_display_cabinet", region);
        Assert.Equal("locked_display_cabinet", regional.Id);
        Assert.Equal("coffer", regional.Art.Id);
        Assert.Equal("Locked field cache", regional.DisplayName);
        Assert.Equal("cabinet", catalog.Get("locked_display_cabinet", "ruins").Art.Id);
        Assert.Null(catalog.Get("locked_display_cabinet", "ruins").DisplayName);
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Null(catalog.Get("faded_memory", region));
    }

    [Fact]
    public void RegionOnlyPaintingLeavesClassicOtherRegionsAndUnknownContextsToOriginalArt()
    {
        var document = Manifest(); document["Version"] = 3;
        var entry = (JObject)document["Entries"]![0]!;
        entry["Id"] = "alchemy_table"; entry.Remove("ArtId");
        entry["Regions"] = new JObject { ["dd2_city"] = new JObject {
            ["ArtId"] = "coffer", ["DisplayName"] = "Abandoned apothecary trolley" } };
        var catalog = NativeCurioCatalog.Parse(document.ToString());
        Assert.Equal("alchemy_table", catalog.Get("alchemy_table", "dd2_city").Id);
        Assert.Equal("Abandoned apothecary trolley", catalog.Get("alchemy_table", "dd2_city").DisplayName);
        foreach (string region in new[] { null, "ruins", "warrens", "weald", "cove", "darkest", "dd2_forest", "dd2_farm", "dd2_coast", "unknown" })
            Assert.Null(catalog.Get("alchemy_table", region));
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Contains(catalog.Entries, definition => definition.Id == "alchemy_table" && definition.Art != null);
        Assert.Equal("coffer", catalog.Get("thanks_chest", "ruins").Art.Id);
    }

    [Theory]
    [InlineData("old_schema")]
    [InlineData("no_regions")]
    [InlineData("empty_regions")]
    [InlineData("empty_default")]
    [InlineData("missing_default")]
    [InlineData("duplicate_identity")]
    [InlineData("protected_owner")]
    public void RegionOnlyEntriesStillRequireValidBindingsAndUniqueUnprotectedIdentities(string problem)
    {
        var document = Manifest(); document["Version"] = 3;
        var entry = (JObject)document["Entries"]![0]!; entry.Remove("ArtId");
        entry["Regions"] = new JObject { ["dd2_city"] = new JObject { ["ArtId"] = "coffer", ["DisplayName"] = "Street apparatus" } };
        if (problem == "old_schema") document["Version"] = 2;
        else if (problem == "no_regions") entry.Remove("Regions");
        else if (problem == "empty_regions") entry["Regions"] = new JObject();
        else if (problem == "empty_default") entry["ArtId"] = "";
        else if (problem == "missing_default") entry["ArtId"] = "missing";
        else if (problem == "duplicate_identity") entry["Id"] = "thanks_chest";
        else entry["Id"] = "faded_memory";
        Assert.Throws<InvalidDataException>(() => NativeCurioCatalog.Parse(document.ToString()));
    }

    [Theory]
    [InlineData("dd2_city", "missing", "Locked cache", 3)]
    [InlineData("ruins", "coffer", "Locked cache", 3)]
    [InlineData("dd2_city", "coffer", " ", 3)]
    [InlineData("dd2_city", "coffer", "Locked\ncache", 3)]
    [InlineData("dd2_city", "coffer", "Locked cache", 2)]
    public void InvalidRegionalBindingsCannotOverrideClassicOrOwnerArt(string region, string art, string name, int version)
    {
        var document = Manifest(); document["Version"] = version;
        document["Entries"]![0]!["Regions"] = new JObject { [region] = new JObject { ["ArtId"] = art, ["DisplayName"] = name } };
        Assert.Throws<InvalidDataException>(() => NativeCurioCatalog.Parse(document.ToString()));
    }

    [Fact]
    public void RegionalResultWordingOnlyReplacesExplicitExactMessages()
    {
        var document = Manifest(); document["Version"] = 3;
        document["Entries"]![0]!["Regions"] = new JObject { ["dd2_city"] = new JObject {
            ["ArtId"] = "coffer", ["DisplayName"] = "Locked street cache",
            ["ResultTexts"] = new JObject { ["The cabinet is a trap!"] = "The cache is trapped!" } } };
        var catalog = NativeCurioCatalog.Parse(document.ToString());
        var regional = catalog.Get("unlocked_strongbox", "dd2_city");
        Assert.Equal("The cache is trapped!", regional.PresentResult("The cabinet is a trap!"));
        Assert.Equal("The cabinet is a trap!", catalog.Get("unlocked_strongbox", "ruins").PresentResult("The cabinet is a trap!"));
        Assert.Equal("the cabinet is a trap!", regional.PresentResult("the cabinet is a trap!"));
        Assert.Equal("A different outcome.", regional.PresentResult("A different outcome."));
        Assert.Null(regional.PresentResult(null));
        Assert.Equal("unlocked_strongbox", regional.Id);
    }

    [Theory]
    [InlineData("blank_key")]
    [InlineData("blank_value")]
    [InlineData("line_break")]
    [InlineData("oversized")]
    [InlineData("too_many")]
    public void RegionalResultWordingIsBoundedAndCannotInjectControlCharacters(string problem)
    {
        var texts = new JObject { ["Original result"] = "Regional result" };
        if (problem == "blank_key") texts = new JObject { [" "] = "Regional result" };
        else if (problem == "blank_value") texts["Original result"] = " ";
        else if (problem == "line_break") texts["Original result"] = "First\nsecond";
        else if (problem == "oversized") texts["Original result"] = new string('x', 513);
        else for (int i = 0; i < 16; i++) texts["Other " + i] = "Regional result";
        var document = Manifest(); document["Version"] = 3;
        document["Entries"]![0]!["Regions"] = new JObject { ["dd2_city"] = new JObject {
            ["ArtId"] = "coffer", ["DisplayName"] = "Locked street cache", ["ResultTexts"] = texts } };
        Assert.Throws<InvalidDataException>(() => NativeCurioCatalog.Parse(document.ToString()));
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
