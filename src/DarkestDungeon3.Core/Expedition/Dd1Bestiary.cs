using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>
/// DD1 monsters to DD2 enemies (<c>data/monsters.json</c>, shipped with the mod). A rolled DD1 encounter becomes a
/// DD2 line-up of look-alikes in the same order, front rank first, so fights follow DD1's encounter tables.
/// </summary>
public sealed class Dd1Bestiary
{
    private sealed class Entry
    {
        public List<string> Dd2 = new();
        public List<string> Champion = new();
        public string Art;
    }

    private readonly Dictionary<string, Entry> _map = new();

    public int Count => _map.Count;

    public static Dd1Bestiary Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dd1Bestiary();

    public static Dd1Bestiary Parse(string json)
    {
        var b = new Dd1Bestiary();
        if (JObject.Parse(json)["monsters"] is not JObject monsters) return b;
        foreach (var p in monsters.Properties())
            b._map[p.Name] = new Entry
            {
                Dd2 = p.Value["dd2"]?.Values<string>().ToList() ?? new List<string>(),
                Champion = p.Value["champion"]?.Values<string>().ToList() ?? new List<string>(),
                Art = (string)p.Value["art"],
            };
        return b;
    }

    /// <summary>"skeleton_arbalist_B" → ("skeleton_arbalist", 'B'); no tier letter → 'A'.</summary>
    public static (string Family, char Tier) Split(string dd1Monster)
    {
        int n = dd1Monster.Length;
        if (n > 2 && dd1Monster[n - 2] == '_' && char.IsUpper(dd1Monster[n - 1])) return (dd1Monster.Substring(0, n - 2), dd1Monster[n - 1]);
        return (dd1Monster, 'A');
    }

    /// <summary>The DD1 monster folder whose animations draw this family (itself unless the map names another).</summary>
    public string ArtFamily(string family) => family != null && _map.TryGetValue(family, out var e) && e.Art != null ? e.Art : family;

    public bool Knows(string dd1Monster) => _map.TryGetValue(Split(dd1Monster).Family, out var e) && e.Dd2.Count > 0;

    /// <summary>
    /// The DD2 line-up for a DD1 encounter, or null when a monster has no stand-in (boss pieces: the zone's own DD2
    /// battle is used then). Champions (_C) take the stronger DD2 form, veterans (_B) one time in three. Enemies at
    /// the back are dropped if DD2's sizes no longer fit four ranks.
    /// </summary>
    public List<string> Translate(IReadOnlyList<string> dd1, Rng rng, Func<string, int> dd2Size)
    {
        if (dd1 == null || dd1.Count == 0) return null;
        var lineUp = new List<string>();
        foreach (var monster in dd1)
        {
            var (family, tier) = Split(monster);
            if (!_map.TryGetValue(family, out var e) || e.Dd2.Count == 0) return null;
            bool champion = e.Champion.Count > 0 && (tier >= 'C' || (tier == 'B' && rng.Next(3) == 0));
            var pool = champion ? e.Champion : e.Dd2;
            lineUp.Add(pool[rng.Next(pool.Count)]);
        }
        int ranks = 0, keep = 0;
        foreach (var enemy in lineUp)
        {
            int size = Math.Max(1, dd2Size(enemy));
            if (ranks + size > 4) break;
            ranks += size;
            keep++;
        }
        return keep == 0 ? null : lineUp.Take(keep).ToList();
    }
}
