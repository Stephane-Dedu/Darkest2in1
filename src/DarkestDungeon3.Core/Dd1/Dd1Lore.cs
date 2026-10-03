using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>Small DD1 facts the Hamlet borrows: recruit names, quirk polarity, trinket prices by rarity.</summary>
public sealed class Dd1Lore
{
    public List<string> HeroNames { get; } = new();
    /// <summary>DD1 quirk id → (positive, disease).</summary>
    public Dictionary<string, (bool Positive, bool Disease)> Quirks { get; } = new();
    public Dictionary<string, int> TrinketPriceByRarity { get; } = new();
    /// <summary>DD1's English monster names ("skeleton_arbalist_A" → "Bone Arbalist") and monster skill names ("crossbow_shot").</summary>
    public Dictionary<string, string> MonsterNames { get; } = new();
    public Dictionary<string, string> MonsterSkillNames { get; } = new();

    public static Dd1Lore Load(Dd1Install dd1)
    {
        var lore = new Dd1Lore();

        // names.string_table.xml holds every language; the English block comes first.
        var names = dd1.PathOf("localization", "names.string_table.xml");
        if (File.Exists(names))
        {
            string xml = File.ReadAllText(names);
            int english = xml.IndexOf("<language id=\"english\"", System.StringComparison.Ordinal);
            int end = english >= 0 ? xml.IndexOf("</language>", english, System.StringComparison.Ordinal) : -1;
            string block = english >= 0 && end > english ? xml.Substring(english, end - english) : xml;
            foreach (Match m in Regex.Matches(block, "id=\"hero_name_\\d+\"><!\\[CDATA\\[([^\\]]+)\\]\\]>"))
                if (!lore.HeroNames.Contains(m.Groups[1].Value)) lore.HeroNames.Add(m.Groups[1].Value);
        }

        // Monster and monster skill names: the English block of every string table.
        string localization = dd1.PathOf("localization");
        if (Directory.Exists(localization))
            foreach (var file in Directory.GetFiles(localization, "*.string_table.xml"))
            {
                string xml = File.ReadAllText(file);
                if (xml.IndexOf("str_monster", System.StringComparison.Ordinal) < 0) continue;
                int english = xml.IndexOf("<language id=\"english\"", System.StringComparison.Ordinal);
                int end = english >= 0 ? xml.IndexOf("</language>", english, System.StringComparison.Ordinal) : -1;
                string block = english >= 0 && end > english ? xml.Substring(english, end - english) : xml;
                foreach (Match m in Regex.Matches(block, @"id=""str_monstername_([^""]+)""><!\[CDATA\[([^\]]*)\]\]>"))
                    lore.MonsterNames[m.Groups[1].Value] = m.Groups[2].Value;
                foreach (Match m in Regex.Matches(block, @"id=""str_monster_skill_([^""]+)""><!\[CDATA\[([^\]]*)\]\]>"))
                    lore.MonsterSkillNames[m.Groups[1].Value] = m.Groups[2].Value;
            }

        var quirks = dd1.PathOf("shared", "quirk", "quirk_library.json");
        if (File.Exists(quirks))
            foreach (var q in JToken.Parse(File.ReadAllText(quirks))["quirks"] ?? new JArray())
                lore.Quirks[(string)q["id"]] = ((bool?)q["is_positive"] ?? false, (bool?)q["is_disease"] ?? false);

        var trinkets = dd1.PathOf("trinkets", "base.entries.trinkets.json");
        if (File.Exists(trinkets))
            foreach (var g in (JToken.Parse(File.ReadAllText(trinkets))["entries"] ?? new JArray())
                     .GroupBy(t => (string)t["rarity"]))
            {
                var prices = g.Select(t => (int?)t["price"] ?? 0).Where(p => p > 1).ToList();
                if (prices.Count > 0) lore.TrinketPriceByRarity[g.Key] = (int)prices.Average();
            }
        return lore;
    }

    public string RandomName(Rng rng) => HeroNames.Count > 0 ? rng.Pick(HeroNames) : "Nameless";
}
