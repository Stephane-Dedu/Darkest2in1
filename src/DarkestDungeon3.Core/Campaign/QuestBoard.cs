using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>Builds the week's quest board from DD1's quest generation tables.</summary>
public static class QuestBoard
{
    private static readonly int[] Difficulties = { 1, 3, 5 };

    /// <param name="extraZones">Toggled-in zones (e.g. DD2 regions). They borrow the Ruins' quest table.</param>
    public static List<QuestOffer> Generate(Estate estate, Dd1Campaign dd1, IEnumerable<string> extraZones = null)
    {
        var rng = estate.NextRng();
        int progress = Math.Max(0, estate.QuestsCompleted);

        var zones = dd1.ZoneUnlocks.Where(z => estate.QuestsCompleted >= z.Value).Select(z => z.Key).ToList();
        if (extraZones != null) zones.AddRange(extraZones.Where(z => !zones.Contains(z)));
        if (zones.Count == 0) return new List<QuestOffer>();

        int count = At(dd1.QuestsPerVisit, progress, 2);
        var offers = new List<QuestOffer>();

        // Deal zones round-robin from a shuffled order, so every open zone gets work before any gets a second.
        var order = new List<string>(zones);
        rng.Shuffle(order);
        for (int i = 0; i < count; i++)
        {
            string zone = order[i % order.Count];
            var table = dd1.QuestTables.TryGetValue(zone, out var t) ? t : dd1.QuestTables.Values.First();
            var options = At(table, progress, table[table.Count - 1]);
            var (type, length) = PickWeighted(options, rng);

            int zoneLevel = dd1.ZoneLevel(estate.ZoneXp.TryGetValue(zone, out var xp) ? xp : 0);
            int difficulty = PickDifficulty(Dd1Campaign.MaxDifficultyForZoneLevel(zoneLevel), rng);

            offers.Add(Build(estate, dd1, zone, type, length, difficulty, rng));
        }
        return offers;
    }

    public static QuestOffer Build(Estate estate, Dd1Campaign dd1, string zone, string type, int length, int difficulty, Rng rng)
    {
        var quest = new QuestOffer
        {
            Id = Estate.NewId(),
            Dungeon = zone,
            Type = type,
            Length = length,
            Difficulty = difficulty,
            MapSeed = estate.NextSeed(),
        };

        int gold = dd1.Gold(difficulty, length);
        if (gold > 0) quest.Rewards.Add(new Reward(Currency.Gold, gold));

        var heirlooms = dd1.HeirloomTypes.TryGetValue(zone, out var h) ? h : Currency.Heirlooms.ToList();
        string heirloom = rng.Pick(heirlooms);
        int amount = dd1.HeirloomAmount(heirloom, difficulty, length);
        if (amount > 0) quest.Rewards.Add(new Reward(heirloom, amount));
        return quest;
    }

    /// <summary>Mostly the zone's current tier, sometimes an easier one (DD1 keeps lower tiers available).</summary>
    private static int PickDifficulty(int max, Rng rng)
    {
        var allowed = Difficulties.Where(d => d <= max).ToList();
        return allowed.Count > 1 && rng.Chance(0.35) ? rng.Pick(allowed.Take(allowed.Count - 1).ToList()) : allowed.Last();
    }

    private static (string, int) PickWeighted(List<(string Type, int Length, float Chance)> options, Rng rng)
    {
        double roll = rng.NextDouble() * options.Sum(o => o.Chance);
        foreach (var o in options)
        {
            roll -= o.Chance;
            if (roll < 0) return (o.Type, o.Length);
        }
        var last = options[options.Count - 1];
        return (last.Type, last.Length);
    }

    private static T At<T>(IReadOnlyList<T> list, int index, T fallback) =>
        list.Count == 0 ? fallback : list[Math.Min(index, list.Count - 1)];
}
