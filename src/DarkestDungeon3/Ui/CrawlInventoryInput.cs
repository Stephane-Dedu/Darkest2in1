using System;
using DarkestDungeon3.Core.Expedition;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal static class CrawlInventoryInput
{
    public static string CurioPanelKey(Crawl crawl)
    {
        string curio = crawl.CurioHere;
        var exp = crawl.State;
        return curio == null ? null : (exp.InRoom ? "r" + exp.RoomId : $"c{exp.CorridorId}:{exp.TileIndex}") + ":" + curio;
    }

    public static bool RightClick(Rect rect, Crawl crawl, string openPanel, Action<string> onCurio, Action onHero)
    {
        var e = Event.current;
        if (!GUI.enabled || Drag.Active || Drag.JustDropped || !crawl.CanNavigate || crawl.IsBlocked
            || e.type != EventType.MouseDown || e.button != 1 || !rect.Contains(e.mousePosition)) return false;
        e.Use();
        string here = CurioPanelKey(crawl);
        if (here != null && openPanel == here) onCurio(crawl.CurioHere);
        else onHero();
        return true;
    }
}
