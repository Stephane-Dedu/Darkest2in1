using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Tests;

public class ZoneBossArtTests
{
    [Fact]
    public void PlotFinalWallWinsWithoutBorrowingAnotherPlotsArt()
    {
        var art = new ZoneArt();
        art.FinalRooms.AddRange(new[] { "weald.final_room_wall.plot_crow_trinket.png", "weald.final_room_wall.png",
            "weald.final_room_wall.plot_trinket_retention_1.png" });
        art.Rooms.Add("weald.room_wall.basic.png");
        Assert.Equal(art.FinalRooms[2], art.BossRoom("brigand_16pounder_A", "plot_trinket_retention_1"));
        Assert.Equal(art.FinalRooms[1], art.BossRoom("hag_A", "missing_plot"));
        art.FinalRooms.RemoveAt(1);
        Assert.Equal(art.Rooms[0], art.BossRoom("hag_A", "missing_plot"));
    }

    [Fact]
    public void ExactBossAndFamilyArtPrecedeGenericFinalWall()
    {
        var art = new ZoneArt();
        art.FinalRooms.AddRange(new[] { "crypts.final_room_wall.png", "crypts.final_room_wall.necromancer.png",
            "crypts.final_room_wall.necromancer_B.png" });
        Assert.Equal(art.FinalRooms[2], art.BossRoom("necromancer_B"));
        Assert.Equal(art.FinalRooms[1], art.BossRoom("necromancer_A"));
    }

    [Fact]
    public void InstalledNecromancerFallbackIsLibraryAndNeverEntrance()
    {
        var art = ZoneArt.Load(Dd1Install.Find(), "crypts");
        foreach (string tier in new[] { "A", "B", "C" })
        {
            string wall = art.BossRoom("necromancer_" + tier);
            Assert.True(File.Exists(wall));
            Assert.EndsWith("crypts.room_wall.library.png", wall);
            Assert.NotEqual(art.Entrance, wall);
        }
    }

    [Fact]
    public void InstalledPlotSpecificFinalArtRetainsExactIdentity()
    {
        var art = ZoneArt.Load(Dd1Install.Find(), "weald");
        string wall = art.BossRoom("shrieker_A", "plot_crow_trinket");
        Assert.True(File.Exists(wall));
        Assert.EndsWith("weald.final_room_wall.plot_crow_trinket.png", wall);
    }
}
