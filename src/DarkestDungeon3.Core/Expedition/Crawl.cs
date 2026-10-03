using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>
/// DD1's dungeon crawl: walking corridors square by square, spending light, taking hallway stress, tripping
/// traps, eating, scouting, and stopping for fights. Fights themselves happen in DD2's combat; the caller
/// reports the outcome back with <see cref="ResolveBattle"/>.
/// </summary>
public sealed class Crawl
{
    public ExpeditionState State { get; }
    private readonly CrawlRules _rules;
    private readonly IParty _party;
    private readonly CrawlContent _content;
    private readonly List<CrawlEvent> _events = new();

    public Crawl(ExpeditionState state, CrawlRules rules, IParty party, CrawlContent content = null)
    {
        State = state;
        _rules = rules;
        _party = party;
        _content = content;
    }

    /// <summary>The untouched curio where the party stands (room or hall square), if any.</summary>
    public string CurioHere =>
        State.InRoom
            ? (CurrentRoom.CurioId != null && !CurrentRoom.CurioTaken && !IsBlocked ? CurrentRoom.CurioId : null)
            : (CurrentTile is { Content: HallContent.Curio, Resolved: false } t ? t.ContentId : null);

    /// <summary>
    /// A hero investigates the curio here, optionally using an item on it. Loot goes into the pack; whatever
    /// doesn't fit comes back in <paramref name="overflow"/> for the player to sort out, as in DD1.
    /// </summary>
    public CurioReport InteractCurio(string heroId, string itemId, out List<LootDrop> overflow)
    {
        overflow = new List<LootDrop>();
        string curio = CurioHere;
        if (curio == null) return null;

        bool isGoal = State.InRoom ? CurrentRoom.IsQuestGoal : CurrentTile.IsQuestGoal;
        if (isGoal && State.Goal != null) return InteractQuestCurio(curio, heroId, itemId);
        if (_content?.Curios == null) return null;

        var report = _content.Curios.Resolve(curio, heroId, itemId, State, _party, NextRng());
        if (State.InRoom) CurrentRoom.CurioTaken = true;
        else CurrentTile.Resolved = true;

        foreach (var drop in report.Loot)
        {
            if (drop.Type == "trinket" || State.Pack.HasRoomFor(drop.Key, drop.Amount, _content.Items)) State.Pack.Add(drop.Key, drop.Amount);
            else overflow.Add(drop);
        }
        return report;
    }

    /// <summary>The item an inventory-activate quest curio needs (e.g. holy water for a corrupted altar), or null.</summary>
    public string QuestItemNeededHere
    {
        get
        {
            bool isGoal = CurioHere != null && (State.InRoom ? CurrentRoom.IsQuestGoal : CurrentTile.IsQuestGoal);
            return isGoal && State.Goal is { NeedsItem: true } g ? g.StartingItems[0].Id : null;
        }
    }

    private CurioReport InteractQuestCurio(string curio, string heroId, string itemId)
    {
        var goal = State.Goal;
        var report = new CurioReport { CurioId = curio, HeroId = heroId, OutcomeType = "Quest" };
        if (goal.NeedsItem)
        {
            string needed = goal.StartingItems[0].Id;
            if (itemId != needed || !State.Pack.TryUse(needed))
            {
                report.OutcomeType = "NeedsItem";
                report.Text = $"Something is needed here: {needed}.";
                return report;   // left untouched; the party can come back with the item
            }
            report.ItemUsed = needed;
        }
        else if (goal.Type == "gather" && goal.QuestItem != null)
        {
            State.Pack.Add(goal.QuestItem, 1);
            report.Loot.Add(new LootDrop { Type = "quest_item", Id = goal.QuestItem, Amount = 1 });
        }

        State.GoalProgress++;
        report.Text = $"Quest objective {State.GoalProgress}/{goal.Amount}.";
        if (State.InRoom) CurrentRoom.CurioTaken = true;
        else CurrentTile.Resolved = true;
        CheckQuest();
        return report;
    }

