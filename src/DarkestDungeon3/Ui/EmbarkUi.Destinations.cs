using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Audio;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// The DD2-style quest select (a preview, switched with <see cref="Plugin.Dd2DestinationMenu"/>): the innkeeper's
/// destination cards, one per map position with DD2's region painting, and the region's quests where DD2 shows its
/// modifier. The Mountain sits above them as a banner. Paired areas keep their switch arrow; the quest details, the
/// roster, the party and the provisioner are the DD1 quest select's own.
/// </summary>
internal sealed partial class EmbarkUi
{
    private static bool Destinations => Plugin.Dd2DestinationMenu?.Value == true;

    private readonly Dictionary<string, int> _cardPages = new();
    private string _hoveredCard;
    private static bool _soundFailed;

    private void DrawDestinations()
    {
        PrepareAreas();
        _expeditionStyle = true;
        DrawDestinationBackdrop();
        Gui.Text(new Rect(560, 18, 900, 60), "Choose an expedition", 48, Gui.Dd1Name, TextAnchor.MiddleCenter, heading: true);

        string hovered = null;
        DrawMountainBanner(ref hovered);
        var locations = CampaignRegions.Legacy.Where(l => CampaignRegions.AtLocation(E, l).Count > 0).ToList();
        for (int i = 0; i < locations.Count; i++)
            DrawDestinationCard(DestinationLayout.Card(i, locations.Count), _mapAreas[locations[i]], CampaignRegions.AtLocation(E, locations[i]), ref hovered);
        if (Event.current.type == EventType.Repaint && hovered != _hoveredCard)
        {
            if (hovered != null) Dd2Sound(() => AudioPathsBhv.InnBiomeHoverLeft);
            _hoveredCard = hovered;
        }

        if (_quest != null) DrawPaintedQuestScroll(_quest);
        DrawPartyLineUp();
        DrawEmbarkControls();
        DrawMenuSwitch();
    }

    /// <summary>The approved painted landscape when present, else DD1's quest map, dimmed under the cards.</summary>
    private static void DrawDestinationBackdrop()
    {
        var screen = new Rect(0, 0, Gui.W, Gui.H);
        var art = ExpeditionMapArt.Background ?? Qs("quest_select.background.png");
        if (art != null) GUI.DrawTexture(screen, art, ScaleMode.ScaleAndCrop);
        Gui.Fill(screen, new Color(0.02f, 0.02f, 0.025f, art != null ? 0.62f : 1f));
    }

