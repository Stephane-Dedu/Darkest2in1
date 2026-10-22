using System.Security.Cryptography;
using System.Text;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Tests;

public class HamletSunShadowTests
{
    [Fact]
    public void WorldSamplingUsesPixelCentresAndMinZFirstRows()
    {
        var field = new HamletSunShadow(new byte[] { 153, 204, 230, 255 }, 2, -2, 2, 10, 14);
        Assert.Equal(153 / 255f, field.At(-1, 11));
        Assert.Equal(204 / 255f, field.At(1, 11));
        Assert.Equal(230 / 255f, field.At(-1, 13));
        Assert.Equal(1, field.At(1, 13));
        Assert.Equal((153 + 204 + 230 + 255) / (4 * 255f), field.At(0, 12), 6);
        Assert.Equal(153 / 255f, field.At(-2, 10));
        Assert.Equal(1, field.At(-2.01f, 10));
        Assert.Equal(1, field.At(0, float.NaN));
    }

    [Fact]
    public void FieldBytesAreImmutableAndItsCacheIdentityIncludesBoundsAndSource()
    {
        var bytes = new byte[] { 153, 204, 230, 255 };
        var field = new HamletSunShadow(bytes, 2, 0, 2, 0, 2, "scene-one");
        bytes[0] = 255;
        Assert.Equal(.6f, field.At(.5f, .5f));
        Assert.NotEqual(field.Identity, new HamletSunShadow(bytes, 2, 0, 2, 0, 2, "scene-one").Identity);
        Assert.NotEqual(field.Identity, new HamletSunShadow(new byte[] { 153, 204, 230, 255 }, 2, 0, 2, 0, 2, "scene-two").Identity);
        Assert.NotEqual(field.Identity, new HamletSunShadow(new byte[] { 153, 204, 230, 255 }, 2, 0, 3, 0, 2, "scene-one").Identity);
    }

    [Fact]
    public void SunlightAttenuationUsesLinearLightAndWhiteIsExact()
    {
        Assert.Equal(.8f, HamletSunShadow.Daylight(.8f, 1));
        Assert.Equal(1.2f, HamletSunShadow.Daylight(1.2f, 1));
        double linear = Math.Pow((.8 + .055) / 1.055, 2.4) * .6;
        double expected = 1.055 * Math.Pow(linear, 1 / 2.4) - .055;
        Assert.InRange(Math.Abs(HamletSunShadow.Daylight(.8f, .6f) - expected), 0, .001);
        Assert.True(HamletSunShadow.Daylight(.8f, .6f) > .8f * .6f);
    }

    [Fact]
    public void WhiteFieldKeepsAllExistingPaintBytesAndSunDoesNotDimDoorwayLightOrValley()
    {
        byte[] Tile(byte v) => Enumerable.Repeat(v, 512 * 512 * 4).ToArray();
        HamletSunShadow Field(byte v) => new(Enumerable.Repeat(v, 4).ToArray(), 2,
            HamletLayout.MinX, HamletLayout.MaxX, HamletLayout.MinZ, HamletLayout.MaxZ);
        var tile = Tile(148);var black = Tile(0);
        var original = HamletPaintBaker.Bake(tile, tile, null, tile, size: 64, fieldSize: 16);
        var white = HamletPaintBaker.Bake(tile, tile, null, tile, size: 64, fieldSize: 16, sun: Field(255));
        Assert.Equal(original.Ground, white.Ground);Assert.Equal(original.Lanes, white.Lanes);Assert.Equal(original.Valley, white.Valley);
        var shadow = HamletPaintBaker.Bake(tile, tile, null, tile, size: 64, fieldSize: 16, sun: Field(153));
        Assert.Equal(original.Valley, shadow.Valley);
        for (int i = 0; i < original.Ground.Length; i++)
        {
            Assert.True(shadow.Ground[i] <= original.Ground[i]);Assert.True(shadow.Lanes[i] <= original.Lanes[i]);
            if (i % 4 == 3) { Assert.Equal(original.Ground[i], shadow.Ground[i]);Assert.Equal(original.Lanes[i], shadow.Lanes[i]); }
        }
        Assert.NotEqual(original.Ground, shadow.Ground);Assert.NotEqual(original.Lanes, shadow.Lanes);
        var lamp = HamletPaintBaker.Bake(black, black, null, black, size: 64, fieldSize: 16);
        var shadedLamp = HamletPaintBaker.Bake(black, black, null, black, size: 64, fieldSize: 16, sun: Field(153));
        Assert.Equal(lamp.Ground, shadedLamp.Ground);Assert.Equal(lamp.Lanes, shadedLamp.Lanes);
    }

