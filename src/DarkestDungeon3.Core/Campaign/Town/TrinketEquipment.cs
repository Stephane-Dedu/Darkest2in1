using System.Collections.Generic;
using System.Linq;

namespace DarkestDungeon3.Core.Campaign.Town;

/// <summary>DD1 two-slot transfers: validate both sides, then swap atomically. No item can be created by a stale drag.</summary>
public static class TrinketEquipment
{
    public static string Refusal(Estate estate, IHeroCatalog catalog, string id, string fromHero, int fromSlot, HeroRecord target, int slot)
    {
        if (string.IsNullOrEmpty(id) || target == null || slot < 0 || slot > 1 || !estate.Roster.Contains(target) || !target.IsAvailable)
            return "This hero cannot change trinkets now.";
        if (!catalog.TrinketFits(id, target.ClassId)) return "This trinket is restricted to another hero class.";
        int sourceSlot;
        HeroRecord source = null;
        if (fromHero == null)
        {
            sourceSlot = fromSlot >= 0 ? fromSlot : estate.Trinkets.IndexOf(id);
            if (sourceSlot < 0 || sourceSlot >= estate.Trinkets.Count || estate.Trinkets[sourceSlot] != id) return "The trinket is no longer there.";
        }
        else
        {
            source = estate.Hero(fromHero);
            sourceSlot = fromSlot >= 0 ? fromSlot : source?.Trinkets.IndexOf(id) ?? -1;
            if (source == null || !source.IsAvailable || source.TrinketAt(sourceSlot) != id) return "The trinket is no longer available.";
        }
        string displaced = target.TrinketAt(slot);
        if (!FitsCount(catalog, target, id, slot, source == target ? sourceSlot : -1)) return "This hero already wears that trinket.";
        if (source != null && source != target && displaced != null &&
            (!catalog.TrinketFits(displaced, source.ClassId) || !FitsCount(catalog, source, displaced, sourceSlot, -1)))
            return "The other hero cannot wear the trinket being swapped.";
        return null;
    }

    private static bool FitsCount(IHeroCatalog catalog, HeroRecord hero, string id, int replaced, int moved)
    {
        int limit = catalog.TrinketEquipLimit(id), count = 1;
        for (int i = 0; i < hero.Trinkets.Count; i++) if (i != replaced && i != moved && hero.Trinkets[i] == id) count++;
        return limit <= 0 || count <= limit;
    }

    public static bool Transfer(Estate estate, IHeroCatalog catalog, string id, string fromHero, int fromSlot, HeroRecord target, int slot)
    {
        if (Refusal(estate, catalog, id, fromHero, fromSlot, target, slot) != null) return false;
        string displaced = target.TrinketAt(slot);
        if (fromHero == null)
        {
            int at = fromSlot >= 0 ? fromSlot : estate.Trinkets.IndexOf(id);
            if (displaced == null) estate.Trinkets.RemoveAt(at);
            else estate.Trinkets[at] = displaced;
        }
        else
        {
            var source = estate.Hero(fromHero);
            int at = fromSlot >= 0 ? fromSlot : source.Trinkets.IndexOf(id);
            if (source == target && at == slot) return false;
            source.SetTrinket(at, displaced);
        }
        target.SetTrinket(slot, id);
        return true;
    }

    public static bool Unequip(Estate estate, HeroRecord hero, int slot)
    {
        if (hero == null || !estate.Roster.Contains(hero) || !hero.IsAvailable || hero.TrinketAt(slot) is not { } id) return false;
        hero.SetTrinket(slot, null);
        estate.Trinkets.Add(id);
        return true;
    }

    /// <summary>DD2 returns a compact item list. Keep surviving items in their previous slots, fill vacancies with
    /// new items, and retain the exact returned counts, including identical unlimited items.</summary>
    public static List<string> RestoreSlots(IReadOnlyList<string> previous, IEnumerable<string> returned)
    {
        var remaining = returned.Where(id => !string.IsNullOrEmpty(id)).ToList();
        var slots = new List<string>();
        foreach (string old in previous)
        {
            int at = old == null ? -1 : remaining.IndexOf(old);
            slots.Add(at < 0 ? null : old);
            if (at >= 0) remaining.RemoveAt(at);
        }
        foreach (string item in remaining)
        {
            int empty = slots.IndexOf(null);
            if (empty >= 0) slots[empty] = item;
            else slots.Add(item);
        }
        while (slots.Count > 0 && slots[slots.Count - 1] == null) slots.RemoveAt(slots.Count - 1);
        return slots;
    }
}
