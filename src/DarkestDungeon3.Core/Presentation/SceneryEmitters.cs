using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Optional native fire bounds projected into a private room plate. No paths or gameplay data.</summary>
public sealed class SceneryEmitters
{
    private readonly Dictionary<string,IReadOnlyList<ExpeditionMap3D.MapRect>> _scenes = new(StringComparer.Ordinal);
    private static readonly IReadOnlyList<ExpeditionMap3D.MapRect> Empty = Array.Empty<ExpeditionMap3D.MapRect>();
    public IReadOnlyList<ExpeditionMap3D.MapRect> For(string file) => file != null && _scenes.TryGetValue(file,out var bounds) ? bounds : Empty;

    public static SceneryEmitters Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var root = JObject.Parse(json);
            if (root["scenes"] is not JObject scenes || scenes.Count > 64) return null;
            var result = new SceneryEmitters();
            foreach (var entry in scenes.Properties())
            {
                if (!entry.Name.StartsWith("dd2_city-arena-",StringComparison.Ordinal) || !entry.Name.EndsWith(".png",StringComparison.Ordinal)
                    || entry.Name.Length > 100 || entry.Name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || entry.Name.Contains("..")
                    || entry.Value is not JArray anchors || anchors.Count > 16) continue;
                var bounds = new List<ExpeditionMap3D.MapRect>();
                foreach (var anchor in anchors)
                {
                    if (anchor is not JArray a || a.Count != 4) continue;
                    var f = new float[4];
                    bool valid = true;
                    for (int i = 0; i < 4; i++)
                    {
                        if (a[i].Type is not (JTokenType.Float or JTokenType.Integer)) { valid = false; break; }
                        f[i] = (float)a[i];
                        if (float.IsNaN(f[i]) || float.IsInfinity(f[i])) valid = false;
                    }
                    if (!valid || f[0] < 0 || f[0] > 1920 || f[1] < 0 || f[1] > 600 || f[2] < 8 || f[2] > 450 || f[3] < 8 || f[3] > 450) continue;
                    bounds.Add(new ExpeditionMap3D.MapRect(f[0],f[1],f[2],f[3]));
                }
                result._scenes[entry.Name] = bounds;
            }
            return result;
        }
        catch { return null; }
    }
}
