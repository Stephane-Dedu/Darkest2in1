using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;

namespace DarkestDungeon3.Runtime;

internal static class FadedMemoryController
{
    public static void Place(ExpeditionState state)
    {
        // Only the audited regional boss is exposed until the other adapters can finish their encounters.
        if (ZoneBase.Of(state?.Quest?.Dungeon) == "crypts") FadedMemory.Place(state, "crypts", "necromancer_A", 1);
    }

    public static Dictionary<uint, Dd1HeroArt> HeroArt(ExpeditionState state, Dd2Party party)
    {
        var result = new Dictionary<uint, Dd1HeroArt>();
        foreach (string id in party.Alive)
        {
            string cls = Session.Current.Save.Estate.Hero(id)?.ClassId;
            // Explicit visual counterparts for the two DD2-exclusive classes; skills and actors stay DD2.
            string sprite = cls == "duelist" ? "grave_robber" : cls == "runaway" ? "houndmaster"
                : Session.Current.Campaign.HeroUpgrades.Dd1Class(cls);
            var art = Dd1HeroArt.Find(Session.Current.Dd1, sprite);
            if (art == null) return null;
            result[party.Guid(id)] = art;
            state.FadedMemory.HeroSprites[id] = sprite;
        }
        return result;
    }

    public static bool TryPlan(ExpeditionState state, Dd2Party party, out FightPlan plan, out Dictionary<uint, Dd1HeroArt> heroes)
    {
        plan = null; heroes = null;
        if (state?.FadedMemory is not { BossId: "necromancer_A", Dungeon: "crypts" } memory
            || !Dd1EnemyData.TryRegister("necromancer", 'A', out var enemy)
            || !Dd1EnemyData.TryRegister("skeleton_common", 'A', out _)
            || !Dd1EnemyData.TryRegister("skeleton_militia", 'A', out _)) return false;
        heroes = HeroArt(state, party);
        if (heroes == null || heroes.Count == 0) return false;
        plan = Session.Current.Zones.Plan(memory.Dungeon, memory.Difficulty, FightKind.Room, new Rng(state.Seed));
        plan.Enemies = new List<string> { enemy.Id };
        plan.NativePresentation = false;
        return true;
    }

    public static bool Ready(ExpeditionState state, Dd2Party party)
    {
        if (!TryPlan(state, party, out _, out _)) return false;
        var estate = Session.Current.Save.Estate;
        var owned = estate.Trinkets.Concat(estate.Roster.SelectMany(h => h.WornTrinkets))
            .Concat(state.Pack.Items.Where(p => p.Key.StartsWith("trinket:")).SelectMany(p => Enumerable.Repeat(p.Key.Substring(8), p.Value)));
        return FadedMemory.PrepareRewards(state.FadedMemory, Session.Current.MemoryTrinkets, owned, new Rng(state.Seed ^ 0x4d454d))
            && state.FadedMemory.Rewards.All(Dd1TrinketData.Register);
    }
}