    /// <summary>Walk past a curio without touching it.</summary>
    public void SkipCurio()
    {
        if (State.InRoom) { if (CurrentRoom.CurioId != null) CurrentRoom.CurioTaken = true; }
        else if (CurrentTile is { Content: HallContent.Curio } t) t.Resolved = true;
    }

    private DungeonMap Map => State.Map;

    public Room CurrentRoom => State.InRoom ? Map.Room(State.RoomId) : null;
    public Corridor CurrentCorridor => State.CorridorId >= 0 ? Map.Corridor(State.CorridorId) : null;
    public HallTile CurrentTile => !State.InRoom && CurrentCorridor != null ? CurrentCorridor.Tiles[State.TileIndex] : null;

    /// <summary>A fight or obstacle the party has to deal with before it can go on.</summary>
    public bool IsBlocked =>
        State.InRoom
            ? CurrentRoom.HasBattle && !CurrentRoom.Cleared
            : CurrentTile != null && !CurrentTile.Resolved && CurrentTile.Content is HallContent.Battle or HallContent.Obstacle;

    /// <summary>Put the party in the entrance room at the start of the expedition.</summary>
    public List<CrawlEvent> Begin()
    {
        _events.Clear();
        State.Started = true;
        State.RoomId = Map.EntranceRoomId;
        EnterRoom(Map.Room(State.RoomId));
        return Flush();
    }

    /// <summary>From a room, step into the corridor leading to a neighbouring room.</summary>
    public List<CrawlEvent> Travel(int toRoomId)
    {
        _events.Clear();
        if (!State.InRoom || IsBlocked) return Blocked();
        var corridor = Map.FindCorridor(State.RoomId, toRoomId);
        if (corridor == null) return Blocked();

        State.CorridorId = corridor.Id;
        State.HeadingRoomId = toRoomId;
        State.TileIndex = corridor.RoomA == State.RoomId ? 0 : corridor.Tiles.Count - 1;
        State.RoomId = -1;
        EnterTile(forward: true);
        return Flush();
    }

    /// <summary>Walk one square toward the room the party is heading for (forward) or back the way it came.</summary>
    public List<CrawlEvent> Step(bool forward)
    {
        _events.Clear();
        if (State.InRoom) return Blocked();
        var tile = CurrentTile;
        // An unresolved fight pins the party; an obstacle only blocks the way forward.
        if (!tile.Resolved && (tile.Content == HallContent.Battle || (forward && tile.Content == HallContent.Obstacle)))
            return Blocked();

        var corridor = CurrentCorridor;
        int towardB = State.HeadingRoomId == corridor.RoomB ? 1 : -1;
        int dir = forward ? towardB : -towardB;
        int next = State.TileIndex + dir;

        if (next < 0 || next >= corridor.Tiles.Count)
        {
            int roomId = next < 0 ? corridor.RoomA : corridor.RoomB;
            State.RoomId = roomId;
            State.CorridorId = -1;
            State.TileIndex = -1;
            State.HeadingRoomId = -1;
            EnterRoom(Map.Room(roomId));
        }
        else
        {
            State.TileIndex = next;
            EnterTile(forward);
        }
        return Flush();
    }

    /// <summary>Report a won fight at the party's current spot.</summary>
    /// <summary>The loot of the last fight won (DD1 rules, see <see cref="BattleLoot"/>).</summary>
    public BattleSpoils LastSpoils { get; private set; }

    public List<CrawlEvent> ResolveBattle()
    {
        _events.Clear();
        State.BattlesWon++;
        LastSpoils = TakeSpoils(State.InRoom ? (CurrentRoom.Content == RoomContent.Boss ? "boss" : "room") : "hall");
        CountDownBuffs();
        if (State.InRoom)
        {
            var room = CurrentRoom;
            room.Cleared = true;
            if (room.Content is RoomContent.GuardedCurio or RoomContent.GuardedTreasure && room.CurioId != null)
                Emit(CrawlEventType.Curio, roomId: room.Id, contentId: room.CurioId);
        }
        else if (CurrentTile != null)
        {
            CurrentTile.Resolved = true;
        }
        CheckQuest();
        return Flush();
    }

