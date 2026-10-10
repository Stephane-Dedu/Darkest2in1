using System.Diagnostics;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class PngPreloaderTests
{
    [Fact]
    public void NativeTownWarmupDefersAllTextureApisAndUploadsAtMostOneImagePerPump()
    {
        var loader = new PngPreloader();
        var dd1 = Dd1Install.Find();
        string[] paths = PngPreloader.InitialTownImages.Select(relative => dd1.PathOf(relative.Split('/'))).ToArray();
        Assert.Equal(12, paths.Length);
        Assert.Equal(paths.Length, paths.Distinct().Count());
        Texture2D.ApiThreads.Clear();
        int decodes = Texture2D.RoomDecodeCount;
        foreach (string path in paths)
        {
            Assert.True(File.Exists(path), path);
            loader.Request(path);
            loader.Request(path);
            Assert.True(loader.TryGet(path, out var texture, out bool finished));
            Assert.Null(texture);
            Assert.False(finished);
        }
        Assert.Empty(Texture2D.ApiThreads);
        PumpUntil(loader, () => paths.All(path => loader.TryGet(path, out _, out bool done) && done));
        Assert.Equal(paths.Length, Texture2D.RoomDecodeCount - decodes);
        Assert.All(Texture2D.ApiThreads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        foreach (string path in paths)
        {
            Assert.True(loader.TryGet(path, out var texture, out bool finished));
            Assert.True(finished);
            Assert.NotNull(texture);
            Assert.True(texture.width > 0 && texture.height > 0);
            loader.Request(path);
            loader.Update();
            Assert.True(loader.TryGet(path, out var cached, out finished));
            Assert.Same(texture, cached);
        }
        Assert.Equal(paths.Length, Texture2D.RoomDecodeCount - decodes);
        Assert.False(loader.TryGet("not_requested", out _, out _));
    }

    [Fact]
    public void MissingAndCorruptImagesFinishWithoutBlockingLaterValidImages()
    {
        Fixture((loader, fixture) =>
        {
            string missing = Path.Combine(fixture, "missing.png");
            string corrupt = Path.Combine(fixture, "corrupt.png");
            string valid = Dd1Install.Find().PathOf("campaign", "town", "town_bg.png");
            File.WriteAllBytes(corrupt, Array.Empty<byte>());
            loader.Request(missing);
            loader.Request(corrupt);
            loader.Request(valid);
            PumpUntil(loader, () => new[] { missing, corrupt, valid }.All(path => loader.TryGet(path, out _, out bool done) && done));
            Assert.True(loader.TryGet(missing, out var absent, out _));
            Assert.Null(absent);
            Assert.True(loader.TryGet(corrupt, out var broken, out _));
            Assert.Null(broken);
            Assert.True(loader.TryGet(valid, out var texture, out _));
            Assert.NotNull(texture);
        });
    }

    [Fact]
    public void ReadFailureIsObservedAndCompletesAsAFallback()
    {
        Fixture((loader, fixture) =>
        {
            string path = Path.Combine(fixture, "locked.png");
            using var locked = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            loader.Request(path);
            PumpUntil(loader, () => loader.TryGet(path, out _, out bool done) && done);
            Assert.True(loader.TryGet(path, out var texture, out bool finished));
            Assert.True(finished);
            Assert.Null(texture);
        });
    }

    private static void PumpUntil(PngPreloader loader, Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.Elapsed.TotalSeconds < 10)
        {
            int before = Texture2D.RoomDecodeCount;
            loader.Update();
            Assert.InRange(Texture2D.RoomDecodeCount - before, 0, 1);
            if (!done()) Thread.Sleep(1);
        }
        Assert.True(done(), "preload did not complete");
    }

    private static void Fixture(Action<PngPreloader, string> test)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_png_warmup_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        try
        {
            Directory.CreateDirectory(fixture);
            test(new PngPreloader(), fixture);
        }
        finally
        {
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
