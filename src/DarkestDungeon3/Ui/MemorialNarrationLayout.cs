using System;
using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Ui;

internal sealed class MemorialNarrationLayout
{
    public sealed class Row
    {
        public MemorialNarration Narration;
        public string Caption;
        public float Height, TextHeight;
    }

    public List<Row> Rows = new();
    public float Height;

    public static MemorialNarrationLayout Build(IEnumerable<MemorialNarration> entries, Dd1Lore lore, Func<string, float> measure)
    {
        var layout = new MemorialNarrationLayout();
        foreach (var entry in entries)
        {
            string caption = entry.Caption(lore) ?? "";
            float textHeight = Math.Max(96, measure(caption));
            float height = textHeight + 24;
            layout.Rows.Add(new Row { Narration = entry, Caption = caption, Height = height, TextHeight = textHeight });
            layout.Height += height + 10;
        }
        return layout;
    }
}
