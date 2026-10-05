using System;
using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign.Town;

namespace DarkestDungeon3.Ui;

/// <summary>Measured native journal cards, independent of Unity drawing and estate state.</summary>
internal sealed class MemorialJournalLayout
{
    public sealed class Row
    {
        public MemorialJournal Journal;
        public float Y, Height, TitleHeight, BodyHeight;
    }

    public List<Row> Rows = new();
    public float Height;

    public static MemorialJournalLayout Build(IEnumerable<MemorialJournal> journals, Func<string, float> titleHeight, Func<string, float> bodyHeight)
    {
        var layout = new MemorialJournalLayout();
        foreach (var journal in journals)
        {
            float title = Math.Max(50, titleHeight(journal.Title));
            float body = Math.Max(30, bodyHeight(journal.Text));
            float height = Math.Max(130, title + body + 32);
            layout.Rows.Add(new Row { Journal = journal, Y = layout.Height, Height = height, TitleHeight = title, BodyHeight = body });
            layout.Height += height + 10;
        }
        return layout;
    }
}
