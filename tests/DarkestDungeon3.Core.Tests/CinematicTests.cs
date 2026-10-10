using System.IO;
using System.Linq;
using System.Text;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's opening cinematics: subtitles and audio from video/*.ogv and *.sub.</summary>
public class CinematicTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void HouseOfRuinIsSubtitledWithTheAncestorsLines()
    {
        var subs = Dd1Cinematic.Subtitles(Install, "house_of_ruin");
        Assert.Equal(29, subs.Count);
        Assert.Equal((3207, 6250, "Ruin has come to our family."), subs[0]);
        Assert.Equal("of the Darkest Dungeon.", subs[28].Text);
        Assert.Equal("Ruin has come to our family.", Dd1Cinematic.SubtitleAt(subs, 4.0));
        Assert.Null(Dd1Cinematic.SubtitleAt(subs, 1.0));
        Assert.Equal(16, Dd1Cinematic.Subtitles(Install, "old_road").Count);
        Assert.All(Dd1Cinematic.Opening, v => Assert.True(File.Exists(Dd1Cinematic.VideoPath(Install, v))));
    }

    [Fact]
    public void TheVorbisTrackComesOutAsAPlainOggFile()
    {
        var ogv = File.ReadAllBytes(Dd1Cinematic.VideoPath(Install, "old_road"));
        var ogg = Dd1Cinematic.VorbisAudio(ogv);
        Assert.True(ogg.Length > 100_000);
        Assert.Equal("OggS", Encoding.ASCII.GetString(ogg, 0, 4));
        Assert.Equal("vorbis", Encoding.ASCII.GetString(ogg, 29, 6));           // first page: the Vorbis id header
        // No Theora (or Skeleton) page: the Theora id header is 0x80 "theora".
        var theora = new byte[] { 0x80, (byte)'t', (byte)'h', (byte)'e', (byte)'o', (byte)'r', (byte)'a' };
        Assert.False(Enumerable.Range(0, ogg.Length - theora.Length).Any(i => ogg[i] == 0x80 && ogg.Skip(i).Take(theora.Length).SequenceEqual(theora)));
        Assert.DoesNotContain("fishead", Encoding.ASCII.GetString(ogg.Take(4096).ToArray()));
    }
}
