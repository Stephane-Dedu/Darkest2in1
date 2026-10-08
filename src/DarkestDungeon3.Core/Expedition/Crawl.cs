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
        State.PendingCurio?.RestoreLootLinks();
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
        if (State.Ended || heroId == null || !_party.Alive.Contains(heroId)) return null;
        if (!CanLeave(State.PendingCurio?.LeftBehind) || !CanLeave(State.PendingSpoils?.LeftBehind)) return null;
        string curio = CurioHere;
        if (curio == null) return null;

        bool isGoal = State.InRoom ? CurrentRoom.IsQuestGoal : CurrentTile.IsQuestGoal;
        if (isGoal && State.Goal != null)
        {
            var questReport = InteractQuestCurio(curio, heroId, itemId);
            overflow.AddRange(questReport.LeftBehind);
            return State.PendingCurio = questReport;
        }
        if (_content?.Curios == null) return null;

        var report = _content.Curios.Resolve(curio, heroId, itemId, State, _party, NextRng());
        if (State.InRoom) CurrentRoom.CurioTaken = true;
        else CurrentTile.Resolved = true;
        var pickRng = NextRng();
        for (int i = 0; i < report.Loot.Count; i++) report.Loot[i] = LootDrop.ResolveTrinket(report.Loot[i], TrinketOfRarity, pickRng);

        foreach (var drop in report.Loot)
        {
            if (!State.Pack.TryTake(drop, _content.Items)) { overflow.Add(drop); report.LeftBehind.Add(drop); }
        }
        return State.PendingCurio = report;
    }

    public CurioReport LastCurio => State.PendingCurio;

    public bool DismissCurio(CurioReport report)
    {
        if (State.Ended || report == null || report != LastCurio || !CanLeave(report.LeftBehind)) return false;
        State.PendingCurio = null;
        return true;
    }

    /// <summary>DD1's loot scroll: take as much of a waiting drop as fits, retaining its remainder.</summary>
    public bool TakeLeftBehind(List<LootDrop> leftBehind, int index, List<LootDrop> taken = null)
    {
        if (State.Ended || leftBehind == null || index < 0 || index >= leftBehind.Count) return false;
        var drop = leftBehind[index];
        int amount = State.Pack.TakePartial(drop, _content.Items);
        if (amount == 0) return false;
        if (amount == drop.Amount)
        {
            leftBehind.RemoveAt(index);
            taken?.Add(drop);
        }
        else
        {
            taken?.Add(new LootDrop { Type = drop.Type, Id = drop.Id, Amount = amount });
            drop.Amount -= amount;
        }
        return true;
    }

    /// <summary>DD1: shift+click a pack item to throw one away (quest items can't be).</summary>
    public bool Discard(string key)
    {
        if (State.Ended || string.IsNullOrEmpty(key) || IsQuestItem(key)) return false;
        return State.Pack.TryUse(key, 1);
    }

    /// <summary>DD1 won't close a loot scroll that still holds a quest item.</summary>
    public static bool CanLeave(IEnumerable<LootDrop> leftBehind) => leftBehind == null || !leftBehind.Any(d => IsQuestItem(d.Key));

    /// <summary>Explicit returns cannot discard required loot or abandon a no-retreat quest.</summary>
    public bool CanLeaveExpedition => !State.Ended
        && CanLeave(State.PendingCurio?.LeftBehind) && CanLeave(State.PendingSpoils?.LeftBehind)
        && (State.QuestComplete || State.Quest?.CanRetreat != false);

    public bool TryLeave()
    {
        if (!CanLeaveExpedition) return false;
        if (State.QuestComplete) State.Ended = true;
        else Retreat();
        return State.Ended;
    }

    private static bool IsQuestItem(string key) => key != null && key.StartsWith("quest_item+", StringComparison.Ordinal);

    /// <summary>The item an inventory-activate quest curio needs (e.g. holy water for a corrupted altar), or null.</summary>
    public string QuestItemNeededHere
    {
        get
        {
            bool isGoal = CurioHere != null && (State.InRoom ? CurrentRoom.IsQuestGoal : CurrentTile.IsQuestGoal);
            return isGoal && State.Goal is { NeedsItem: true } g ? ItemCatalog.QuestKey(g.StartingItems[0].Id) : null;
        }
    }

    private CurioReport InteractQuestCurio(string curio, string heroId, string itemId)
    {
        var goal = State.Goal;
        var report = new CurioReport { CurioId = curio, HeroId = heroId, OutcomeType = "Quest" };
        if (goal.NeedsItem)
        {
            string needed = ItemCatalog.QuestKey(goal.StartingItems[0].Id);
            if (itemId != needed || !State.Pack.TryUse(needed))
            {
                report.OutcomeType = "NeedsItem";
                report.Text = $"Something is needed here: {goal.StartingItems[0].Id.Replace('_', ' ')}.";
                return report;   // left untouched; the party can come back with the item
            }
            report.ItemUsed = needed;
        }
        else if (goal.Type == "gather" && goal.QuestItem != null)
        {
            var drop = new LootDrop { Type = "quest_item", Id = goal.QuestItem, Amount = 1 };
            report.Loot.Add(drop);
            if (_content?.Items == null || !State.Pack.TryTake(drop, _content.Items)) report.LeftBehind.Add(drop);
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
        if (State.Ended) return;
        // A quest curio is never skipped for good: the quest needs it.
        if (State.InRoom) { if (CurrentRoom.CurioId != null && !CurrentRoom.IsQuestGoal) CurrentRoom.CurioTaken = true; }
        else if (CurrentTile is { Content: HallContent.Curio, IsQuestGoal: false } t) t.Resolved = true;
    }

    private DungeonMap Map => State.Map;

    public Room CurrentRoom => State.InRoom ? Map.Room(State.RoomId) : null;
    public Corridor CurrentCorridor => State.CorridorId >= 0 ? Map.Corridor(State.CorridorId) : null;
    public HallTile CurrentTile => !State.InRoom && CurrentCorridor != null ? CurrentCorridor.Tiles[State.TileIndex] : null;

    /// <summary>A fight or obstacle the party has to deal with before it can go on.</summary>
    public bool IsBlocked =>
        State.InRoom
            ? CurrentRoom.HasBattle && !CurrentRoom.Cleared
            : CurrentTile != null && !CurrentTile.Resolved
              && (CurrentTile.Content is HallContent.Battle or HallContent.Obstacle
                  || (CurrentTile.Content == HallContent.Trap && CurrentTile.Scouted));   // DD1: a spotted trap stops the party until dealt with

    /// <summary>Put the party in the entrance room at the start of the expedition.</summary>
    public List<CrawlEvent> Begin()
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        if (State.Started) return Resume();
        State.Started = true;
        State.RoomId = Map.EntranceRoomId;
        EnterRoom(Map.Room(State.RoomId), enteringDungeon: true);
        return Flush();
    }

    public bool CanResume => ExpeditionRecovery.PositionIsValid(State);

    /// <summary>Present an interrupted crawl at its saved spot without walking, scouting, eating or rolling again.</summary>
    public List<CrawlEvent> Resume()
    {
        _events.Clear();
        if (!CanResume) return Blocked();
        if (State.InRoom) Emit(CrawlEventType.EnteredRoom, roomId: State.RoomId);
        else Emit(CrawlEventType.EnteredTile, tile: CurrentTile, contentId: CurrentTile.ContentId);
        var saved = ExpeditionFight.Presentation(State) ?? State.PendingEncounter;
        bool atSpot = saved != null && (State.InRoom ? saved.RoomId == State.RoomId
            : saved.RoomId == -1 && saved.CorridorId == State.CorridorId && saved.TileIndex == State.TileIndex);
        if (atSpot && saved.Type is CrawlEventType.Battle or CrawlEventType.Ambush)
            _events.Add(CopyEncounter(saved));
        else if (State.Camp == null)
        {
            // Older saves have no surprise snapshot; don't invent or reroll one.
            if (State.InRoom && CurrentRoom.HasBattle && !CurrentRoom.Cleared)
                Emit(CrawlEventType.Battle, roomId: State.RoomId);
            else if (!State.InRoom && CurrentTile is { Resolved: false } tile)
            {
                if (tile.Content == HallContent.Battle) Emit(CrawlEventType.Battle, tile: tile);
                else if (tile.Content == HallContent.Obstacle) Emit(CrawlEventType.Obstacle, tile: tile, contentId: tile.ContentId);
                else if (tile.Content == HallContent.Trap && tile.Scouted) Emit(CrawlEventType.Trap, tile: tile, contentId: tile.ContentId);
            }
            if (LastCurio == null && CurioHere is { } curio) Emit(CrawlEventType.Curio, roomId: State.InRoom ? State.RoomId : -1,
                tile: CurrentTile, contentId: curio);
        }
        return Flush();
    }

    private static CrawlEvent CopyEncounter(CrawlEvent e) => new()
    {
        Type = e.Type, RoomId = e.RoomId, CorridorId = e.CorridorId, TileIndex = e.TileIndex,
        ContentId = e.ContentId, HeroesSurprised = e.HeroesSurprised, MonstersSurprised = e.MonstersSurprised
    };

    /// <summary>From a room, step into the corridor leading to a neighbouring room.</summary>
    public List<CrawlEvent> Travel(int toRoomId)
    {
        _events.Clear();
        if (State.Ended || !State.InRoom || IsBlocked) return Blocked();
        var corridor = Map.FindCorridor(State.RoomId, toRoomId);
        if (corridor == null) return Blocked();

        State.CorridorId = corridor.Id;
        State.HeadingRoomId = toRoomId;
        State.TileIndex = corridor.RoomA == State.RoomId ? 0 : corridor.Tiles.Count - 1;
        State.RoomId = -1;
        EnterTile(forward: true);
        return Flush();
    }

    public bool CanEnterSecretRoom => !State.Ended && !State.InRoom && !IsBlocked
        && CurrentTile is { SecretRoomId: >= 0 } tile && tile.SecretRoomId < Map.Rooms.Count
        && Map.Room(tile.SecretRoomId).IsSecret
        && (tile.SecretDoorAlwaysAccessible || Map.Room(tile.SecretRoomId).Scouted || Map.Room(tile.SecretRoomId).Visited);

    public List<CrawlEvent> EnterSecretRoom()
    {
        _events.Clear();
        if (!CanEnterSecretRoom) return Blocked();
        var secret = Map.Room(CurrentTile.SecretRoomId);
        State.SecretReturnCorridorId = State.CorridorId;
        State.SecretReturnTileIndex = State.TileIndex;
        State.SecretReturnHeadingRoomId = State.HeadingRoomId;
        State.RoomId = secret.Id;
        State.CorridorId = State.TileIndex = State.HeadingRoomId = -1;
        secret.Scouted = true;
        EnterRoom(secret);
        return Flush();
    }

    public List<CrawlEvent> ExitSecretRoom()
    {
        _events.Clear();
        if (State.Ended || CurrentRoom?.IsSecret != true || IsBlocked
            || State.SecretReturnCorridorId < 0 || State.SecretReturnCorridorId >= Map.Corridors.Count) return Blocked();
        var corridor = Map.Corridor(State.SecretReturnCorridorId);
        if (State.SecretReturnTileIndex < 0 || State.SecretReturnTileIndex >= corridor.Tiles.Count
            || corridor.Tiles[State.SecretReturnTileIndex].SecretRoomId != State.RoomId
            || (State.SecretReturnHeadingRoomId != corridor.RoomA && State.SecretReturnHeadingRoomId != corridor.RoomB)) return Blocked();
        State.RoomId = -1;
        State.CorridorId = corridor.Id;
        State.TileIndex = State.SecretReturnTileIndex;
        State.HeadingRoomId = State.SecretReturnHeadingRoomId;
        State.SecretReturnCorridorId = State.SecretReturnTileIndex = State.SecretReturnHeadingRoomId = -1;
        // Returning to the same square replays no movement, stress, light, hunger or ambush effects.
        Emit(CrawlEventType.EnteredTile, tile: CurrentTile, contentId: CurrentTile.ContentId);
        return Flush();
    }

    /// <summary>Walk one square toward the room the party is heading for (forward) or back the way it came.</summary>
    public List<CrawlEvent> Step(bool forward)
    {
        _events.Clear();
        if (State.Ended || State.InRoom) return Blocked();
        var tile = CurrentTile;
        // An unresolved fight pins the party; an obstacle only blocks the way forward.
        if (!tile.Resolved && (tile.Content == HallContent.Battle || (forward && tile.Content == HallContent.Obstacle)))
            return Blocked();

        // DD1: passing an armed trap springs it even when spotted. Backing away leaves it alone.
        if (forward && !tile.Resolved && tile.Content == HallContent.Trap)
        {
            TriggerTrap(tile, scouted: false);
            return Flush();
        }

        var corridor = CurrentCorridor;
        int towardB = State.HeadingRoomId == corridor.RoomB ? 1 : -1;
        int dir = forward ? towardB : -towardB;
        int next = State.TileIndex + dir;

        if (next < 0 || next >= corridor.Tiles.Count)
        {
            int roomId = next < 0 ? corridor.RoomA : corridor.RoomB;
            State.CameFromCorridorId = corridor.Id;
            State.CameFromTileIndex = State.TileIndex;
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

    /// <summary>
    /// DD1's retreat from a fight: the battle stays where it was and the party falls back the way it came: from a
    /// room to the corridor square it entered from, from a hall square one square back. No events fire on the way.
    /// </summary>
    public List<CrawlEvent> FleeBattle()
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        if (State.InRoom)
        {
            var corridor = State.CameFromCorridorId >= 0 ? Map.Corridor(State.CameFromCorridorId) : null;
            if (corridor == null) return Flush();            // nowhere to fall back to (shouldn't happen)
            int fromRoom = State.RoomId;
            State.CorridorId = corridor.Id;
            State.TileIndex = State.CameFromTileIndex;
            State.HeadingRoomId = corridor.RoomA == fromRoom ? corridor.RoomB : corridor.RoomA;   // facing away
            State.RoomId = -1;
        }
        else if (CurrentCorridor is { } corridor)
        {
            int towardB = State.HeadingRoomId == corridor.RoomB ? 1 : -1;
            int back = State.TileIndex - towardB;
            if (back < 0 || back >= corridor.Tiles.Count)
            {
                State.RoomId = back < 0 ? corridor.RoomA : corridor.RoomB;
                State.CameFromCorridorId = corridor.Id;
                State.CameFromTileIndex = State.TileIndex;
                State.CorridorId = -1;
                State.TileIndex = -1;
                State.HeadingRoomId = -1;
            }
            else State.TileIndex = back;
        }
        State.PendingEncounter = null;
        State.FightCheckpoint = null;
        Emit(CrawlEventType.Retreated);
        return Flush();
    }

    /// <summary>Report a won fight at the party's current spot.</summary>
    /// <summary>The last battle or camping loot batch, including drops that still need pack space.</summary>
    public BattleSpoils LastSpoils { get => State.PendingSpoils; private set => State.PendingSpoils = value; }

    /// <summary>Close the current loot scroll; DD1 won't leave quest items, and a stale scroll can't clear a new one.</summary>
    public bool DismissSpoils(BattleSpoils report)
    {
        if (State.Ended || report == null || report != LastSpoils || !CanLeave(report.LeftBehind)) return false;
        State.PendingSpoils = null;
        return true;
    }

    public List<CrawlEvent> ResolveBattle()
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        State.PendingEncounter = null;
        State.FightCheckpoint = null;
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
                string key = hero + "|" + id;
                if (buff == null || (buff.Battles == 0 && !State.BuffBattlesLeft.ContainsKey(key))) { keep.Add(id); continue; }
                int left = (State.BuffBattlesLeft.TryGetValue(key, out var l) ? l : buff.Battles) - 1;
                if (left > 0) { State.BuffBattlesLeft[key] = left; keep.Add(id); }
                else State.BuffBattlesLeft.Remove(key);
            }
            State.PendingBuffs[hero] = keep;
        }
    }

    /// <summary>DD1 supplies used on a hero outside combat. Returns what happened, or null if it does nothing here
    /// (bleed, blight and horror don't outlast a DD2 fight, so bandages, antivenom and laudanum wait for curios).</summary>
    public string UseSupply(string heroId, string key)
    {
        if (State.Ended || heroId == null || !_party.Alive.Contains(heroId)) return null;
        if (key == Supply.Food)
        {
            // DD1: a provision eaten from the pack heals provision_hp_heal (5%) of max HP.
            if (_party.HpFraction(heroId) >= 1f) return null;
            if (!State.Pack.TryUse(Supply.Food)) return null;
            _party.Heal(heroId, _rules.ProvisionHeal);
            return $"Ate a provision: +{Math.Round(_rules.ProvisionHeal * 100)}% health.";
        }
        if (key == Supply.HolyWater)
        {
            if (!State.Pack.TryUse(Supply.HolyWater)) return null;
            // DD1 effect "holy_water": four resistance buffs for 3 battles.
            if (!State.PendingBuffs.TryGetValue(heroId, out var list)) State.PendingBuffs[heroId] = list = new List<string>();
            foreach (var id in new[] { "holy_water_blight_resist", "holy_water_bleed_resist", "holy_water_disease_resist", "holy_water_debuff_resist" })
            {
                if (!list.Contains(id)) list.Add(id);
                State.BuffBattlesLeft[heroId + "|" + id] = 3;
            }
            return "Blessed: resistances up for 3 battles.";
        }
        return null;
    }

    /// <summary>Where the party stands, as a key for the fight waiting there.</summary>
    private string FightSpot => State.InRoom ? "room:" + State.RoomId : $"hall:{State.CorridorId}:{State.TileIndex}";

    /// <summary>The DD1 monsters of the fight about to start here: a hand-made map's set fight (its named mash row), else
    /// DD1's encounter table for the zone and quest difficulty (hall, room, boss). After a retreat the same group is still
    /// waiting at that spot.</summary>
    public List<string> FightMonsters(string kind)
    {
        if (State.Ended) return new List<string>();
        if (State.FightMonsters is { Count: > 0 } waiting && State.FightAt == FightSpot) return waiting;
        string named = State.InRoom ? CurrentRoom?.MashName : CurrentTile?.MashName;
        int difficulty = State.Quest?.Difficulty ?? 1;
        State.FightMonsters = (named != null ? _content?.Battles?.NamedEncounter(State.Quest?.Dungeon, difficulty, named, NextRng()) : null)
                              ?? _content?.Battles?.RollEncounter(State.Quest?.Dungeon, difficulty, kind, NextRng()) ?? new List<string>();
        State.FightAt = FightSpot;
        return State.FightMonsters;
    }

    private BattleSpoils TakeSpoils(string kind)
    {
        var spoils = new BattleSpoils { Kind = kind };
        if (_content?.Battles == null || _content.Loot == null) return spoils;
        List<LootDrop> drops;
        if (State.FightMonsters is { Count: > 0 } fought && State.FightAt == FightSpot)
        {
            spoils.Dd1Monsters = new List<string>(fought);
            drops = _content.Battles.RollFor(_content.Loot, fought, State.Quest?.Dungeon, State.Quest?.Difficulty ?? 1, NextRng());
        }
        else drops = _content.Battles.Roll(_content.Loot, State.Quest?.Dungeon, State.Quest?.Difficulty ?? 1, kind, NextRng(), out spoils.Dd1Monsters);
        State.FightMonsters = null;
        State.FightAt = null;
        var pick = NextRng();
        drops = drops.Select(d => LootDrop.ResolveTrinket(d, TrinketOfRarity, pick)).ToList();
        foreach (var drop in drops)
        {
            if (State.Pack.TryTake(drop, _content.Items)) spoils.Taken.Add(drop);
            else spoils.LeftBehind.Add(drop);
        }
        return spoils;
    }

    /// <summary>Clear the obstacle ahead: with a shovel, or by force (damage and stress, as in DD1).</summary>
    public List<CrawlEvent> ClearObstacle()
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        var tile = CurrentTile;
        if (tile == null || tile.Content != HallContent.Obstacle || tile.Resolved) return Blocked();

        var obstacle = _content?.Obstacles?.Get(tile.ContentId);
        if (obstacle?.AncestorTalk != true && !State.Pack.TryUse(Supply.Shovel))
        {
            var rng = NextRng();
            ChangeLight(obstacle?.TorchChange ?? -20f);
            foreach (var hero in _party.Alive)
            {
                float health = obstacle?.HealthFraction ?? -0.05f;
                if (health < 0) _party.Damage(hero, -health, "obstacle");
                if (obstacle != null)
                    foreach (var effect in obstacle.FailEffects) _content.Curios.ApplyEffect(effect, hero, _party, rng);
                else StressDd1(hero, 15, rng, "obstacle");
            }
        }
        tile.Resolved = true;
        Emit(CrawlEventType.ObstacleCleared, tile: tile, contentId: tile.ContentId);
        return Flush();
    }

    /// <summary>A spotted trap: the chosen hero (DD1: whoever is selected) tries to disarm it, with the bonus for seeing
    /// it coming. Failing springs it on them.</summary>
    public List<CrawlEvent> DisarmTrap(string heroId = null)
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        var tile = CurrentTile;
        if (tile == null || tile.Content != HallContent.Trap || tile.Resolved) return Blocked();
        TriggerTrap(tile, scouted: true, heroId);
        return Flush();
    }

    /// <summary>Picks a trinket of a DD1 rarity (rarity, rng → trinket id) so loot holds real trinkets; the plugin
    /// sets this (DD2's trinkets). Without it, trinket drops stay as rarities and are picked at homecoming.</summary>
    public Func<string, Rng, string> TrinketOfRarity;

    /// <summary>A hero's DD1 class (for its trap disarm chance); the plugin sets this.</summary>
    public Func<string, string> HeroDd1Class;

    /// <summary>DD1's chance: the class's trap stat, +40% for a spotted trap, minus the difficulty's penalty.</summary>
    public float TrapDisarmChance(string heroId, bool scouted)
    {
        int difficulty = Math.Min(State.Quest?.Difficulty ?? 1, _rules.TrapDifficultyPenalty.Length - 1);
        float chance = (_content?.Traps?.DisarmBase(HeroDd1Class?.Invoke(heroId)) ?? 0.4f)
                       + (scouted ? _rules.TrapScoutDisarmBonus : 0f) - _rules.TrapDifficultyPenalty[difficulty];
        return Math.Max(0f, Math.Min(0.95f, chance));
    }

    public List<CrawlEvent> UseTorch()
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        if (State.Light < 100 && State.Pack.TryUse(Supply.Torch)) ChangeLight(_rules.TorchLight);
        return Flush();
    }

    /// <summary>Snuff the torch (DD1 lets you trade safety for loot and surprise).</summary>
    public List<CrawlEvent> SnuffTorch(float amount = 25f)
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        ChangeLight(-amount);
        return Flush();
    }

    // ---- camping ----

    /// <summary>DD1 camps need firewood and a safe (cleared) room.</summary>
    public bool CanCamp => !State.Ended && State.InRoom && !IsBlocked && State.Camp == null && State.Pack.Count(Supply.Firewood) > 0;

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
        if (State.Ended || State.Camp == null || State.Camp.Ate) return false;
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
        if (State.Ended) return "Expedition ended.";
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
        var loot = new List<LootDrop>();
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
            foreach (var t in targets) ApplyCampEffect(effect, t, rng, loot);
        }
        if (loot.Count > 0)
        {
            LastSpoils = new BattleSpoils { Kind = "camp" };
            foreach (var drop in loot)
                if (State.Pack.TryTake(drop, _content.Items)) LastSpoils.Taken.Add(drop);
                else LastSpoils.LeftBehind.Add(drop);
        }
        return true;
    }

    private void ApplyCampEffect(CampEffect effect, string hero, Rng rng, List<LootDrop> loot)
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
                    {
                        var drop = LootDrop.ResolveTrinket(d, TrinketOfRarity, rng);
                        loot.Add(drop);
                    }
                break;
            // remove_bleeding / remove_poison / remove_deaths_door_recovery_buffs: DoTs already landed out of
            // combat, and DD2 owns death's door recovery, so there's nothing left to undo.
        }
    }

    /// <summary>Strike camp: the torch is relit, and DD1 rolls for a night ambush.</summary>
    public List<CrawlEvent> BreakCamp()
    {
        _events.Clear();
        if (State.Ended || State.Camp == null) return Blocked();
        var rng = NextRng();
        float ambush = Math.Max(0f, _rules.AmbushCampChance - State.Camp.AmbushReduction);
        if (!State.Camp.Ate) foreach (var hero in _party.Alive) StressDd1(hero, _rules.Meals[Meal.None].StressDd1, rng, "no meal");
        State.Camp = null;
        ChangeLight(_rules.CampRestoreTorch - State.Light);
        if (rng.Chance(ambush))
        {
            // DD1: the night ambush snuffs the torch (ambush_torch_reduction -100); the fight starts in the dark.
            ChangeLight(_rules.AmbushTorchChange);
            State.PendingEncounter = new CrawlEvent { Type = CrawlEventType.Ambush, RoomId = State.RoomId, HeroesSurprised = true, ContentId = "camp" };
            _events.Add(CopyEncounter(State.PendingEncounter));
        }
        return Flush();
    }

    public void Retreat()
    {
        if (!CanLeaveExpedition || State.Quest?.CanRetreat == false) return;
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
        if (tile.SecretRoomId >= 0 && tile.SecretDoorAlwaysAccessible)
            Map.Room(tile.SecretRoomId).Scouted = true;
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

    private void EnterRoom(Room room, bool enteringDungeon = false)
    {
        bool firstVisit = !room.Visited;
        room.Visited = true;
        Emit(CrawlEventType.EnteredRoom, roomId: room.Id);

        if (firstVisit && !room.IsSecret) Scout(room, enteringDungeon);

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

    private void TriggerTrap(HallTile tile, bool scouted, string heroId = null)
    {
        var rng = NextRng();
        tile.Resolved = true;
        var trap = _content?.Traps?.Get(tile.ContentId, State.Quest?.Difficulty ?? 1);
        // DD1: walking into an unseen trap springs it; only a deliberate disarm gets a roll.
        var hero = heroId != null && _party.Alive.Contains(heroId) ? heroId : _party.Alive.FirstOrDefault();
        if (scouted && rng.Chance(TrapDisarmChance(hero, scouted)))
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

    /// <summary>DD1 scouting reveals six hallway squares, or twelve on a critical success, along each branch.</summary>
    private void Scout(Room room, bool enteringDungeon = false)
    {
        if (State.Quest?.ScoutingEnabled == false) return;   // DD1: no scouting in the Darkest Dungeon
        var rng = NextRng();
        float chance = enteringDungeon ? _rules.ScoutEntryChance
            : _rules.ScoutChanceBase + _rules.Band(State.Light).ScoutingIncrease / 100f;
        if (chance <= 1f && !rng.Chance(chance)) return;
        bool critical = rng.Chance(_rules.ScoutCriticalChance * (chance > 1f ? chance : 1f));
        int revealed = Map.ScoutFrom(room.Id, critical ? 12 : 6, revealSecrets: critical);
        if (revealed > 0) Emit(CrawlEventType.Scouted, roomId: room.Id, amount: revealed);
    }

    /// <summary>
    /// DD1's surprise weights before normalization: room/corridor base, torch band and active hero buffs.
    /// A lone hero loses the party base before modifiers. Forced ambushes bypass weights and surprise only heroes.
    /// </summary>
    public (float Heroes, float Monsters) SurpriseChances(bool corridor, bool known, bool ambush)
    {
        if (State.Quest?.SurpriseEnabled == false && !ambush) return (0f, 0f);   // DD1: no surprise in the Darkest Dungeon
        if (ambush) return (1f, 0f);
        var band = _rules.Band(State.Light);
        float heroes = known ? (corridor ? _rules.SurpriseKnownCorridorParty : _rules.SurpriseKnownRoomParty)
            : corridor ? _rules.SurpriseCorridorParty : _rules.SurpriseRoomParty;
        float monsters = known ? (corridor ? _rules.SurpriseKnownCorridorMonsters : _rules.SurpriseKnownRoomMonsters)
            : corridor ? _rules.SurpriseCorridorMonsters : _rules.SurpriseRoomMonsters;
        if (_party.Alive.Count == 1) heroes = 0f;
        heroes += band.HeroesSurprisedIncrease / 100f;
        monsters += band.MonstersSurprisedIncrease / 100f;
        foreach (var (_, buff) in FightBuffs())
        {
            if (buff.Stat == "party_surprise_chance") heroes += buff.Amount;
            else if (buff.Stat == "monsters_surprise_chance") monsters += buff.Amount;
        }
        return (Math.Max(0f, Math.Min(heroes, _rules.SurpriseMaxParty)), Math.Max(0f, Math.Min(monsters, _rules.SurpriseMaxMonsters)));
    }

    private void EmitBattle(CrawlEventType type, HallTile tile, bool corridor, Rng rng, int roomId = -1)
    {
        bool known = tile != null ? tile.Scouted : roomId >= 0 && Map.Room(roomId) is { Scouted: true };
        // A roaming corridor fight is native ac_battle, not a forced camp ambush.
        // BreakCamp supplies the forced camp encounter separately.
        var (heroes, monsters) = SurpriseChances(corridor, known, ambush: false);
        // Native 1405fe3d0 inserts none first, then party, then monsters and draws once over their total.
        float none = Math.Max(0.25f, Math.Min(1f, 1f - (heroes + monsters)));
        double draw = rng.NextDouble() * (none + heroes + monsters);
        bool heroesSurprised = draw >= none && draw < none + heroes;
        bool monstersSurprised = draw >= none + heroes;

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
        State.PendingEncounter = CopyEncounter(e);
        _events.Add(e);
    }

    /// <summary>Mark the quest done once its goal is met (called as the party moves and fights).</summary>
    public void CheckQuest()
    {
        if (State.Ended || State.QuestComplete || State.Quest == null) return;
        var goal = State.Goal;
        float explorePct = goal?.Type == "explore_room" && goal.Percentage > 0 ? goal.Percentage : 0.9f;
        bool done = goal?.Type == "tutorial_room"
            // DD1's opening raid: reach the last room and win its fight.
            ? Map.Rooms.Any(r => r.IsQuestGoal && r.Visited && (!r.HasBattle || r.Cleared))
            : State.Quest.Type switch
        {
            "explore" => Map.QuestRooms.Count(r => r.Visited) >= Math.Ceiling(Map.QuestRooms.Count() * explorePct),
            "cleanse" => Map.Rooms.Where(r => r.HasBattle).All(r => r.Cleared),
            "kill_boss" => Map.BossRoomId >= 0 && Map.Room(Map.BossRoomId).Cleared,
            "gather" or "activate" or "inventory_activate" when goal != null && goal.Amount > 0 => State.GoalProgress >= goal.Amount,
            _ => Map.QuestRooms.Count(r => r.Visited) >= Math.Ceiling(Map.QuestRooms.Count() * 0.9),
        };
        if (!done) return;
        State.QuestComplete = true;
        Emit(CrawlEventType.QuestComplete);
    }

    /// <summary>A DD1 monster skill darkened the torch (DD1's "Darkness" effects, .torch_decrease) during a fight.</summary>
    public List<CrawlEvent> Darken(float amount)
    {
        _events.Clear();
        if (State.Ended) return Blocked();
        if (amount > 0) ChangeLight(-amount);
        return Flush();
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
