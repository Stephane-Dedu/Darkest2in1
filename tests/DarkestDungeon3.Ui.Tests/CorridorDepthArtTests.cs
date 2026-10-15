using System.Diagnostics;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CorridorDepthArtTests
{
    // Header fixtures test loader scheduling and lifetime, not native Unity PNG decoding.
    private static byte[] Header()
    {
        var png = new byte[33];
        new byte[] { 137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82 }.CopyTo(png,0);
        png[18] = 16; png[22] = 3; png[24] = 8; png[25] = 6;
        return png;
    }

    private static void Write(string folder, string scene)
    {
        for (int i = 0; i < PrivateCorridorLayers.Count; i++)
            File.WriteAllBytes(Path.Combine(folder, PrivateCorridorLayers.FileName(scene,i)), Header());
    }

    [Fact]
    public void OnlyOneCompleteCorridorLivesOnTheMainThreadAndSwitchingReleasesIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-corridor-depth-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        const string first = "dd2_city-corridor-01.png", second = "dd2_city-corridor-02.png";
        var destroyed = new List<object>();
        try
        {
            Write(folder, first); Write(folder, second);
            CorridorDepthArt.Configure(folder);
            Texture2D.ApiThreads.Clear(); Texture2D.RoomDecodeCount = 0; UnityEngine.Object.ObserveDestroy = destroyed.Add;
            Time.unscaledTime = 10;
            Assert.Null(CorridorDepthArt.Request(first, out _));
            Assert.Equal(0, Texture2D.RoomDecodeCount);
            Texture2D[] layers = null;
            var watch = Stopwatch.StartNew();
            while (layers == null && watch.Elapsed.TotalSeconds < 5)
            {
                int before = Texture2D.RoomDecodeCount;
                CorridorDepthArt.Update();
                Assert.InRange(Texture2D.RoomDecodeCount-before,0,1);
                layers = CorridorDepthArt.Request(first, out _);
                Thread.Sleep(1);
            }
            Assert.NotNull(layers); Assert.Equal(3, Texture2D.RoomDecodeCount);
            CorridorDepthArt.Request(first, out float start); Assert.Equal(0,start);
            Time.unscaledTime += 1;
            Assert.Same(layers,CorridorDepthArt.Request(first,out float end)); Assert.Equal(1,end);
            Assert.Null(CorridorDepthArt.Request(second,out _));
            Assert.Empty(destroyed); // Request never destroys or allocates during OnGUI.
            CorridorDepthArt.Update();
            Assert.All(layers, layer => Assert.Contains(layer,destroyed));
            watch.Restart();
            Texture2D[] next = null;
            while (next == null && watch.Elapsed.TotalSeconds < 5)
            { CorridorDepthArt.Update(); next = CorridorDepthArt.Request(second,out _); Thread.Sleep(1); }
            Assert.NotNull(next); Assert.Equal(6,Texture2D.RoomDecodeCount);
            CorridorDepthArt.Clear();
            Assert.All(next,layer => Assert.Contains(layer,destroyed));
            Assert.All(Texture2D.ApiThreads,thread => Assert.Equal(Environment.CurrentManagedThreadId,thread));
        }
        finally { CorridorDepthArt.Clear(); Directory.Delete(folder,true); UnityEngine.Object.ObserveDestroy = null; }
    }

    [Fact]
    public void MissingCorruptAndDiscardedSetsNeverExposePartialDepths()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-corridor-depth-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        const string scene = "dd2_forest-corridor-01.png";
        try
        {
            Write(folder,scene);
            File.WriteAllBytes(Path.Combine(folder,PrivateCorridorLayers.FileName(scene,2)),new byte[] { 1,2,3 });
            CorridorDepthArt.Configure(folder); Texture2D.RoomDecodeCount = 0;
            CorridorDepthArt.Request(scene,out _);
            for (int i = 0; i < 100; i++) { CorridorDepthArt.Update(); Thread.Sleep(1); }
            Assert.Null(CorridorDepthArt.Request(scene,out _)); Assert.Equal(0,Texture2D.RoomDecodeCount);
            Write(folder,scene); // A broken set stays on the complete plate until a new request/region load.
            for (int i = 0; i < 5; i++) CorridorDepthArt.Update();
            Assert.Equal(0,Texture2D.RoomDecodeCount);
            CorridorDepthArt.Clear(); CorridorDepthArt.Configure(folder);
            CorridorDepthArt.Request(scene,out _); CorridorDepthArt.Update(); CorridorDepthArt.Clear();
            Thread.Sleep(20); CorridorDepthArt.Update();
            Assert.Equal(0,Texture2D.RoomDecodeCount);
            CorridorDepthArt.Configure(folder);
            CorridorDepthArt.Request("dd2_forest-corridor-02.png",out _);
            for (int i = 0; i < 20; i++) { CorridorDepthArt.Update(); Thread.Sleep(1); }
            Assert.Null(CorridorDepthArt.Request("dd2_forest-corridor-02.png",out _));
            Assert.Equal(0,Texture2D.RoomDecodeCount);
        }
        finally { CorridorDepthArt.Clear(); Directory.Delete(folder,true); }
    }

    [Fact]
    public void FailedThirdUploadReleasesBothEarlierLayersAndKeepsTheFallback()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-corridor-depth-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        const string scene = "dd2_coast-corridor-01.png";
        var destroyed = new List<object>();
        try
        {
            Write(folder,scene); CorridorDepthArt.Configure(folder);
            Texture2D.RoomDecodeCount = 0; UnityEngine.Object.ObserveDestroy = destroyed.Add;
            Texture2D.RoomDecodeResult = _ => Texture2D.RoomDecodeCount != 3;
            CorridorDepthArt.Request(scene,out _);
            var watch = Stopwatch.StartNew();
            while (Texture2D.RoomDecodeCount < 3 && watch.Elapsed.TotalSeconds < 5)
            { CorridorDepthArt.Update(); Thread.Sleep(1); }
            Assert.Equal(3,Texture2D.RoomDecodeCount);
            Assert.Null(CorridorDepthArt.Request(scene,out _));
            Assert.Equal(3,destroyed.Count);
        }
        finally
        {
            Texture2D.RoomDecodeResult = null; CorridorDepthArt.Clear();
            Directory.Delete(folder,true); UnityEngine.Object.ObserveDestroy = null;
        }
    }

    [Fact]
    public void RealRegionLoaderKeepsCompanionsOutOfTheVariantPoolAndSharesTheUploadBudget()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-corridor-depth-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        const string scene = "dd2_city-corridor-01.png";
        var config = Plugin.NativeRoomSceneryPath.Value;
        try
        {
            Write(folder,scene); File.WriteAllBytes(Path.Combine(folder,scene),Header());
            Plugin.NativeRoomSceneryPath.Value = folder;
            RegionSceneryArt.Clear(); Texture2D.RoomDecodeCount = 0;
            var plan = RegionalScenery.For("dd2_city");
            Texture2D[] layers = null;
            var watch = Stopwatch.StartNew();
            while (layers == null && watch.Elapsed.TotalSeconds < 5)
            {
                int before = Texture2D.RoomDecodeCount;
                RegionSceneryArt.Prepare(plan.Region);
                Assert.InRange(Texture2D.RoomDecodeCount-before,0,1);
                var choice = RegionSceneryArt.CorridorChoice(plan,42,0);
                layers = RegionSceneryArt.CorridorLayers(choice?.FileName,out _);
                Thread.Sleep(1);
            }
            Assert.NotNull(layers);
            for (int id = 0; id < 12; id++)
                Assert.Equal(scene,RegionSceneryArt.CorridorChoice(plan,42,id).FileName);
            Assert.Null(RegionSceneryArt.RoomTextureFor(PrivateCorridorLayers.FileName(scene,0)));
            RegionSceneryArt.Prepare("crypts");
            Assert.Null(RegionSceneryArt.CorridorLayers(scene,out _));
        }
        finally { RegionSceneryArt.Clear(); Plugin.NativeRoomSceneryPath.Value = config; Directory.Delete(folder,true); }
    }
}
