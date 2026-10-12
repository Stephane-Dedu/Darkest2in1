using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionMapVfxTests
{
    private const string Valid = """
        {"effects":[{"id":"fire","file":"fire.png","columns":8,"rows":8,"frames":64,"fps":30},
                    {"id":"mist","file":"fog.png","columns":1,"rows":1,"frames":1,"fps":0}]}
        """;

    [Fact]
    public void FractionalFrameRateDoesNotDependOnDesktopNumberFormatting()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal(29.5f, ExpeditionMapVfx.Parse(Valid.Replace("\"fps\":30", "\"fps\":29.5")).Clips["fire"].Fps);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void NativeSheetUsesEveryFrameAndLoopsAtItsDeclaredRate()
    {
        var clip = ExpeditionMapVfx.Parse(Valid).Clips["fire"];
        var uvs = Enumerable.Range(0, 64).Select(i => clip.Uv(i / 30f + 0.0001f)).ToList();
        Assert.Equal(64, uvs.Distinct().Count());
        Assert.Equal((0f, 0.875f), (uvs[0].X, uvs[0].Y));
        Assert.Equal((0.875f, 0f), (uvs[63].X, uvs[63].Y));
        Assert.Equal(uvs[0], clip.Uv(64 / 30f + 0.0001f));
        Assert.NotEqual(clip.Uv(0), clip.Uv(0, 0.5f));
        foreach (var uv in uvs) { Assert.InRange(uv.X, 0, 0.875f); Assert.InRange(uv.Y, 0, 0.875f); }
    }

    [Fact]
    public void FogIsStaticAndInvalidClockValuesCannotCorruptUvs()
    {
        var pack = ExpeditionMapVfx.Parse(Valid);
        Assert.Equal(pack.Clips["mist"].Uv(0), pack.Clips["mist"].Uv(1900));
        var clip = pack.Clips["fire"];
        Assert.Equal(clip.Uv(0), clip.Uv(float.NaN));
        Assert.Equal(clip.Uv(0), clip.Uv(float.PositiveInfinity));
        Assert.InRange(clip.Uv(-1).Y, 0, 0.875f);
    }

    [Theory]
    [InlineData("fire.png", "../fire.png")]
    [InlineData("fire.png", "..\\\\fire.png")]
    [InlineData("fire.png", "C:/fire.png")]
    [InlineData("fire.png", "fire.dds")]
    [InlineData("\"columns\":8", "\"columns\":99")]
    [InlineData("\"columns\":8", "\"columns\":99999999999999999999999999999999")]
    [InlineData("\"rows\":8", "\"rows\":0")]
    [InlineData("\"frames\":64", "\"frames\":65")]
    [InlineData("\"fps\":30", "\"fps\":-1")]
    [InlineData("\"fps\":30", "\"fps\":100")]
    [InlineData("\"fps\":30", "\"fps\":1e300")]
    [InlineData("\"id\":\"fire\"", "\"id\":\"unexpected\"")]
    public void InvalidClipIsDroppedWhileValidFogRemains(string from, string to)
    {
        var pack = ExpeditionMapVfx.Parse(Valid.Replace(from, to));
        Assert.NotNull(pack);
        Assert.Equal(new[] { "mist" }, pack.Clips.Keys);
    }
}
