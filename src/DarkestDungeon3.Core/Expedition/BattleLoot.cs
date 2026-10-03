using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>
/// DD1's battle loot. Our fights are DD2 battles, so the loot follows the DD1 encounter the fight stands for:
/// roll one from the zone's mash file for the quest level (dungeons/&lt;zone&gt;/&lt;zone&gt;.&lt;level&gt;.mash.darkest:
/// hall / room / boss lists of DD1 monster classes), then each monster's loot codes
/// (monsters/**/&lt;class&gt;.info.darkest, "loot: .code "A" .count 1") draw from DD1's loot tables.
/// </summary>
public sealed class BattleLoot
{
    private sealed class Encounter
    {
        public float Weight;
        public List<string> Monsters = new();
    }

    // zone -> level -> kind (hall/room/boss) -> encounters
    private readonly Dictionary<string, Dictionary<int, Dictionary<string, List<Encounter>>>> _mash = new();
    private readonly Dictionary<string, List<(string Code, int Count)>> _monsterLoot = new();

    public int MonsterCount => _monsterLoot.Count;

    public static BattleLoot Load(Dd1Install dd1)
    {
        var loot = new BattleLoot();
        string monsters = dd1.PathOf("monsters");
        if (Directory.Exists(monsters))
            foreach (var file in Directory.GetFiles(monsters, "*.info.darkest", SearchOption.AllDirectories))
            {
                string id = Path.GetFileName(file).Replace(".info.darkest", "");
                var codes = DarkestFile.Load(file).Where(r => r.Type == "loot")
                    .Select(r => (Code: r.Str("code", ""), Count: r.Int("count")))
                    .Where(c => c.Code.Length > 0 && c.Code != "NONE" && c.Count > 0).ToList();
                loot._monsterLoot[id] = codes;
            }

        string dungeons = dd1.PathOf("dungeons");
        if (Directory.Exists(dungeons))
            foreach (var zoneDir in Directory.GetDirectories(dungeons))
            {
                string zone = Path.GetFileName(zoneDir);
                foreach (var file in Directory.GetFiles(zoneDir, zone + ".*.mash.darkest"))
                {
                    // zone.<level>.mash.darkest; skip the "conditional" variants.
                    string middle = Path.GetFileName(file).Substring(zone.Length + 1).Replace(".mash.darkest", "");
                    if (!int.TryParse(middle, out int level)) continue;
                    if (!loot._mash.TryGetValue(zone, out var levels)) loot._mash[zone] = levels = new Dictionary<int, Dictionary<string, List<Encounter>>>();
                    var kinds = levels[level] = new Dictionary<string, List<Encounter>>();
                    foreach (var r in DarkestFile.Load(file))
                    {
                        if (!r.Has("types")) continue;
                        if (!kinds.TryGetValue(r.Type, out var list)) kinds[r.Type] = list = new List<Encounter>();
                        list.Add(new Encounter { Weight = r.Float("chance", 0, 1f), Monsters = r.Values("types").ToList() });
                    }
                }
            }
        return loot;
    }

    /// <summary>The DD1 monsters of an encounter of this kind (hall, room, boss), for the zone and quest difficulty.</summary>
    public List<string> RollEncounter(string zone, int difficulty, string kind, Rng rng)
    {
        if (zone == null || !_mash.TryGetValue(zone, out var levels) || levels.Count == 0) return new List<string>();
        int want = difficulty >= 6 ? 5 : System.Math.Max(1, difficulty);
        int level = levels.Keys.OrderBy(l => System.Math.Abs(l - want)).ThenBy(l => l).First();
        var kinds = levels[level];
        if (!kinds.TryGetValue(kind, out var list) || list.Count == 0)
            if (!kinds.TryGetValue(kind == "boss" ? "room" : "hall", out list) || list.Count == 0) return new List<string>();
        float total = list.Sum(e => e.Weight), pick = (float)rng.NextDouble() * total;
        foreach (var e in list)
        {
            pick -= e.Weight;
            if (pick <= 0) return new List<string>(e.Monsters);
        }
        return new List<string>(list[list.Count - 1].Monsters);
    }

    public IReadOnlyList<(string Code, int Count)> LootOf(string monster) =>
        monster != null && _monsterLoot.TryGetValue(monster, out var codes) ? codes : new List<(string, int)>();

    /// <summary>Everything a won fight of this kind leaves behind. Zones without DD1 tables (DD2 zones) use a plain
    /// three-monster draw from DD1's common table.</summary>
    public List<LootDrop> Roll(LootTables tables, string zone, int difficulty, string kind, Rng rng, out List<string> monsters)
    {
        monsters = RollEncounter(zone, difficulty, kind, rng);
        return RollFor(tables, monsters, zone, difficulty, rng);
    }

    /// <summary>What these DD1 monsters leave behind (DD1's per-monster loot codes); none rolled → a common draw.</summary>
    public List<LootDrop> RollFor(LootTables tables, IReadOnlyList<string> monsters, string zone, int difficulty, Rng rng)
    {
        var drops = new List<LootDrop>();
        if (monsters.Count == 0)
        {
            if (tables.Has("A")) drops.AddRange(tables.Roll("A", 3, difficulty, zone, rng));
            return Merge(drops);
        }
        foreach (var m in monsters)
            foreach (var (code, count) in LootOf(m))
                if (tables.Has(code)) drops.AddRange(tables.Roll(code, count, difficulty, zone, rng));
        return Merge(drops);
    }

    private static List<LootDrop> Merge(List<LootDrop> drops)
    {
        var merged = new List<LootDrop>();
        foreach (var d in drops)
        {
            var same = d.Type == "trinket" ? null : merged.FirstOrDefault(m => m.Type == d.Type && m.Id == d.Id);
            if (same != null) same.Amount += d.Amount; else merged.Add(new LootDrop { Type = d.Type, Id = d.Id, Amount = d.Amount });
        }
        return merged;
    }
}

/// <summary>What the party picked up after a fight, and what didn't fit.</summary>
public sealed class BattleSpoils
{
    public string Kind;
    public List<string> Dd1Monsters = new();
    public List<LootDrop> Taken = new();
    public List<LootDrop> LeftBehind = new();
}
