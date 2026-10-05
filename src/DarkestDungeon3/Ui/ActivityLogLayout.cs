using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;

namespace DarkestDungeon3.Ui;

/// <summary>Measured DD1 week entries. Rendering can visit only the rows intersecting the scroll viewport.</summary>
internal sealed class ActivityLogLayout
{
    public const float Width = 600, ViewWidth = 620, ViewHeight = 550;
    public enum Kind { Week, Town, Raid }
    public sealed class Row
    {
        public Kind Type;
        public string Text;
        public ActivityRaid Raid;
        public float Y, Height, NameHeight;
    }

    public List<Row> Rows { get; } = new();
    public float Height { get; private set; }

    public static ActivityLogLayout Build(IEnumerable<ActivityWeek> weeks, Func<string, float, float> measure, Func<string, string> regionName = null)
    {
        var layout = new ActivityLogLayout();
        foreach (var week in (weeks ?? Enumerable.Empty<ActivityWeek>()).OrderByDescending(w => w.Week))
        {
            if (week.Town.Count == 0 && week.Raids.Count == 0) continue;
            layout.Add(new Row { Type = Kind.Week, Text = $"Week {week.Week}", Height = 123 });
            // DD1 shows the return, the Hamlet's activities, then the departure for this week.
            foreach (var raid in week.Raids.Where(r => r.Result != "embark").Reverse()) layout.Party(raid, measure, regionName);
            foreach (var message in week.Town) layout.Message(message, measure);
            foreach (var raid in week.Raids.Where(r => r.Result == "embark").Reverse()) layout.Party(raid, measure, regionName);
        }
        return layout;
    }

    private void Add(Row row)
    {
        row.Y = Height;
        Rows.Add(row);
        Height += row.Height + 20; // activity_log_entry_layout.vertical_spacing
    }

    private void Message(string text, Func<string, float, float> measure) =>
        Add(new Row { Type = Kind.Town, Text = text, Height = Math.Max(120, measure(text, Width - 50) + 32) });

    private void Party(ActivityRaid raid, Func<string, float, float> measure, Func<string, string> regionName)
    {
        string quest = raid.Quest ?? "";
        string suffix = " in " + raid.Region;
        if (quest.EndsWith(suffix, StringComparison.Ordinal) && regionName != null)
            quest = quest.Substring(0, quest.Length - suffix.Length) + " in " + regionName(raid.Region);
        string text = (raid.Result == "embark" ? "Embarked: " : raid.Result == "complete" ? "Quest complete: " : raid.Result == "defeat" ? "Party lost: " : "Retreated: ") + quest;
        float nameHeight = raid.Heroes.Take(4).Select(h => measure(h.Name ?? "", 130)).DefaultIfEmpty(0).Max();
        Add(new Row { Type = Kind.Raid, Raid = raid, Text = text, NameHeight = nameHeight,
            Height = (raid.Result == "embark" ? 0 : 60) + 125 + nameHeight + measure(text, Width - 50) + 16 });
        foreach (var message in raid.Messages) Message(message, measure);
    }

    public IEnumerable<Row> Visible(float top, float height)
    {
        int low = 0, high = Rows.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (Rows[mid].Y + Rows[mid].Height <= top) low = mid + 1; else high = mid;
        }
        for (int i = low; i < Rows.Count && Rows[i].Y < top + height; i++) yield return Rows[i];
    }
}
