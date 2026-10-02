using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>Everything the player can do in the Hamlet, and the end-of-week cycle. DD1 rules, DD2 heroes.</summary>
public sealed class Hamlet
{
    public Estate Estate { get; }
    public Dd1Campaign Dd1 { get; }
    public Buildings Buildings { get; }
    public IHeroCatalog Catalog { get; }
    public CampingSkills Camping { get; }

    public Hamlet(Estate estate, Dd1Campaign dd1, Buildings buildings, IHeroCatalog catalog, CampingSkills camping = null)
    {
        Estate = estate;
        Dd1 = dd1;
        Buildings = buildings;
        Catalog = catalog;
        Camping = camping;
    }

    /// <summary>A brand-new estate: DD1 starts you with two heroes, some gold, and the Ruins open.</summary>
    public static Estate NewEstate(int seed, Dd1Campaign dd1, Buildings buildings, IHeroCatalog catalog, CampingSkills camping = null)
    {
        var estate = new Estate { Seed = seed };
        estate.Add(Currency.Gold, 500);
        // DD1 opens with the Ruins tutorial; we skip it but pay out what it pays (3000 gold, 4 crests).
        var tutorial = dd1.Goals?.Plot.FirstOrDefault(p => p.Id == "plot_tutorial_crypts");
        if (tutorial != null)
        {
            foreach (var r in tutorial.Rewards.Where(r => r.Type != "trinket")) estate.Add(r.Type, r.Amount);
            estate.CompletedPlotQuests.Add(tutorial.Id);
        }
        var hamlet = new Hamlet(estate, dd1, buildings, catalog, camping);
        var rng = estate.NextRng();
        // DD1's first recruits are fixed (Crusader, Highwayman, Plague Doctor, Vestal). Use the DD2 classes
        // that share those names, falling back to whatever DD2 offers.
        foreach (var cls in new[] { "man_at_arms", "highwayman", "plague_doctor", "vestal" })
            estate.Roster.Add(hamlet.MakeHero(catalog.RecruitableClasses.Contains(cls) ? cls : rng.Pick(catalog.RecruitableClasses), rng, level: 0));
        hamlet.RefreshWeek();
        return estate;
    }

    // ---------------- Stagecoach ----------------

    public HeroRecord MakeHero(string classId, Rng rng, int level)
    {
        var hero = new HeroRecord
        {
            Id = Estate.NewId(),
            ClassId = classId,
            Name = Catalog.RandomName(classId, rng),
            ResolveLevel = level,
            ResolveXp = level > 0 ? Dd1.ZoneLevelThresholds[Math.Min(level, Dd1.ZoneLevelThresholds.Count - 1)] : 0,
            WeekRecruited = Estate.Week,
        };
        // DD1 recruits arrive with one positive and one negative quirk, more for experienced recruits.
        hero.Quirks.AddRange(Catalog.StartingQuirks(classId, rng, positives: 1 + level / 2, negatives: 1 + level / 3));
        if (Camping != null) hero.CampingSkills = Camping.Starting(classId, rng);
        return hero;
    }

    public bool CanRecruit => Estate.Roster.Count < Buildings.RosterSize(Estate);

    public bool Recruit(string recruitId)
    {
        var hero = Estate.Recruits.FirstOrDefault(h => h.Id == recruitId);
        if (hero == null || !CanRecruit) return false;
        Estate.Recruits.Remove(hero);
        hero.WeekRecruited = Estate.Week;
        Estate.Roster.Add(hero);
        return true;
    }

    public bool Dismiss(string heroId)
    {
        var hero = Estate.Hero(heroId);
        if (hero == null) return false;
        Estate.Roster.Remove(hero);
        foreach (var t in hero.Trinkets) Estate.Trinkets.Add(t);
        return true;
    }

    // ---------------- Abbey and Tavern ----------------

    public IEnumerable<ActivityDef> OpenActivities =>
        Buildings.Activities.Where(a => Buildings.IsOpen(a.Building, Estate));

    public int UsedSlots(string activityKey) => Estate.Roster.Count(h => h.Activity == activityKey);

    public string WhyCantDo(HeroRecord hero, ActivityDef activity)
    {
        if (hero == null || activity == null) return "Unknown";
        if (!Buildings.IsOpen(activity.Building, Estate)) return "The building is not open yet.";
        if (!hero.IsAvailable && hero.Activity != activity.Key) return hero.MissingWeeks > 0 ? "Missing." : "Busy.";
        if (UsedSlots(activity.Key) >= activity.Slots(Estate)) return "No free slot.";
        var banned = activity.ForbiddenQuirks.Select(Catalog.MapDd1Quirk).Where(q => q != null);
        if (hero.Quirks.Any(banned.Contains)) return "Refuses: a quirk forbids it.";
        var cost = activity.Cost(Estate);
        if (cost != null && Estate.Get(cost.Type) < cost.Amount) return "Not enough " + cost.Type + ".";
        return null;
    }

