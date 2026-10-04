using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

public sealed class MemorialCategory
{
    public string Name, Label, LabelBackdrop, EntryBackdrop;
    public int Priority;
}

public sealed class MemorialVideo
{
    public string Name, VideoName, Category, AccessIfPlot;
    public bool ShowOnlyIfViewed, AccessOnlyIfViewed;

    public bool Visible(ICollection<string> viewed = null) => !ShowOnlyIfViewed || viewed?.Contains(Name) == true;
    public bool CanPlay(ICollection<string> completedPlots, ICollection<string> viewed = null) =>
        !AccessOnlyIfViewed || viewed?.Contains(Name) == true || (AccessIfPlot != null && completedPlots.Contains(AccessIfPlot));
}

/// <summary>DD1's Memorial media categories and video unlock rules, read from statue_media_info.json.</summary>
public sealed class Memorial
{
    public List<MemorialCategory> Categories = new();
    public List<MemorialVideo> Videos = new();

    public static Memorial Load(Dd1Install dd1)
    {
        var data = JObject.Parse(File.ReadAllText(dd1.PathOf("campaign", "town", "buildings", "statue", "statue_media_info.json")));
        return new Memorial
        {
            Categories = (data["categories"] ?? new JArray()).Select(c => new MemorialCategory
            {
                Name = (string)c["name"], Label = (string)c["label"], LabelBackdrop = (string)c["label_backdrop"],
                EntryBackdrop = (string)c["entry_backdrop"], Priority = (int?)c["sort_priority"] ?? 0,
            }).OrderBy(c => c.Priority).ToList(),
            Videos = (data["videos"] ?? new JArray()).Select(v => new MemorialVideo
            {
                Name = (string)v["name"], VideoName = (string)v["video_name"], Category = (string)v["category"],
                AccessIfPlot = (string)v["access_if_plot_finished"],
                ShowOnlyIfViewed = (bool?)v["show_only_if_previously_viewed"] ?? false,
                AccessOnlyIfViewed = (bool?)v["access_only_if_previously_viewed"] ?? false,
            }).ToList(),
        };
    }
}
