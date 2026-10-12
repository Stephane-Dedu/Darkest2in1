using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkestDungeon3.Ui;

/// <summary>Text feedback independent of the native template's optional banner transition.</summary>
internal sealed class CampaignButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Button _button;
    private TMP_Text[] _labels;
    private Color _normal;
    private bool _hover, _selected;

    internal void Initialize(Button button, Color normal)
    {
        _button = button; _normal = normal;
        _labels = button.GetComponentsInChildren<TMP_Text>(true);
        Paint();
    }
    public void OnPointerEnter(PointerEventData data) { _hover = true; Paint(); }
    public void OnPointerExit(PointerEventData data) { _hover = false; Paint(); }
    public void OnSelect(BaseEventData data) { _selected = true; Paint(); }
    public void OnDeselect(BaseEventData data) { _selected = false; Paint(); }
    private void OnDisable() { _hover = _selected = false; Paint(); }
    private void Paint()
    {
        if (_button == null || _labels == null) return;
        var color = _button.interactable && (_hover || _selected) ? new Color(.90f, .73f, .44f) : _normal;
        foreach (var label in _labels) label.color = color;
    }
}

internal sealed class EstateDoubleClick : MonoBehaviour, IPointerClickHandler
{
    private Action _enter;
    internal void Initialize(Action enter) => _enter = enter;
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && data.clickCount == 2) _enter?.Invoke();
    }
}
