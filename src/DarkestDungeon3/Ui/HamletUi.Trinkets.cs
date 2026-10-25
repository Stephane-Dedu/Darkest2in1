using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class HamletUi
{
    private readonly TrinketSelection _wagonSelection = new();
    private Vector2 _wagonEffectScroll;
    private string _wagonDescriptionKey;
    private Estate _wagonEstate;
    private int _wagonPage;

    private void DrawWagon(Rect area, Hamlet hamlet)
    {
        area = new Rect(Window.x + 244, Window.y + 92, Window.width - 278, Window.height - 116);
        ExpeditionSkin.Sprite(new Rect(Window.x + 12, Window.y + 170, 218, 480), "npc_hoarder", fit: true);
        Gui.Text(new Rect(Window.x + 24, Window.y + 104, 190, 56), "Nomad Wagon", 30,
            CampaignPanelSkin.Brass, TextAnchor.MiddleCenter, heading: true);
        if (_wagonEstate != E)
        {
            _wagonEstate = E;
            _wagonSelection.Clear();
            _wagonDescriptionKey = null;
            _wagonPage = 0;
        }
        _wagonSelection.Reconcile(E.WagonStock);
        var layout = new TrinketShopLayout(area);
        int pages = Mathf.Max(1, (E.WagonStock.Count + TrinketShopLayout.Capacity - 1) / TrinketShopLayout.Capacity);
        _wagonPage = Mathf.Clamp(_wagonPage, 0, pages - 1);
        Gui.Text(layout.Header, "This week's finds", 30, CampaignPanelSkin.Ivory, heading: true);
        Gui.Text(new Rect(area.xMax - 220, area.y + 4, 220, 32), Gui.Num(E.Get(Currency.Gold), "#,0") + " gold", 24,
            CampaignPanelSkin.Brass, TextAnchor.MiddleRight);
        ExpeditionSkin.Rule(new Rect(area.x, area.y + 44, area.width, 6));
        for (int i = 0; i < TrinketShopLayout.Capacity; i++)
        {
            var cell = layout.Cell(i);
            int index = _wagonPage * TrinketShopLayout.Capacity + i;
            bool stocked = index < E.WagonStock.Count;
            string id = stocked ? E.WagonStock[index] : null;
            bool selected = stocked && id == _wagonSelection.Id;
            bool hover = stocked && cell.Contains(Event.current.mousePosition) && GUI.enabled;
            ExpeditionSkin.StoreSlot(cell, selected, hover);
            if (!stocked) continue;
            var icon = new Rect(cell.x + 7, cell.y + 6, cell.width - 14, cell.height - 12);
            HeroSheet.TrinketIcon(icon, id);
            int price = hamlet.WagonPrice(id);
            Gui.Text(layout.Price(i), Gui.Num(price, "#,0"), 22,
                E.Get(Currency.Gold) >= price ? CampaignPanelSkin.Brass : CampaignPanelSkin.Red, TextAnchor.MiddleCenter);
            if (Gui.Hotspot(cell)) _wagonSelection.Select(id, E.WagonStock);
        }
        if (pages > 1)
        {
            var footer = new Rect(layout.Stock.x, area.yMax - 54, layout.Stock.width, 44);
            if (Gui.DdButton(new Rect(footer.x, footer.y, 120, 42), "Previous", _wagonPage > 0, 22)) _wagonPage--;
            Gui.Text(footer, $"{_wagonPage + 1} / {pages}", 22, CampaignPanelSkin.Muted, TextAnchor.MiddleCenter);
            if (Gui.DdButton(new Rect(footer.xMax - 120, footer.y, 120, 42), "Next", _wagonPage < pages - 1, 22)) _wagonPage++;
        }
        else Gui.Text(new Rect(layout.Stock.x, area.yMax - 54, layout.Stock.width, 44),
            E.WagonStock.Count == 0 ? "New stock arrives next week." : "Select a trinket to inspect its effects.",
            22, CampaignPanelSkin.Muted, TextAnchor.MiddleCenter);
        ExpeditionSkin.TooltipFrame(layout.Details);
        string selectedId = _wagonSelection.Id;
        if (selectedId == null)
        {
            Gui.Text(new Rect(layout.Details.x + 24, layout.Details.y + 140, layout.Details.width - 48, 140),
                "Sold out\nThe wagon will return with new finds next week.", 26, CampaignPanelSkin.Muted, TextAnchor.MiddleCenter);
            return;
        }
        var details = HeroSheet.DescribeTrinket(selectedId);
        var frame = layout.Details;
        HeroSheet.TrinketIcon(new Rect(frame.x + 22, frame.y + 22, 76, 92), selectedId);
        Gui.Text(new Rect(frame.x + 116, frame.y + 20, frame.width - 136, 92), details.Name, 29,
            CampaignPanelSkin.Ivory, TextAnchor.MiddleLeft, heading: true);
        Gui.Text(new Rect(frame.x + 22, frame.y + 120, frame.width - 44, 30), details.Rarity, 24, details.RarityColour);
        ExpeditionSkin.Rule(new Rect(frame.x + 20, frame.y + 151, frame.width - 40, 5));
        string key = selectedId + "\n" + details.Requirement + "\n" + details.Effects;
        if (_wagonDescriptionKey != key) { _wagonDescriptionKey = key; _wagonEffectScroll = Vector2.zero; }
        Gui.EffectDescription(layout.Description, details.Requirement, details.Effects, ref _wagonEffectScroll);
        int cost = hamlet.WagonPrice(selectedId), gold = E.Get(Currency.Gold);
        Gui.Text(layout.Status, gold >= cost ? Gui.Num(cost, "#,0") + " gold" : "Need " + Gui.Num(cost - gold, "#,0") + " more gold",
            24, gold >= cost ? CampaignPanelSkin.Brass : CampaignPanelSkin.Red, TextAnchor.MiddleCenter);
        if (Gui.DdButton(layout.Purchase, "Purchase", gold >= cost, 28) && hamlet.BuyTrinket(selectedId))
        {
            S.Persist();
            Gui.Announce(details.Name + " added to the Trinket Inventory.");
            _wagonSelection.Reconcile(E.WagonStock);
        }
    }
}