    /// <summary>The DD1 buffs (camp skills, town visits) each living hero carries into the next fight.</summary>
    public List<(string Hero, Dd1Buff Buff)> FightBuffs()
    {
        var list = new List<(string, Dd1Buff)>();
        if (_content?.Buffs == null) return list;
        foreach (var kv in State.PendingBuffs)
        {
            if (!_party.Alive.Contains(kv.Key)) continue;
            foreach (var id in kv.Value.Distinct())
                if (_content.Buffs.Get(id) is { InDungeon: true } buff) list.Add((kv.Key, buff));
        }
        return list;
    }

    /// <summary>After a fight: buffs that last a number of battles (DD1 "combat_end") count down.</summary>
    private void CountDownBuffs()
    {
        if (_content?.Buffs == null) return;
        foreach (var hero in State.PendingBuffs.Keys.ToList())
        {
            var keep = new List<string>();
            foreach (var id in State.PendingBuffs[hero].Distinct())
            {
                var buff = _content.Buffs.Get(id);
                if (buff == null || buff.Battles == 0) { keep.Add(id); continue; }
                string key = hero + "|" + id;
                int left = (State.BuffBattlesLeft.TryGetValue(key, out var l) ? l : buff.Battles) - 1;
                if (left > 0) { State.BuffBattlesLeft[key] = left; keep.Add(id); }
                else State.BuffBattlesLeft.Remove(key);
            }
            State.PendingBuffs[hero] = keep;
        }
    }

    private BattleSpoils TakeSpoils(string kind)
    {
        var spoils = new BattleSpoils { Kind = kind };
        if (_content?.Battles == null || _content.Loot == null) return spoils;
        var drops = _content.Battles.Roll(_content.Loot, State.Quest?.Dungeon, State.Quest?.Difficulty ?? 1, kind, NextRng(), out spoils.Dd1Monsters);
        foreach (var drop in drops)
        {
            if (drop.Type == "trinket" || State.Pack.HasRoomFor(drop.Key, drop.Amount, _content.Items))
            {
                State.Pack.Add(drop.Key, drop.Amount);
                spoils.Taken.Add(drop);
            }
            else spoils.LeftBehind.Add(drop);
        }
        return spoils;
    }

    /// <summary>Clear the obstacle ahead: with a shovel, or by force (damage and stress, as in DD1).</summary>
    public List<CrawlEvent> ClearObstacle()
    {
        _events.Clear();
        var tile = CurrentTile;
        if (tile == null || tile.Content != HallContent.Obstacle || tile.Resolved) return Blocked();

        if (!State.Pack.TryUse(Supply.Shovel))
        {
            var rng = NextRng();
            foreach (var hero in _party.Alive)
            {
                _party.Damage(hero, 0.1f, "obstacle");
                StressDd1(hero, 10, rng, "obstacle");
            }
        }
        tile.Resolved = true;
        Emit(CrawlEventType.ObstacleCleared, tile: tile, contentId: tile.ContentId);
        return Flush();
    }

    /// <summary>Try to disarm a trap the party scouted (DD1 gives a bonus for seeing it coming).</summary>
    public List<CrawlEvent> DisarmTrap()
    {
        _events.Clear();
        var tile = CurrentTile;
        if (tile == null || tile.Content != HallContent.Trap || tile.Resolved) return Blocked();
        TriggerTrap(tile, scouted: true);
        return Flush();
    }

    public List<CrawlEvent> UseTorch()
    {
        _events.Clear();
        if (State.Light < 100 && State.Pack.TryUse(Supply.Torch)) ChangeLight(_rules.TorchLight);
        return Flush();
    }

    /// <summary>Snuff the torch (DD1 lets you trade safety for loot and surprise).</summary>
    public List<CrawlEvent> SnuffTorch(float amount = 25f)
    {
        _events.Clear();
        ChangeLight(-amount);
        return Flush();
    }

