using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Small private native VFX atlas pack; only flat PNG filenames and bounded animation grids are accepted.</summary>
public sealed class ExpeditionMapVfx
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal) { "mist", "smoke", "fire", "ember", "gas", "forest" };
    public sealed class Clip
    {
        public string Id, File;
        public int Columns, Rows, Frames;
        public float Fps;

        public ExpeditionMap3D.MapRect Uv(float seconds, float phase = 0)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
            if (float.IsNaN(phase) || float.IsInfinity(phase)) phase = 0;
            double frame = Math.Floor((double)seconds * Fps + phase * Frames) % Frames;
            int index = (int)(frame < 0 ? frame + Frames : frame);
            return new ExpeditionMap3D.MapRect((index % Columns) / (float)Columns,
                1 - (index / Columns + 1) / (float)Rows, 1f / Columns, 1f / Rows);
        }
    }

    private readonly Dictionary<string, Clip> _clips = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, Clip> Clips => _clips;

    public static ExpeditionMapVfx Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JObject root;
        try { root = JObject.Parse(json); } catch { return null; }
        if (root["effects"] is not JArray effects || effects.Count > 12) return null;
        var pack = new ExpeditionMapVfx();
        foreach (var effect in effects)
        {
            if (effect is not JObject o || o["id"]?.Type != JTokenType.String || o["file"]?.Type != JTokenType.String) continue;
            string id = (string)o["id"], file = (string)o["file"];
            if (!Known.Contains(id) || string.IsNullOrEmpty(file) || file.Length > 100 || !file.EndsWith(".png", StringComparison.Ordinal)
                || file.Contains("/") || file.Contains("\\") || file.Contains(":") || file.Contains("..")) continue;
            if (!Integer(o["columns"], out int columns) || !Integer(o["rows"], out int rows) || !Integer(o["frames"], out int frames)
                || columns < 1 || columns > 8 || rows < 1 || rows > 8 || frames < 1 || frames > columns * rows
                || o["fps"]?.Type is not (JTokenType.Float or JTokenType.Integer)) continue;
            if (!float.TryParse(o["fps"].ToString(Newtonsoft.Json.Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out float fps)) continue;
            // Zero FPS selects a static atlas variant by phase (native sparks and soft washes).
            if (float.IsNaN(fps) || float.IsInfinity(fps) || fps < 0 || fps > 60) continue;
            pack._clips[id] = new Clip { Id = id, File = file, Columns = columns, Rows = rows, Frames = frames, Fps = fps };
        }
        return pack._clips.Count > 0 ? pack : null;
    }

    private static bool Integer(JToken token, out int value)
    {
        value = 0;
        if (token?.Type != JTokenType.Integer) return false;
        if (!int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            || number < 0 || number > 64) return false;
        value = (int)number; return true;
    }
}
