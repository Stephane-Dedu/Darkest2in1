using System.Diagnostics;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class SpineArtTests
{
    [Fact]
    public void FirstRequestQueuesTheBakeAndUnityApisStayOnThePumpThread()
    {
        Texture2D.ApiThreads.Clear();
        var folder = TownLayout.ArtFolder(Dd1Install.Find(), "stage_coach", true, 0);
        const string variant = "test-idle";
        var first = SpineArt.Get(folder, variant, s => TownLayout.IdleSlot(s.Name));
        Assert.Null(first);
        Assert.Empty(Texture2D.ApiThreads); // no synchronous native decode/raster/upload in OnGUI
        var pump = typeof(SpineArt).GetMethod("Update");
        Assert.NotNull(pump);
        var watch = Stopwatch.StartNew();
        SpineArt.Picture picture = null;
        while (picture == null && watch.Elapsed.TotalSeconds < 10)
        {
            pump.Invoke(null, null);
            picture = SpineArt.Get(folder, variant, s => TownLayout.IdleSlot(s.Name));
            if (picture == null) Thread.Sleep(2);
        }
        Assert.NotNull(picture);
        Assert.NotEmpty(Texture2D.ApiThreads);
        Assert.All(Texture2D.ApiThreads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        Assert.Same(picture, SpineArt.Get(folder, variant, s => throw new Exception("cached predicate must not run")));

        // Upload rows are reversed; CPU rows remain top-down for hit testing.
        var image = picture.Pixels;
        int stride = image.Width * 4;
        Assert.Equal(image.Pixels.Take(stride), picture.Texture.Raw.Skip((image.Height - 1) * stride));
        int opaque = Enumerable.Range(0, image.Width * image.Height).First(i => image.Pixels[i * 4 + 3] > 40);
        var origin = new Vector2(500, 500);
        var point = new Vector2(500 - picture.Pivot.x + opaque % image.Width + 0.5f,
                               500 - picture.Pivot.y + opaque / image.Width + 0.5f);
        Assert.True(picture.Hit(origin, 1, point));
        Assert.False(picture.Hit(origin, 1, new Vector2(-100, -100)));
    }
}
