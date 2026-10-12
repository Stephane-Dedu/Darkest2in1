using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Tests;

public class NativeRoomSceneryTests
{
    private static readonly string[] Files = { "dd2_city-arena-boss-01.png", "dd2_city-arena-entrance-01.png",
        "dd2_city-arena-battle-01.png", "dd2_city-arena-battle-02.png", "dd2_city-arena-quiet-01.png", "dd2_city-arena-treasure-01.png" };

    [Theory]
    [InlineData(RoomContent.Boss, false, "boss")]
    [InlineData(RoomContent.Entrance, false, "entrance")]
    [InlineData(RoomContent.Battle, false, "battle")]
    [InlineData(RoomContent.GuardedCurio, false, "battle")]
    [InlineData(RoomContent.GuardedTreasure, false, "battle")]
    [InlineData(RoomContent.Curio, false, "quiet")]
    [InlineData(RoomContent.Empty, false, "quiet")]
    [InlineData(RoomContent.Treasure, false, "treasure")]
    [InlineData(RoomContent.Empty, true, "treasure")]
    public void PurposeSelectsNativeArenaAndClearingDoesNotChangeItsIdentity(RoomContent content, bool secret, string role)
    {
        var plan = RegionalScenery.For("dd2_city");
        foreach (int seed in new[] { int.MinValue, -1, 0, 2719, int.MaxValue })
        {
            var room = new Room { Id = 7, Content = content, IsSecret = secret };
            var before = PrivateRoomScenery.Choose(plan, seed, room, Files);
            Assert.Equal(role, PrivateRoomScenery.SceneRole(before.FileName, plan.Region));
            Assert.False(before.Mirror);
            room.Cleared = room.Visited = room.CurioTaken = true;
            var after = PrivateRoomScenery.Choose(plan, seed, room, Files.ToArray());
            Assert.Equal(before.FileName, after.FileName); Assert.Equal(before.Mirror, after.Mirror);
        }
    }

    [Fact]
    public void IncompletePackFallsBackWithoutUsingAnotherRegionsArenaOrOrdinaryRoomAsBoss()
    {
        var plan = RegionalScenery.For("dd2_city");
        var boss = new Room { Id = 3, Content = RoomContent.Boss };
        Assert.Equal("battle", PrivateRoomScenery.SceneRole(PrivateRoomScenery.Choose(plan, 0, boss,
            Files.Where(f => !f.Contains("-boss-")).ToArray()).FileName, plan.Region));
        Assert.Contains(PrivateRoomScenery.Choose(plan, 0, boss, new[] { "dd2_city-arena-quiet-01.png", "dd2_farm-arena-boss-01.png" }).FileName,
            plan.RoomBackgrounds);
        Assert.Equal("dd2_city-arena-01.png", PrivateRoomScenery.Choose(plan, 0, boss, new[] { "dd2_city-arena-01.png" }).FileName);
    }

    [Fact]
    public void DiscoveryPrioritizesRolePackOverLegacyFilesAndRejectsNonCanonicalPaths()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-native-rooms-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            foreach (string file in Files.Concat(Enumerable.Range(1, 24).Select(i => $"dd2_city-arena-{i:00}.png"))
                .Concat(new[] { "dd2_city-arena-boss-00.png", "dd2_city-arena-boss-1.png", "dd2_city-arena-evil-01.png", "dd2_farm-arena-boss-01.png" }))
                File.WriteAllBytes(Path.Combine(folder, file), Array.Empty<byte>());
            var found = PrivateRoomScenery.Find(folder, "dd2_city");
            Assert.Equal(PrivateRoomScenery.MaxVariants, found.Count);
            Assert.All(Files, file => Assert.Contains(file, found));
            Assert.DoesNotContain(found, file => file.Contains("evil") || file.Contains("farm") || file.EndsWith("boss-00.png") || file.EndsWith("boss-1.png"));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
