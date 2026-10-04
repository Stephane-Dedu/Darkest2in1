using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// DD1's corpse rule (monsters/&lt;family&gt;/&lt;class&gt;/&lt;class&gt;.info.darkest <c>death_class</c>): a slain monster leaves a
/// corpse only if its class has a death class of type "corpse", and not when the killing blow was a critical hit
/// (<c>.is_valid_on_crit</c>) or bleed/blight/burn damage (<c>.is_valid_on_bleed_dot</c>..., false unless set).
/// Monsters without a death class (maggots, ...) leave nothing and the ranks close up.
/// </summary>
public sealed class Dd1Corpse
{
    private sealed class Rule { public bool OnCrit, OnBleed, OnBlight, OnBurn; }

    private readonly Dd1Install _dd1;
    private readonly Dictionary<string, Rule> _rules = new();

    public Dd1Corpse(Dd1Install dd1) => _dd1 = dd1;

    private static bool Flag(DarkestRecord row, string key) => string.Equals(row.Str(key, "False"), "True", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this DD1 monster class (e.g. maggot_A) leaves a corpse when slain this way; null when DD1 has no
    /// such monster (leave it to DD2).</summary>
    public bool? LeavesCorpse(string monsterClass, bool crit, bool dot)
    {
        if (string.IsNullOrEmpty(monsterClass)) return null;
        if (!_rules.TryGetValue(monsterClass, out var rule))
        {
            string family = Dd1Bestiary.Split(monsterClass).Family;
            string path = _dd1.PathOf("monsters", family, monsterClass, monsterClass + ".info.darkest");
            if (!File.Exists(path)) return null;
            var row = DarkestFile.Load(path).FirstOrDefault(r => r.Type == "death_class" && r.Str("type", "") == "corpse");
            rule = row == null ? null : new Rule
            {
                OnCrit = Flag(row, "is_valid_on_crit"),
                OnBleed = Flag(row, "is_valid_on_bleed_dot"),
                OnBlight = Flag(row, "is_valid_on_blight_dot"),
                OnBurn = Flag(row, "is_valid_on_burn_dot"),
            };
            _rules[monsterClass] = rule;
        }
        if (rule == null) return false;
        if (crit && !rule.OnCrit) return false;
        if (dot && !(rule.OnBleed || rule.OnBlight || rule.OnBurn)) return false;
        return true;
    }
}
