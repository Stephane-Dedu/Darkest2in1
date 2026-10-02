using System.Collections.Generic;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal enum Phase
{
    Off,         // plain DD2
    Hamlet,      // our town screens
    Embarking,   // waiting for the DD2 host run to reach the road
    Crawling,    // our dungeon screens
    Fighting,    // DD2 combat (our UI hidden)
    Homecoming,  // results screen before the Hamlet
}

/// <summary>
/// The DD1 game loop on top of DD2: Hamlet → embark (host a DD2 run) → crawl, handing fights to DD2 → homecoming.
/// Screens call into this; it calls into Core for rules and into the Dd2 bridge for anything in the game.
/// </summary>
internal sealed class Driver : MonoBehaviour
{
    public static Driver Instance { get; private set; }

    public Phase Phase { get; private set; } = Phase.Off;
    public Crawl Crawl { get; private set; }
    public Dd2Party Party { get; private set; }
    public List<string> Log { get; } = new();
    public List<string> HomecomingLog { get; private set; } = new();
    public CurioReport LastCurio { get; private set; }

    private Session S => Session.Current;
    public ExpeditionState Expedition => S?.Save?.Expedition;

    private void Awake()
    {
        Instance = this;
        Dd2Combat.Finished += OnFightFinished;
    }

    private void OnDestroy() => Dd2Combat.Finished -= OnFightFinished;

    public void Say(string line)
    {
        Log.Add(line);
        if (Log.Count > 60) Log.RemoveAt(0);
        Plugin.Log.LogInfo("[dd3] " + line);
    }

    // ---------------- Hamlet ----------------

    public void EnterHamlet(int slot)
    {
        S.LoadOrCreate(slot);
        if (S.Save.Expedition is { Started: false } stillborn)
        {
            // The party never reached the dungeon (the game closed or failed during embark): call it off.
            S.Save.Estate.Add(Currency.Gold, stillborn.ProvisionCost);
            S.Save.Expedition = null;
            Say($"The expedition never left. {stillborn.ProvisionCost} gold of provisions refunded.");
            S.Persist();
        }
        else if (S.Save.Expedition != null)
        {
            // An expedition was interrupted (the game closed mid-dungeon). DD1 counts that as a retreat.
            Say("The last expedition was cut short. The party limps home.");
            S.Save.Expedition.Retreated = true;
            var outcomes = S.Save.Expedition.Party.Select(id => new HeroOutcome { HeroId = id, Stress = S.Save.Estate.Hero(id)?.Stress ?? 0 });
            HomecomingLog = Homecoming.Apply(S.Save.Estate, S.Campaign, S.Save.Expedition, outcomes);
            S.Save.Expedition = null;
            S.Persist();
        }
        Phase = Phase.Hamlet;
    }

    public void LeaveHamlet()
    {
        S?.Persist();
        Phase = Phase.Off;
    }

    // ---------------- Embark ----------------

    public string Embark(QuestOffer quest, List<HeroRecord> party, Inventory bought)
    {
        var why = Core.Campaign.Embark.WhyCantEmbark(S.Save.Estate, quest, party);
        if (why != null) return why;
        int cost = bought.Items.Sum(kv => S.Provisioner.Price(kv.Key) * kv.Value);
        if (S.Save.Estate.Get(Currency.Gold) < cost) return "Not enough gold for these provisions.";

        S.Save.Estate.Add(Currency.Gold, -cost);
        var exp = Core.Campaign.Embark.Create(S.Campaign, quest, party, bought, S.Provisioner);
        exp.ProvisionCost = cost;
        S.Save.Expedition = exp;
        // DD1 resolves town activities while the party is away.
        S.Hamlet.EndWeek();
        S.Persist();

        Phase = Phase.Embarking;
        Say($"The party sets out: {quest}.");
        Dd2Run.Start(OnRoadReady);
        return null;
    }

    private void OnRoadReady()
    {
        var heroes = Expedition.Party.Select(id => S.Save.Estate.Hero(id)).Where(h => h != null).ToList();
        var guids = Dd2Heroes.BuildParty(heroes);
        Party = new Dd2Party(guids, S.Catalog);
        Crawl = new Crawl(Expedition, S.Rules, Party, S.Content);
        Handle(Crawl.Begin());
        Dd2Api.Torch = Expedition.Light;
        Phase = Phase.Crawling;
        Say($"Entered {S.Zones.ZoneName(Expedition.Quest.Dungeon)}.");
    }

    // ---------------- Crawl ----------------

    public void Travel(int roomId) => Handle(Crawl.Travel(roomId));
    public void Step(bool forward) => Handle(Crawl.Step(forward));
    public void UseTorch() => Handle(Crawl.UseTorch());

    public void Fight()
    {
        if (Crawl == null || !Crawl.IsBlocked) return;
        bool inRoom = Expedition.InRoom;
        var room = inRoom ? Crawl.CurrentRoom : null;
        var kind = room is { Content: Core.Dungeon.RoomContent.Boss } ? FightKind.Boss : inRoom ? FightKind.Room : FightKind.Hall;
        StartFight(kind, heroesSurprised: false);
    }

