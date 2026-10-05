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
        using var input = new MemoryStream(ogv, writable: false);
        using var output = new MemoryStream();
        VorbisAudio(input, output);
        return output.ToArray();
    }

    /// <summary>Copy complete Vorbis pages sequentially using at most one Ogg page of memory.
    /// Does not seek or close the caller's streams. An incomplete or non-Ogg tail is omitted.</summary>
    public static long VorbisAudio(Stream input, Stream output)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (output == null) throw new ArgumentNullException(nameof(output));
        // 27-byte header + 255 lacing values + 255 * 255 body bytes.
        var page = new byte[65307];
        int? vorbis = null;
        long copied = 0;
        while (ReadPart(input, page, 0, 27) && page[0] == 'O' && page[1] == 'g' && page[2] == 'g' && page[3] == 'S')
        {
            byte flags = page[5];
            int serial = page[14] | page[15] << 8 | page[16] << 16 | page[17] << 24;
            int segments = page[26];
            if (!ReadPart(input, page, 27, segments)) break;
            int body = 0;
            for (int s = 0; s < segments; s++) body += page[27 + s];
            int length = 27 + segments + body;
            int data = 27 + segments;
            if (!ReadPart(input, page, data, body)) break;
            if ((flags & 0x02) != 0 && vorbis == null && body >= 7 && page[data] == 1 &&
                page[data + 1] == 'v' && page[data + 2] == 'o' && page[data + 3] == 'r' && page[data + 4] == 'b' && page[data + 5] == 'i' && page[data + 6] == 's')
                vorbis = serial;
            if (vorbis == serial)
            {
                output.Write(page, 0, length);
                copied += length;
            }
        }
        return copied;
    }

    private static bool ReadPart(Stream input, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = input.Read(buffer, offset, count);
            if (read == 0) return false;
            offset += read;
            count -= read;
        }
        return true;
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
