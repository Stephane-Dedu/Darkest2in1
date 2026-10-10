using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>DD1 identity and restrictions, without substituting DD2 items or translating effects.</summary>
public sealed class Dd1Trinket
{
    public string Id { get; }
    public string Rarity { get; }
    public string OriginDungeon { get; }
    public int Price { get; }
    public int Limit { get; }
    public IReadOnlyList<string> BuffIds { get; }
    public IReadOnlyList<string> HeroClasses { get; }

    internal Dd1Trinket(JObject entry)
    {
        Id = (string)entry["id"];
        Rarity = (string)entry["rarity"];
        OriginDungeon = (string)entry["origin_dungeon"];
        Price = (int?)entry["price"] ?? -1;
        Limit = (int?)entry["limit"] ?? -1;
        BuffIds = Strings(entry, "buffs");
        HeroClasses = Strings(entry, "hero_class_requirements");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Rarity)
            || OriginDungeon == null || Price < 0 || Limit < 0)
            throw new FormatException("Incomplete DD1 trinket identity: " + Id);
    }

    private static IReadOnlyList<string> Strings(JObject entry, string key)
    {
        if (entry[key] is not JArray array || array.Any(t => t.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)t)))
            throw new FormatException("Invalid DD1 trinket " + key);
        return Array.AsReadOnly(array.Values<string>().ToArray());
    }
}

/// <summary>Base-file coverage only. Exact rarity queries do not settle DLC, ownership or reward weighting.</summary>
public sealed class Dd1Trinkets
{
    private readonly Dictionary<string, Dd1Trinket> _entries = new(StringComparer.Ordinal);
    public int Count => _entries.Count;
    public Dd1Trinket Get(string id) => id != null && _entries.TryGetValue(id, out var item) ? item : null;

    public IReadOnlyList<Dd1Trinket> ForRarity(string rarity) => Array.AsReadOnly(_entries.Values
        .Where(t => string.Equals(t.Rarity, rarity, StringComparison.Ordinal)).OrderBy(t => t.Id, StringComparer.Ordinal).ToArray());

    public static Dd1Trinkets LoadBase(Dd1Install dd1)
    {
        if (dd1 == null) throw new ArgumentNullException(nameof(dd1));
        return Parse(File.ReadAllText(dd1.PathOf("trinkets", "base.entries.trinkets.json")));
    }

    public static Dd1Trinkets Parse(string json)
    {
        if (JToken.Parse(json)["entries"] is not JArray entries)
            throw new FormatException("Missing DD1 trinket entries.");
        var result = new Dd1Trinkets();
        var keys = new HashSet<string>(new[] { "id", "rarity", "origin_dungeon", "price", "limit", "buffs", "hero_class_requirements" }, StringComparer.Ordinal);
        foreach (var token in entries)
        {
            if (token is not JObject entry || entry.Properties().Any(p => !keys.Contains(p.Name)))
                throw new FormatException("Unsupported DD1 trinket definition.");
            var item = new Dd1Trinket(entry);
            if (result._entries.ContainsKey(item.Id)) throw new FormatException("Duplicate DD1 trinket: " + item.Id);
            result._entries.Add(item.Id, item);
        }
        return result;
    }
}
