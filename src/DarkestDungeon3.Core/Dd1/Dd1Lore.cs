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
