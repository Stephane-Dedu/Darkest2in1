using UnityEngine;
using UnityEngine.UI;

namespace DarkestDungeon3.Ui;

/// <summary>IMGUI helpers: a 1920x1080 virtual canvas scaled to the window, DD-ish colours, and an input blocker.</summary>
internal static class Gui
{
    public const float W = 1920f, H = 1080f;

    public static readonly Color Parchment = new(0.86f, 0.80f, 0.66f);
    public static readonly Color Blood = new(0.62f, 0.10f, 0.08f);
    public static readonly Color Gold = new(0.85f, 0.70f, 0.30f);
    public static readonly Color Dim = new(0.55f, 0.52f, 0.47f);

    private static Texture2D _panel, _white;
    private static GUIStyle _label, _title, _button, _small, _box;

    /// <summary>Call first in OnGUI: maps the 1920x1080 layout onto the real window.</summary>
    public static void Begin()
    {
        float s = Mathf.Min(Screen.width / W, Screen.height / H);
        float ox = (Screen.width - W * s) / 2f, oy = (Screen.height - H * s) / 2f;
        GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0), Quaternion.identity, new Vector3(s, s, 1));
        EnsureStyles();
    }

    private static void EnsureStyles()
    {
        if (_label != null) return;
        _panel = Solid(new Color(0.05f, 0.04f, 0.04f, 0.86f));
        _white = Solid(Color.white);
        _label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, richText = true, normal = { textColor = Parchment } };
        _small = new GUIStyle(_label) { fontSize = 16 };
        _title = new GUIStyle(_label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = Gold } };
        _button = new GUIStyle(GUI.skin.button) { fontSize = 20, wordWrap = true, richText = true };
        _button.normal.textColor = Parchment;
        _button.hover.textColor = Color.white;
        _box = new GUIStyle(GUI.skin.box) { normal = { background = _panel } };
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    public static void Panel(Rect r) => GUI.Box(r, GUIContent.none, _box);

    public static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _white);
        GUI.color = old;
    }

    public static void Image(Rect r, Texture tex, ScaleMode mode = ScaleMode.ScaleAndCrop)
    {
        if (tex != null) GUI.DrawTexture(r, tex, mode);
    }

    public static void Title(Rect r, string text) => GUI.Label(r, text, _title);
    public static void Label(Rect r, string text) => GUI.Label(r, text, _label);
    public static void Small(Rect r, string text) => GUI.Label(r, text, _small);

    public static bool Button(Rect r, string text, bool enabled = true)
    {
        bool old = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GUI.Button(r, text, _button);
        GUI.enabled = old;
        return clicked;
    }

    public static void Bar(Rect r, float fraction, Color fg)
    {
        Fill(r, new Color(0, 0, 0, 0.7f));
        Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(fraction), r.height - 2), fg);
    }

    public static string Colour(string text, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";

    // ---- input blocker: a uGUI raycast target under IMGUI so clicks don't reach DD2's own UI ----

    private static GameObject _blocker;

    public static void BlockInput(bool on)
    {
        if (_blocker == null)
        {
            if (!on) return;
            _blocker = new GameObject("DD3InputBlocker");
            Object.DontDestroyOnLoad(_blocker);
            var canvas = _blocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            _blocker.AddComponent<GraphicRaycaster>();
            var img = new GameObject("Blocker").AddComponent<Image>();
            img.transform.SetParent(_blocker.transform, false);
            img.color = new Color(0, 0, 0, 0.004f);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        if (_blocker.activeSelf != on) _blocker.SetActive(on);
    }
}