    [Fact]
    public void ShadowChangesInvalidateCachedPaintWithoutChangingSourceTiles()
    {
        var tile = Enumerable.Repeat((byte)148, 512 * 512 * 4).ToArray();var cache = new HamletPaintCache();
        HamletSunShadow Field(byte v, string source) => new(Enumerable.Repeat(v, 4).ToArray(), 2,
            HamletLayout.MinX, HamletLayout.MaxX, HamletLayout.MinZ, HamletLayout.MaxZ, source);
        var first = cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8);
        var shaded = cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8, sun: Field(153, "one"));
        Assert.NotSame(first, shaded);
        Assert.Same(shaded, cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8, sun: Field(153, "one")));
        Assert.NotSame(shaded, cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8, sun: Field(153, "two")));
        Assert.NotSame(shaded, cache.GetOrBake(tile, tile, null, tile, size: 32, fieldSize: 8, sun: Field(204, "two")));
        Assert.All(tile, value => Assert.Equal(148, value));
    }

    [Fact]
    public void OptionalPackageRejectsStaleGeometryModelsOrDamagedDataAndAcceptsTheCurrentScene()
    {
        string root = Path.Combine(Path.GetTempPath(), "dd3-shadow-test-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var terrain = HamletTerrain.Build();var models = HamletLayout.Sites.ToDictionary(s => s.Id, _ => new string('a', 64));
            Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            var bytes = Enumerable.Repeat((byte)153, 64 * 64).ToArray();File.WriteAllBytes(Path.Combine(root, "ground-shadow.gray"), bytes);
            var manifest = new JObject { ["version"] = 1, ["size"] = 64, ["scene_sha256"] = HamletSunShadow.SceneDigest(terrain),
                ["source_glb_hashes"] = JObject.FromObject(models),["raw_file"] = "ground-shadow.gray",
                ["raw_sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                ["bounds"] = new JObject { ["MinX"] = HamletLayout.MinX,["MaxX"] = HamletLayout.MaxX,["MinZ"] = HamletLayout.MinZ,["MaxZ"] = HamletLayout.MaxZ } };
            void Save() => File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToString());
            Save();Assert.NotNull(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            terrain.Positions[0] += .1f;Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));terrain.Positions[0] -= .1f;
            // Build again rather than relying on floating-point addition to undo a stale scene.
            terrain = HamletTerrain.Build();models["guild"] = new string('b', 64);
            Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));models["guild"] = new string('a', 64);
            bytes[0]++;File.WriteAllBytes(Path.Combine(root, "ground-shadow.gray"), bytes);
            Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            File.WriteAllText(Path.Combine(root, "manifest.json"), "{broken");Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            manifest["size"] = new JObject();Save();Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SceneReferenceAllowsSubMillimetreRuntimeRoundingButRejectsChangedTopologyAndVertices()
    {
        string root = Path.Combine(Path.GetTempPath(), "dd3-shadow-rounding-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var terrain = HamletTerrain.Build();var reference = HamletSunShadow.SceneReference(terrain);
            var models = HamletLayout.Sites.ToDictionary(s => s.Id, _ => new string('a', 64));
            var pixels = Enumerable.Repeat((byte)153, 64 * 64).ToArray();
            File.WriteAllBytes(Path.Combine(root, "scene-reference.bin"), reference);
            File.WriteAllBytes(Path.Combine(root, "ground-shadow.gray"), pixels);
            var manifest = new JObject { ["version"] = 1,["size"] = 64,["reference_file"] = "scene-reference.bin",
                ["scene_sha256"] = Convert.ToHexString(SHA256.HashData(reference)).ToLowerInvariant(),
                ["source_glb_hashes"] = JObject.FromObject(models),["raw_file"] = "ground-shadow.gray",
                ["raw_sha256"] = Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant(),
                ["bounds"] = new JObject { ["MinX"] = HamletLayout.MinX,["MaxX"] = HamletLayout.MaxX,["MinZ"] = HamletLayout.MinZ,["MaxZ"] = HamletLayout.MaxZ } };
            File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToString());
            terrain.Positions[0] += .00002f;
            Assert.NotEqual((string)manifest["scene_sha256"], HamletSunShadow.SceneDigest(terrain));
            Assert.NotNull(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            terrain.Ground[0]++;Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));terrain.Ground[0]--;
            terrain.Positions[0] += .01f;Assert.Null(HamletSunShadow.ReadOptional(root, terrain, models, out _));
            reference[^1]++;File.WriteAllBytes(Path.Combine(root, "scene-reference.bin"), reference);
            Assert.Null(HamletSunShadow.ReadOptional(root, HamletTerrain.Build(), models, out _));
        }
        finally { Directory.Delete(root, true); }
    }
}
