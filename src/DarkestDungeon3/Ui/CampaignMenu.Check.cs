using System;
using System.Linq;
using TMPro;
using UnityEngine.EventSystems;

namespace DarkestDungeon3.Ui;

internal sealed partial class CampaignMenu
{
    private bool _inputChecked;
    private void CheckMenuInput()
    {
        if (_inputChecked || !ReadOnlyCheck || _view != View.Estates
            || !Environment.GetCommandLineArgs().Contains("-dd3-menu-input-check")) return;
        _inputChecked = true;
        bool interactable = _entry.interactable;
        try
        {
            // Opening the estate page disables every native menu entry. Exercise its normal enabled state.
            _entry.interactable = true;
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 };
            var label = _entry.GetComponentsInChildren<TMP_Text>(true).First();
            ExecuteEvents.Execute(_entry.gameObject, pointer, ExecuteEvents.deselectHandler);
            var normal = label.color;
            ExecuteEvents.Execute(_entry.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Require(label.color != normal, "Hamlet hover did not change its label.");
            ExecuteEvents.Execute(_entry.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Require(label.color == normal, "Hamlet hover did not reset.");
            ExecuteEvents.Execute(_entry.gameObject, pointer, ExecuteEvents.selectHandler);
            Require(label.color != normal, "Controller selection did not highlight Hamlet.");
            ExecuteEvents.Execute(_entry.gameObject, pointer, ExecuteEvents.deselectHandler);
            var row = _estates[1];
            ExecuteEvents.Execute(row.Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(_selected == row && _view == View.Estates, "Single click did not just select the estate.");
            pointer.clickCount = 2; pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(row.Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(_view == View.Estates, "Right double-click entered an estate.");
            pointer.button = PointerEventData.InputButton.Left;
            row.Error = new InvalidOperationException("synthetic unreadable summary");
            ExecuteEvents.Execute(row.Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(_view == View.Estates, "Unreadable estate bypassed the loading guard.");
            row.Error = null;
            ExecuteEvents.Execute(row.Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(_view == View.Loading && _pendingSlot == row.Slot && _newName == null,
                "Left double-click did not use the selected estate's normal loading gate.");
            Plugin.Log.LogInfo("[menu input check] PASS: hover/reset/controller focus; single/right/invalid clicks; double-click selected synthetic estate through torch gate; no estate files accessed");
        }
        catch (Exception e) { Plugin.Log.LogError("[menu input check] FAIL: " + e); }
        finally { _entry.interactable = interactable; }
    }
}