    private void StartFight(FightKind kind, bool heroesSurprised)
    {
        var quest = Expedition.Quest;
        var plan = S.Zones.Plan(quest.Dungeon, quest.Difficulty, kind, new Rng(Expedition.Seed * 7 + Expedition.BattlesWon * 131 + Expedition.StepsTaken), quest.BossId);
        var guids = Expedition.Party.Select(Party.Guid).Where(g => g != 0 && !Dd2Api.IsDead(g)).ToList();
        if (Dd2Combat.Start(plan, guids, Expedition.Light, heroesSurprised))
        {
            Phase = Phase.Fighting;
            S.Persist();
        }
        else Say("The fight could not start (see the log).");
    }

    private void OnFightFinished(bool wiped)
    {
        if (Phase != Phase.Fighting) return;
        Expedition.Light = Dd2Api.Torch;
        if (wiped || Party.Alive.Count == 0)
        {
            Say("The party has fallen.");
            Expedition.Ended = true;
            FinishExpedition();
            return;
        }
        Handle(Crawl.ResolveBattle());
        Phase = Phase.Crawling;
        S.Persist();
    }

    public void ClearObstacle() => Handle(Crawl.ClearObstacle());
    public void DisarmTrap() => Handle(Crawl.DisarmTrap());

    public void Investigate(string heroId, string itemId)
    {
        LastCurio = Crawl.InteractCurio(heroId, itemId, out var overflow);
        if (LastCurio != null)
        {
            Say($"{S.Save.Estate.Hero(heroId)?.Name}: {LastCurio.Text ?? LastCurio.OutcomeType}" +
                (LastCurio.Loot.Count > 0 ? " Found " + string.Join(", ", LastCurio.Loot) + "." : ""));
            foreach (var drop in overflow) Say($"No room for {drop}; left behind.");
            if (Expedition.QuestComplete) Say("The quest is complete! You may return to the Hamlet.");
        }
        S.Persist();
    }

    public void SkipCurio() => Crawl.SkipCurio();

    public void MakeCamp() => Handle(Crawl.MakeCamp());
    public bool EatMeal(Meal meal) => Crawl.EatMeal(meal);
    public bool UseCampSkill(string hero, string skill, string target) => Crawl.UseCampSkill(hero, skill, target);
    public void BreakCamp() => Handle(Crawl.BreakCamp());

    /// <summary>Leave the dungeon: after the quest is done, or as a retreat before it is.</summary>
    public void Leave()
    {
        if (!Expedition.QuestComplete) { Crawl.Retreat(); Say("The party retreats."); }
        Expedition.Ended = true;
        FinishExpedition();
    }

    private void Handle(List<CrawlEvent> events)
    {
        Dd2Api.Torch = Expedition.Light;
        foreach (var e in events)
        {
            switch (e.Type)
            {
                case CrawlEventType.Battle:
                    Say(e.HeroesSurprised ? "Ambush! The heroes are surprised!" : "Enemies ahead!");
                    break;
                case CrawlEventType.Ambush:
                    Say(e.ContentId == "camp" ? "The camp is ambushed in the night!" : "Something stirs in the dark...");
                    StartFight(e.ContentId == "camp" ? FightKind.CampAmbush : FightKind.Hall, heroesSurprised: true);
                    return;
                case CrawlEventType.TrapSprung: Say($"A trap ({e.ContentId}) is sprung!"); break;
                case CrawlEventType.TrapDisarmed: Say($"A trap ({e.ContentId}) was disarmed."); break;
                case CrawlEventType.Trap: Say($"A {e.ContentId} trap lies ahead."); break;
                case CrawlEventType.Obstacle: Say($"The way is blocked ({e.ContentId})."); break;
                case CrawlEventType.ObstacleCleared: Say("The way is clear."); break;
                case CrawlEventType.Curio: Say($"There is something here: {e.ContentId}."); break;
                case CrawlEventType.Ate: Say("The party eats."); break;
                case CrawlEventType.Starving: Say("There is no food. The party starves!"); break;
                case CrawlEventType.Scouted: Say("Scouting reveals the way ahead."); break;
                case CrawlEventType.QuestComplete: Say("The quest is complete! You may return to the Hamlet."); break;
                case CrawlEventType.Stress: break;
            }
        }
        S.Persist();
    }

    // ---------------- Homecoming ----------------

    private void FinishExpedition()
    {
        var exp = Expedition;
        var outcomes = exp.Party
            .Select(id => (hero: S.Save.Estate.Hero(id), guid: Party?.Guid(id) ?? 0u))
            .Where(x => x.hero != null)
            .Select(x => Dd2Heroes.ReadBack(x.hero, x.guid))
            .ToList();
        HomecomingLog = Homecoming.Apply(S.Save.Estate, S.Campaign, exp, outcomes);
        S.Save.Expedition = null;
        S.Persist();
        Crawl = null;
        Party = null;
        Phase = Phase.Homecoming;
        Dd2Run.End();
    }

    public void BackToHamlet() => Phase = Phase.Hamlet;
}
