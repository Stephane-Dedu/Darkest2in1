using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>
/// DD1's new-game state (scripts/starting_save/persist.roster.json and persist.estate.json): the two heroes of the
/// opening, Reynauld and Dismas, with their quirks and stress, and the estate's first wallet (heirlooms, no gold).
/// </summary>
public sealed class StartingSave
{
    public sealed class Hero
    {
        public string Name, Dd1Class;
        public int Stress;                 // DD1 scale (0-200)
        public int ResolveXp;
        public List<string> Quirks = new();
    }

    public List<Hero> Heroes { get; } = new();
    public List<(string Type, int Amount)> Wallet { get; } = new();

    public static StartingSave Load(Dd1Install dd1)
    {
        var save = new StartingSave();
        string dir = dd1.PathOf("scripts", "starting_save");
        string rosterPath = Path.Combine(dir, "persist.roster.json"), estatePath = Path.Combine(dir, "persist.estate.json");
        var names = HeroNames(dd1);
        if (File.Exists(rosterPath))
            foreach (var h in (Parse(rosterPath)?["data"]?["heroes"] as JObject ?? new JObject()).Properties().OrderBy(p => p.Name).Select(p => p.Value))
            {
                string nameId = (string)h["actor"]?["name_id"] ?? "";
                save.Heroes.Add(new Hero
                {
                    Name = names.TryGetValue(nameId, out var n) ? n : Pretty(nameId.Replace("hero_name_", "")),
                    Dd1Class = (string)h["heroClass"],
                    Stress = (int?)h["stress"] ?? 0,
                    ResolveXp = (int?)h["resolveXp"] ?? 0,
                    Quirks = (h["quirks"] as JObject ?? new JObject()).Properties().Select(p => p.Name).ToList(),
                });
            }
        if (File.Exists(estatePath))
            foreach (var w in (Parse(estatePath)?["data"]?["wallet"] as JObject ?? new JObject()).Properties().Select(p => p.Value))
                if ((string)w["type"] is { } type) save.Wallet.Add((type, (int?)w["amount"] ?? 0));
        return save;
    }

    // DD1's starting save has trailing commas: drop them before parsing.
    private static JToken Parse(string path) =>
        JToken.Parse(Regex.Replace(File.ReadAllText(path), @",(\s*[}\]])", "$1"));

    private static Dictionary<string, string> HeroNames(Dd1Install dd1)
    {
        var names = new Dictionary<string, string>();
        string path = dd1.PathOf("localization", "names.string_table.xml");
        if (!File.Exists(path)) return names;
        string xml = File.ReadAllText(path);
        int a = xml.IndexOf("<language id=\"english\"");
        int b = a < 0 ? -1 : xml.IndexOf("</language>", a);
        if (a < 0 || b < 0) return names;
        foreach (Match m in Regex.Matches(xml.Substring(a, b - a), "<entry id=\"(hero_name_[a-z_]+)\"><!\\[CDATA\\[([^\\]]+)\\]\\]>"))
            names[m.Groups[1].Value] = m.Groups[2].Value;
        return names;
    }

    private static string Pretty(string id) => id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);
}
