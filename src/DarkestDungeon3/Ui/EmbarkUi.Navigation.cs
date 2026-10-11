using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class EmbarkUi
{
    private QuestOffer _quest;
    private readonly List<string> _party = new();      // front rank first
    private readonly Inventory _cart = new();
    private string _error;
    private bool _confirmLow, _provisioning;
    internal bool Preview { get; set; }

    // The visible lineup runs back-to-front, while the campaign stores front-to-back.
    private string PartyHeroAtCell(int cell)
    {
        int rank = 3 - cell;
        return cell >= 0 && cell < 4 && rank < _party.Count ? _party[rank] : null;
    }

    private bool DrawProvisioningBack()
    {
        if (!ProvisionBackButton()) return false;
        // Purchases are only charged at embark, so cancelling needs no estate refund.
        _cart.Items.Clear();
        _cart.Layout.Clear();
        _error = null;
        _provisioning = false;
        _confirmLow = false;
        return true;
    }
}
