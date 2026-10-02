using System.Collections.Generic;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>A quest on the Hamlet's quest board.</summary>
public sealed class QuestOffer
{
    public string Id;
    public string Dungeon;          // zone id: crypts, weald, warrens, cove (or a toggled-in DD2 zone)
    public string Type;             // explore, cleanse, gather, activate, inventory_activate, kill_boss
    public int Length;              // 1 short, 2 medium, 3 long
    public int Difficulty;          // 1 apprentice, 3 veteran, 5 champion (DD1 values)
    public int MapSeed;
    public bool IsPlot;             // boss / Darkest Dungeon quests
    public string PlotId;
    public string BossId;           // for kill_boss
    public List<Reward> Rewards = new();

    public string Size => Length switch { 1 => "short", 2 => "medium", _ => "long" };

    public string DifficultyName => Difficulty switch { 1 => "Apprentice", 3 => "Veteran", 5 => "Champion", _ => "Level " + Difficulty };

    public override string ToString() => $"{DifficultyName} {Size} {Type} in {Dungeon}";
}

public sealed class Reward
{
    public string Type;             // gold, bust, portrait, deed, crest, trinket
    public string Id;               // trinket rarity or id
    public int Amount;

    public Reward() { }
    public Reward(string type, int amount, string id = null) { Type = type; Amount = amount; Id = id; }

    public override string ToString() => Id == null ? $"{Amount} {Type}" : $"{Amount} {Type} ({Id})";
}
