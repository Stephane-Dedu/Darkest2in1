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
    private bool _provisioning;
    // DD1 asks each low-supply question in turn. _armedWarning is the question the next Embark press answers;
    // _confirmedWarnings holds the ones already answered, so changing the cart can raise a new one.
    private string _armedWarning;
    private readonly HashSet<string> _confirmedWarnings = new();
    internal bool Preview { get; set; }

    /// <summary>Embark was pressed with these warnings showing (most important first). True when every one has been
    /// answered and the party may leave; otherwise the next unanswered warning is armed and shown with its hint.</summary>
    internal static bool ConfirmWarnings(IReadOnlyList<string> warnings, HashSet<string> confirmed, ref string armed)
    {
        foreach (string warning in warnings)
        {
            if (confirmed.Contains(warning)) continue;
            if (armed != warning) { armed = warning; return false; }   // first press: ask
            confirmed.Add(warning);                                    // second press: answered, check the next one
        }
        armed = null;
        return true;
    }

    /// <summary>The warning to show: the first one not answered yet.</summary>
    internal static string PendingWarning(IReadOnlyList<string> warnings, HashSet<string> confirmed)
    {
        foreach (string warning in warnings) if (!confirmed.Contains(warning)) return warning;
        return null;
    }

    private void ResetWarnings() { _armedWarning = null; _confirmedWarnings.Clear(); }

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
        ResetWarnings();
        return true;
    }
}