    // ---- camping ----

    /// <summary>DD1 camps need firewood and a safe (cleared) room.</summary>
    public bool CanCamp => State.InRoom && !IsBlocked && State.Camp == null && State.Pack.Count(Supply.Firewood) > 0;

    public List<CrawlEvent> MakeCamp()
    {
        _events.Clear();
        if (!CanCamp) return Blocked();
        State.Pack.TryUse(Supply.Firewood);
        State.Camp = new CampState { RespiteLeft = _rules.CampPoints };
        State.CampsMade++;
        return Flush();
    }

    /// <summary>Food for each living hero by meal size. Not enough food means no meal at all.</summary>
    public int MealCost(Meal meal) => (int)Math.Ceiling(_party.Alive.Count * _rules.Meals[meal].RationsPer);

    public bool EatMeal(Meal meal)
    {
        if (State.Camp == null || State.Camp.Ate) return false;
        if (!State.Pack.TryUse(Supply.Food, MealCost(meal)) && MealCost(meal) > 0) return false;
        var (_, heal, stress) = _rules.Meals[meal];
        var rng = NextRng();
        foreach (var hero in _party.Alive)
        {
            if (heal > 0) _party.Heal(hero, heal);
            else if (heal < 0) _party.Damage(hero, -heal, "hunger");
            if (stress != 0) StressDd1(hero, stress, rng, "meal");
        }
        State.Camp.Ate = true;
        return true;
    }

    public string WhyCantUseCampSkill(string heroId, string skillId)
    {
        var camp = State.Camp;
        var skill = _content?.Camping?.Get(skillId);
        if (camp == null) return "Not camping.";
        if (skill == null) return "Unknown skill.";
        if (!_party.Alive.Contains(heroId)) return "Dead.";
        if (!State.CampSkills.TryGetValue(heroId, out var known) || !known.Contains(skillId)) return "Not known.";
        if (skill.Cost > camp.RespiteLeft) return "Not enough respite.";
        if (camp.Uses.TryGetValue(heroId + ":" + skillId, out var used) && used >= skill.UseLimit) return "Already used.";
        return null;
    }

    /// <summary>Use a camp skill. Individual-target skills need a target hero.</summary>
    public bool UseCampSkill(string heroId, string skillId, string targetId = null)
    {
        if (WhyCantUseCampSkill(heroId, skillId) != null) return false;
        var skill = _content.Camping.Get(skillId);
        if (skill.NeedsTarget && (targetId == null || !_party.Alive.Contains(targetId))) return false;

        var camp = State.Camp;
        camp.RespiteLeft -= skill.Cost;
        string key = heroId + ":" + skillId;
        camp.Uses[key] = (camp.Uses.TryGetValue(key, out var n) ? n : 0) + 1;

        var rng = NextRng();
        foreach (var effect in skill.Effects)
        {
            if (!rng.Chance(effect.Chance)) continue;
            var targets = effect.Selection switch
            {
                "self" => new[] { heroId },
                "individual" => new[] { targetId },
                "party" => _party.Alive.ToArray(),
                "party_other" => _party.Alive.Where(h => h != heroId).ToArray(),
                _ => new[] { heroId },
            };
            foreach (var t in targets) ApplyCampEffect(effect, t, rng);
        }
        return true;
    }

