using UnityEngine;
using UnityEngine.UI;

namespace DarkestDungeon3.Ui;

/// <summary>The installed animated flame and opaque background share one Canvas, below the IMGUI labels.</summary>
internal sealed class HamletLoadingTorch : MonoBehaviour
{
    private static HamletLoadingTorch _instance;
    private int _lastDraw;
    internal static bool Visible => _instance != null && _instance.gameObject.activeSelf;

    internal static void Show()
    {
        if (_instance == null)
        {
            var root = new GameObject("DD3 town loading torch", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background=new GameObject("Cinder background",typeof(RectTransform)).AddComponent<Image>();
            background.transform.SetParent(root.transform,false);
            background.rectTransform.anchorMin=Vector2.zero; background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            background.color=new Color(.025f,.022f,.020f,1); background.raycastTarget=false;
            var torch = new GameObject("Burning torch", typeof(RectTransform));
            torch.transform.SetParent(root.transform, false);
            var rect = (RectTransform)torch.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, 95);
            rect.sizeDelta = new Vector2(500, 430);
            torch.AddComponent<CampaignTorch>().Build();
            _instance = root.AddComponent<HamletLoadingTorch>();
        }
        _instance._lastDraw = Time.frameCount;
    }

    internal static void Hide()
    {
        if (_instance == null) return;
        _instance.gameObject.SetActive(false);
        Destroy(_instance.gameObject);
        _instance = null;
    }

    private void Update()
    {
        if (Time.frameCount - _lastDraw > 3) Hide();
    }
}
