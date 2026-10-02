using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// One record of a DD1 <c>.darkest</c> file, e.g. <c>hall: .chance 2 .types skeleton_common_A maggot_A</c>.
/// A record starts with <c>name:</c> and owns every <c>.key value...</c> pair until the next record.
/// </summary>
public sealed class DarkestRecord
{
    private readonly Dictionary<string, List<string>> _params = new(StringComparer.Ordinal);

    public string Type { get; }
    public IReadOnlyDictionary<string, List<string>> Params => _params;

    public DarkestRecord(string type) => Type = type;

    internal List<string> Begin(string key)
    {
        // A repeated key keeps all values, in order.
        if (!_params.TryGetValue(key, out var list)) _params[key] = list = new List<string>();
        return list;
    }

    public bool Has(string key) => _params.ContainsKey(key);

    public IReadOnlyList<string> Values(string key) =>
        _params.TryGetValue(key, out var v) ? v : (IReadOnlyList<string>)Array.Empty<string>();

    public string Str(string key, string fallback = null) =>
        _params.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : fallback;

    public int Int(string key, int index = 0, int fallback = 0) =>
        _params.TryGetValue(key, out var v) && v.Count > index
            ? (int)Math.Round(double.Parse(v[index], CultureInfo.InvariantCulture))
            : fallback;

    public float Float(string key, int index = 0, float fallback = 0f) =>
        _params.TryGetValue(key, out var v) && v.Count > index
            ? float.Parse(v[index], CultureInfo.InvariantCulture)
            : fallback;

    /// <summary>A "min max" pair; a single value means min == max.</summary>
    public IntRange Range(string key)
    {
        var v = Values(key);
        if (v.Count == 0) return new IntRange(0, 0);
        int a = ParseInt(v[0]);
        int b = v.Count > 1 ? ParseInt(v[1]) : a;
        return new IntRange(Math.Min(a, b), Math.Max(a, b));
    }

    private static int ParseInt(string s) => (int)Math.Round(double.Parse(s, CultureInfo.InvariantCulture));

    public override string ToString() =>
        Type + ": " + string.Join(" ", _params.Select(p => "." + p.Key + " " + string.Join(" ", p.Value)));
}

public readonly struct IntRange
{
    public readonly int Min, Max;
    public IntRange(int min, int max) { Min = min; Max = max; }
    public int Roll(Random rng) => rng.Next(Min, Max + 1);
    public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
}

/// <summary>Parser for DD1's plain-text <c>.darkest</c> format.</summary>
public static class DarkestFile
{
    public static List<DarkestRecord> Load(string path) => Parse(File.ReadAllText(path));

    public static List<DarkestRecord> Parse(string text)
    {
        var records = new List<DarkestRecord>();
        DarkestRecord current = null;
        List<string> values = null;

        foreach (var token in Tokenize(text))
        {
            if (token.Quoted)
            {
                values?.Add(token.Text);
            }
            else if (token.Text.EndsWith(":", StringComparison.Ordinal) && token.Text.Length > 1 && !token.Text.StartsWith(".", StringComparison.Ordinal))
            {
                current = new DarkestRecord(token.Text.Substring(0, token.Text.Length - 1));
                records.Add(current);
                values = null;
            }
            else if (token.Text.Length > 1 && token.Text[0] == '.' && !char.IsDigit(token.Text[1]))
            {
                if (current == null) { current = new DarkestRecord(""); records.Add(current); }
                values = current.Begin(token.Text.Substring(1));
            }
            else
            {
                values?.Add(token.Text);
            }
        }
        return records;
    }

    private readonly struct Token
    {
        public readonly string Text;
        public readonly bool Quoted;
        public Token(string text, bool quoted) { Text = text; Quoted = quoted; }
    }

    private static IEnumerable<Token> Tokenize(string text)
    {
        int i = 0, n = text.Length;
        var sb = new StringBuilder();
        while (i < n)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < n && text[i + 1] == '/')
            {
                while (i < n && text[i] != '\n') i++;
                continue;
            }
            if (c == '"')
            {
                sb.Clear();
                i++;
                while (i < n && text[i] != '"') sb.Append(text[i++]);
                i++;
                yield return new Token(sb.ToString(), quoted: true);
                continue;
            }
            sb.Clear();
            while (i < n && !char.IsWhiteSpace(text[i])) sb.Append(text[i++]);
            yield return new Token(sb.ToString(), quoted: false);
        }
    }
}