    private void DrawDestinationCard(Rect card, string zone, IReadOnlyList<string> areas, ref string hovered)
    {
        bool unlocked = CampaignRegions.Unlocked(E, S.Campaign, zone);
        bool selected = _focusedZone == zone;
        bool hover = card.Contains(Event.current.mousePosition) && !Drag.Active;
        if (hover) hovered = zone;
        if (hover && !selected) card.y -= 4;   // DD2 lifts the card under the mouse; kept small beside the quests
        ExpeditionFrame(card, 0.94f, selected || hover);

        var painting = DestinationLayout.PaintingOf(card);
        DrawRegionPicture(painting, zone, preferNative: true, fromTop: 0.3f);
        // DD2 veils the cards that are not chosen or looked at.
        if (!unlocked) Gui.Fill(painting, new Color(0, 0, 0, 0.65f));
        else if (!selected && !hover) Gui.Fill(painting, new Color(0, 0, 0, 0.32f));

        // Header: the name, the switch to the area sharing this position, the level and its progress.
        string name = AreaName(zone);
        Gui.Text(new Rect(card.x + 10, card.y + 4, card.width - 64, 34), name, 26, selected ? Gui.Parchment : Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        E.ZoneXp.TryGetValue(zone, out int xp);
        int level = S.Campaign.ZoneLevel(xp);
        var thresholds = S.Campaign.ZoneLevelThresholds;
        Gui.Text(new Rect(card.xMax - 40, card.y + 4, 32, 34), level.ToString(), 24, Gui.Dd1Text, TextAnchor.MiddleCenter, heading: true);
        if (thresholds.Count > level + 1)
        {
            float from = thresholds[level], to = thresholds[level + 1];
            Gui.Fill(new Rect(card.x + 10, card.y + 38, (card.width - 20) * Mathf.Clamp01((xp - from) / Mathf.Max(1, to - from)), 4), new Color(0.75f, 0.62f, 0.3f));
        }
        if (areas.Count > 1)
        {
            string nextZone = areas[(areas.ToList().IndexOf(zone) + 1) % areas.Count];
            float nameWidth = Dd1Font.Heading?.Measure(name, 26 * Gui.HeadingScale).x ?? 150;
            var next = new Rect(card.x + 10 + Mathf.Min(nameWidth + 8, card.width - 92), card.y + 6, 24, 30);
            Gui.Image(next, Art.Dd1("shared", "character", "next_hero.png"), ScaleMode.ScaleToFit);
            if (next.Contains(Event.current.mousePosition)) Gui.Tip("Switch to " + AreaName(nextZone));
            if (Gui.Hotspot(next)) { FocusArea(nextZone); Dd2Sound(() => AudioPathsBhv.InnBiomeSelect); }
        }
        // The header and painting choose the region (after the arrow, so it keeps its own clicks).
        if (Gui.Hotspot(new Rect(card.x, card.y, card.width, painting.yMax - card.y)))
        {
            FocusArea(zone);
            Dd2Sound(() => AudioPathsBhv.InnBiomeSelect);
        }

        if (!unlocked)
        {
            Gui.Text(new Rect(painting.x + 12, painting.center.y - 40, painting.width - 24, 80),
                $"Opens after {CampaignRegions.UnlockAfter(S.Campaign, zone)} quests", 20, Gui.Dd1Text, TextAnchor.MiddleCenter);
            return;
        }
        DrawCardQuests(card, zone);
    }

    /// <summary>DD2's modifier slot: the region's quests, three to a page.</summary>
    private void DrawCardQuests(Rect card, string zone)
    {
        var quests = E.Quests.Where(q => q.Dungeon == zone).ToList();
        if (quests.Count == 0)
        {
            Gui.Text(DestinationLayout.QuestLine(card), "No quests this week", 17, Gui.Dim, TextAnchor.MiddleCenter);
            return;
        }
        int pages = DestinationLayout.Pages(quests.Count);
        _cardPages.TryGetValue(zone, out int page);
        page = Mathf.Clamp(page, 0, pages - 1);
        var row = new Rect(card.x, DestinationLayout.Medallion(card, 0, 1).y - 6, card.width, DestinationLayout.Medal + 12);
        if (pages > 1 && row.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
        {
            page = Mathf.Clamp(page + (Event.current.delta.y > 0 ? 1 : Event.current.delta.y < 0 ? -1 : 0), 0, pages - 1);
            Event.current.Use();
        }

        var (first, count) = DestinationLayout.Page(quests.Count, page);
        QuestOffer shown = null;
        for (int i = 0; i < count; i++)
        {
            var q = quests[first + i];
            var r = DestinationLayout.Medallion(card, i, count);
            bool hover = r.Contains(Event.current.mousePosition);
            DrawQuestMedal(r, q, hover, 48);
            if (hover) { shown = q; Gui.Tip(QuestTip(q)); }
            if (Gui.Hotspot(r)) { SelectQuest(q); Dd2Sound(() => AudioPathsBhv.ClickConfirm); }
        }
        shown ??= _quest != null && quests.Contains(_quest) ? _quest : quests[first];
        Gui.Text(DestinationLayout.QuestLine(card), $"{shown.DifficultyName} / {Cap(shown.Size)}", 18,
            shown == _quest ? Gui.Parchment : Gui.Dd1Text, TextAnchor.MiddleCenter);

        if (pages > 1)
        {
            var back = DestinationLayout.PreviousPage(card);
            var more = DestinationLayout.NextPage(card);
            if (page > 0)
            {
                Gui.Image(back, Art.Dd1("shared", "widgets", "scrollbar_uparrow.png"), ScaleMode.ScaleToFit);
                if (back.Contains(Event.current.mousePosition)) Gui.Tip($"Quests {page} of {pages}");
                if (Gui.Hotspot(back)) page--;
            }
            if (page < pages - 1)
            {
                Gui.Image(more, Art.Dd1("shared", "widgets", "scrollbar_downarrow.png"), ScaleMode.ScaleToFit);
                if (more.Contains(Event.current.mousePosition)) Gui.Tip($"Quests {page + 2} of {pages}");
                if (Gui.Hotspot(more)) page++;
            }
        }
        _cardPages[zone] = page;
    }

    /// <summary>The Mountain: a banner over the regions, locked until a region reaches level 6.</summary>
    private void DrawMountainBanner(ref string hovered)
    {
        const string zone = QuestBoard.DarkestDungeon;
        var r = DestinationLayout.Mountain;
        var quests = E.Quests.Where(q => q.Dungeon == zone).ToList();
        bool open = quests.Count > 0, selected = _focusedZone == zone;
        bool hover = r.Contains(Event.current.mousePosition) && !Drag.Active;
        if (hover) hovered = zone;
        ExpeditionFrame(r, 0.94f, selected || hover);
        var picture = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
        DrawRegionPicture(picture, zone, preferNative: true, fromTop: 0.25f);
        Gui.Fill(picture, new Color(0, 0, 0, selected || hover ? 0.35f : 0.55f));
        Gui.Text(new Rect(r.x + 16, r.y, 240, r.height), "The Mountain", 28, open ? (selected ? Gui.Parchment : Gui.Dd1Name) : Gui.Dim, TextAnchor.MiddleLeft, heading: true);
        if (!open)
        {
            Gui.Text(new Rect(r.x + 200, r.y, r.width - 216, r.height), "Locked", 20, Gui.Dim, TextAnchor.MiddleRight);
            if (hover) Gui.Tip("The Mountain\nOpens when a region reaches level 6.");
            return;
        }
        for (int i = 0; i < quests.Count && i < 3; i++)
        {
            var q = quests[i];
            var m = new Rect(r.xMax - 58 - i * 54, r.y + 7, 48, 48);
            bool over = m.Contains(Event.current.mousePosition);
            DrawQuestMedal(m, q, over, 36);
            if (over) Gui.Tip(QuestTip(q));
            if (Gui.Hotspot(m)) { SelectQuest(q); Dd2Sound(() => AudioPathsBhv.ClickConfirm); }
        }
        if (Gui.Hotspot(r)) { FocusArea(zone); Dd2Sound(() => AudioPathsBhv.InnBiomeSelect); }
    }

    /// <summary>A quest's type, goal, rewards and how many heroes would go.</summary>
    private string QuestTip(QuestOffer q)
    {
        string rewards = string.Join(" · ", q.Rewards.Take(4).Select(rw => rw.Type == "trinket"
            ? HamletUi.Pretty(rw.Id ?? "trinket") : $"{Gui.Num(rw.Amount, "#,0")} {HamletUi.Pretty(rw.Type)}"));
        int willing = E.Roster.Count(h => h.IsAvailable && h.MissingWeeks <= 0 && Homecoming.WillEmbark(h, q, S.Hamlet.AnyResolveCanEmbark));
        return $"{HamletUi.Pretty(q.Type)}{(q.IsPlot ? " · Plot" : "")}\n{q.DifficultyName} · {Cap(q.Size)}\n{GoalLine(q)}"
            + (rewards.Length > 0 ? "\n" + rewards : "") + $"\n{willing} {(willing == 1 ? "hero" : "heroes")} in the roster will go";
    }

    /// <summary>The party waiting to leave, between the cards and its slots: rank 4 on the left, as DD1 lines it up.</summary>
    private void DrawPartyLineUp()
    {
        for (int s = 0; s < 4; s++)
        {
            int rank = 3 - s;
            if (rank >= _party.Count) continue;
            var figure = E.Hero(_party[rank]) is { } h ? Art.HeroFigure(h.ClassId) : null;
            if (figure != null) Art.DrawSprite(new Rect(960 - 330 + s * 165, 618, 165, 230), figure);
        }
    }

    /// <summary>A region's picture: DD2's region painting first on the cards, the private selector preview first on the
    /// painted map, DD1's loading screen for the area as the last resort on the cards.</summary>
    private static void DrawRegionPicture(Rect r, string zone, bool preferNative, float fromTop = 0.35f)
    {
        if (preferNative && RegionCardArt.Painting(zone) is { } painting) { RegionCardArt.DrawCover(r, painting, fromTop); return; }
        var preview = ExpeditionMapArt.Preview(zone);
        if (preview != null) { GUI.DrawTexture(r, preview, ScaleMode.ScaleAndCrop); return; }
        if (!preferNative) return;
        var loading = Art.Dd1("loading_screen", $"loading_screen.{ZoneBase.Of(zone)}_0.png");
        if (loading != null) GUI.DrawTexture(r, loading, ScaleMode.ScaleAndCrop);
        else Gui.Fill(r, new Color(0.08f, 0.07f, 0.06f));
    }

    /// <summary>The switch between the two quest selects, top left on both.</summary>
    private void DrawMenuSwitch()
    {
        var r = new Rect(30, 34, 360, 34);
        bool hover = r.Contains(Event.current.mousePosition);
        Gui.Text(r, Destinations ? "Switch to the DD1 quest map" : "Switch to DD2 destinations", 20, hover ? Color.white : Gui.Dd1Class, TextAnchor.MiddleLeft);
        if (!Gui.Hotspot(r)) return;
        Plugin.Dd2DestinationMenu.Value = !Destinations;
        if (!Destinations) RegionCardArt.Release();
    }

    private static void Dd2Sound(Func<FMODUnity.EventReference> sound)
    {
        if (_soundFailed) return;
        try
        {
            if (SingletonMonoBehaviour<AudioMgr>.HasInstance() && SingletonMonoBehaviour<AudioPathsBhv>.HasInstance())
                SingletonMonoBehaviour<AudioMgr>.Instance.Play(sound());
        }
        catch (Exception e)
        {
            _soundFailed = true;
            Plugin.Log.LogWarning($"[destinations] DD2 sounds unavailable: {e.Message}");
        }
    }
}
