using System.Diagnostics;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CinematicConversionTests
{
    [Fact]
    public void AFailedEncoderStartProgressesEveryQueuedMovieAndReleasesTheirKeys()
    {
        Fixture((dd1, directory) =>
        {
            string encoder = Path.Combine(directory, "missing_encoder.exe");
            CinematicCache.PreparePictures(dd1, new[] { "first", "second" }, encoder, directory);
            Assert.False(CinematicCache.HasPendingPictures);
            Assert.Contains(Plugin.Log.Warnings, line => line.Contains("first: ffmpeg failed to start"));
            Assert.Contains(Plugin.Log.Warnings, line => line.Contains("second: ffmpeg failed to start"));
            Assert.Empty(Directory.GetFiles(directory));
            Plugin.Log.Warnings.Clear();
            CinematicCache.PreparePictures(dd1, new[] { "second" }, encoder, directory);
            Assert.Single(Plugin.Log.Warnings); // failure did not permanently reserve this output
            Assert.False(CinematicCache.HasPendingPictures);
        });
    }

    [Fact]
    public void DuplicateActiveAndQueuedRequestsConvertOnceAndPreserveAnExternalReadyOutput()
    {
        Fixture((dd1, directory) =>
        {
            // A harmless Windows lookup utility rejects ffmpeg arguments and exits without writing assets.
            string helper = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
            Assert.True(File.Exists(helper));
            CinematicCache.PreparePictures(dd1, new[] { "first", "second", "second" }, helper, directory);
            Assert.True(CinematicCache.HasPendingPictures);
            CinematicCache.PreparePictures(dd1, new[] { "first", "second" }, helper, directory);
            string preserved = Path.Combine(directory, "dd1_first.webm");
            byte[] existing = { 4, 5, 6 };
            File.WriteAllBytes(preserved, existing);
            DrainPictures();
            Assert.Single(Plugin.Log.Infos.Where(line => line.Contains("first: converting")));
            Assert.Single(Plugin.Log.Infos.Where(line => line.Contains("second: converting")));
            Assert.Contains(Plugin.Log.Infos, line => line.Contains("first: picture ready"));
            Assert.Contains(Plugin.Log.Infos, line => line.Contains("second: picture failed"));
            Assert.Equal(existing, File.ReadAllBytes(preserved));
            Assert.Equal(new[] { preserved }, Directory.GetFiles(directory));
            Assert.All(Plugin.Log.Threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        });
    }

    [Fact]
    public void ReadyOutputAndMissingSourceDoNotStartAnEncoderOrBlockLaterWork()
    {
        Fixture((dd1, directory) =>
        {
            string preserved = Path.Combine(directory, "dd1_first.webm");
            File.WriteAllBytes(preserved, new byte[] { 7 });
            CinematicCache.PreparePictures(dd1, new[] { "first", "missing_source", "second" }, Path.Combine(directory, "missing_encoder.exe"), directory);
            Assert.False(CinematicCache.HasPendingPictures);
            Assert.Single(Plugin.Log.Warnings);
            Assert.Contains("second: ffmpeg failed to start", Plugin.Log.Warnings[0]);
            Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(preserved));
            Assert.Empty(Plugin.Log.Infos);
        });
    }

    private static void DrainPictures()
    {
        var watch = Stopwatch.StartNew();
        while (CinematicCache.HasPendingPictures && watch.Elapsed.TotalSeconds < 10)
        {
            CinematicCache.Update();
            if (CinematicCache.HasPendingPictures) Thread.Sleep(1);
        }
        Assert.False(CinematicCache.HasPendingPictures, "encoder queue did not finish");
    }

    private static void Fixture(Action<Dd1Install, string> test)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_conversion_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "scripts"));
            Directory.CreateDirectory(Path.Combine(fixture, "dungeons"));
            Directory.CreateDirectory(Path.Combine(fixture, "video"));
            string directory = Path.Combine(fixture, "cache");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(fixture, "scripts", "map_generator.darkest"), "");
            foreach (string name in new[] { "first", "second" }) File.WriteAllText(Path.Combine(fixture, "video", name + ".ogv"), "synthetic video");
            var dd1 = Dd1Install.Find(fixture);
            Assert.Equal(fixture, dd1.Root);
            Plugin.Log.Infos.Clear(); Plugin.Log.Warnings.Clear(); Plugin.Log.Threads.Clear();
            test(dd1, directory);
        }
        finally
        {
            DrainPictures();
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
