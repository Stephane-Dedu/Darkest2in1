using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// A DD1 hero class's numbers from its info file (base heroes or a campaign DLC, like the Shieldbreaker): weapons and
/// armours by rank, resistances, and every combat skill at each level. It turns them into DD2 terms for a DD2 hero
/// class built at runtime: a skill's damage is its weapon's times the skill's modifier, its crit the weapon's plus the
/// skill's; DD2 has no accuracy or dodge.
/// </summary>
public sealed class Dd1HeroKit
{
    public sealed class Gear
    {
        public int DamageMin, DamageMax, Speed, Hp;
        public float Crit, Dodge, Protection;
    }

    public sealed class SkillLevel
    {
        public string Id, Type, TargetText;
        public int Level, MoveBack, MoveForward, PerBattleLimit;
        public float DamageMod, Crit;
        public bool CritValid = true, IgnoreProtection, IgnoreGuard, IgnoreStealth;
        public List<int> Launch = new();
        public List<string> Effects = new();
    }

    public string ClassId { get; private set; }
    public string Root { get; private set; }
    public IReadOnlyList<Gear> Weapons { get; private set; }
    public IReadOnlyList<Gear> Armours { get; private set; }
    /// <summary>DD2 resistance names: stun, blight, bleed, disease, move, debuff, death.</summary>
    public IReadOnlyDictionary<string, float> Resists { get; private set; }
    /// <summary>The class's combat skills in DD1's order.</summary>
    public IReadOnlyList<string> SkillIds { get; private set; }
    /// <summary>How many skills DD1 lets the hero take into a fight.</summary>
    public int SelectedMax { get; private set; }
    private Dictionary<(string, int), SkillLevel> _skills;

    private Dictionary<string, string> _icons = new(StringComparer.Ordinal);

    /// <summary>The skill's DD1 icon (&lt;class&gt;.ability.&lt;icon&gt;.png beside the info file), or null.</summary>
    public string IconFile(string skillId) =>
        skillId != null && _icons.TryGetValue(skillId, out var icon) ? Path.Combine(Root, ClassId + ".ability." + icon + ".png") : null;

    /// <summary>The hero's roster portrait in the first outfit.</summary>
    public string PortraitFile => Path.Combine(Root, ClassId + "_A", ClassId + "_portrait_roster.png");

    public SkillLevel Skill(string id, int level) =>
        _skills.TryGetValue((id, level), out var s) ? s : _skills.TryGetValue((id, 0), out s) ? s : null;

    public static Dd1HeroKit Read(Dd1Install dd1, string classId)
    {
        if (dd1 == null || string.IsNullOrEmpty(classId) || classId.Any(c => !(c >= 'a' && c <= 'z') && c != '_')) return null;
        foreach (string root in Dd1HeroArt.Candidates(dd1, classId))
        {
            string file = Path.Combine(root, classId + ".info.darkest");
            if (!File.Exists(file)) continue;
            var kit = Parse(classId, root, DarkestFile.Load(file));
            if (kit == null) continue;
            string art = Path.Combine(root, classId + ".art.darkest");
            if (File.Exists(art))
                foreach (var r in DarkestFile.Load(art).Where(r => r.Type == "combat_skill"))
                    if (r.Str("id") is { } id && r.Str("icon") is { } icon && icon.All(c => char.IsLetterOrDigit(c) || c == '_'))
                        kit._icons[id] = icon;
            return kit;
        }
        return null;
    }

    private static Dd1HeroKit Parse(string classId, string root, IReadOnlyList<DarkestRecord> records)
    {
        Gear Weapon(DarkestRecord r)
        {
            var dmg = r.Range("dmg");
            return new Gear { DamageMin = dmg.Min, DamageMax = dmg.Max, Crit = r.Float("crit"), Speed = r.Int("spd") };
        }
        Gear Armour(DarkestRecord r) => new() { Hp = r.Int("hp"), Dodge = r.Float("def"), Protection = r.Float("prot"), Speed = r.Int("spd") };

        var weapons = records.Where(r => r.Type == "weapon").Select(Weapon).ToList();
        var armours = records.Where(r => r.Type == "armour").Select(Armour).ToList();
        var skills = new Dictionary<(string, int), SkillLevel>();
        var order = new List<string>();
        foreach (var r in records.Where(r => r.Type == "combat_skill"))
        {
            string id = r.Str("id");
            if (string.IsNullOrEmpty(id)) continue;
            var move = r.Values("move");
            var s = new SkillLevel
            {
                Id = id,
                Level = r.Int("level"),
                Type = r.Str("type"),
                TargetText = string.Concat(r.Values("target")),
                DamageMod = r.Float("dmg"),
                Crit = r.Float("crit"),
                CritValid = !string.Equals(r.Str("is_crit_valid"), "False", StringComparison.OrdinalIgnoreCase),
                Launch = Ranks(string.Concat(r.Values("launch"))),
                Effects = r.Values("effect").ToList(),
                MoveBack = move.Count > 0 && int.TryParse(move[0], out int back) ? back : 0,
                MoveForward = move.Count > 1 && int.TryParse(move[1], out int forward) ? forward : 0,
                PerBattleLimit = r.Int("per_battle_limit"),
                IgnoreProtection = Flag(r, "ignore_protection"),
                IgnoreGuard = Flag(r, "ignore_guard"),
                IgnoreStealth = Flag(r, "ignore_stealth"),
            };
            skills[(id, s.Level)] = s;
            if (!order.Contains(id)) order.Add(id);
        }
        if (weapons.Count == 0 || armours.Count == 0 || order.Count == 0) return null;

        var resist = records.FirstOrDefault(r => r.Type == "resistances");
        var resists = new Dictionary<string, float>(StringComparer.Ordinal);
        if (resist != null)
            foreach (var (dd1, dd2) in new[] { ("stun", "stun"), ("poison", "blight"), ("bleed", "bleed"), ("disease", "disease"),
                         ("move", "move"), ("debuff", "debuff"), ("death_blow", "death") })
                if (resist.Has(dd1)) resists[dd2] = resist.Float(dd1);

        return new Dd1HeroKit
        {
            ClassId = classId,
            Root = root,
            Weapons = weapons,
            Armours = armours,
            Resists = resists,
            SkillIds = order,
            SelectedMax = records.FirstOrDefault(r => r.Type == "skill_selection")?.Int("number_of_selected_combat_skills_max", fallback: 4) ?? 4,
            _skills = skills,
        };
    }

