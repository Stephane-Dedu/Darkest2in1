using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
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
    private static Session _current;
    private static string _loadError, _loadStage;
    private static int _loadStep;
    public static Session Current => Volatile.Read(ref _current);
    public static string LoadError => Volatile.Read(ref _loadError);
    public static string LoadStage => Volatile.Read(ref _loadStage) ?? "Finding Darkest Dungeon 1";
    public static float LoadProgress => Volatile.Read(ref _loadStep) / 10f;
    public static bool IsLoading => _loading != null && !_loading.IsCompleted;

    private static Task _loading;

    public Dd1Install Dd1;
    public Dd1Campaign Campaign;
    public Buildings Buildings;
    public CrawlContent Content;
    public CrawlRules Rules;
    public ResolveStress ResolveRules;
    public Dd1Lore Lore;
    public Provisioner Provisioner;
    public ZoneEncounters Zones;
    public Dd1Bestiary Bestiary;
    public Dd1Trinkets MemoryTrinkets;
    public Core.Dd2Data.Dd2Tables Dd2Data;
    public Core.Dd2Data.HeroPathCatalog HeroPaths;
    public TownLayout TownLayout;
    public TownArtPack TownArt;

    private readonly CampaignSaveSlot _slot = new();
    public SaveFile Save { get => _slot.Save; set => _slot.Save = value; }
    public string SavePath => _slot.Path;

    /// <summary>Start loading DD1 content in the background. Safe to call more than once.</summary>
    public static void BeginLoad(string configuredDd1Path, string pluginDir, string streamingAssetsPath, string hamletArtPath = null)
    {
        if (IsLoading || Current != null) return;
        Volatile.Write(ref _loadError, null);
        Volatile.Write(ref _loadStep, 0);
        Volatile.Write(ref _loadStage, "Finding Darkest Dungeon 1");
        _loading = Task.Run(() =>
        {
            try
            {
                var dd1 = Dd1Install.Find(configuredDd1Path);
                if (dd1 == null)
                {
                    Volatile.Write(ref _loadError, "Darkest Dungeon 1 was not found. Set its folder in BepInEx/config/piral.darkestdungeon3.cfg, then retry.");
                    return;
                }
                var s = new Session { Dd1 = dd1 };
                Stage(0, "Reading campaign and quests");
                s.Campaign = Dd1Campaign.Load(dd1);
                Stage(1, "Reading Hamlet buildings");
                s.Buildings = Buildings.Load(dd1);
                Stage(2, "Reading expeditions and provisions");
                s.Content = CrawlContent.Load(dd1);
                Stage(3, "Reading trinkets");
                s.MemoryTrinkets = Dd1Trinkets.LoadBase(dd1);
                Stage(4, "Reading dungeon rules");
                s.Rules = CrawlRules.FromDd1(s.Campaign.Rules);
                s.ResolveRules = ResolveStress.Load(dd1,s.Campaign.Rules);
                Stage(5, "Reading names and campaign lore");
                s.Lore = Dd1Lore.Load(dd1);
                Stage(6, "Preparing the provisioner");
                s.Provisioner = Provisioner.Load(dd1, s.Content.Items);
                Stage(7, "Reading encounter mappings");
                s.Zones = ZoneEncounters.Load(Path.Combine(pluginDir, "data", "zones.json"));
                s.Bestiary = Dd1Bestiary.Load(Path.Combine(pluginDir, "data", "monsters.json"));
                Stage(8, "Reading DD2 heroes and equipment");
                s.Dd2Data = Core.Dd2Data.Dd2Tables.Load(Path.Combine(streamingAssetsPath, "Excel"));
                Core.Dd2Data.QuirkCompatibility.AllowCampaignEffects(s.Dd2Data, s.Buildings.Activities.SelectMany(a => a.ForbiddenQuirks));
                s.HeroPaths = Core.Dd2Data.HeroPathCatalog.Load(Path.Combine(streamingAssetsPath, "Excel"));
                Stage(9, "Reading the town layout");
                s.TownLayout = TownLayout.Load(dd1);
                try { s.TownArt = TownArtPack.Load(hamletArtPath); }
                catch (Exception e) { Plugin.Log.LogWarning("[hamlet art] Private pack rejected; using DD1 town: " + e.Message); }
                Volatile.Write(ref _current, s);
                Stage(10, "Campaign data ready");
                Plugin.Log.LogInfo($"[session] DD1 content loaded from {dd1.Root}: {s.Campaign.MapGen.All.Count} map configs, " +
                                   $"{s.Buildings.Activities.Count} activities, {s.Content.Camping.Skills.Count} camp skills, {s.Lore.HeroNames.Count} names");
            }
            catch (Exception e)
            {
                Volatile.Write(ref _loadError, "Loading campaign content failed: " + e.Message);
                Plugin.Log.LogError(e);
            }
        });
    }

    private static void Stage(int step, string label)
    {
        Volatile.Write(ref _loadStage, label);
        Volatile.Write(ref _loadStep, step);
    }

    // ---------------- saves ----------------

    public static string SaveDir => Path.Combine(Application.persistentDataPath, "DarkestDungeon3");

    public static string SlotPath(int slot) => EstateSlots.PathFor(SaveDir, slot);

    public static void DeleteEstate(int slot)
    {
        if (Driver.Instance?.Phase != Phase.Off) throw new InvalidOperationException("Return to the main menu before deleting an estate.");
        EstateSlots.Delete(SaveDir, slot);
        if (string.Equals(Current?.SavePath, SlotPath(slot), StringComparison.OrdinalIgnoreCase)) Current._slot.Clear();
    }

    public bool HasSave(int slot) => File.Exists(SlotPath(slot));

    public Dd2Catalog Catalog => _catalog ??= new Dd2Catalog(Lore);
    private Dd2Catalog _catalog;

    public Hamlet Hamlet => Save?.Estate == null ? null : new Hamlet(Save.Estate, Campaign, Buildings, Catalog, Content.Camping);

    public void LoadOrCreate(int slot, string newEstateName = null)
    {
        _slot.Select(SlotPath(slot));
        if (newEstateName != null && Save != null)
            throw new IOException("This estate slot is already occupied. Return to the menu and create another estate.");
        if (Save == null)
        {
            Save = new SaveFile { Estate = Hamlet.NewEstate(Environment.TickCount, Campaign, Buildings, Catalog, Content.Camping) };
            if (!string.IsNullOrWhiteSpace(newEstateName)) Save.Estate.Name = newEstateName.Trim();
            Plugin.Log.LogInfo($"[session] new estate in slot {slot}");
            if (!Persist()) throw new IOException("The new estate could not be saved.");
            // DD1 opens a new campaign with its cinematics: "House of Ruin", then "The Old Road".
            Ui.CinematicPlayer.Play(Core.Dd1.Dd1Cinematic.Opening);
        }
        else
        {
            if (Save.RecoveredFromBackup) Plugin.Log.LogWarning($"[session] slot {slot} recovered from its previous-save backup");
            Plugin.Log.LogInfo($"[session] loaded slot {slot}: week {Save.Estate.Week}, {Save.Estate.Roster.Count} heroes");
        }
        Migrate(Save.Estate);
    }

    /// <summary>One-time fixes for estates saved by older builds of the mod.</summary>
    private void Migrate(Estate estate)
    {
        // Campaign/layout migration is owned by Core. Loading must not complete or pay the tutorial.
        var repairs = Hamlet.RepairEstate();
        foreach (var line in repairs) Plugin.Log.LogInfo("[session] repaired: " + line);
        if (repairs.Count > 0) Persist();
    }

    public bool Persist()
    {
        try
        {
            var driver = Driver.Instance;
            if (Save?.Expedition != null && driver?.Crawl?.State == Save.Expedition
                && driver.Party != null && driver.Phase is Phase.Crawling or Phase.Fighting)
                driver.CapturePartyState();
            Save?.Save(SavePath);
            return Save != null;
        }
        catch (Exception e) { Plugin.Log.LogError("Saving failed: " + e); return false; }
    }
}
