using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Expedition;

public sealed class CurioResult
{
    public string Name;        // loot table id, effect name, quirk id, disease id
    public float Weight;       // pick weight; for Loot it's the number of draws
}

public sealed class CurioOutcome
{
    public string Type;        // Nothing, Loot, Quirk, Effect, Purge, Scouting, Teleport, Disease
    public float Weight;
    public List<CurioResult> Results = new();
    public string Text;
}

public sealed class ItemInteraction
{
    public string Item;        // DD1 supply id
    public CurioOutcome Outcome;
}

public sealed class CurioDef
{
    public string Id;
    public string Name;
    public string Category;    // Good, Mixed, Bad...
    public string Region;
    public List<string> Tags = new();
    public List<CurioOutcome> Outcomes = new();
    public List<ItemInteraction> Items = new();
}

/// <summary>DD1's curio library (<c>curios/curio_type_library.csv</c>), a spreadsheet of one block per curio.</summary>
public sealed class CurioLibrary
{
    public Dictionary<string, CurioDef> Curios { get; } = new();
    private IReadOnlyDictionary<string, string> _propSprites = new Dictionary<string, string>();

    public CurioDef Get(string id) => id != null && Curios.TryGetValue(id, out var c) ? c : null;

    public string SpriteOf(string propId) => propId != null && _propSprites.TryGetValue(propId, out var sprite) ? sprite : propId;

    public static CurioLibrary Load(Dd1Install dd1)
    {
        var library = Parse(File.ReadAllLines(dd1.PathOf("curios", "curio_type_library.csv")));
        library._propSprites = LoadPropSprites(dd1);
        return library;
    }

    /// <summary>Native prop IDs can share art without sharing effects (e.g. the Thanks Chest).</summary>
    public static IReadOnlyDictionary<string, string> LoadPropSprites(Dd1Install dd1)
    {
        var sprites = new Dictionary<string, string>();
        string path = dd1.PathOf("curios", "curio_props.csv");
        if (!File.Exists(path)) return sprites;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var cells = Csv(line);
            if (cells.Count >= 2 && !string.IsNullOrWhiteSpace(cells[0]) && !string.IsNullOrWhiteSpace(cells[1]))
                sprites[cells[0].Trim()] = cells[1].Trim();
        }
        return sprites;
    }

    public static CurioLibrary Parse(IEnumerable<string> lines)
    {
        var lib = new CurioLibrary();
        CurioDef current = null;
        bool inItems = false;
        string pendingLabel = null;

        foreach (var line in lines)
        {
            var c = Csv(line);
            string Col(int i) => i < c.Count ? c[i].Trim() : "";

            // ",N,Name,,Category" starts a new curio.
            if (int.TryParse(Col(1), out _) && Col(2) != "")
            {
                current = new CurioDef { Name = Col(2), Category = Col(4) };
                inItems = false;
                pendingLabel = null;
                continue;
            }
            if (current == null) continue;

            if (Col(2) == "ID STRING") continue;
            if (Col(2) == "Item Interactions") { inItems = true; continue; }

            if (inItems)
            {
                if (Col(4) == "") continue;
                current.Items.Add(new ItemInteraction
                {
                    Item = Col(4) == "provision" ? Supply.Food : Col(4),   // DD1 calls food "provision"
                    Outcome = ReadOutcome(Col(5), 1f, c, Col(17)),
                });
                continue;
            }

            // Left-hand labels: the id comes first, then "REGION FOUND"/value, "FULL CURIO?"/value, "TAGS"/values.
            string label = Col(2);
            if (current.Id == null && label != "" && Col(4) == "Nothing")
            {
                current.Id = label;
                lib.Curios[label] = current;
            }
            else if (label is "REGION FOUND" or "FULL CURIO?" or "TAGS") pendingLabel = label;
            else if (pendingLabel == "REGION FOUND" && label != "") { current.Region = label; pendingLabel = null; }
            else if (pendingLabel == "TAGS" && label != "")
            {
                current.Tags.Add(label);
                if (Col(3) != "") current.Tags.Add(Col(3));
            }

            // The outcome table on the right: type, weight, then up to three (result, weight) pairs.
            string type = Col(4);
            if (type is "Nothing" or "Loot" or "Quirk" or "Effect" or "Purge" or "Scouting" or "Teleport" or "Disease")
            {
                float weight = TryFloat(Col(5));
                var outcome = ReadOutcome(type, weight, c, Col(17));
                if (weight > 0 || (type == "Nothing" && outcome.Text != null)) current.Outcomes.Add(outcome);
            }
        }
        return lib;
    }

    private static CurioOutcome ReadOutcome(string type, float weight, List<string> c, string text)
    {
        string Col(int i) => i < c.Count ? c[i].Trim() : "";
        var outcome = new CurioOutcome { Type = type, Weight = weight, Text = text == "" ? null : text };
        foreach (int col in new[] { 7, 10, 13 })
        {
            string name = Col(col);
            if (name == "" || name == "N/A") continue;
            outcome.Results.Add(new CurioResult { Name = name, Weight = TryFloat(Col(col + 1)) });
        }
        return outcome;
    }

    private static float TryFloat(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        try { return DarkestRecord.ParseFloat(s); } catch { return 0f; }
    }

    /// <summary>Split one CSV line, honouring double-quoted fields with commas inside.</summary>
    private static List<string> Csv(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        cells.Add(sb.ToString());
        return cells;
    }
}

/// <summary>DD1 effects (<c>effects/base.effects.darkest</c>) used by curios and traps.</summary>
public sealed class EffectLibrary
{
    public Dictionary<string, DarkestRecord> Effects { get; } = new();

    public static EffectLibrary Load(Dd1Install dd1)
    {
        var lib = new EffectLibrary();
        foreach (var file in Directory.GetFiles(dd1.PathOf("effects"), "*.effects.darkest"))
            foreach (var r in DarkestFile.Load(file).Where(r => r.Type == "effect" && r.Str("name") != null))
                lib.Effects[r.Str("name")] = r;
        // Campaign DLCs (the Shieldbreaker's skills) add their own; the base game's win a shared name.
        foreach (var dlc in dd1.DlcFolders())
        {
            string dir = Path.Combine(dlc, "effects");
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.effects.darkest").OrderBy(f => f, System.StringComparer.Ordinal))
                foreach (var r in DarkestFile.Load(file).Where(r => r.Type == "effect" && r.Str("name") != null))
                    if (!lib.Effects.ContainsKey(r.Str("name"))) lib.Effects[r.Str("name")] = r;
        }
        return lib;
    }

    public DarkestRecord Get(string name) => name != null && Effects.TryGetValue(name, out var e) ? e : null;
}
