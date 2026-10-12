using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public sealed class SceneryEmittersTests
{
    [Fact]
    public void NativeFireBoundsStayOnTheirOwnSceneAndProduceFlameAndLightAtTheSameAnchor()
    {
        var pack = SceneryEmitters.Parse("{\"scenes\":{\"dd2_city-arena-battle-01.png\":[[300,420,80,160]]}}");
        var fires = pack.For("dd2_city-arena-battle-01.png");
        Assert.Single(fires);
        Assert.Empty(pack.For("dd2_city-arena-boss-01.png"));
        var particles = new List<DungeonAtmosphere.Particle>();
        DungeonAtmosphere.Fill(particles,"dd2_city",1,2,100,fires);
        Assert.Equal(26,particles.Count);
        Assert.Equal("fire",particles[^1].Effect);
        Assert.Equal(300,particles[^1].X); Assert.Equal(420,particles[^1].Y);
        Assert.Equal(80,particles[^1].Width); Assert.Equal(160,particles[^1].Height);
        Assert.Equal("mist",particles[^2].Effect); Assert.Equal(300,particles[^2].X);
    }

    [Theory]
    [InlineData("../dd2_city-arena-battle.png")]
    [InlineData("dd2_city-arena-../battle.png")]
    [InlineData("C:\\dd2_city-arena-battle.png")]
    [InlineData("dd2_farm-arena-battle.png")]
    public void ForeignAndTraversalNamesCannotBindEffects(string file)
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new { scenes = new Dictionary<string,object> { [file] = new[] { new[] { 300,420,80,160 } } } });
        Assert.Empty(SceneryEmitters.Parse(json).For(file));
    }

    [Fact]
    public void BadAnchorsAreSkippedAndMalformedPacksAreOptional()
    {
        Assert.Null(SceneryEmitters.Parse("broken"));
        var pack = SceneryEmitters.Parse("{\"scenes\":{\"dd2_city-arena-battle.png\":[[300,420,80,160],[300,420,-1,160],[4000,0,80,160],[0,0,80,\"bad\"],[0,0,0,0]]}}");
        Assert.Single(pack.For("dd2_city-arena-battle.png"));
        Assert.Empty(pack.For(null));
    }
}
