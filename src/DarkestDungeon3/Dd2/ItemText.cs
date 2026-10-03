using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Dd2;

/// <summary>DD2's own description of an item (a trinket's effects), as plain text for our DD1 tooltips.</summary>
internal static class ItemText
{
    private static readonly Dictionary<string, string> Cache = new();
    private static bool _warned;

    /// <summary>The effects DD2 lists for this item, one per line, or null.</summary>
    public static string Effects(string itemId)
    {
        if (itemId == null) return null;
        if (Cache.TryGetValue(itemId, out var text)) return text;
        try
        {
            var library = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Library.Library<string, Assets.Code.Item.ItemDefinition>>.Instance;
            var def = library?.GetLibraryElement(itemId);
            if (def != null)
                text = Plain(Assets.Code.Item.ItemDescription.GetDescription(def, 1, false, 0, false, false, hideTitle: true, showBlockedItems: false, showItemTag: false));
        }
        catch (Exception e)
        {
            if (!_warned) Plugin.Log.LogWarning("[items] description: " + e.Message);
            _warned = true;
        }
        return Cache[itemId] = text;
    }

    /// <summary>DD2's rich text as plain lines: icons become their names, other tags go, blank lines collapse.</summary>
    private static string Plain(string rich)
    {
        if (string.IsNullOrEmpty(rich)) return null;
        string s = Regex.Replace(rich, "<sprite[^>]*name=\"?([A-Za-z0-9_]+)\"?[^>]*>", m => Pretty(m.Groups[1].Value) + " ");
        s = Regex.Replace(s, "<[^>]*>", "");
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @"\s*\n\s*", "\n").Trim();
        return s.Length == 0 ? null : s;
    }

    private static string Pretty(string id)
    {
        string s = id.Replace("token_", "").Replace("icon_", "").Replace('_', ' ').Trim();
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
