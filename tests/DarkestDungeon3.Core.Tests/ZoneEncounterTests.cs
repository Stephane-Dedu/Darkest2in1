using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ZoneEncounterTests
{
    private static readonly string ZonesJson = Path.GetFullPath(Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "zones.json"));
    private static readonly ZoneEncounters Zones = ZoneEncounters.Load(ZonesJson);
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    [Fact]
    public void EveryDd1ZoneHasFights()
    {
        foreach (var zone in new[] { "crypts", "weald", "warrens", "cove", "darkestdungeon" })
            foreach (int difficulty in new[] { 1, 3, 5, 6 })
                foreach (var kind in new[] { FightKind.Hall, FightKind.Room, FightKind.CampAmbush })
                {
                    var plan = Zones.Plan(zone, difficulty, kind, new Rng(difficulty));
                    Assert.False(string.IsNullOrEmpty(plan.Battle), $"{zone} {difficulty} {kind}");
                    Assert.NotEmpty(plan.Arenas);
                }
    }

    [Fact]
    public void DifficultyPicksTheRightTier()
    {
        Assert.Equal("table:lost_battalion_mashes_normal", Zones.Plan("crypts", 1, FightKind.Hall, new Rng(1)).Battle);
        Assert.Equal("table:lost_battalion_mashes_hard", Zones.Plan("crypts", 3, FightKind.Hall, new Rng(1)).Battle);
        Assert.Equal("table:lost_battalion_mashes_hard_champions", Zones.Plan("crypts", 5, FightKind.Hall, new Rng(1)).Battle);
        Assert.Equal("table:swine_mashes_normal", Zones.Plan("warrens", 1, FightKind.Room, new Rng(1)).Battle);
    }

    [Fact]
    public void EveryDd1BossHasADd2Fight()
    {
        foreach (var plot in Dd1.Goals.Plot.Where(p => p.Type == "kill_boss" && p.Progression))
        {
            var goal = Dd1.Goals.Goals[plot.GoalIds[0]];
            string boss = goal.MonsterClasses.First();
            var plan = Zones.Plan(plot.Dungeon, plot.Difficulty, FightKind.Boss, new Rng(1), boss);
            Assert.True(plan.Kind == FightKind.Boss && plan.Battle != null, $"{plot.Id}: {boss}");
            Assert.DoesNotContain("faction_mashes_road", plan.Battle);   // not the generic fallback
        }
        Assert.Equal("necromancer", ZoneEncounters.BossKey("necromancer_A"));
        Assert.Equal("ancestor_heart", ZoneEncounters.BossKey("ancestor_heart_D"));
    }
}