    private static bool Flag(DarkestRecord r, string key) => string.Equals(r.Str(key), "true", StringComparison.OrdinalIgnoreCase);

    private static List<int> Ranks(string s) => s.Where(c => c >= '1' && c <= '4').Select(c => c - '0').Distinct().OrderBy(x => x).ToList();

    private Gear At(IReadOnlyList<Gear> gear, int rank) => gear[Math.Max(0, Math.Min(rank, gear.Count - 1))];

    /// <summary>The skill at a level, with the weapon of a rank, in the shape DD1 monster skills take.</summary>
    public SkillShape Shape(string id, int level = 0, int weaponRank = 0)
    {
        var s = Skill(id, level);
        if (s == null) return null;
        var weapon = At(Weapons, weaponRank);
        bool friendly = s.TargetText.Length == 0 || s.TargetText.Contains('@');
        bool damages = !friendly && s.DamageMod > -1f;
        return new SkillShape
        {
            Id = id,
            Ranged = s.Type == "ranged",
            Friendly = friendly,
            LaunchRanks = s.Launch.ToList(),
            TargetRanks = Ranks(s.TargetText),
            AllTargets = s.TargetText.Contains('~'),
            DamageMin = damages ? Math.Max(1, (int)Math.Round(weapon.DamageMin * (1 + s.DamageMod), MidpointRounding.AwayFromZero)) : 0,
            DamageMax = damages ? Math.Max(1, (int)Math.Round(weapon.DamageMax * (1 + s.DamageMod), MidpointRounding.AwayFromZero)) : 0,
            Crit = s.CritValid ? weapon.Crit + s.Crit : 0f,
            CritValid = s.CritValid,
            Effects = s.Effects.ToList(),
            MoveBack = s.MoveBack,
            MoveForward = s.MoveForward,
            IgnoreProtection = s.IgnoreProtection,
            IgnoreGuard = s.IgnoreGuard,
            IgnoreStealth = s.IgnoreStealth,
            PerBattleLimit = s.PerBattleLimit,
        };
    }

    /// <summary>
    /// The stand-in DD2 hero's stats block (keys DD2 needs: stress, death's door, turns...) with this class's HP (scaled
    /// to DD2's like-for-like), speed and resistances. Burn, which DD1 lacks, stays the stand-in's.
    /// </summary>
    public string StatsText(string standInStats, float hpScale, int armourRank = 0, int weaponRank = 0)
    {
        var armour = At(Armours, armourRank);
        var weapon = At(Weapons, weaponRank);
        var lines = standInStats.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        int keyIndex = lines.FindIndex(l => l.StartsWith("key_map,", StringComparison.Ordinal));
        int valueIndex = lines.FindIndex(l => l.StartsWith("add_stats,", StringComparison.Ordinal));
        if (keyIndex >= 0 && valueIndex >= 0)
        {
            var keys = lines[keyIndex].Split(',').ToList();
            var values = lines[valueIndex].Split(',').ToList();
            void Set(string key, float value)
            {
                int i = keys.IndexOf(key);
                if (i > 0 && i < values.Count) values[i] = value.ToString("0.###", CultureInfo.InvariantCulture);
            }
            Set("health_max", Math.Max(1, (int)Math.Round(armour.Hp * hpScale, MidpointRounding.AwayFromZero)));
            Set("speed", weapon.Speed + armour.Speed);
            lines[valueIndex] = string.Join(",", values);
        }
        for (int i = 0; i < lines.Count; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length >= 4 && parts[0] == "sub_stat" && parts[1] == "resistance" && Resists.TryGetValue(parts[2], out float r))
            {
                parts[3] = r.ToString("0.###", CultureInfo.InvariantCulture);
                lines[i] = string.Join(",", parts);
            }
        }
        return string.Join("\n", lines) + "\n";
    }
}
