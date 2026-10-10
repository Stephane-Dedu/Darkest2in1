using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class PrivateRoomSceneryTests
{
    [Fact]
    public void OptionalPackDiscoveryIsRegionalOrderedAndBounded()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-room-art-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            foreach (int i in Enumerable.Range(1, 24).Reverse())
                File.WriteAllBytes(Path.Combine(folder, $"dd2_city-arena-{i:00}.png"), Array.Empty<byte>());
            foreach (string name in new[] { "dd2_farm-arena-01.png", "dd2_city-arena-00.png", "dd2_city-arena-1.png", "dd2_city-arena-evil.png", "dd2_city-arena-25.png.bak" })
                File.WriteAllBytes(Path.Combine(folder, name), Array.Empty<byte>());
            Directory.CreateDirectory(Path.Combine(folder, "nested"));
            File.WriteAllBytes(Path.Combine(folder, "nested/dd2_city-arena-25.png"), Array.Empty<byte>());
            Assert.Equal(Enumerable.Range(1, PrivateRoomScenery.MaxVariants).Select(i => $"dd2_city-arena-{i:00}.png"), PrivateRoomScenery.Find(folder, "dd2_city"));
            Assert.Single(PrivateRoomScenery.Find(folder, "dd2_farm"));
            Assert.Empty(PrivateRoomScenery.Find(folder, "crypts"));
            Assert.Empty(PrivateRoomScenery.Find(folder, "../../dd2_city"));
            Assert.Empty(PrivateRoomScenery.Find(folder + "-missing", "dd2_city"));
            Assert.Empty(PrivateRoomScenery.Find(null!, "dd2_city"));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(folder));
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LargerSetsVisitEverySceneBeforeRepeatingAndStayStableAfterReload()
    {
        foreach (int count in new[] { 1, 6, PrivateRoomScenery.MaxVariants })
        foreach (int seed in new[] { int.MinValue, -1, 0, 2719, int.MaxValue })
        {
            var plan = RegionalScenery.For("dd2_city");
            var files = Enumerable.Range(1, count).Select(i => $"dd2_city-arena-{i:00}.png").ToArray();
            var choices = Enumerable.Range(0, count * 2).Select(room => plan.RoomBackground(seed, room, files)).ToArray();
            Assert.Equal(count, choices.Take(count).Select(c => c.FileName).Distinct().Count());
            Assert.Equal(count * 2, choices.Select(c => (c.FileName, c.Mirror)).Distinct().Count());
            for (int room = 0; room < choices.Length; room++)
            {
                var reload = RegionalScenery.For("dd2_city").RoomBackground(seed, room, files.ToArray());
                Assert.Equal(choices[room].FileName, reload.FileName);
                Assert.Equal(choices[room].Mirror, reload.Mirror);
                if (room > 0 && count > 1) Assert.NotEqual(choices[room - 1].FileName, choices[room].FileName);
            }
            Assert.Null(plan.RoomBackground(seed, -1, files));
            Assert.Null(plan.RoomBackground(seed, 0, Array.Empty<string>()));
            Assert.Null(plan.RoomBackground(seed, 0, null!));
        }
    }

    [Fact]
    public void PngBoundsAreCheckedBeforeDecodeIncludingUnsignedDimensions()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data/scenery"));
        var data = File.ReadAllBytes(Path.Combine(folder, "dd2_city-01.png"));
        Assert.True(PrivateRoomScenery.ValidPng(data));
        Assert.False(PrivateRoomScenery.ValidPng(data.Take(32).ToArray()));
        var signature = (byte[])data.Clone(); signature[0] = 0;
        Assert.False(PrivateRoomScenery.ValidPng(signature));
        var width = (byte[])data.Clone(); width[16] = 255;
        Assert.False(PrivateRoomScenery.ValidPng(width));
        var aspect = (byte[])data.Clone(); aspect[20] = 0; aspect[21] = 0; aspect[22] = 4; aspect[23] = 0;
        Assert.False(PrivateRoomScenery.ValidPng(aspect));
        var chunk = (byte[])data.Clone(); chunk[12] = (byte)'X';
        Assert.False(PrivateRoomScenery.ValidPng(chunk));
        var indexed = (byte[])data.Clone(); indexed[25] = 3;
        Assert.False(PrivateRoomScenery.ValidPng(indexed));
        Assert.False(PrivateRoomScenery.ValidPng(null!));
        Assert.False(PrivateRoomScenery.ValidPng(new byte[PrivateRoomScenery.MaxFileBytes + 1]));
    }
}