    public bool StartActivity(string heroId, string activityKey)
    {
        var hero = Estate.Hero(heroId);
        var activity = Buildings.Activity(activityKey);
        if (WhyCantDo(hero, activity) != null) return false;
        var cost = activity.Cost(Estate);
        if (cost != null) Estate.Add(cost.Type, -cost.Amount);
        hero.Activity = activityKey;
        return true;
    }

    public void CancelActivity(string heroId)
    {
        var hero = Estate.Hero(heroId);
        if (hero == null || hero.Activity == null || hero.ActivityLocked) return;
        // DD1 refunds a cancelled stay.
        if (Buildings.Activity(hero.Activity) is { } a && a.Cost(Estate) is { } cost) Estate.Add(cost.Type, cost.Amount);
        hero.Activity = null;
        hero.ActivityTarget = null;
    }

    // ---------------- Sanitarium ----------------

    public Reward TreatmentCost(HeroRecord hero, string quirkId)
    {
        if (Catalog.IsDisease(quirkId)) return Buildings.DiseaseTreatmentCost(Estate);
        if (Catalog.IsPositive(quirkId)) return Buildings.QuirkTreatmentCost(Estate, "positive");
        return Buildings.QuirkTreatmentCost(Estate, hero.LockedQuirks.Contains(quirkId) ? "permanent_negative" : "negative");
    }

    public bool StartTreatment(string heroId, string quirkId)
    {
        var hero = Estate.Hero(heroId);
        if (hero == null || !hero.IsAvailable || !hero.Quirks.Contains(quirkId)) return false;
        if (!Buildings.IsOpen(Buildings.Sanitarium, Estate)) return false;
        string activity = Catalog.IsDisease(quirkId) ? "sanitarium.disease_treatment" : "sanitarium.treatment";
        if (Estate.Roster.Count(h => h.Activity == activity) >= Buildings.SanitariumSlots(Estate, activity.Split('.')[1])) return false;
        var cost = TreatmentCost(hero, quirkId);
        if (cost == null || Estate.Get(cost.Type) < cost.Amount) return false;
        Estate.Add(cost.Type, -cost.Amount);
        hero.Activity = activity;
        hero.ActivityTarget = quirkId;
        return true;
    }

    // ---------------- Upgrades ----------------

    public bool BuyUpgrade(string treeId, string code) => Buildings.Trees.TryBuy(Estate, treeId, code);

    // ---------------- End of week ----------------

    /// <summary>
    /// The party embarked (or the player chose to skip): resolve every town activity, then roll the next week's
    /// recruits, wagon stock and quest board. DD1 advances the week once per expedition.
    /// </summary>
    public List<string> EndWeek()
    {
        var log = new List<string>();
        var rng = Estate.NextRng();

        foreach (var hero in Estate.Roster.ToList())
        {
            if (hero.MissingWeeks > 0)
            {
                if (--hero.MissingWeeks == 0) log.Add($"{hero.Name} has returned to the Hamlet.");
                continue;
            }
            if (hero.Activity == null) continue;

            if (hero.Activity.StartsWith("sanitarium."))
                FinishTreatment(hero, rng, log);
            else if (Buildings.Activity(hero.Activity) is { } activity)
                FinishActivity(hero, activity, rng, log);

            if (!hero.ActivityLocked)
            {
                hero.Activity = null;
                hero.ActivityTarget = null;
            }
            else hero.ActivityLocked = false; // locked for one more week only
        }

        Estate.Week++;
        RefreshWeek();
        Estate.TownLog = log;
        return log;
    }

    private void FinishActivity(HeroRecord hero, ActivityDef activity, Rng rng, List<string> log)
    {
        var (low, high) = activity.StressHeal(Estate);
        int dd1Heal = rng.Range(low, high);
        int points = ToDd2Points(dd1Heal, rng);
        int before = hero.Stress;
        hero.Stress = Math.Max(0, hero.Stress - points);
        log.Add($"{hero.Name} spent the week at the {activity.Building} ({activity.Id}): stress {before} → {hero.Stress}.");

        if (!rng.Chance(activity.SideEffectChance)) return;
        var effect = PickWeighted(activity.SideEffects, rng);
        if (effect != null) ApplySideEffect(hero, activity, effect, rng, log);
    }

