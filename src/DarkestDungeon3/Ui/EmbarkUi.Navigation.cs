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

    private bool DrawProvisioningBack()
    {
        if (!Gui.DdButton(new Rect(30, 1000, 300, 60), "Back to the quests", size: 24)) return false;
        // Purchases are only charged at embark, so cancelling needs no estate refund.
        _cart.Items.Clear();
        _cart.Layout.Clear();
        _error = null;
        _provisioning = false;
        _confirmLow = false;
        return true;
    }
}
