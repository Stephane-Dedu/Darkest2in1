using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// DD1's own English strings (localization/*.string_table.xml, which hold every language: we read the English
/// block), looked up by id. Loaded per table on first use.
/// </summary>
internal static class Dd1Text
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new();
    private static readonly Regex Entry = new("<entry id=\"([^\"]+)\"><!\\[CDATA\\[(.*?)\\]\\]></entry>", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string Get(string table, string id)
    {
        // A request before content is ready must not permanently cache an empty table.
        if (Session.Current?.Dd1 == null) return null;
        if (!Tables.TryGetValue(table, out var strings)) Tables[table] = strings = Load(table);
        return id != null && strings.TryGetValue(id, out var s) ? s : null;
    }

    private static Dictionary<string, string> Load(string table)
    {
        var strings = new Dictionary<string, string>();
        var path = Session.Current?.Dd1.PathOf("localization", table + ".string_table.xml");
        if (path == null || !File.Exists(path)) return strings;
        string xml = File.ReadAllText(path);
        int start = xml.IndexOf("<language id=\"english\"", System.StringComparison.Ordinal);
        int end = start >= 0 ? xml.IndexOf("</language>", start, System.StringComparison.Ordinal) : -1;
        string block = start >= 0 && end > start ? xml.Substring(start, end - start) : xml;
        foreach (Match m in Entry.Matches(block))
            if (!strings.ContainsKey(m.Groups[1].Value)) strings[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        return strings;
    }

    /// <summary>A camping skill's DD1 name ("Encourage"), else a tidied id.</summary>
    public static string CampSkillName(string id)
    {
        var s = Get("heroes", "camping_skill_name_" + id);
        return s != null ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()) : Ui.HamletUi.Pretty(id);
    }
}
