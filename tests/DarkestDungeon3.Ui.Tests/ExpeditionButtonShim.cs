using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class EmbarkUi
{
    // Only native button art is substituted; production cancellation owns cart and selection state.
    private bool ProvisionBackButton() => Gui.DdButton(new Rect(30, 1000, 300, 60), "Back to quests", size: 26);
}
