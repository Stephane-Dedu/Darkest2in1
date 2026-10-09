using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class CorridorSceneryTests
{
    [Theory]
    [InlineData("dd2_city")]
    [InlineData("dd2_farm")]
    [InlineData("dd2_forest")]
    [InlineData("dd2_coast")]
    public void CorridorsHaveIndependentStableDiverseScenes(string region)
    {
        var plan = RegionalScenery.For(region);
        foreach (int seed in new[] { int.MinValue, -1, 0, 2719, int.MaxValue })
        foreach (int count in new[] { 1, 2, 12 })
        {
            var files = Enumerable.Range(1, count).Select(i => $"{region}-corridor-{i:00}.png").ToArray();
            var scenes = Enumerable.Range(0, count * 2).Select(id => plan.CorridorBackground(seed, id, files)).ToArray();
            Assert.Equal(count, scenes.Take(count).Select(s => s.FileName).Distinct().Count());
            Assert.Equal(count * 2, scenes.Select(s => (s.FileName, s.Mirror)).Distinct().Count());
            foreach (int id in Enumerable.Range(0, scenes.Length))
            {
                var reload = RegionalScenery.For(region).CorridorBackground(seed, id, files.ToArray());
                Assert.Equal((scenes[id].FileName, scenes[id].Mirror), (reload.FileName, reload.Mirror));
                Assert.StartsWith(region + "-corridor-", reload.FileName);
                if (id > 0 && count > 1) Assert.NotEqual(scenes[id - 1].FileName, reload.FileName);
            }
            Assert.Null(plan.CorridorBackground(seed, -1, files));
            Assert.Null(plan.CorridorBackground(seed, 0, Array.Empty<string>()));
        }
    }

    [Fact]
    public void DiscoverySeparatesRegionsRoomsAndCorridorsAndCapsBothPools()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-corridor-art-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var region in CampaignRegions.Primary)
            foreach (var kind in new[] { "arena", "corridor" })
            foreach (int i in Enumerable.Range(1, 15).Reverse())
                File.WriteAllText(Path.Combine(folder, $"{region}-{kind}-{i:00}.png"), "");
            foreach (string bad in new[] { "00", "1", "999", "xx" })
                File.WriteAllText(Path.Combine(folder, "dd2_city-corridor-" + bad + ".png"), "");
            foreach (var region in CampaignRegions.Primary)
            {
                var hall = PrivateRoomScenery.FindCorridors(folder, region);
                Assert.Equal(Enumerable.Range(1, 12).Select(i => $"{region}-corridor-{i:00}.png"), hall);
                Assert.Empty(hall.Intersect(PrivateRoomScenery.Find(folder, region)));
            }
            Assert.Empty(PrivateRoomScenery.FindCorridors(folder, "crypts"));
            Assert.Empty(PrivateRoomScenery.FindCorridors(folder, "../../dd2_city"));
            Assert.Empty(PrivateRoomScenery.FindCorridors(folder + "-missing", "dd2_city"));
            Assert.Empty(PrivateRoomScenery.FindCorridors(null!, "dd2_city"));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(folder));
            Directory.Delete(folder, true);
        }
    }
}