    private void ApplySideEffect(HeroRecord hero, ActivityDef activity, JObject effect, Rng rng, List<string> log)
    {
        var options = effect["data"] as JArray ?? new JArray();
        var pick = PickWeighted(options, rng);
        switch ((string)effect["type"])
        {
            case "activity_lock":
                hero.ActivityLocked = true;
                log.Add($"{hero.Name} refuses to leave the {activity.Building}.");
                break;
            case "go_missing":
                hero.MissingWeeks = (int?)pick?["duration"] ?? 1;
                log.Add($"{hero.Name} has gone missing for {hero.MissingWeeks} week(s).");
                break;
            case "add_quirk":
                var quirk = Catalog.MapDd1Quirk((string)pick?["quirk_library_name"]);
                if (quirk != null && !hero.Quirks.Contains(quirk))
                {
                    hero.Quirks.Add(quirk);
                    log.Add($"{hero.Name} gained a quirk: {quirk}.");
                }
                break;
            case "apply_buff":
                foreach (var b in pick?["buff_library_ids"] ?? new JArray()) hero.PendingBuffs.Add((string)b);
                log.Add($"{hero.Name} leaves the {activity.Building} worse for wear.");
                break;
            case "remove_currency":
                int amount = Math.Min(Estate.Get((string)pick?["type"] ?? Currency.Gold), (int?)pick?["amount"] ?? 0);
                Estate.Add((string)pick?["type"] ?? Currency.Gold, -amount);
                log.Add($"{hero.Name} lost {amount} gold.");
                break;
            case "add_trinket":
                var found = Catalog.RandomTrinket((string)pick?["rarity"], rng);
                if (found != null) { Estate.Trinkets.Add(found); log.Add($"{hero.Name} came back with a trinket: {found}."); }
                break;
            case "remove_trinket":
                if (hero.Trinkets.Count > 0)
                {
                    var lost = rng.Pick(hero.Trinkets);
                    hero.Trinkets.Remove(lost);
                    log.Add($"{hero.Name} lost a trinket: {lost}.");
                }
                break;
        }
    }

    private void FinishTreatment(HeroRecord hero, Rng rng, List<string> log)
    {
        string target = hero.ActivityTarget;
        if (target == null) return;
        if (hero.Activity == "sanitarium.disease_treatment")
        {
            if (rng.Chance(Buildings.DiseaseCureAllChance(Estate)))
                hero.Quirks.RemoveAll(Catalog.IsDisease);
            else hero.Quirks.Remove(target);
            log.Add($"{hero.Name}'s illness was treated.");
        }
        else if (Catalog.IsPositive(target))
        {
            if (!hero.LockedQuirks.Contains(target)) hero.LockedQuirks.Add(target);
            log.Add($"{hero.Name}'s {target} is now locked in.");
        }
        else
        {
            hero.Quirks.Remove(target);
            hero.LockedQuirks.Remove(target);
            log.Add($"{hero.Name} was cured of {target}.");
        }
    }

    /// <summary>New recruits, a new wagon stock and a new quest board.</summary>
    public void RefreshWeek()
    {
        var rng = Estate.NextRng();

        Estate.Recruits.Clear();
        var experienced = Buildings.ExperiencedRecruits(Estate).OrderByDescending(t => t.Level).ToList();
        for (int i = 0; i < Buildings.RecruitsPerWeek(Estate); i++)
        {
            int level = 0;
            foreach (var (lvl, chance) in experienced)
                if (rng.Chance(chance)) { level = lvl; break; }
            Estate.Recruits.Add(MakeHero(rng.Pick(Catalog.RecruitableClasses), rng, level));
        }

        Estate.WagonStock.Clear();
        string[] rarities = { "very_common", "very_common", "common", "common", "uncommon", "rare", "very_rare" };
        for (int i = 0; i < Buildings.WagonStock(Estate); i++)
        {
            var t = Catalog.RandomTrinket(rng.Pick(rarities), rng);
            if (t != null) Estate.WagonStock.Add(t);
        }

        Estate.Quests = QuestBoard.Generate(Estate, Dd1, ToggledZones());
    }

    public int WagonPrice(string trinketId) =>
        (int)Math.Round(Catalog.TrinketPrice(trinketId) * (1f - Buildings.WagonDiscount(Estate)));

    public bool BuyTrinket(string trinketId)
    {
        if (!Estate.WagonStock.Contains(trinketId)) return false;
        int price = WagonPrice(trinketId);
        if (Estate.Get(Currency.Gold) < price) return false;
        Estate.Add(Currency.Gold, -price);
        Estate.WagonStock.Remove(trinketId);
        Estate.Trinkets.Add(trinketId);
        return true;
    }

    /// <summary>Zones added by toggles ("zone.<id>" = true), e.g. DD2 regions crawled DD1-style.</summary>
    public IEnumerable<string> ToggledZones() =>
        Estate.Toggles.Where(t => t.Value && t.Key.StartsWith("zone.")).Select(t => t.Key.Substring(5));

    // ---------------- helpers ----------------

    /// <summary>DD1 stress (100 = affliction) to whole DD2 points (10 = meltdown), rounding by chance.</summary>
    public static int ToDd2Points(float dd1Stress, Rng rng)
    {
        float points = dd1Stress / CrawlRules.Dd1StressPerDd2Point;
        int whole = (int)Math.Floor(points);
        return rng.Chance(points - whole) ? whole + 1 : whole;
    }

    private static JObject PickWeighted(JArray options, Rng rng)
    {
        float total = options.Sum(o => (float?)o["chance"] ?? 0f);
        if (total <= 0) return null;
        double roll = rng.NextDouble() * total;
        foreach (JObject o in options)
        {
            roll -= (float?)o["chance"] ?? 0f;
            if (roll < 0) return o;
        }
        return (JObject)options.Last;
    }
}
