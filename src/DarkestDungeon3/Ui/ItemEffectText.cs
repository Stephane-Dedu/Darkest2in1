using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Ui;

/// <summary>Keep the supplied effect wording/order, separating trigger labels from their effects.</summary>
internal static class ItemEffectText
{
    internal readonly struct Line
    {
        internal readonly string Text;
        internal readonly bool Condition, Indent;
        internal Line(string text, bool condition = false, bool indent = false)
        { Text = text; Condition = condition; Indent = indent; }
    }

    internal static IEnumerable<Line> Lines(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            int colon = line.IndexOf(':');
            // A label such as "Turn Start:" is presentation, not a new gameplay rule.
            // Numeric ratios/times stay intact. Do not guess beneficial/harmful effects by sign.
            if (colon > 0 && colon < 120 && !char.IsDigit(line[colon - 1]))
            {
                yield return new Line(line.Substring(0, colon + 1), condition: true);
                string effect = line.Substring(colon + 1).TrimStart();
                if (effect.Length > 0) yield return new Line(effect, indent: true);
            }
            else yield return new Line(line);
        }
    }
}
