using DarkestDungeon3.Runtime;
using System.Diagnostics;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CinematicCacheTests
{
    [Fact]
    public void VoicePublishesCompleteAudioAndReusesItWithoutReopeningTheVideo()
    {
        Fixture(fixture =>
        {
            byte[] page = new byte[35];
            new byte[] { 79, 103, 103, 83 }.CopyTo(page, 0);
            page[5] = 2;
            page[14] = 1;
            page[26] = 1;
            page[27] = 7;
            new byte[] { 1, 118, 111, 114, 98, 105, 115 }.CopyTo(page, 28);
            string source = Path.Combine(fixture, "source.ogv");
            File.WriteAllBytes(source, page);
            Assert.Null(CinematicCache.Voice("fixture_intro", source, out bool complete));
            Assert.False(complete);
            string voice = ReadyVoice("fixture_intro", source);
            Assert.Equal(Path.Combine(fixture, "cache", "dd1_fixture_intro.ogg"), voice);
            Assert.Equal(page, File.ReadAllBytes(voice));
            Assert.All(Session.SaveDirReads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
            Assert.Single(Directory.GetFiles(Path.Combine(fixture, "cache")));
            File.Delete(source);
            Assert.Equal(voice, CinematicCache.Voice("fixture_intro", source));
            Assert.Equal(page, File.ReadAllBytes(voice));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrInvalidAudioNeverPublishesAnEmptyCacheOrLeavesTemporaryFiles(bool createInvalidVideo)
    {
        Fixture(fixture =>
        {
            string source = Path.Combine(fixture, "missing_or_invalid.ogv");
            if (createInvalidVideo) File.WriteAllBytes(source, new byte[] { 0, 1, 2 });
            Assert.Null(ReadyVoice("fixture_intro", source));
            Assert.Empty(Directory.GetFiles(Path.Combine(fixture, "cache")));
            Assert.All(Session.SaveDirReads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        });
    }

    private static void Fixture(Action<string> test)
    {
        string previous = Session.SaveDir;
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_voice_cache_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(fixture);
            Session.SaveDir = fixture;
            Session.SaveDirReads.Clear();
            test(fixture);
        }
        finally
        {
            Session.SaveDir = previous;
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }

    private static string ReadyVoice(string name, string source)
    {
        var watch = Stopwatch.StartNew();
        bool complete = false;
        string voice = null;
        while (!complete && watch.Elapsed.TotalSeconds < 10)
        {
            voice = CinematicCache.Voice(name, source, out complete);
            if (!complete) Thread.Sleep(1);
        }
        Assert.True(complete, "audio worker did not finish");
        return voice;
    }
}
