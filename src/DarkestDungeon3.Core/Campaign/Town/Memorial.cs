using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

public sealed class MemorialCategory
{
    public string Name, Label, LabelBackdrop, EntryBackdrop, Filter;
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

public sealed class MemorialJournal
{
    public int Page;
    public string Title, Text;
}

public sealed class MemorialNarration
{
    public string PlotId, Category, AudioEvent, Sample, Portrait;
    public bool Complete;
    public string Caption(Dd1Lore lore) => lore.Text(Complete ? "str_" + PlotId + "_audio_line" : "str_caretaker_goal_" + PlotId);

    public static string SampleFor(string audioEvent)
    {
        const string load = "/vo/load/";
        if (audioEvent?.StartsWith(load) == true) return "vo_narr_load_" + audioEvent.Substring(load.Length);
        return audioEvent switch
        {
            "/vo/neutral/darkest_01_loading" => "vo_narr_neut_darkest01_load",
            "/vo/neutral/darkest_02_loading" => "vo_narr_neut_darkest02_load",
            "/vo/neutral/darkest_03_loading" => "vo_narr_neut_darkest03_load",
            _ => null,
        };
    }
}

/// <summary>DD1's Memorial media categories and video unlock rules, read from statue_media_info.json.</summary>
public sealed class Memorial
{
    public List<MemorialCategory> Categories = new();
    public List<MemorialVideo> Videos = new();
    public List<MemorialNarration> Narration = new();

    public IReadOnlyList<MemorialNarration> Narrations(Estate estate, Dd1Campaign dd1)
    {
        var goals = CaretakerGoals.Quests(estate, dd1).ToDictionary(g => g.Id);
        return Narration.Where(n => goals.ContainsKey(n.PlotId)).Select(n => new MemorialNarration
        {
            PlotId = n.PlotId, Category = n.Category, AudioEvent = n.AudioEvent, Sample = n.Sample,
            Portrait = n.Portrait, Complete = goals[n.PlotId].Complete,
        }).ToList();
    }

    public static IReadOnlyList<MemorialJournal> Journals(Estate estate, Dd1Lore lore) => estate.CollectedJournalPages
        .Where(p => p >= 0).OrderBy(p => p).Select(p => new MemorialJournal
        {
            Page = p, Title = lore.Text("journal_page_title_" + p), Text = lore.Text("journal_page_text_" + p)
        }).Where(p => !string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(p.Text)).ToList();

    public static Memorial Load(Dd1Install dd1)
    {
        var data = JObject.Parse(File.ReadAllText(dd1.PathOf("campaign", "town", "buildings", "statue", "statue_media_info.json")));
        var media = new Memorial
        {
            Categories = (data["categories"] ?? new JArray()).Select(c => new MemorialCategory
            {
                Name = (string)c["name"], Label = (string)c["label"], LabelBackdrop = (string)c["label_backdrop"],
                EntryBackdrop = (string)c["entry_backdrop"], Priority = (int?)c["sort_priority"] ?? 0, Filter = (string)c["regex_filter"],
            }).OrderBy(c => c.Priority).ToList(),
            Videos = (data["videos"] ?? new JArray()).Select(v => new MemorialVideo
            {
                Name = (string)v["name"], VideoName = (string)v["video_name"], Category = (string)v["category"],
                AccessIfPlot = (string)v["access_if_plot_finished"],
                ShowOnlyIfViewed = (bool?)v["show_only_if_previously_viewed"] ?? false,
                AccessOnlyIfViewed = (bool?)v["access_only_if_previously_viewed"] ?? false,
            }).ToList(),
        };
        var narration = JObject.Parse(File.ReadAllText(dd1.PathOf("audio", "narration.json")));
        foreach (var entry in (narration["entries"] ?? new JArray()).Where(e => (string)e["id"] == "loading_screen_start"))
        foreach (var audio in entry["audio_events"] ?? new JArray())
        foreach (var tag in audio["tags"] ?? new JArray())
        {
            string plot = (string)tag, audioEvent = (string)audio["audio_event"];
            var category = media.Categories.FirstOrDefault(c => (c.Name == "boss_entries" || c.Name == "dd_entries")
                && c.Filter != null && Regex.IsMatch(plot, "^(?:" + c.Filter + ")$"));
            string sample = MemorialNarration.SampleFor(audioEvent);
            if (category == null || sample == null || media.Narration.Any(n => n.PlotId == plot)) continue;
            string portrait = plot.StartsWith("plot_kill_") ? Regex.Replace(plot.Substring("plot_kill_".Length), "_[0-9]+$", "") : "darkest_dungeon";
            media.Narration.Add(new MemorialNarration { PlotId = plot, Category = category.Name, AudioEvent = audioEvent,
                Sample = sample, Portrait = "portrait_" + portrait + ".png" });
        }
        return media;
    }
}
