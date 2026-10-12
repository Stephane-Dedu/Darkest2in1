using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>Prepares campaign assets without opening or changing an estate.</summary>
internal static class CampaignAssets
{
    private static bool _requested, _queued;
    private static float _since;
    private static string _error;
    private static Estate _selected;
    private static bool _estateQueued;
    private static readonly HashSet<string> Buildings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Heroes = new();

    public static void Begin(Estate estate = null)
    {
        _requested = true;
        _since = Time.unscaledTime;
        _error = null;
        _selected = estate;
        _estateQueued = false;
        ActorResources.RetryFailures();
    }

    public static CampaignLoadStatus Status => new(Session.Current != null && _queued && _estateQueued ? 1 : Math.Min(Session.LoadProgress, .99f),
        Art.PreparedImages, Art.RequestedImages, Buildings.Count(folder => SpineArt.IsFinished(folder)), Buildings.Count,
        Heroes.Count(ActorResources.IsFinished), Heroes.Count, Session.LoadStage, Session.LoadError ?? ActorResources.LoadError ?? _error);

    public static void Update()
    {
        if (!_requested || _error != null) return;
        if (Session.Current == null)
        {
            if (Time.unscaledTime - _since > 90f) _error = "Campaign data is taking too long to load. Retry, or return to the main menu.";
            return;
        }
        try
        {
            var session = Session.Current;
            if (!_queued)
            {
                Art.PrepareTown(session.Dd1);
                foreach (var spot in session.TownLayout.Spots.Where(s => s.Id != "circus"))
                {
                    foreach (bool open in new[] { false, true })
                    {
                        string folder = TownLayout.ArtFolder(session.Dd1, spot.Id, open, 0);
                        if (Buildings.Add(folder))
                            SpineArt.Get(folder, "idle", slot => TownLayout.IdleSlot(slot.Name));
                    }
                }
                _queued = true;
            }
            if (!_estateQueued)
            {
                if (_selected != null)
                {
                    Art.PrepareImage(session.Dd1.PathOf(session.Campaign.TownRender.Background(_selected.LastReturnPlotId, _selected.TownEventId).Split('/')));
                    foreach (var spot in session.TownLayout.Spots.Where(s => s.Id != "circus"))
                    {
                        var trees = session.Buildings.Trees.Trees.Where(t => t.Key.StartsWith(spot.Id + ".")).ToList();
                        int total = trees.Sum(t => t.Value.Count);
                        float upgraded = total == 0 ? 0 : trees.Sum(t => t.Value.Count(l => _selected.Upgrades.Contains(l.Key))) / (float)total;
                        bool open = spot.Id == Core.Campaign.Town.Buildings.Memorial || spot.Id == Core.Campaign.Town.Buildings.Graveyard || session.Buildings.IsOpen(spot.Id, _selected);
                        string folder = TownLayout.ArtFolder(session.Dd1, spot.Id, open, upgraded);
                        if (Buildings.Add(folder)) SpineArt.Get(folder, "idle", slot => TownLayout.IdleSlot(slot.Name));
                    }
                }
                _estateQueued = true;
            }
            // The owned-DLC location result must arrive before choosing recruitable classes.
            ActorResources.Has(RecruitClasses.Crusader);
            if (ActorResources.LocationsReady)
            {
                if (Heroes.Count == 0)
                    Heroes.AddRange(RecruitClasses.For(ActorResources.Has(RecruitClasses.Crusader) == true,
                        ownsDuelist: ActorResources.Has(RecruitClasses.Duelist) == true,
                        ownsAbomination: ActorResources.Has(RecruitClasses.Abomination) == true)
                        .Where(id => !RecruitClasses.IsDd1Only(id)));
                foreach (string id in Heroes) Art.Portrait(id);
            }
            if (Status.Ready)
            {
                _requested = false;
                Plugin.Log.LogInfo($"[campaign assets] prepared {Art.PreparedImages} PNGs, {Buildings.Count} town renders, {Heroes.Count} native heroes in {Time.unscaledTime - _since:0.00}s");
            }
            else if (Time.unscaledTime - _since > 90f)
                _error = "Campaign assets are taking too long to load. Retry, or return to the main menu.";
        }
        catch (Exception e)
        {
            _error = "Campaign assets could not load: " + e.Message;
            Plugin.Log.LogError("[campaign assets] " + e);
        }
    }
}
