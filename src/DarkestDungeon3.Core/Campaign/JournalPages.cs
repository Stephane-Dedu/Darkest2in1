using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Campaign;

public static class JournalPages
{
    public const string Prefix = "journal_page+";
    public static string Key(int page) => Prefix + page.ToString(CultureInfo.InvariantCulture);

    public static bool TryPage(string key, out int page)
    {
        page = -1;
        return key != null && key.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(key.Substring(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 0;
    }

    /// <summary>Only surviving, carried pages become estate history; duplicate copies confer no extra reward.</summary>
    public static List<int> Collect(Estate estate, Inventory pack, bool survived)
    {
        if (!survived) return new List<int>();
        return pack.Items.Where(kv => kv.Value > 0 && TryPage(kv.Key, out _))
            .Select(kv => { TryPage(kv.Key, out int page); return page; }).Distinct().OrderBy(p => p)
            .Where(estate.CollectedJournalPages.Add).ToList();
    }
}
