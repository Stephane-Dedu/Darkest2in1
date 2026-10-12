using System.Diagnostics;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class RegionSceneryArtTests
{
    [Fact]
    public void NativeRolePackIsAdoptedAfterDecodeAndSkippedBossUsesBattleWithoutARevisitSwap()
    {
        string pack = Path.Combine(Path.GetTempPath(), "dd3-role-runtime-" + Guid.NewGuid());
        Directory.CreateDirectory(pack);
        var oldConfig = Plugin.NativeRoomSceneryPath.Value;
        var png = File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data/scenery/dd2_city-01.png")));
        try
        {
            File.WriteAllBytes(Path.Combine(pack, "dd2_city-arena-boss-01.png"), new byte[] { 1, 2, 3 });
            foreach (string role in new[] { "battle", "quiet" })
                File.WriteAllBytes(Path.Combine(pack, $"dd2_city-arena-{role}-01.png"), png);
            File.WriteAllText(Path.Combine(pack,"scenery-emitters.json"),"{\"scenes\":{\"dd2_city-arena-battle-01.png\":[[300,420,80,160]]}}");
            Plugin.NativeRoomSceneryPath.Value = pack;
            RegionSceneryArt.Clear(); Time.unscaledTime = 10;
            var plan = RegionalScenery.For("dd2_city");
            var watch = Stopwatch.StartNew();
            while (RegionSceneryArt.RoomSceneFiles(plan.Region).Count != 2 && watch.Elapsed.TotalSeconds < 10)
            { RegionSceneryArt.Prepare(plan.Region); Thread.Sleep(1); }
            Assert.Equal(2, RegionSceneryArt.RoomSceneFiles(plan.Region).Count);
            Assert.Single(RegionSceneryArt.FireBounds("dd2_city-arena-battle-01.png"));
            var room = new Room { Id = 3, Content = RoomContent.Boss };
            var before = RegionSceneryArt.RoomChoice(plan, 7, room);
            Assert.Equal("dd2_city-arena-battle-01.png", before.FileName); Assert.False(before.Mirror);
            room.Cleared = true;
            for (int tick = 0; tick < 10; tick++) RegionSceneryArt.Prepare(plan.Region);
            Assert.Equal(before.FileName, RegionSceneryArt.RoomChoice(plan, 7, room).FileName);
            Assert.Empty(RegionSceneryArt.RoomSceneFiles("dd2_farm"));
            RegionSceneryArt.Clear();
            Assert.Empty(RegionSceneryArt.RoomSceneFiles(plan.Region));
            Assert.Null(RegionSceneryArt.RoomTextureFor(before.FileName));
            Assert.Empty(RegionSceneryArt.FireBounds("dd2_city-arena-battle-01.png"));
        }
        finally
        {
            RegionSceneryArt.Clear(); Plugin.NativeRoomSceneryPath.Value = oldConfig;
            Directory.Delete(pack, recursive: true);
        }
    }

    [Fact]
    public void RealLoaderAdoptsACompletedPackSkipsBrokenFilesAndReleasesOnRegionExit()
    {
        string pack = Path.Combine(Path.GetTempPath(), "dd3-scenery-runtime-" + Guid.NewGuid());
        Directory.CreateDirectory(pack);
        string data = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "data/scenery");
        Directory.CreateDirectory(data);
        var originals = new Dictionary<string, byte[]?>();
        var oldConfig = Plugin.NativeRoomSceneryPath.Value;
        var png = File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data/scenery/dd2_city-01.png")));
        try
        {
            foreach (string file in RegionalScenery.For("dd2_city").RoomBackgrounds)
            {
                string target = Path.Combine(data, file);
                originals[target] = File.Exists(target) ? File.ReadAllBytes(target) : null;
                File.WriteAllBytes(target, png);
            }
            foreach (int i in Enumerable.Range(1, 7))
                File.WriteAllBytes(Path.Combine(pack, $"dd2_city-arena-{i:00}.png"), i == 4 ? new byte[] { 1, 2, 3 } : png);
            foreach (int i in Enumerable.Range(1, 3))
                File.WriteAllBytes(Path.Combine(pack, $"dd2_city-corridor-{i:00}.png"), i == 2 ? new byte[] { 1, 2, 3 } : png);
            File.WriteAllBytes(Path.Combine(pack, "dd2_farm-corridor-01.png"), png);
            Plugin.NativeRoomSceneryPath.Value = pack;
            RegionSceneryArt.Clear();
            Texture2D.ApiThreads.Clear(); Texture2D.RoomDecodeCount = 0; Time.unscaledTime = 10;
            var plan = RegionalScenery.For("dd2_city");
            RegionSceneryArt.Prepare(plan.Region);
            Assert.Equal(0, Texture2D.RoomDecodeCount); // Only worker file IO was queued.
            Assert.Null(RegionSceneryArt.CorridorChoice(plan, 2719, 0));
            var watch = Stopwatch.StartNew();
            while (!RegionSceneryArt.PrivateRoom(RegionSceneryArt.RoomChoice(plan, 2719, 0)?.FileName) && watch.Elapsed.TotalSeconds < 10)
            {
                int before = Texture2D.RoomDecodeCount;
                RegionSceneryArt.Prepare(plan.Region);
                Assert.InRange(Texture2D.RoomDecodeCount - before, 0, 1);
                Thread.Sleep(1);
            }
            Assert.Equal(10, Texture2D.RoomDecodeCount); // Two fallback, six private rooms and two corridors, both bad headers skipped.
            var corridors = Enumerable.Range(0, 2).Select(id => RegionSceneryArt.CorridorChoice(plan, 2719, id)).ToArray();
            Assert.Equal(2, corridors.Select(c => c.FileName).Distinct().Count());
            Assert.All(corridors, c => Assert.StartsWith("dd2_city-corridor-", c.FileName));
            Assert.DoesNotContain(corridors, c => c.FileName.EndsWith("02.png"));
            Assert.All(corridors, c => Assert.NotNull(RegionSceneryArt.RoomTextureFor(c.FileName)));
            Assert.Equal(0, RegionSceneryArt.RoomAlpha(corridors[0].FileName));
            Assert.Null(RegionSceneryArt.CorridorChoice(RegionalScenery.For("dd2_farm"), 2719, 0));
            var choices = Enumerable.Range(0, 6).Select(room => RegionSceneryArt.RoomChoice(plan, 2719, room)).ToArray();
            Assert.Equal(6, choices.Select(c => c.FileName).Distinct().Count());
            Assert.DoesNotContain(choices, c => c.FileName == "dd2_city-arena-04.png");
            Assert.All(choices, c => Assert.NotNull(RegionSceneryArt.RoomTextureFor(c.FileName)));
            Assert.Equal(0, RegionSceneryArt.RoomAlpha(choices[0].FileName));
            Time.unscaledTime += 1;
            Assert.Equal(1, RegionSceneryArt.RoomAlpha(choices[0].FileName));
            Assert.Equal(1, RegionSceneryArt.RoomAlpha(corridors[0].FileName));
            Assert.All(Texture2D.ApiThreads, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
            RegionSceneryArt.Clear();
            Assert.Equal(0, Addressables.LiveHandles);
            Assert.All(choices, c => Assert.Null(RegionSceneryArt.RoomTextureFor(c.FileName)));
            Assert.All(corridors, c => Assert.Null(RegionSceneryArt.RoomTextureFor(c.FileName)));
            Assert.Null(RegionSceneryArt.CorridorChoice(plan, 2719, 0));
            Assert.Contains(RegionSceneryArt.RoomChoice(plan, 2719, 0).FileName, plan.RoomBackgrounds);
            Plugin.NativeRoomSceneryPath.Value = pack + "-missing";
            RegionSceneryArt.Prepare(plan.Region);
            Assert.Contains(RegionSceneryArt.RoomChoice(plan, 2719, 0).FileName, plan.RoomBackgrounds);
            Assert.Null(RegionSceneryArt.CorridorChoice(plan, 2719, 0));
            RegionSceneryArt.Prepare("crypts");
            Assert.Equal(0, Addressables.LiveHandles);
            RegionSceneryArt.Prepare(plan.Region);
            RegionSceneryArt.Clear(); // Discard an outstanding IO task; it owns no Unity resources.
            Thread.Sleep(20);
            Assert.Equal(0, Addressables.LiveHandles);
            Assert.Null(RegionSceneryArt.RoomTextureFor(choices[0].FileName));
        }
        finally
        {
            RegionSceneryArt.Clear(); Plugin.NativeRoomSceneryPath.Value = oldConfig;
            foreach (var original in originals)
                if (original.Value != null) File.WriteAllBytes(original.Key, original.Value); else File.Delete(original.Key);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(pack));
            Directory.Delete(pack, recursive: true);
        }
    }
}
