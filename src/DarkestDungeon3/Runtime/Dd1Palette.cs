using System;
using DarkestDungeon3.Core.Dd1;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal static class Dd1Palette
{
    private static Dd1Colours _colours;
    public static Color Get(string id, Color fallback)
    {
        if (_colours == null && Session.Current != null)
        {
            try { _colours = Dd1Colours.Load(Session.Current.Dd1.PathOf("colours", "base.colours.darkest")); }
            catch (Exception e) { Plugin.Log.LogWarning("[art] DD1 palette: " + e.Message); _colours = Dd1Colours.Parse(""); }
        }
        if (_colours?.Rgba(id) is not uint rgba) return fallback;
        return new Color((rgba >> 24) / 255f, ((rgba >> 16) & 255) / 255f, ((rgba >> 8) & 255) / 255f, (rgba & 255) / 255f);
    }
}
