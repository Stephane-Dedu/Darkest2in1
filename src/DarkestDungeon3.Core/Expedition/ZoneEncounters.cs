using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

public enum FightKind { Hall, Room, Boss, CampAmbush }

/// <summary>A DD2 fight to start: a battle reference ("table:x" or "config:x") and arenas to try in order.</summary>
public sealed class FightPlan
{
    public string Battle;
    public List<string> Arenas = new();
    public FightKind Kind;
    /// <summary>Keep the native arena, camera lighting and enemy art for a DD2 destination.</summary>
    public bool NativePresentation;
    /// <summary>DD2 enemies translated from a rolled DD1 encounter (front rank first); when set, they are the
    /// battle and <see cref="Battle"/> is only the fallback.</summary>
    public List<string> Enemies;

    public bool IsTable => Battle != null && Battle.StartsWith("table:", StringComparison.Ordinal);
    public string BattleId => Battle?.Substring(Battle.IndexOf(':') + 1);

    public override string ToString() => $"{Kind}: {(Enemies != null ? "[" + string.Join(", ", Enemies) + "] else " : "")}{Battle} in [{string.Join(", ", Arenas)}]";
}

/// <summary>Which DD2 encounters each DD1 zone uses (<c>data/zones.json</c>, shipped with the mod).</summary>
public sealed class ZoneEncounters
{
    private JObject _root = new();

    public static ZoneEncounters Load(string path) => Parse(File.ReadAllText(path));

    public static ZoneEncounters Parse(string json)
    {
        var z = new ZoneEncounters { _root = JObject.Parse(json) };
        foreach (var p in (z._root["zones"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            Core.Dungeon.ZoneBase.Register(p.Name, (string)p.Value["dd1_zone"], (string)p.Value["region_boss"]);
        return z;
    }

    public IEnumerable<string> Zones => (_root["zones"] as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();

    public string ZoneName(string zone) => (string)_root["zones"]?[zone]?["name"] ?? zone;

    /// <summary>One line about an extra zone, for the estate options.</summary>
    public string Blurb(string zone) => (string)_root["zones"]?[zone]?["blurb"] ?? "";

    /// <summary>DD1 boss monster classes look like "necromancer_A"; strip the tier letter.</summary>
    public static string BossKey(string dd1MonsterClass)
    {
        if (string.IsNullOrEmpty(dd1MonsterClass)) return null;
        int us = dd1MonsterClass.LastIndexOf('_');
        return us > 0 && dd1MonsterClass.Length - us == 2 ? dd1MonsterClass.Substring(0, us) : dd1MonsterClass;
    }

    public FightPlan Plan(string zone, int difficulty, FightKind kind, Rng rng, string dd1Boss = null)
    {
        var z = _root["zones"]?[zone] as JObject;
        var plan = new FightPlan { Kind = kind, NativePresentation = Dungeon.ZoneBase.IsExtra(zone) };

        if (kind == FightKind.CampAmbush && !plan.NativePresentation)
        {
            plan.Battle = (string)_root["camp_ambush"];
            plan.Arenas.AddRange(Arenas(z, "room"));
            return Finish(plan);
        }

        if (kind == FightKind.Boss && z?["bosses"]?[BossKey(dd1Boss) ?? ""] is JObject boss)
        {
            plan.Battle = (string)boss["battle"];
            if (boss["arena"] != null) plan.Arenas.Add((string)boss["arena"]);
            plan.Arenas.AddRange(Arenas(z, "room"));
            return Finish(plan);
        }

        // Nearest defined difficulty at or below the quest's (a veteran zone without its own table uses apprentice).
        var battles = z?["battles"] as JObject;
        var tier = battles?.Properties()
            .Select(p => (Level: int.TryParse(p.Name, out var l) ? l : 0, p.Value))
            .OrderByDescending(t => t.Level)
            .FirstOrDefault(t => t.Level <= difficulty).Value
            ?? battles?.Properties().FirstOrDefault()?.Value;

        string slot = kind == FightKind.Hall ? "hall" : "room";
        var options = (tier?[slot] as JArray)?.Select(o => (string)o).ToList() ?? new List<string>();
        plan.Battle = options.Count > 0 ? rng.Pick(options) : (string)_root["fallback_battle"];
        var arenas = Arenas(z, slot).ToList();
        rng.Shuffle(arenas);
        plan.Arenas.AddRange(arenas);
        return Finish(plan);
    }

    private FightPlan Finish(FightPlan plan)
    {
        plan.Battle ??= (string)_root["fallback_battle"];
        plan.Arenas = plan.Arenas.Distinct().ToList();
        return plan;
    }

    private static IEnumerable<string> Arenas(JObject zone, string slot) =>
        (zone?["arenas"]?[slot] as JArray)?.Select(a => (string)a) ?? Enumerable.Empty<string>();
}