    private void ApplyCampEffect(CampEffect effect, string hero, Rng rng)
    {
        switch (effect.Type)
        {
            case "stress_heal_amount": StressDd1(hero, -effect.Amount, rng, "camp"); break;
            case "stress_damage_amount": StressDd1(hero, effect.Amount, rng, "camp"); break;
            case "health_heal_max_health_percent": _party.Heal(hero, effect.Amount); break;
            case "health_damage_max_health_percent": _party.Damage(hero, effect.Amount, "camp"); break;
            case "remove_disease": _party.CureDisease(hero); break;
            case "reduce_ambush_chance": State.Camp.AmbushReduction += effect.Amount; break;
            case "reduce_torch": ChangeLight(-effect.Amount); break;
            case "buff":
                if (!string.IsNullOrEmpty(effect.SubType))
                {
                    if (!State.PendingBuffs.TryGetValue(hero, out var list)) State.PendingBuffs[hero] = list = new List<string>();
                    if (!list.Contains(effect.SubType)) list.Add(effect.SubType);
                    State.BuffBattlesLeft.Remove(hero + "|" + effect.SubType);
                }
                break;
            case "loot":
                if (_content?.Loot != null && !string.IsNullOrEmpty(effect.SubType))
                    foreach (var d in _content.Loot.Roll(effect.SubType, Math.Max(1, (int)effect.Amount), State.Quest?.Difficulty ?? 1, State.Quest?.Dungeon ?? "", rng))
                        State.Pack.Add(d.Key, d.Amount);
                break;
            // remove_bleeding / remove_poison / remove_deaths_door_recovery_buffs: DoTs already landed out of
            // combat, and DD2 owns death's door recovery, so there's nothing left to undo.
        }
    }

