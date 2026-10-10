using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ShieldbreakerModelTests
{
    private static ModelPoseKey Key(float t, float z) => new() { T = t, Right = new[] { 1f, 2f, z }, Left = new[] { -1f, 2f, z }, Spear = new[] { 0f, 0f, 1f } };
    private static ModelClip Clip(bool loop = false) => new() { Duration = 2, Loop = loop, Keys = new[] { Key(0, 0), Key(1, 4), Key(2, 0) } };
    private static ShieldbreakerModelData Model() => new() {
        Version = 1, Donor = "hellion", Bones = new[] { "ROOTSHJnt" },
        Vertices = new[] { new[] { 0f, 0f, 0f }, new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f } },
        Normals = Enumerable.Range(0, 3).Select(_ => new[] { 0f, 0f, 1f }).ToArray(),
        Uv = Enumerable.Range(0, 3).Select(_ => new[] { 0f, 0f }).ToArray(),
        Weights = Enumerable.Range(0, 3).Select(_ => new[] { 1f, 0f, 0f, 0f }).ToArray(),
        Indices = Enumerable.Range(0, 3).Select(_ => new[] { 0, 0, 0, 0 }).ToArray(),
        Bindposes = new[] { new[] { 1f,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 } },
        Triangles = new[] { 0, 1, 2 }, Parts = new[] { new ModelPart { Name = "body", Start = 0, Count = 3 } },
        Clips = new() { ["idle"] = Clip(true), ["attack"] = Clip(), ["defend"] = Clip() }
    };

    [Fact]
    public void PrivateModelRoundTripsThroughTheRuntimeReader()
    {
        string file = Path.GetTempFileName();
        try {
            File.WriteAllText(file, JsonConvert.SerializeObject(Model()));
            var read = ShieldbreakerModelData.Read(file);
            Assert.Equal("hellion", read.Donor);
            Assert.Equal(4, read.Clips["attack"].Sample(1).Right.Z);
        } finally { File.Delete(file); }
    }

    [Fact]
    public void NativeOutlineChannelsRoundTripAndOldPacksRemainReadable()
    {
        var model = Model();
        model.Validate(); // Earlier packs omit these optional native channels.
        model.Tangents = Enumerable.Range(0, 3).Select(_ => new[] { 0f, 0f, 1f, 0f }).ToArray();
        model.Colors = Enumerable.Range(0, 3).Select(_ => new[] { 1f, 0f, 0f, 1f }).ToArray();
        var read = JsonConvert.DeserializeObject<ShieldbreakerModelData>(JsonConvert.SerializeObject(model))!;
        read.Validate();
        Assert.Equal(model.Tangents[1], read.Tangents[1]);
        Assert.Equal(model.Colors[2], read.Colors[2]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MalformedNativeChannelsCannotReachUnity(int bad)
    {
        var model = Model();
        if (bad == 0) model.Tangents = new[] { new[] { 0f, 0f, 1f, 0f } };
        if (bad == 1) model.Tangents = Enumerable.Range(0, 3).Select(_ => new[] { float.NaN, 0f, 1f, 0f }).ToArray();
        if (bad == 2) model.Colors = Enumerable.Range(0, 3).Select(_ => new[] { 2f, 0f, 0f, 1f }).ToArray();
        if (bad == 3) model.Colors = Enumerable.Range(0, 3).Select(_ => new[] { 1f, 0f, 0f }).ToArray();
        Assert.Throws<FormatException>(model.Validate);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void InvalidGeometryCannotReachUnity(int bad)
    {
        var m = Model();
        switch (bad) {
            case 0: m.Indices[0][0] = 1; break;
            case 1: m.Weights[0][0] = .5f; break;
            case 2: m.Triangles[0] = 3; break;
            case 3: m.Vertices[0][0] = float.NaN; break;
            case 4: m.Bindposes[0][0] = float.PositiveInfinity; break;
            case 5: m.Normals = null!; break;
            case 6: m.Clips.Remove("defend"); break;
        }
        Assert.Throws<FormatException>(m.Validate);
    }

    [Fact]
    public void AttackReturnsToIdleAndClampsRatherThanRepeating()
    {
        var clip = Clip(); clip.Validate();
        Assert.Equal(0, clip.Sample(-1).Right.Z);
        Assert.Equal(4, clip.Sample(1).Right.Z);
        Assert.Equal(0, clip.Sample(20).Right.Z);
        Assert.InRange(Math.Abs(clip.Sample(.999f).Right.Z - clip.Sample(1.001f).Right.Z), 0, .0001f);
        // Smoothstep gives a zero end velocity at each held key.
        Assert.InRange(clip.Sample(.001f).Right.Z, 0, .0001f);
    }

    [Fact]
    public void IdleLoopsWithoutAHandOrWeaponJump()
    {
        var clip = Clip(true); clip.Validate();
        Assert.Equal(clip.Sample(.5f).Right.Z, clip.Sample(2.5f).Right.Z);
        Assert.InRange(Math.Abs(clip.Sample(1.999f).Right.Z - clip.Sample(2.001f).Right.Z), 0, .0001f);
        clip.Keys[2].Right[0] += 1;
        Assert.Throws<FormatException>(clip.Validate);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void InvalidClipTimingOrWeaponDirectionIsRejected(int bad)
    {
        var c = Clip();
        if (bad == 0) c.Keys[1].T = 0;
        if (bad == 1) c.Keys[1].Spear = new float[3];
        if (bad == 2) c.Duration = float.NaN;
        if (bad == 3) c.Keys[1].Right[0] = float.PositiveInfinity;
        Assert.Throws<FormatException>(c.Validate);
    }

    [Fact]
    public void TextureBoundsAreCheckedBeforeNativeDecode()
    {
        byte[] header = { 137,80,78,71,13,10,26,10, 0,0,0,13, 73,72,68,82, 0,0,16,0, 0,0,8,0, 8,6,0,0,0, 0,0,0,0 };
        Assert.True(ShieldbreakerModelData.ValidTexturePng(header));
        header[18] = 255; Assert.False(ShieldbreakerModelData.ValidTexturePng(header));
        Assert.False(ShieldbreakerModelData.ValidTexturePng(new byte[20]));
    }
}
