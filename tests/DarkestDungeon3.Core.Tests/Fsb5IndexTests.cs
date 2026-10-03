using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class Fsb5IndexTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void FindsDd1SamplesByName()
    {
        string dir = Install.PathOf("audio", "secondary_banks");
        var index = Fsb5Index.Load(new[] { "music", "ambience", "general", "town" }.Select(b => Path.Combine(dir, b + ".bank")));
        Assert.True(index.Count > 800);
        Assert.True(index.TryGet("Town_Stereo_Mix_LOOP_1", out var chunk, out int i));
        Assert.EndsWith("music.bank", chunk.File);
        Assert.Equal("Town_Stereo_Mix_LOOP_1", chunk.Names[i]);
        Assert.True(index.Has("amb_dun_ruins_base"));
        Assert.True(index.Has("gen_party_foot_stone_01"));
        Assert.True(index.Has("town_enter_tavern"));
        // The FSB5 sits whole inside its bank file.
        Assert.All(index.Chunks, c => Assert.True(c.Offset + c.Length <= new FileInfo(c.File).Length));
    }
}
