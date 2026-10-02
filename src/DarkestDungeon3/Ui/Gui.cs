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

    // ---- DD1 look ----

    public static readonly Color Dd1Name = new(177 / 255f, 161 / 255f, 108 / 255f);
    public static readonly Color Dd1Class = new(154 / 255f, 152 / 255f, 143 / 255f);
    public static readonly Color Dd1Health = new(0.75f, 0f, 0f);
    public static readonly Color Dd1Text = new(0.82f, 0.78f, 0.68f);

    /// <summary>Text in DD1's fonts (DwarvenAxe for headings, Ubuntu for body), falling back to IMGUI.</summary>
    public static void Text(Rect r, string text, float size, Color colour, TextAnchor align = TextAnchor.UpperLeft, bool heading = false)
    {
        var font = heading ? Runtime.Dd1Font.Heading : Runtime.Dd1Font.Body;
        if (font != null) { font.Draw(r, text, size, colour, align); return; }
        EnsureStyles();
        var style = new GUIStyle(_label) { fontSize = (int)(size * 0.8f), alignment = align, normal = { textColor = colour } };
        GUI.Label(r, text, style);
    }

    /// <summary>Draw a DD1 texture at its natural size with its top-left at (x, y).</summary>
    public static Rect At(Texture tex, float x, float y, bool flipX = false)
    {
        if (tex == null) return new Rect(x, y, 0, 0);
        var r = new Rect(x, y, tex.width, tex.height);
        if (flipX) GUI.DrawTextureWithTexCoords(r, tex, new Rect(1, 0, -1, 1), true);
        else GUI.DrawTexture(r, tex);
        return r;
    }

    /// <summary>A DD1-style text button: dark plate, thin gold frame, DwarvenAxe label, brighter on hover.</summary>
    public static bool DdButton(Rect r, string label, bool enabled = true, float size = 26)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Fill(r, new Color(0.06f, 0.05f, 0.04f, 0.92f));
        var frame = enabled ? (hover ? Gold : new Color(0.45f, 0.38f, 0.24f)) : new Color(0.25f, 0.23f, 0.2f);
        Fill(new Rect(r.x, r.y, r.width, 2), frame);
        Fill(new Rect(r.x, r.yMax - 2, r.width, 2), frame);
        Fill(new Rect(r.x, r.y, 2, r.height), frame);
        Fill(new Rect(r.xMax - 2, r.y, 2, r.height), frame);
        Text(r, label, size, enabled ? (hover ? Color.white : Dd1Name) : Dim, TextAnchor.MiddleCenter, heading: true);
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    /// <summary>An invisible click area (for art that acts as a button).</summary>
    public static bool Hotspot(Rect r) => GUI.Button(r, GUIContent.none, GUIStyle.none);

    // ---- announcements (DD1 shows events as a banner, not a log) ----

    private static string _announce;
    private static float _announceUntil;

    public static void Announce(string text, float seconds = 2.2f)
    {
        _announce = text;
        _announceUntil = Time.unscaledTime + seconds;
    }

    public static void DrawAnnouncement()
    {
        if (_announce == null || Time.unscaledTime > _announceUntil) return;
        float a = Mathf.Clamp01((_announceUntil - Time.unscaledTime) * 2f);
        var frame = Runtime.Art.Overlay("announcement_frame.png");
        var r = new Rect(960 - 310, 210 - 68, 620, 136);
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, a);
        if (frame != null) GUI.DrawTexture(r, frame); else Fill(r, new Color(0, 0, 0, 0.8f));
        GUI.color = old;
        Text(new Rect(r.x + 40, r.y + 20, r.width - 80, r.height - 50), _announce, 30, new Color(Dd1Name.r, Dd1Name.g, Dd1Name.b, a), TextAnchor.MiddleCenter, heading: true);
    }

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
