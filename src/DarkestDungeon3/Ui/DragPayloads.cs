using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

// What can be carried with Drag. Value equality: screens create a fresh payload each frame for the same thing.

/// <summary>An item on the Provisioner's shelf.</summary>
internal sealed class ShelfItem
{
    public readonly string Id;
    public ShelfItem(string id) => Id = id;
    public override bool Equals(object o) => o is ShelfItem s && s.Id == Id;
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}

/// <summary>A stack in an arranged inventory (the pack being bought, or the expedition's pack).</summary>
internal sealed class PackStack
{
    public readonly Inventory Pack;
    public readonly int Slot;
    public readonly string Key;
    public PackStack(Inventory pack, int slot, string key) { Pack = pack; Slot = slot; Key = key; }
    public override bool Equals(object o) => o is PackStack p && ReferenceEquals(p.Pack, Pack) && p.Slot == Slot;
    public override int GetHashCode() => Slot;
}

/// <summary>A hero picked up from the roster (<see cref="FromSlot"/> &lt; 0) or from a slot (party rank, activity).</summary>
internal sealed class HeroDrag
{
    public readonly string HeroId;
    public readonly int FromSlot;
    public HeroDrag(string heroId, int fromSlot = -1) { HeroId = heroId; FromSlot = fromSlot; }
    public override bool Equals(object o) => o is HeroDrag h && h.HeroId == HeroId && h.FromSlot == FromSlot;
    public override int GetHashCode() => HeroId?.GetHashCode() ?? 0;
}

/// <summary>A trinket from the estate's stash (<see cref="FromHero"/> null) or worn by a hero.</summary>
internal sealed class TrinketDrag
{
    public readonly string TrinketId;
    public readonly string FromHero;
    public TrinketDrag(string trinketId, string fromHero = null) { TrinketId = trinketId; FromHero = fromHero; }
    public override bool Equals(object o) => o is TrinketDrag t && t.TrinketId == TrinketId && t.FromHero == FromHero;
    public override int GetHashCode() => TrinketId?.GetHashCode() ?? 0;
}

internal static class ItemArt
{
    /// <summary>A DD1 inventory stack: the item's art (fuller pictures for fuller stacks) and its count.</summary>
    public static void Stack(Rect r, string key, int count, int stackLimit, bool dim = false)
    {
        var old = GUI.color;
        if (dim) GUI.color = new Color(0.4f, 0.4f, 0.4f, old.a);
        var icon = Art.InventoryIcon(key, Mathf.Max(1, count), Mathf.Max(1, stackLimit));
        if (icon != null) GUI.DrawTexture(r, icon); else Gui.Text(r, HamletUi.Pretty(key), 16, Gui.Dd1Text, TextAnchor.MiddleCenter);
        GUI.color = old;
        if (stackLimit > 1 || count > 1)
            Gui.Text(new Rect(r.x, r.yMax - 28, r.width - 4, 26), count.ToString(), 21, Color.white, TextAnchor.LowerRight);
    }
}

/// <summary>A stagecoach recruit being dragged to the roster (DD1's way of hiring).</summary>
internal sealed class RecruitDrag
{
    public readonly string HeroId;
    public RecruitDrag(string heroId) { HeroId = heroId; }
    public override bool Equals(object o) => o is RecruitDrag r && r.HeroId == HeroId;
    public override int GetHashCode() => HeroId?.GetHashCode() ?? 0;
}
