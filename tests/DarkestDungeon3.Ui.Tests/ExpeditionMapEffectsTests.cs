using System.Diagnostics;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests
{
    // The existing Unity shim checks actual loader scheduling/ownership, not GPU image decoding.
    public sealed class ExpeditionMapEffectsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "dd3-map-vfx-" + Guid.NewGuid());
        private readonly string _old = Plugin.ExpeditionArtPath.Value;
        public ExpeditionMapEffectsTests()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "vfx"));
            ExpeditionMapEffects.Clear(); Plugin.ExpeditionArtPath.Value = _folder;
            Texture2D.ApiThreads.Clear(); Texture2D.RoomDecodeCount = 0; Time.frameCount = 100;
        }

        [Fact]
        public void UploadsOncePerFrameSharesAtlasAndReleasesAfterLeavingTheMap()
        {
            WritePack("cloud.png");
            File.WriteAllBytes(Path.Combine(_folder, "vfx", "cloud.png"), Header(1024, 1024));
            File.WriteAllBytes(Path.Combine(_folder, "vfx", "mist.png"), Header(512, 512));
            ExpeditionMapEffects.Prepare();
            Assert.Equal(0, Texture2D.RoomDecodeCount); // File IO worker owns no Unity calls.
            Ready(3);
            Assert.Equal(2, Texture2D.RoomDecodeCount);
            Assert.Equal(2, ExpeditionMapEffects.TextureCount);
            Assert.True(ExpeditionMapEffects.TryGet("gas", out _, out var gas));
            Assert.True(ExpeditionMapEffects.TryGet("forest", out _, out var forest));
            Assert.Same(gas, forest);
            Time.frameCount += 3; ExpeditionMapEffects.ReleaseUnused();
            Assert.Equal(2, ExpeditionMapEffects.TextureCount);
            Time.frameCount++; ExpeditionMapEffects.ReleaseUnused();
            Assert.Equal(0, ExpeditionMapEffects.TextureCount);
            Assert.False(ExpeditionMapEffects.TryGet("gas", out _, out _));
            Ready(3); // Re-entry must reload instead of keeping a destroyed texture.
            Assert.True(ExpeditionMapEffects.TryGet("gas", out _, out var reloaded));
            Assert.NotSame(gas, reloaded);
            Assert.All(Texture2D.ApiThreads, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
        }

        [Theory]
        [InlineData(4096, 1024)]
        [InlineData(1024, 4096)]
        [InlineData(1, 1024)]
        public void OversizedOrInvalidImagesNeverReachUnity(int width, int height)
        {
            WritePack("cloud.png");
            File.WriteAllBytes(Path.Combine(_folder, "vfx", "cloud.png"), Header(width, height));
            for (int i = 0; i < 50; i++) { Time.frameCount++; ExpeditionMapEffects.Prepare(); Thread.Sleep(1); }
            Assert.Equal(0, Texture2D.RoomDecodeCount);
            Assert.Equal(0, ExpeditionMapEffects.ReadyCount);
        }

        [Fact]
        public void FolderSwitchDiscardsOutstandingWorkAndMissingPackIsHarmless()
        {
            WritePack("cloud.png");
            File.WriteAllBytes(Path.Combine(_folder, "vfx", "cloud.png"), Header(256, 256));
            ExpeditionMapEffects.Prepare();
            Plugin.ExpeditionArtPath.Value = _folder + "-missing";
            for (int i = 0; i < 50; i++) { Time.frameCount++; ExpeditionMapEffects.Prepare(); Thread.Sleep(1); }
            Assert.Equal(0, Texture2D.RoomDecodeCount);
            Assert.Equal(0, ExpeditionMapEffects.TextureCount);
            Plugin.ExpeditionArtPath.Value = _folder;
            Ready(2);
        }

        private void Ready(int count)
        {
            var watch = Stopwatch.StartNew();
            while (ExpeditionMapEffects.ReadyCount < count && watch.Elapsed.TotalSeconds < 5)
            {
                Time.frameCount++;
                int before = Texture2D.RoomDecodeCount;
                ExpeditionMapEffects.Prepare(); ExpeditionMapEffects.Prepare();
                Assert.InRange(Texture2D.RoomDecodeCount - before, 0, 1);
                Thread.Sleep(1);
            }
            Assert.Equal(count, ExpeditionMapEffects.ReadyCount);
        }
        private void WritePack(string file) => File.WriteAllText(Path.Combine(_folder, "vfx", "vfx-manifest.json"),
            "{\"effects\":[{\"id\":\"gas\",\"file\":\"" + file + "\",\"columns\":2,\"rows\":2,\"frames\":4,\"fps\":0}," +
            "{\"id\":\"forest\",\"file\":\"" + file + "\",\"columns\":2,\"rows\":2,\"frames\":4,\"fps\":0}," +
            "{\"id\":\"mist\",\"file\":\"mist.png\",\"columns\":1,\"rows\":1,\"frames\":1,\"fps\":0}]}");
        private static byte[] Header(int width, int height)
        {
            var data = new byte[33];
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(data, 0);
            new byte[] { 73, 72, 68, 82 }.CopyTo(data, 12);
            for (int i = 0; i < 4; i++) { data[16 + i] = (byte)(width >> (24 - i * 8)); data[20 + i] = (byte)(height >> (24 - i * 8)); }
            data[24] = 8; data[25] = 6;
            return data;
        }
        public void Dispose()
        {
            ExpeditionMapEffects.Clear(); Plugin.ExpeditionArtPath.Value = _old;
            Directory.Delete(_folder, recursive: true);
        }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type target, string method) { _ = target; _ = method; }
    }
}
