using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>
/// DD1's cinematics (video/&lt;name&gt;.ogv: Theora video + Vorbis audio). A new campaign opens with "House of Ruin"
/// ("Ruin has come to our family...") then "The Old Road". Their subtitles are the .sub file's timings (start,end in
/// milliseconds, one line each) paired in order with localization's <c>str_vo_&lt;name&gt;_N</c>.
/// </summary>
public static class Dd1Cinematic
{
    public static readonly string[] Opening = { "house_of_ruin", "old_road" };

    public static string VideoPath(Dd1Install dd1, string name) => dd1.PathOf("video", name + ".ogv");

    public static List<(int StartMs, int EndMs, string Text)> Subtitles(Dd1Install dd1, string name)
    {
        var lines = new List<(int, int, string)>();
        string sub = dd1.PathOf("video", name + ".sub");
        if (!File.Exists(sub)) return lines;
        var texts = Strings(dd1, "str_vo_" + name + "_");
        int i = 0;
        foreach (var row in File.ReadAllLines(sub))
        {
            var parts = row.Split(',');
            if (parts.Length < 2 || !int.TryParse(parts[0].Trim(), out int start) || !int.TryParse(parts[1].Trim(), out int end)) continue;
            lines.Add((start, end, texts.TryGetValue(i, out var t) ? t : ""));
            i++;
        }
        return lines;
    }

    /// <summary>The subtitle on screen at this time, or null.</summary>
    public static string SubtitleAt(List<(int StartMs, int EndMs, string Text)> lines, double seconds)
    {
        int ms = (int)(seconds * 1000);
        foreach (var (start, end, text) in lines)
            if (ms >= start && ms <= end) return text;
        return null;
    }

    /// <summary>
    /// The audio of an .ogv as a plain Ogg Vorbis file: the pages of the logical stream whose first packet is a Vorbis
    /// header, copied as they are (their checksums and sequence numbers stay valid).
    /// </summary>
    public static byte[] VorbisAudio(byte[] ogv)
    {
        var output = new MemoryStream();
        int? vorbis = null;
        int pos = 0;
        while (pos + 27 <= ogv.Length && ogv[pos] == 'O' && ogv[pos + 1] == 'g' && ogv[pos + 2] == 'g' && ogv[pos + 3] == 'S')
        {
            byte flags = ogv[pos + 5];
            int serial = BitConverter.ToInt32(ogv, pos + 14);
            int segments = ogv[pos + 26];
            if (pos + 27 + segments > ogv.Length) break;
            int body = 0;
            for (int s = 0; s < segments; s++) body += ogv[pos + 27 + s];
            int length = 27 + segments + body;
            if (pos + length > ogv.Length) break;
            int data = pos + 27 + segments;
            if ((flags & 0x02) != 0 && vorbis == null && body >= 7 && ogv[data] == 1 &&
                ogv[data + 1] == 'v' && ogv[data + 2] == 'o' && ogv[data + 3] == 'r' && ogv[data + 4] == 'b' && ogv[data + 5] == 'i' && ogv[data + 6] == 's')
                vorbis = serial;
            if (vorbis == serial) output.Write(ogv, pos, length);
            pos += length;
        }
        return output.ToArray();
    }

    private static Dictionary<int, string> Strings(Dd1Install dd1, string prefix)
    {
        var result = new Dictionary<int, string>();
        string path = dd1.PathOf("localization", "miscellaneous.string_table.xml");
        if (!File.Exists(path)) return result;
        string xml = File.ReadAllText(path);
        int a = xml.IndexOf("<language id=\"english\"");
        int b = a < 0 ? -1 : xml.IndexOf("</language>", a);
        if (a < 0 || b < 0) return result;
        foreach (Match m in Regex.Matches(xml.Substring(a, b - a), "<entry id=\"" + Regex.Escape(prefix) + "(\\d+)\"><!\\[CDATA\\[(.*?)\\]\\]>", RegexOptions.Singleline))
            result[int.Parse(m.Groups[1].Value)] = Regex.Replace(m.Groups[2].Value, "\\s+", " ").Trim();
        return result;
    }
}
