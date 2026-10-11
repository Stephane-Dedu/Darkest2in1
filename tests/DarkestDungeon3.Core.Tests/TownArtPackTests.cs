using DarkestDungeon3.Core.Campaign.Town;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Tests;

public sealed class TownArtPackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-town-" + Guid.NewGuid().ToString("N"));
    private readonly JObject _manifest;

    public TownArtPackTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "scene.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_root, "tint.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_root, "outline.png"), new byte[] { 1 });
        // Top row opaque/transparent; bottom row transparent/opaque.
        File.WriteAllBytes(Path.Combine(_root, "visible.mask"), new byte[] { 255, 0, 0, 255 });
        _manifest = new JObject { ["version"] = 1, ["width"] = 1920, ["height"] = 1080, ["background"] = "scene.png",
            ["buildings"] = new JArray(Buildings.All.Select((id, i) => new JObject {
                ["id"] = id, ["x"] = 100 + i * 10, ["y"] = 200, ["width"] = 2, ["height"] = 2, ["order"] = i,
                ["mask"] = "visible.mask", ["tint"] = "tint.png", ["outline"] = "outline.png" })) };
    }

    private TownArtPack Load()
    {
        File.WriteAllText(Path.Combine(_root, "manifest.json"), _manifest.ToString());
        return TownArtPack.Load(_root);
    }

    [Fact]
    public void OptionalPackDoesNotCreateAnything()
    {
        Assert.Null(TownArtPack.Load(null));
        Assert.Null(TownArtPack.Load(Path.Combine(_root, "absent")));
        Assert.False(Directory.Exists(Path.Combine(_root, "absent")));
    }

    [Fact]
    public void HitsUseTopDownVisibleAlphaAndHalfOpenBounds()
    {
        var pack = Load();
        Assert.Equal(Buildings.StageCoach, pack.Hit(100.9f, 200.9f)?.Id);
        Assert.Null(pack.Hit(101, 200));
        Assert.Null(pack.Hit(100, 201));
        Assert.Equal(Buildings.StageCoach, pack.Hit(101.9f, 201.9f)?.Id);
        Assert.Null(pack.Hit(102, 202));
        Assert.Null(pack.Hit(99.99f, 200));
        Assert.Null(pack.Hit(float.NaN, 200));
        Assert.Null(pack.Hit(float.PositiveInfinity, 200));
    }

    [Fact]
    public void ForegroundWinsEvenWhenManifestOrderIsShuffled()
    {
        var entries = (JArray)_manifest["buildings"]!;
        entries[0]["x"] = entries[1]["x"] = 100;
        entries[0]["order"] = 500;
        Assert.Equal(Buildings.StageCoach, Load().Hit(100, 200)?.Id);
    }

    [Theory]
    [InlineData("version", 2)]
    [InlineData("width", 1600)]
    [InlineData("height", 900)]
    public void IncompatibleCanvasRejects(string key, int value)
    {
        _manifest[key] = value;
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Theory]
    [InlineData("x", -1)]
    [InlineData("y", 1080)]
    [InlineData("width", 0)]
    [InlineData("width", int.MaxValue)]
    [InlineData("height", 1081)]
    public void InvalidBoundsRejectBeforeReadingMasks(string key, int value)
    {
        _manifest["buildings"]![0]![key] = value;
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Fact]
    public void PartialOrDuplicateServiceMappingRejects()
    {
        var entries = (JArray)_manifest["buildings"]!;
        entries.RemoveAt(entries.Count - 1);
        Assert.Throws<InvalidDataException>(() => Load());
        entries.Add(entries[0].DeepClone());
        Assert.Throws<InvalidDataException>(() => Load());
        entries[0]["id"] = "village_west";
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Theory]
    [InlineData("../escaped.png")]
    [InlineData("/absolute.png")]
    [InlineData("missing.png")]
    public void FilesMustExistInsideThePack(string path)
    {
        _manifest["background"] = path;
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Fact]
    public void EmptyOrTruncatedMasksReject()
    {
        File.WriteAllBytes(Path.Combine(_root, "visible.mask"), new byte[] { 255 });
        Assert.Throws<InvalidDataException>(() => Load());
        File.WriteAllBytes(Path.Combine(_root, "visible.mask"), new byte[4]);
        Assert.Throws<InvalidDataException>(() => Load());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
