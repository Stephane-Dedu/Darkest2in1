using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Dd2Data;

namespace DarkestDungeon3.Dd2;

/// <summary>DD2 effects for DD1 tooltips, including the cold Hamlet before run libraries exist.</summary>
internal static class ItemText
{
    private static readonly Dictionary<string, string> Cache = new();
    private static object _library, _language;
    private static Task<TrinketDescriptions> _reading;
    private static bool _warned;

    public static void Prime()
    {
        if (_reading != null) return;
        string path = UnityEngine.Application.streamingAssetsPath;
        _reading = Task.Run(() => TrinketDescriptions.Load(path));
    }

    public static string Effects(string itemId)
    {
        if (itemId == null) return null;
        Prime();
        var library = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Library.Library<string, Assets.Code.Item.ItemDefinition>>.Instance;
        var loc = Assets.Code.Utils.Singleton<Assets.Code.Locale.Localization>.Instance;
        object language = loc?.GetLanguage();
        if (!ReferenceEquals(_library, library) || !ReferenceEquals(_language, language))
        {
            Cache.Clear();
            _library = library;
            _language = language;
        }
        if (Cache.TryGetValue(itemId, out var cached)) return cached;
        try
        {
            // TryGet avoids triggering the library's empty-library validation while it is loading.
            if (library != null && library.IsInitializationFinished() && library.TryGetLibraryElement(itemId, out var def) && def != null)
            {
                string text = TrinketDescriptions.Plain(Assets.Code.Item.ItemDescription.GetDescription(def, 1, false, 0, false, false,
                    hideTitle: true, showBlockedItems: false, showItemTag: false));
                if (!string.IsNullOrWhiteSpace(text)) return Cache[itemId] = text;
            }
        }
        catch (Exception e) { Warn(e); }
        // Do not cache misses or fallbacks: native libraries and localization can become ready next frame.
        if (_reading.IsCompleted)
        {
            try { return _reading.GetAwaiter().GetResult().Effects(itemId, out _, key => loc?.TryGetString(key)); }
            catch (Exception e) { Warn(e); }
        }
        return null;
    }

    private static void Warn(Exception e)
    {
        if (!_warned) Plugin.Log.LogWarning("[items] description: " + e.Message);
        _warned = true;
    }
}
