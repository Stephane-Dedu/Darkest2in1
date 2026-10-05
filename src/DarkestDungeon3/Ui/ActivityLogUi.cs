using System.Linq;
using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed class ActivityLogUi
{
    private Estate _estate;
    private ActivityLogLayout _layout;
    private int _week, _weeks, _town, _raids;
    private Dd1Font _font;
    private Vector2 _goalScroll;
    private IReadOnlyList<string> _goalSource;
    private List<string> _goalClasses, _goalTexts;
    private List<float> _goalHeights;
    private Dd1Font _goalFont;
    private bool _showQuests = true;
    private Vector2 _questScroll;
    private Estate _questEstate;
    private Dd1Campaign _questCampaign;
    private int _questRegions, _questCompleted;
    private Dd1Font _questFont;
    private IReadOnlyList<CaretakerQuestGoal> _questGoals;
    private List<string> _questTexts;
    private List<float> _questHeights;

    public void Draw(Rect window, Estate estate, ref Vector2 scroll)
    {
        Gui.Text(new Rect(window.x + 70, window.y + 35, 570, 65), "Activity Log", 36, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var current = estate.ActivityLog?.FirstOrDefault(w => w.Week == estate.Week);
        int weeks = estate.ActivityLog?.Count ?? 0, town = current?.Town.Count ?? 0, raids = current?.Raids.Count ?? 0;
        var font = Dd1Font.Body;
        if (_estate != estate || _layout == null || _week != estate.Week || _weeks != weeks || _town != town || _raids != raids || _font != font)
        {
            if (_estate != estate) { scroll = Vector2.zero; _goalScroll = Vector2.zero; _questScroll = Vector2.zero; _showQuests = true; }
            _estate = estate; _week = estate.Week; _weeks = weeks; _town = town; _raids = raids; _font = font;
            _layout = ActivityLogLayout.Build(estate.ActivityLog, (text, width) => Gui.TextHeight(text, 20, width), region => Session.Current.Zones.ZoneName(region),
                upgrade => ActivityLogLayout.BuildingText(upgrade, Session.Current.Lore.Text("str_building_upgraded_to_percent"), Session.Current.Lore.Text("town_name_" + upgrade.Building)));
        }
        var view = new Rect(window.x + 25, window.y + 160, ActivityLogLayout.ViewWidth, ActivityLogLayout.ViewHeight);
        scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, _layout.Height - view.height));
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, ActivityLogLayout.Width, _layout.Height));
        if (_layout.Rows.Count == 0)
            Gui.Text(new Rect(25, 15, 550, 70), "All is quiet. Choose a quest and embark when ready.", 20, Gui.Dd1Text);
        foreach (var row in _layout.Visible(scroll.y, view.height)) DrawRow(row);
        GUI.EndScrollView();
        DrawGoals(window, estate);
    }

    private void DrawGoals(Rect window, Estate estate)
    {
        var session = Session.Current;
        var lore = session.Lore;
        Gui.Text(new Rect(window.x + 720, window.y + 35, 600, 65), lore.Text("str_caretaker_goals_heading") ?? "Caretaker Goals", 36, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
        var next = new Rect(window.x + 1282, window.y + 123, 32, 32);
        Gui.Image(next, Art.Dd1("shared", "character", "next_hero.png"), ScaleMode.ScaleToFit);
        if (next.Contains(Event.current.mousePosition)) Gui.Tip(_showQuests ? "Show Roster Goals" : "Show Quest Goals");
        if (Gui.Hotspot(next)) _showQuests = !_showQuests;
        string heading = _showQuests ? lore.Text("str_caretaker_goals_quest_goals_heading") ?? "Quest Goals"
            : lore.Text("str_caretaker_goals_roster_goals_heading") ?? "Roster Goals";
        Gui.Text(new Rect(window.x + 720, window.y + 123, 550, 32), heading, 24, Gui.Dd1Name, heading: true);
        var source = session.Catalog.RecruitableClasses;
        var font = Dd1Font.Body;
        if (_goalSource != source || _goalClasses == null || _goalFont != font)
        {
            _goalSource = source; _goalFont = font;
            _goalClasses = source.Distinct().OrderBy(c => c).ToList();
            _goalTexts = _goalClasses.Select(c => (lore.Text("str_caretaker_goal_hero_resolve") ?? "Raise a %s to Resolve Level 6")
                .Replace("%s", lore.Text("hero_class_name_" + c) ?? HamletUi.Pretty(c))).ToList();
            _goalHeights = _goalTexts.Select(t => Mathf.Max(40, Gui.TextHeight(t, 20, 520) + 12)).ToList();
        }
        if (_showQuests)
        {
            int regions = 17;
            foreach (var zone in CampaignRegions.Options)
                regions = unchecked(regions * 31 + (CampaignRegions.Enabled(estate, zone) ? zone.GetHashCode() : 0));
            if (_questGoals == null || _questEstate != estate || _questCampaign != session.Campaign || _questRegions != regions
                || _questCompleted != estate.CompletedPlotQuests.Count || _questFont != font)
            {
                _questEstate = estate; _questCampaign = session.Campaign; _questRegions = regions;
                _questCompleted = estate.CompletedPlotQuests.Count; _questFont = font;
                _questGoals = CaretakerGoals.Quests(estate, session.Campaign);
                _questTexts = _questGoals.Select(g =>
                {
                    if (g.BossId != null)
                    {
                        string tier = g.Tier == 1 ? "Apprentice" : g.Tier == 2 ? "Veteran" : "Champion";
                        return $"Defeat the {HamletUi.Pretty(ZoneEncounters.BossKey(g.BossId))} in {session.Zones.ZoneName(g.Region)} ({tier}).";
                    }
                    if (g.Id == "plot_tutorial_crypts" && g.Region != "crypts")
                        return $"Successfully complete your first foray into {session.Zones.ZoneName(g.Region)}.";
                    return lore.Text("str_caretaker_goal_" + g.Id) ?? HamletUi.Pretty(g.Id);
                }).ToList();
                _questHeights = _questTexts.Select(t => Mathf.Max(40, Gui.TextHeight(t, 20, 520) + 12)).ToList();
            }
        }
        var texts = _showQuests ? _questTexts : _goalTexts;
        var heights = _showQuests ? _questHeights : _goalHeights;
        var scroll = _showQuests ? _questScroll : _goalScroll;
        float height = heights.Sum() + texts.Count * 10;
        var view = new Rect(window.x + 720, window.y + 160, 600, 240);
        scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, height - view.height));
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, 580, height));
        var mark = Art.Dd1("shared", "menu", "menu.check_mark.png");
        float y = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            if (y + heights[i] > scroll.y && y < scroll.y + view.height)
            {
                bool complete = _showQuests ? estate.CompletedPlotQuests.Contains(_questGoals[i].Id)
                    : estate.CompletedResolveGoals.Contains(_goalClasses[i]);
                if (complete) Gui.Image(new Rect(0, y + 4, 32, 32), mark, ScaleMode.ScaleToFit);
                Gui.Text(new Rect(50, y + 6, 520, heights[i] - 12), texts[i], 20, complete ? Gui.Dd1Name : Gui.Dd1Text);
            }
            y += heights[i] + 10;
        }
        GUI.EndScrollView();
        if (_showQuests) _questScroll = scroll; else _goalScroll = scroll;
    }

    private static void DrawRow(ActivityLogLayout.Row row)
    {
        var rect = new Rect(0, row.Y, ActivityLogLayout.Width, row.Height);
        if (row.Type == ActivityLogLayout.Kind.Week)
        {
            Gui.Image(rect, Art.Dd1("activity_log", "week_title_bar.png"), ScaleMode.StretchToFill);
            Gui.Text(new Rect(25, row.Y + 35, 550, 55), row.Text, 30, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);
            return;
        }
        if (row.Type == ActivityLogLayout.Kind.Town)
        {
            bool levelUp = row.Actor?.Kind == ActivityEntryKind.LevelUp;
            var backdrop = Art.Dd1("activity_log", row.Building != null ? "building_upgrade_entry_backdrop.png"
                : levelUp ? "hero_level_up_entry_backdrop.png" : "hero_activity_entry_backdrop.png");
            if (backdrop != null) GUI.DrawTexture(rect, backdrop); else Gui.Fill(rect, new Color(0.1f, 0.1f, 0.1f, 0.95f));
            if (row.Actor != null && Art.HeroIcon(row.Actor.HeroClass) is { } portrait)
                Art.DrawSprite(new Rect(20, row.Y + 15, 90, 90), portrait);
            else if (row.Building != null)
                Gui.Image(new Rect(20, row.Y + 15, 90, 90), Art.BuildingIcon(row.Building.Building), ScaleMode.ScaleToFit);
            bool icon = row.Actor != null || row.Building != null;
            Gui.Text(new Rect(icon ? 135 : 25, row.Y + 16,
                icon ? 440 : 550, row.Height - 32), row.Text, 20, levelUp || row.Building != null ? Gui.Dd1Name : Gui.Dd1Text);
            return;
        }
        var raid = row.Raid;
        float y = row.Y;
        if (raid.Result != "embark")
        {
            string banner = raid.Result == "complete" ? "raid_success_banner.png"
                : raid.Result == "retreat" ? "raid_abandon_banner.png" : "raid_failure_banner.png";
            Gui.Image(new Rect(0, y, 600, 60), Art.Dd1("activity_log", banner), ScaleMode.StretchToFill);
            y += 60;
        }
        for (int i = 0; i < raid.Heroes.Count && i < 4; i++)
        {
            var hero = raid.Heroes[i];
            float x = 25 + i * 140;
            var portrait = new Rect(x + 20, y + 10, 90, 90);
            if (hero.Died) Gui.Image(portrait, Art.Dd1("raid_results", "deadhero_portrait.png"), ScaleMode.ScaleToFit);
            else if (Art.HeroIcon(hero.ClassId) is { } icon) Art.DrawSprite(portrait, icon);
            Gui.Text(new Rect(x, y + 104, 130, row.NameHeight), hero.Name, 20, hero.Died ? Gui.Dim : Gui.Dd1Name, TextAnchor.UpperCenter);
        }
        Gui.Text(new Rect(25, y + 125 + row.NameHeight, 550, row.Height - (y - row.Y) - 125 - row.NameHeight), row.Text, 20, Gui.Dd1Text);
    }
}