    /// <summary>Strike camp: the torch is relit, and DD1 rolls for a night ambush.</summary>
    public List<CrawlEvent> BreakCamp()
    {
        _events.Clear();
        if (State.Camp == null) return Blocked();
        var rng = NextRng();
        float ambush = Math.Max(0f, _rules.AmbushCampChance - State.Camp.AmbushReduction);
        if (!State.Camp.Ate) foreach (var hero in _party.Alive) StressDd1(hero, _rules.Meals[Meal.None].StressDd1, rng, "no meal");
        State.Camp = null;
        ChangeLight(_rules.CampRestoreTorch - State.Light);
        if (rng.Chance(ambush))
            _events.Add(new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = State.RoomId, HeroesSurprised = true, ContentId = "camp" });
        return Flush();
    }

    public void Retreat()
    {
        State.Retreated = true;
        State.Ended = true;
    }

    // ---- internals ----

    private void EnterTile(bool forward)
    {
        var tile = CurrentTile;
        var rng = NextRng();
        State.StepsTaken++;

        ChangeLight(-(tile.Visited ? _rules.LightLossVisitedTile : _rules.LightLossNewTile));
        HallwayStress(forward, rng);

        bool firstVisit = !tile.Visited;
        tile.Visited = true;
        Emit(CrawlEventType.EnteredTile, tile: tile, contentId: tile.ContentId);

        if (firstVisit && !tile.Resolved)
        {
            switch (tile.Content)
            {
                case HallContent.Battle:
                    EmitBattle(CrawlEventType.Battle, tile, corridor: true, rng);
                    break;
                case HallContent.Trap:
                    if (tile.Scouted) Emit(CrawlEventType.Trap, tile: tile, contentId: tile.ContentId);
                    else TriggerTrap(tile, scouted: false);
                    break;
                case HallContent.Obstacle:
                    Emit(CrawlEventType.Obstacle, tile: tile, contentId: tile.ContentId);
                    break;
                case HallContent.Curio:
                    Emit(CrawlEventType.Curio, tile: tile, contentId: tile.ContentId);
                    break;
                case HallContent.Hunger:
                    tile.Resolved = true;
                    HungerCheck();
                    break;
            }
        }
        else if (!firstVisit)
        {
            // Walking back through explored halls: DD1 can spring a wandering fight or a hunger check.
            if (rng.Chance(_rules.ReturnBattleChance)) EmitBattle(CrawlEventType.Ambush, tile, corridor: true, rng);
            else if (rng.Chance(_rules.ReturnHungerChance)) HungerCheck();
        }
    }

    private void EnterRoom(Room room)
    {
        bool firstVisit = !room.Visited;
        room.Visited = true;
        Emit(CrawlEventType.EnteredRoom, roomId: room.Id);

        if (firstVisit) Scout(room);

        if (room.HasBattle && !room.Cleared)
            EmitBattle(CrawlEventType.Battle, null, corridor: false, NextRng(), room.Id);
        else if (firstVisit && room.CurioId != null && room.Content is RoomContent.Curio or RoomContent.Treasure)
            Emit(CrawlEventType.Curio, roomId: room.Id, contentId: room.CurioId);

        CheckQuest();
    }

    private void HallwayStress(bool forward, Rng rng)
    {
        var band = _rules.Band(State.Light);
        float chance = (forward ? _rules.StressChanceForward : _rules.StressChanceBack) + band.StressChanceIncrease / 100f;
        float dd1 = (forward ? _rules.StressDd1Forward : _rules.StressDd1Back) * (1f + band.StressDamageIncrease / 100f);
        foreach (var hero in _party.Alive)
            if (rng.Chance(chance)) StressDd1(hero, dd1, rng, "hallway");
    }

    /// <summary>
    /// DD1 stress amounts become whole DD2 points by probabilistic rounding: 2 DD1 stress is a 20% chance of
    /// one DD2 point, 15 is one point plus a 50% chance of a second.
    /// </summary>
    private void StressDd1(string hero, float dd1Amount, Rng rng, string cause)
    {
        float points = dd1Amount / CrawlRules.Dd1StressPerDd2Point;
        int whole = (int)Math.Floor(points);
        if (rng.Chance(points - whole)) whole++;
        if (whole == 0) return;
        _party.AddStress(hero, whole, cause);
        Emit(CrawlEventType.Stress, heroId: hero, amount: whole, contentId: cause);
    }

    private void HungerCheck()
    {
        var alive = _party.Alive;
        if (State.Pack.TryUse(Supply.Food, alive.Count))
        {
            foreach (var hero in alive) _party.Heal(hero, _rules.HungerHealFraction);
            Emit(CrawlEventType.Ate, amount: alive.Count);
            return;
        }
        // Not enough for everyone: DD1 skips the meal entirely and everybody starves.
        var rng = NextRng();
        foreach (var hero in alive)
        {
            _party.Damage(hero, _rules.StarveHpFraction, "starvation");
            StressDd1(hero, _rules.StarveStressDd1, rng, "starvation");
        }
        Emit(CrawlEventType.Starving, amount: alive.Count);
    }

    private void TriggerTrap(HallTile tile, bool scouted)
    {
        var rng = NextRng();
        int difficulty = Math.Min(State.Quest?.Difficulty ?? 1, _rules.TrapDifficultyPenalty.Length - 1);
        float disarm = 0.5f - _rules.TrapDifficultyPenalty[difficulty] + (scouted ? _rules.TrapScoutDisarmBonus : 0f);
        tile.Resolved = true;
        var trap = _content?.Traps?.Get(tile.ContentId, State.Quest?.Difficulty ?? 1);
        // The front hero deals with it, as in DD1.
        var hero = _party.Alive.FirstOrDefault();
        if (rng.Chance(disarm))
        {
            if (hero != null && trap != null)
                foreach (var e in trap.SuccessEffects) _content.Curios.ApplyEffect(e, hero, _party, rng);
            Emit(CrawlEventType.TrapDisarmed, tile: tile, contentId: tile.ContentId, heroId: hero);
            return;
        }
        if (hero != null)
        {
            if (trap != null)
            {
                if (trap.HealthFraction < 0) _party.Damage(hero, -trap.HealthFraction, "trap");
                foreach (var e in trap.FailEffects) _content.Curios.ApplyEffect(e, hero, _party, rng);
            }
            else
            {
                _party.Damage(hero, 0.2f, "trap");
                StressDd1(hero, 15, rng, "trap");
            }
        }
        Emit(CrawlEventType.TrapSprung, tile: tile, contentId: tile.ContentId, heroId: hero);
    }

    /// <summary>On entering a new room, maybe reveal what lies within two corridors (DD1 scouting).</summary>
    private void Scout(Room room)
    {
        var rng = NextRng();
        float chance = _rules.ScoutChanceBase + _rules.Band(State.Light).ScoutingIncrease / 100f;
        if (!rng.Chance(chance)) return;

        var dist = Map.Distances(room.Id);
        int revealed = 0;
        foreach (var r in Map.Rooms.Where(r => dist[r.Id] > 0 && dist[r.Id] <= 2 && !r.Scouted))
        {
            r.Scouted = true;
            revealed++;
        }
        foreach (var c in Map.Corridors.Where(c => Math.Min(dist[c.RoomA], dist[c.RoomB]) <= 1))
            foreach (var t in c.Tiles.Where(t => !t.Scouted))
            {
                t.Scouted = true;
                revealed++;
            }
        if (revealed > 0) Emit(CrawlEventType.Scouted, roomId: room.Id, amount: revealed);
    }

    private void EmitBattle(CrawlEventType type, HallTile tile, bool corridor, Rng rng, int roomId = -1)
    {
        var band = _rules.Band(State.Light);
        float heroes = (corridor ? _rules.SurpriseCorridorParty : _rules.SurpriseRoomParty) + band.HeroesSurprisedIncrease / 100f;
        float monsters = (corridor ? _rules.SurpriseCorridorMonsters : _rules.SurpriseRoomMonsters) + band.MonstersSurprisedIncrease / 100f;
        bool heroesSurprised = rng.Chance(Math.Min(heroes, _rules.SurpriseMaxParty));
        bool monstersSurprised = !heroesSurprised && rng.Chance(monsters);

        var e = new CrawlEvent
        {
            Type = type,
            RoomId = roomId,
            HeroesSurprised = heroesSurprised,
            MonstersSurprised = monstersSurprised,
        };
        if (tile != null)
        {
            e.CorridorId = State.CorridorId;
            e.TileIndex = tile.Index;
            if (type == CrawlEventType.Ambush) { tile.Content = HallContent.Battle; tile.Resolved = false; }
        }
        _events.Add(e);
    }

    private void CheckQuest()
    {
        if (State.QuestComplete || State.Quest == null) return;
        var goal = State.Goal;
        float explorePct = goal?.Type == "explore_room" && goal.Percentage > 0 ? goal.Percentage : 0.9f;
        bool done = State.Quest.Type switch
        {
            "explore" => Map.Rooms.Count(r => r.Visited) >= Math.Ceiling(Map.Rooms.Count * explorePct),
            "cleanse" => Map.Rooms.Where(r => r.HasBattle).All(r => r.Cleared),
            "kill_boss" => Map.BossRoomId >= 0 && Map.Room(Map.BossRoomId).Cleared,
            "gather" or "activate" or "inventory_activate" when goal != null && goal.Amount > 0 => State.GoalProgress >= goal.Amount,
            _ => Map.Rooms.Count(r => r.Visited) >= Math.Ceiling(Map.Rooms.Count * 0.9),
        };
        if (!done) return;
        State.QuestComplete = true;
        Emit(CrawlEventType.QuestComplete);
    }

    private void ChangeLight(float delta)
    {
        float before = State.Light;
        State.Light = Math.Max(0f, Math.Min(100f, State.Light + delta));
        if (Math.Abs(State.Light - before) > 0.001f) Emit(CrawlEventType.LightChanged, amount: State.Light);
    }

    private Rng NextRng() => new(unchecked(State.Seed * 31 + ++State.RandomCounter * 977));

    private List<CrawlEvent> Blocked()
    {
        Emit(CrawlEventType.Blocked);
        return Flush();
    }

    private void Emit(CrawlEventType type, HallTile tile = null, int roomId = -1, string contentId = null,
                      string heroId = null, float amount = 0)
    {
        _events.Add(new CrawlEvent
        {
            Type = type,
            RoomId = roomId,
            CorridorId = tile != null ? State.CorridorId : -1,
            TileIndex = tile?.Index ?? -1,
            ContentId = contentId,
            HeroId = heroId,
            Amount = amount,
        });
    }

    private List<CrawlEvent> Flush()
    {
        var copy = new List<CrawlEvent>(_events);
        _events.Clear();
        return copy;
    }
}
