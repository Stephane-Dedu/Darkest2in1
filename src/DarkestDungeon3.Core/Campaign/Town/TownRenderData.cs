using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>
/// DD1's town display states (campaign/town/town_render_data.json): the Hamlet's background after returning from a
/// Darkest Dungeon part ("active_on_plot_quest_return") or during certain town events ("active_on_town_event"),
/// else the regular sky.
/// </summary>
public sealed class TownRenderData
{
    public sealed class State
    {
        public string Name, PlotQuest, TownEvent, Background;
    }

    public List<State> States { get; } = new();

    public static TownRenderData Load(Dd1Install dd1)
    {
        var data = new TownRenderData();
        var path = dd1.PathOf("campaign", "town", "town_render_data.json");
        if (!File.Exists(path)) return data;
        foreach (var s in JToken.Parse(File.ReadAllText(path))["display_states"] as JArray ?? new JArray())
            data.States.Add(new State
            {
                Name = (string)s["name"],
                PlotQuest = (string)s["active_on_plot_quest_return"],
                TownEvent = (string)s["active_on_town_event"],
                Background = (string)s["background_texture"],
            });
        return data;
    }

    /// <summary>The background texture (DD1-relative path) for this visit: the plot quest just returned from, then
    /// the week's town event, else the regular state.</summary>
    public string Background(string returnedFromPlotQuest, string townEvent)
    {
        var state = States.FirstOrDefault(s => !string.IsNullOrEmpty(s.PlotQuest) && s.PlotQuest == returnedFromPlotQuest)
                    ?? States.FirstOrDefault(s => !string.IsNullOrEmpty(s.TownEvent) && s.TownEvent == townEvent)
                    ?? States.FirstOrDefault(s => string.IsNullOrEmpty(s.PlotQuest) && string.IsNullOrEmpty(s.TownEvent));
        return state?.Background ?? "campaign/town/town_bg.png";
    }
}
