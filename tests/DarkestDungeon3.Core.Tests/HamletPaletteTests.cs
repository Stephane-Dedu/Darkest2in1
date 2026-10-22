using DarkestDungeon3.Core.Presentation;
using System.Security.Cryptography;

namespace DarkestDungeon3.Core.Tests;

public class HamletPaletteTests
{
    [Fact]
    public void ServiceStonePaintingsConvergeWithoutErasingInkOrAlpha()
    {
        var cold=HamletPalette.BuildingSurface("Sanitarium fitted lime v25");
        var warm=HamletPalette.BuildingSurface("Hamlet guild wall v20");
        static int Chroma(int rgb)
        {
            int[] values={rgb>>16,(rgb>>8)&255,rgb&255};
            return values.Max()-values.Min();
        }
        foreach (var sample in new[] {(r:(byte)116,g:(byte)139,b:(byte)109), (r:(byte)155,g:(byte)128,b:(byte)96)})
        {
            int before=HamletPalette.GradeRgb(sample.r,sample.g,sample.b,HamletPalette.Building);
            int after=HamletPalette.GradeRgb(sample.r,sample.g,sample.b,cold);
            Assert.True(Chroma(after)<Chroma(before));
            Assert.Equal(after,HamletPalette.GradeRgb(sample.r,sample.g,sample.b,warm));
        }
        var pixels=new byte[] {0,0,0,0,90,115,80,91,210,199,178,255};
        HamletPalette.GradeRgba(pixels,cold);
        Assert.Equal(new byte[] {0,91,255},new[] {pixels[3],pixels[7],pixels[11]});
        Assert.True(pixels[8]-pixels[0]>170, "Keep strong painted light/dark separation.");
    }

    [Theory]
    [InlineData("Hamlet amber glass")]
    [InlineData("Campfire")]
    [InlineData("Slate underlap shadow v26")]
    [InlineData(null)]
    public void HarmonyKeepsLightsInkAndUnknownMaterialsOnTheirExistingFinish(string name)
    {
        int before=HamletPalette.GradeRgb(224,99,31,HamletPalette.Building);
        Assert.Equal(before,HamletPalette.GradeRgb(224,99,31,HamletPalette.BuildingSurface(name)));
    }

    [Fact]
    public void NewAndOlderRoofCoursesShareFinishesWhileKeepingRoofIdentities()
    {
        int oldSlate=HamletPalette.GradeRgb(112,104,118,HamletPalette.BuildingSurface("DD2 roof_slate turret v20"));
        int newSlate=HamletPalette.GradeRgb(112,104,118,HamletPalette.BuildingSurface("Service weathered slate v28"));
        Assert.Equal(oldSlate,newSlate);
        int clay=HamletPalette.GradeRgb(143,76,62,HamletPalette.BuildingSurface("Guild aged clay courses v28"));
        Assert.True((clay>>16)>(clay&255), "Clay should remain warm.");
        Assert.True((newSlate&255)>(newSlate>>16), "Slate should remain cool.");
    }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(19,63,121)]
    [InlineData(128,220,255)]
    public void NeutralGradingPreservesPaintWithinOneByte(byte r,byte g,byte b)
    {
        int rgb=HamletPalette.GradeRgb(r,g,b,new(1,1,1,0,1));
        Assert.InRange(Math.Abs((rgb>>16)-r),0,1);
        Assert.InRange(Math.Abs(((rgb>>8)&255)-g),0,1);
        Assert.InRange(Math.Abs((rgb&255)-b),0,1);
    }

    [Fact]
    public void RequestedBlackLiftPreservesPaintedHighlights()
    {
        var liftedPaint=new HamletPalette.Paint(1.35f,1.48f,1.6f,.028f,.4f);
        int black=HamletPalette.GradeRgb(0,0,0,liftedPaint);
        int lit=HamletPalette.GradeRgb(82,84,86,liftedPaint);
        Assert.InRange(black&255,40,55);
        Assert.Equal(black>>16,(black>>8)&255);
        Assert.True((lit&255)>(black&255)+20 && (lit&255)<180);
    }

    [Fact]
    public void DesaturationUsesLuminanceAndBrightnessStaysMonotonic()
    {
        int dull=HamletPalette.GradeRgb(220,60,20,new(1,1,1,0,0));
        Assert.Equal(dull>>16,(dull>>8)&255);
        Assert.Equal(dull&255,(dull>>8)&255);
        int lifted=HamletPalette.GradeRgb(220,60,20,new(1,1,1,.03f,0));
        Assert.True((lifted&255)>(dull&255));
    }

    [Theory]
    [InlineData("building","C1DB926A8A835CEACC6746C7C68F313F8EEE5409A1A9943615551366E102E7D7")]
    [InlineData("soil","334324E63FCCC139C894F3437C98761B691FC61CA83E7830F8F07729922CD7B7")]
    [InlineData("rock","F3EE2246608749DADD5B82BF7FFB2870A1563A7526366611AAFBBD6EEEB1DB3F")]
    [InlineData("grass","2851982A21E20EF94B4DFA82EEAF842F1050FFB39B2332C71A04F81D3C683379")]
    [InlineData("paving","0E4588B396C3653144CA5870DF15368DF9D52DD08EEB0FCCAB292D736D6EA854")]
    [InlineData("mountain","A092EF31E3DBC55D996DCDB00C7183AE5C9B311F6E635579412D23322339E151")]
    [InlineData("wildwood","CE958C641007A440FB5E79F48B644543036A3A646FBEC33601106F4BC19C8C8E")]
    public async Task WorkerGradingMatchesTheExistingMaterialLoopAndKeepsAlpha(string name,string expected)
    {
        var pixels=Enumerable.Range(0,16384).Select(i=>(byte)((i*73+i/2048*19+11)%256)).ToArray();
        var alpha=pixels.Where((_,i)=>i%4==3).ToArray();
        var paint=name switch
        {
            "building"=>HamletPalette.Building,"soil"=>HamletPalette.Soil,"rock"=>HamletPalette.Rock,
            "grass"=>HamletPalette.Grass,"paving"=>HamletPalette.Paving,"mountain"=>HamletPalette.Mountain,
            _=>HamletPalette.Wildwood
        };
        await Task.Run(()=>HamletPalette.GradeRgba(pixels,paint));
        Assert.Equal(expected,Convert.ToHexString(SHA256.HashData(pixels)));
        Assert.Equal(alpha,pixels.Where((_,i)=>i%4==3));
    }

    [Fact]
    public void CancelledGradingLeavesOwnedBytesUntouchedAndRejectsPartialPixels()
    {
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        var pixels=new byte[] {4,27,91,0,255,82,16,255}; var before=(byte[])pixels.Clone();
        Assert.Throws<OperationCanceledException>(()=>HamletPalette.GradeRgba(pixels,HamletPalette.Building,cancelled.Token));
        Assert.Equal(before,pixels);
        Assert.Throws<ArgumentException>(()=>HamletPalette.GradeRgba(new byte[7],HamletPalette.Building));
    }
}
