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

    /// <summary>The hero shown in the bottom-left banner (DD1: click a hero to select).</summary>
    public string SelectedHeroId;
    /// <summary>For the corridor slide animation: when and which way the party last moved (+1 forward, -1 back).</summary>
    public float LastStepTime { get; private set; } = -10f;
    public int LastStepDir { get; private set; }

    private Session S => Session.Current;
    public ExpeditionState Expedition => S?.Save?.Expedition;

    // Walking to a clicked map destination, one square at a time.
    private readonly Queue<int> _route = new();
    private int _routeTargetRoom = -1;
    private (int corridor, int tile) _routeTargetTile = (-1, -1);
    private float _nextStepAt;
    private const float StepSeconds = 0.3f;

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

    private static readonly float[] RankX = { 788, 620, 452, 284 };

    private void Update()
    {
        var stage = HeroStage.Instance;
        stage?.SetVisible(Phase == Phase.Crawling && Plugin.HeroModels.Value);
        if (Phase != Phase.Crawling || Crawl == null) return;
        LockRoadInput();
        if (stage != null && Plugin.HeroModels.Value)
        {
            HeroStage.HeroScale = Plugin.HeroModelScale.Value;
            // Living heroes keep their DD1 rank slots; the dead leave a gap.
            var guids = Expedition.Party.Select(Party.Guid).ToList();
            var alive = guids.Where(g => g != 0 && !Dd2Api.IsDead(g)).ToList();
            var xs = guids.Select((g, i) => (g, x: RankX[System.Math.Min(i, 3)])).Where(t => alive.Contains(t.g)).Select(t => t.x).ToList();
            stage.SetParty(alive, xs);
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && Expedition.Camp == null && !Expedition.InRoom)
        {
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) { StopWalking(); Step(true); }
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) { StopWalking(); Step(false); }
        }
        if (kb != null && kb.tKey.wasPressedThisFrame) UseTorch();

        if (IsWalking && Time.unscaledTime >= _nextStepAt) WalkOneStep();
    }

    /// <summary>
    /// DD2 steers its stagecoach with the "Driving" input map, still live behind our crawl screen: switch it off
    /// so walking keys never drive the hidden coach. DD2 turns it back on whenever it changes mode.
    /// </summary>
    private void LockRoadInput()
    {
        if (GameModeMgr.CurrentMode != GameModeType.DRIVING) return;
        var input = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Inputs.InputSystemBhv>.Instance;
        input?.SetInputActionMapEnabled(GameModeType.DRIVING.m_inputMapName, false);
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
        LastCurio = null;
        Handle(Crawl.Begin());
        Dd2Api.Torch = Expedition.Light;
        Phase = Phase.Crawling;
        Say($"Entered {S.Zones.ZoneName(Expedition.Quest.Dungeon)}.");
    }

    // ---------------- Crawl ----------------

    public void Travel(int roomId)
    {
        MarkStep(+1);
        Handle(Crawl.Travel(roomId));
    }

    public void Step(bool forward)
    {
        if (Crawl.IsBlocked && !forward && Crawl.CurrentTile?.Content == Core.Dungeon.HallContent.Battle) return;
        MarkStep(forward ? +1 : -1);
        Handle(Crawl.Step(forward));
    }

    private void MarkStep(int dir)
    {
        LastStepDir = dir;
        LastStepTime = Time.unscaledTime;
    }

    public void UseTorch()
    {
        float before = Expedition.Light;
        Handle(Crawl.UseTorch());
        if (Expedition.Light > before) Ui.Gui.Announce("The torch flares.");
    }

    // ---- walking to a map destination (DD1: click a room or hall square on the map) ----

    public bool IsWalking => _route.Count > 0 || _routeTargetRoom >= 0 || _routeTargetTile.corridor >= 0;

    public void StopWalking()
    {
        _route.Clear();
        _routeTargetRoom = -1;
        _routeTargetTile = (-1, -1);
    }

    /// <summary>Walk to a room: through the rooms on the shortest path, square by square.</summary>
    public void WalkToRoom(int target)
    {
        StopWalking();
        var map = Expedition.Map;
        int from = Expedition.InRoom ? Expedition.RoomId : NearestEnd(target);
        foreach (int r in RoomPath(map, from, target)) _route.Enqueue(r);
        _routeTargetRoom = target;
        _nextStepAt = Time.unscaledTime;
    }

    /// <summary>Walk to a square of the corridor the party is in, or of a corridor next to its room.</summary>
    public void WalkToTile(int corridorId, int tileIndex)
    {
        StopWalking();
        var c = Expedition.Map.Corridor(corridorId);
        if (Expedition.InRoom && c.RoomA != Expedition.RoomId && c.RoomB != Expedition.RoomId) return;
        if (!Expedition.InRoom && Expedition.CorridorId != corridorId) return;
        _routeTargetTile = (corridorId, tileIndex);
        _nextStepAt = Time.unscaledTime;
    }

    private int NearestEnd(int target)
    {
        var c = Crawl.CurrentCorridor;
        var map = Expedition.Map;
        return map.Distances(c.RoomA)[target] <= map.Distances(c.RoomB)[target] ? c.RoomA : c.RoomB;
    }

    private static List<int> RoomPath(Core.Dungeon.DungeonMap map, int from, int to)
    {
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            if (r == to) break;
            foreach (int n in map.Neighbours(r))
                if (!prev.ContainsKey(n)) { prev[n] = r; queue.Enqueue(n); }
        }
        var path = new List<int>();
        if (!prev.ContainsKey(to)) return path;
        for (int at = to; at != -1; at = prev[at]) path.Add(at);
        path.Reverse();
        return path;   // starts with `from`
    }

    private void WalkOneStep()
    {
        _nextStepAt = Time.unscaledTime + StepSeconds;
        if (Crawl.IsBlocked || Crawl.CurioHere != null || Expedition.Camp != null) { StopWalking(); return; }
        int interruptsBefore = _interrupts;

        if (_routeTargetTile.corridor >= 0)
        {
            var (cid, idx) = _routeTargetTile;
            var c = Expedition.Map.Corridor(cid);
            if (Expedition.InRoom)
            {
                Travel(c.Other(Expedition.RoomId));
            }
            else if (Expedition.TileIndex == idx) { StopWalking(); return; }
            else
            {
                bool towardB = idx > Expedition.TileIndex;
                Step(forward: towardB == (Expedition.HeadingRoomId == c.RoomB));
            }
            if (!Expedition.InRoom && Expedition.TileIndex == idx) StopWalking();
        }
        else if (Expedition.InRoom)
        {
            while (_route.Count > 0 && _route.Peek() == Expedition.RoomId) _route.Dequeue();
            if (_route.Count == 0) { StopWalking(); return; }
            Travel(_route.Peek());
        }
        else
        {
            // In a corridor: keep walking toward the next room on the route.
            int next = _route.Count > 0 ? _route.Peek() : _routeTargetRoom;
            Step(forward: Expedition.HeadingRoomId == next);
        }
        if (_interrupts != interruptsBefore) StopWalking();
    }

    public void Fight()
    {
        if (Crawl == null || !Crawl.IsBlocked) return;
        bool inRoom = Expedition.InRoom;
        var room = inRoom ? Crawl.CurrentRoom : null;
        var kind = room is { Content: Core.Dungeon.RoomContent.Boss } ? FightKind.Boss : inRoom ? FightKind.Room : FightKind.Hall;
        StartFight(kind, heroesSurprised: false);
    }

    /// <summary>Testing (F11): start a hall fight right here through the normal crawl path.</summary>
    public void DebugFight()
    {
        if (Phase == Phase.Crawling) StartFight(FightKind.Hall, heroesSurprised: false);
    }

    private void StartFight(FightKind kind, bool heroesSurprised)
    {
        var quest = Expedition.Quest;
        var plan = S.Zones.Plan(quest.Dungeon, quest.Difficulty, kind, new Rng(Expedition.Seed * 7 + Expedition.BattlesWon * 131 + Expedition.StepsTaken), quest.BossId);
        var guids = Expedition.Party.Select(Party.Guid).Where(g => g != 0 && !Dd2Api.IsDead(g)).ToList();
        var buffs = Crawl.FightBuffs().Select(b => (Party.Guid(b.Hero), b.Buff)).Where(b => b.Item1 != 0).ToList();
        if (Dd2Combat.Start(plan, guids, Expedition.Light, heroesSurprised, buffs))
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
        if (Crawl.LastSpoils is { } spoils)
            Plugin.Log.LogInfo($"[loot] {spoils.Kind} fight ({string.Join(" ", spoils.Dd1Monsters)}): " +
                               $"took {string.Join(", ", spoils.Taken)}; left {string.Join(", ", spoils.LeftBehind)}");
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

    /// <summary>Events that should stop an auto-walk (DD1 stops for anything you need to look at).</summary>
    private int _interrupts;

    private void Handle(List<CrawlEvent> events)
    {
        Dd2Api.Torch = Expedition.Light;
        FightKind? fight = null;
        bool surprised = false;
        foreach (var e in events)
        {
            switch (e.Type)
            {
                case CrawlEventType.Battle:
                    // DD1 starts the fight the moment the party walks into it.
                    var room = e.RoomId >= 0 ? Expedition.Map.Room(e.RoomId) : null;
                    fight = room?.Content == Core.Dungeon.RoomContent.Boss ? FightKind.Boss : e.RoomId >= 0 ? FightKind.Room : FightKind.Hall;
                    surprised = e.HeroesSurprised;
                    Announce(e.HeroesSurprised ? "Ambush! The heroes are surprised!" : e.MonstersSurprised ? "The enemy is caught unawares!" : "Enemies ahead!");
                    break;
                case CrawlEventType.Ambush:
                    fight = e.ContentId == "camp" ? FightKind.CampAmbush : FightKind.Hall;
                    surprised = true;
                    Announce(e.ContentId == "camp" ? "The camp is ambushed in the night!" : "Something stirs in the dark...");
                    break;
                case CrawlEventType.TrapSprung: Announce("A trap is sprung!"); break;
                case CrawlEventType.TrapDisarmed: Announce("Trap disarmed."); break;
                case CrawlEventType.Trap: Announce("A trap lies ahead."); break;
                case CrawlEventType.Obstacle: Announce("The way is blocked."); break;
                case CrawlEventType.ObstacleCleared: Say("The way is clear."); break;
                case CrawlEventType.Curio: _interrupts++; Say($"There is something here: {e.ContentId}."); break;
                case CrawlEventType.Ate: Announce("The party eats."); break;
                case CrawlEventType.Starving: Announce("There is no food. The party starves!"); break;
                case CrawlEventType.Scouted: Announce("Scouting reveals the way ahead."); break;
                case CrawlEventType.QuestComplete: Announce("Quest complete! You may return to the Hamlet."); break;
                case CrawlEventType.Stress: break;
            }
        }
        S.Persist();
        if (fight != null) StartFight(fight.Value, surprised);
    }

    private void Announce(string text)
    {
        _interrupts++;
        Say(text);
        Ui.Gui.Announce(text);
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
        HeroStage.Instance?.Clear();
        Phase = Phase.Homecoming;
        Dd2Run.End();
    }

    public void BackToHamlet() => Phase = Phase.Hamlet;
}
