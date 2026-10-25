using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Fixed stock and reading areas; a selected item survives other stock changes.</summary>
internal readonly struct TrinketShopLayout
{
    internal const int Columns = 6, Capacity = 18;
    internal readonly Rect Header, Stock, Details, Description, Purchase, Status;
    internal TrinketShopLayout(Rect area)
    {
        Header = new Rect(area.x, area.y, area.width, 46);
        float stockWidth = area.width - 390;
        Stock = new Rect(area.x, area.y + 68, stockWidth, area.height - 132);
        Details = new Rect(area.x + stockWidth + 18, area.y + 58, area.width - stockWidth - 18, area.height - 58);
        Description = new Rect(Details.x + 20, Details.y + 158, Details.width - 40, Details.height - 274);
        Purchase = new Rect(Details.x + 20, Details.yMax - 66, Details.width - 40, 46);
        Status = new Rect(Details.x + 20, Details.yMax - 108, Details.width - 40, 32);
    }

    internal Rect Cell(int index)
    {
        float pitch = Stock.width / Columns, size = Math.Min(108, pitch - 8);
        return new Rect(Stock.x + index % Columns * pitch + (pitch - size) / 2,
            Stock.y + index / Columns * 150, size, size);
    }

    internal Rect Price(int index)
    {
        var cell = Cell(index);
        return new Rect(cell.x - 3, cell.yMax + 8, cell.width + 6, 30);
    }
}

internal sealed class TrinketSelection
{
    internal string Id { get; private set; }
    internal void Clear() => Id = null;
    internal void Reconcile(IList<string> stock)
    {
        if (Id == null || !stock.Contains(Id)) Id = stock.Count > 0 ? stock[0] : null;
    }
    internal void Select(string id, IList<string> stock)
    {
        if (stock.Contains(id)) Id = id;
    }
}
