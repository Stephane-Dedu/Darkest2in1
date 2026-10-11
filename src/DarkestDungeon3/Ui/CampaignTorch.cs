using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DarkestDungeon3.Ui;

/// <summary>DD2's animated UI flame material and torch art, loaded from the installed game.</summary>
internal sealed class CampaignTorch : MonoBehaviour
{
    private AsyncOperationHandle<Material> _fire;
    private AsyncOperationHandle<Sprite> _torch;
    private Image _flame, _body;
    public bool Finished => _fire.IsDone && _torch.IsDone;

    public void Build()
    {
        _flame = Image("Living flame", new Vector2(0, 112), new Vector2(210, 250));
        _flame.enabled = false;
        _body = Image("Torch", new Vector2(0, -58), new Vector2(210, 300));
        _body.preserveAspect = true;
        _body.enabled = false;
        _fire = Addressables.LoadAssetAsync<Material>("Assets/Art/UI/Animated_Materials/Torch/Orange_Flame/vfx_ui_torch_combat_flame_orange_high.mat");
        _torch = Addressables.LoadAssetAsync<Sprite>("Assets/Art/UI/HUD/ui_profile_torch.png");
    }

    private Image Image(string name, Vector2 offset, Vector2 size)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(transform, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.anchoredPosition = offset;
        image.rectTransform.sizeDelta = size;
        image.raycastTarget = false;
        return image;
    }

    private void Update()
    {
        if (!_flame.enabled && _fire.IsDone && _fire.Status == AsyncOperationStatus.Succeeded)
        {
            _flame.material = _fire.Result;
            _flame.enabled = true;
        }
        if (!_body.enabled && _torch.IsDone && _torch.Status == AsyncOperationStatus.Succeeded)
        {
            _body.sprite = _torch.Result;
            _body.enabled = true;
        }
        // The native shader animates its fire with Unity time. No progress or simulated loading delay.
    }

    private void OnDestroy()
    {
        if (_fire.IsValid()) Addressables.Release(_fire);
        if (_torch.IsValid()) Addressables.Release(_torch);
    }
}
