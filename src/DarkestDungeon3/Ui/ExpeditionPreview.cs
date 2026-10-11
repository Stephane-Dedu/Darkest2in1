using System;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in visual/input QA against real campaign data, without selecting or saving an estate.</summary>
[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class ExpeditionPreview
{
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("-dd3-expedition-preview");
    private static EmbarkUi _ui;
    private static SaveFile _save;
    private static bool _closed;
    internal static bool OwnsSession => _save != null && ReferenceEquals(Session.Current?.Save, _save);

    private static bool Prefix()
    {
        if (!Requested || !Plugin.DebugKeysEnabled.Value || _closed || Driver.Instance?.Phase != Phase.Off
            || GameModeMgr.CurrentMode != GameModeType.MAIN_MENU) return true;
        var session = Session.Current;
        if (session == null) return true;
        if (_ui == null)
        {
            // Refuse to replace a real selection, even if the debug flag was supplied accidentally.
            if (session.Save != null || session.SavePath != null) return true;
            var estate = new Estate { Name = "Expedition UI preview", Seed = 1101, Week = 12, QuestsCompleted = 20,
                RegionLayoutVersion = CampaignRegions.LayoutVersion };
            estate.Add(Currency.Gold, 12000);
            estate.Upgrades.Add("stage_coach.rostersize:a");
            foreach (string zone in CampaignRegions.Legacy) estate.Toggles["zone." + zone] = true;
            string[] classes = { "highwayman", "leper", "jester", "vestal", "flagellant", "hellion", "plague_doctor", "man_at_arms", "occultist", "grave_robber" };
            string[] names = { "Dismas", "Baldwin", "Sarmenti", "Junia", "Damian", "Boudica", "Paracelsus", "Barristan", "Alhazred", "Audrey" };
            for (int i = 0; i < classes.Length; i++) estate.Roster.Add(new HeroRecord
            { Id = "preview-" + i, ClassId = classes[i], Name = names[i], ResolveLevel = 1, Stress = i % 5 });
            _save = new SaveFile { Estate = estate };
            session.Save = _save;
            estate.Quests = QuestBoard.Generate(estate, session.Campaign, CampaignRegions.Open(estate, session.Campaign));
            _ui = EmbarkUi.ForPreview(estate);
            Plugin.Log.LogInfo($"[expedition preview] transient campaign: {estate.Quests.Count} quests; no estate path selected");
        }
        Gui.Begin(); Drag.Begin(); GUI.depth = -1000;
        Gui.BlockInput(true);
        try
        {
            _ui.Draw();
            ExpeditionSkin.Text(new Rect(40, 950, 380, 32), "UI preview · Unsaved", 21, ExpeditionSkin.Muted);
            if (_ui.WantsBack)
            {
                session.Save = null; _save = null; _ui = null; _closed = true;
                Gui.BlockInput(false);
            }
        }
        catch (Exception e) { Plugin.Log.LogError("[expedition preview] " + e); }
        Drag.Overlay(); Gui.DrawTip();
        return false;
    }
}

[HarmonyPatch(typeof(Session), nameof(Session.Persist))]
internal static class ExpeditionPreviewNoSave
{
    private static bool Prefix(ref bool __result)
    {
        if (!ExpeditionPreview.OwnsSession) return true;
        __result = true;
        return false;
    }
}

internal sealed partial class EmbarkUi
{
    internal static EmbarkUi ForPreview(Estate estate)
    {
        var ui = new EmbarkUi { Preview = true };
        ui._party.AddRange(estate.Roster.Take(4).Select(h => h.Id));
        ui.FocusArea("dd2_coast");
        return ui;
    }
}
