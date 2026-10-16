using System.Collections.Generic;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>Owns the DD1 pack side of temporary native DD2 combat-item equipment. Selecting a proxy never
/// moves goods out of the pack; only DD2's confirmed consumption of its one charge commits a use.</summary>
public sealed class CombatSupplyLedger
{
    public static readonly string[] Supplies = { Supply.Bandage, Supply.Antivenom, Supply.Herbs, Supply.HolyWater, Supply.Laudanum };

    public static string NativeId(string supply) => supply switch
    {
        Supply.Bandage => "bandages",
        Supply.Antivenom => "antivenom",
        Supply.Herbs => "medicinal_herbs",
        Supply.HolyWater => "holy_water",
        Supply.Laudanum => "laudanum",
        _ => null,
    };

    public sealed class Selection
    {
        public uint Actor;
        public string SupplyId, NativeId;
        public int Round, Turn;
    }

    private readonly Inventory _pack;
    private readonly Dictionary<uint, Selection> _selected = new();
    private readonly HashSet<(uint Actor, int Round, int Turn)> _used = new();

    public CombatSupplyLedger(Inventory pack) => _pack = pack;

    public bool UsedThisTurn(uint actor, int round, int turn) => _used.Contains((actor, round, turn));

    public Selection Select(uint actor, string supply, int round, int turn)
    {
        string native = NativeId(supply);
        if (actor == 0 || native == null || _pack == null || _pack.Count(supply) <= 0 || UsedThisTurn(actor, round, turn)) return null;
        return _selected[actor] = new Selection { Actor = actor, SupplyId = supply, NativeId = native, Round = round, Turn = turn };
    }

    public Selection Selected(uint actor) => _selected.TryGetValue(actor, out var selection) ? selection : null;

    /// <summary>Native equipment can survive cancellation across turns. Its retained charge is only usable
    /// while the shared pack still owns that item and this hero has not used a supply on the current turn.</summary>
    public bool CanUseSelection(uint actor, string nativeId, int round, int turn)
    {
        var selection = Selected(actor);
        return selection != null && selection.NativeId == nativeId && _pack.Count(selection.SupplyId) > 0 && !UsedThisTurn(actor, round, turn);
    }

    public void Cancel(uint actor) => _selected.Remove(actor);

    /// <summary>The bridge calls this after DD2 removes a native charge. Rejected targets, deselection and
    /// repeated callbacks cannot consume a supply. One medical item is allowed per hero turn, even when its
    /// native slot is changed to another item during that turn.</summary>
    public bool ConfirmUse(uint actor, string nativeId, int before, int after)
    {
        var selection = Selected(actor);
        if (selection == null || selection.NativeId != nativeId || before != 1 || after != 0
            || UsedThisTurn(actor, selection.Round, selection.Turn) || !_pack.TryUse(selection.SupplyId)) return false;
        _used.Add((actor, selection.Round, selection.Turn));
        _selected.Remove(actor);
        return true;
    }
}
