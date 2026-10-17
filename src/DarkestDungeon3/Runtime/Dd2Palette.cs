using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>UI colours from the installed DD2 localization palette, without starting a run library.</summary>
internal static class Dd2Palette
{
    private static Dictionary<string, Color> _colours;

    internal static Color Get(string key, Color fallback)
    {
        if (_colours == null)
        {
            _colours = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string file = Path.Combine(Application.streamingAssetsPath, "Localization", "Sources", "colors.txt");
                foreach (string line in File.ReadLines(file))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && ColorUtility.TryParseHtmlString(line.Substring(eq + 1).Trim(), out var colour))
                        _colours[line.Substring(0, eq).Trim()] = colour;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[UI] native palette: " + e.Message); }
        }
        return _colours.TryGetValue(key, out var result) ? result : fallback;
    }
}
