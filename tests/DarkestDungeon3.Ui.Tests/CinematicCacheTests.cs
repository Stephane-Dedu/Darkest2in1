using DarkestDungeon3.Runtime;
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
            string voice = CinematicCache.Voice("fixture_intro", source);
            Assert.Equal(Path.Combine(fixture, "cache", "dd1_fixture_intro.ogg"), voice);
            Assert.Equal(page, File.ReadAllBytes(voice));
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
            Assert.Null(CinematicCache.Voice("fixture_intro", source));
            Assert.Empty(Directory.GetFiles(Path.Combine(fixture, "cache")));
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
            test(fixture);
        }
        finally
        {
            Session.SaveDir = previous;
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
