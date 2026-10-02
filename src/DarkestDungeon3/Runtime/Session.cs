using System;
using System.IO;
using System.Threading.Tasks;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

/// <summary>
/// Everything loaded from the user's DD1 install plus the current save. DD1 content loads on a worker thread at
/// startup (it's all plain files); anything touching DD2 or Unity happens on the main thread.
/// </summary>
internal sealed class Session
{
    public static Session Current { get; private set; }
    public static string LoadError { get; private set; }
    public static bool IsLoading => _loading != null && !_loading.IsCompleted;

    private static Task _loading;

    public Dd1Install Dd1;
    public Dd1Campaign Campaign;
    public Buildings Buildings;
    public CrawlContent Content;
    public CrawlRules Rules;
    public Dd1Lore Lore;
    public Provisioner Provisioner;
    public ZoneEncounters Zones;

    public SaveFile Save;
    public string SavePath;

    /// <summary>Start loading DD1 content in the background. Safe to call more than once.</summary>
    public static void BeginLoad(string configuredDd1Path, string pluginDir)
    {
        if (_loading != null) return;
        _loading = Task.Run(() =>
        {
            try
            {
                var dd1 = Dd1Install.Find(configuredDd1Path);
                if (dd1 == null)
                {
                    LoadError = "Darkest Dungeon 1 was not found. Set its folder in BepInEx/config/piral.darkestdungeon3.cfg.";
                    return;
                }
                var s = new Session { Dd1 = dd1 };
                s.Campaign = Dd1Campaign.Load(dd1);
                s.Buildings = Buildings.Load(dd1);
                s.Content = CrawlContent.Load(dd1);
                s.Rules = CrawlRules.FromDd1(s.Campaign.Rules);
                s.Lore = Dd1Lore.Load(dd1);
                s.Provisioner = Provisioner.Load(dd1, s.Content.Items);
                s.Zones = ZoneEncounters.Load(Path.Combine(pluginDir, "data", "zones.json"));
                Current = s;
                Plugin.Log.LogInfo($"[session] DD1 content loaded from {dd1.Root}: {s.Campaign.MapGen.All.Count} map configs, " +
                                   $"{s.Buildings.Activities.Count} activities, {s.Content.Camping.Skills.Count} camp skills, {s.Lore.HeroNames.Count} names");
            }
            catch (Exception e)
            {
                LoadError = "Loading DD1 content failed: " + e.Message;
                Plugin.Log.LogError(e);
            }
        });
    }

    // ---------------- saves ----------------

    public static string SaveDir => Path.Combine(Application.persistentDataPath, "DarkestDungeon3");

    public static string SlotPath(int slot) => Path.Combine(SaveDir, $"estate_{slot}.json");

    public bool HasSave(int slot) => File.Exists(SlotPath(slot));

    public Dd2Catalog Catalog => _catalog ??= new Dd2Catalog(Lore);
    private Dd2Catalog _catalog;

    public Hamlet Hamlet => Save?.Estate == null ? null : new Hamlet(Save.Estate, Campaign, Buildings, Catalog, Content.Camping);

    public void LoadOrCreate(int slot)
    {
        SavePath = SlotPath(slot);
        Save = SaveFile.Load(SavePath);
        if (Save == null)
        {
            Save = new SaveFile { Estate = Hamlet.NewEstate(Environment.TickCount, Campaign, Buildings, Catalog, Content.Camping) };
            Plugin.Log.LogInfo($"[session] new estate in slot {slot}");
            Persist();
        }
        else Plugin.Log.LogInfo($"[session] loaded slot {slot}: week {Save.Estate.Week}, {Save.Estate.Roster.Count} heroes");
        Migrate(Save.Estate);
    }

    /// <summary>One-time fixes for estates saved by older builds of the mod.</summary>
    private void Migrate(Estate estate)
    {
        // 0.2.0 started estates without the skipped tutorial's payout.
        var tutorial = Campaign.Goals?.Plot.Find(p => p.Id == "plot_tutorial_crypts");
        if (tutorial != null && !estate.CompletedPlotQuests.Contains(tutorial.Id))
        {
            foreach (var r in tutorial.Rewards) if (r.Type != "trinket") estate.Add(r.Type, r.Amount);
            estate.CompletedPlotQuests.Add(tutorial.Id);
            Plugin.Log.LogInfo("[session] migrated estate: added the tutorial's rewards");
            Persist();
        }
    }

    public void Persist()
    {
        try { Save?.Save(SavePath); }
        catch (Exception e) { Plugin.Log.LogError("Saving failed: " + e); }
    }
}
