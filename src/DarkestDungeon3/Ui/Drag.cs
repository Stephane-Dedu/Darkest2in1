using System;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// DD1-style drag and drop for our IMGUI screens. A screen marks what can be picked up with <see cref="Source"/>
/// (call it before any button on the same rect) and where it can land with <see cref="Drop{T}"/>; UiRoot calls
/// <see cref="Overlay"/> last to draw the carried icon and cancel drops that land nowhere. A press without
/// movement stays a click. Raw event types are used because IMGUI buttons consume drag and release events.
/// </summary>
internal static class Drag
{
    private const float Threshold = 7f;

    private static object _pending, _payload;
    private static Action<Rect> _pendingDraw, _draw;
    private static Vector2 _downAt, _size, _grab;
    private static int _droppedFrame = -1;

    public static bool Active => _payload != null;
    public static object Payload => _payload;
    public static bool Carrying<T>() => _payload is T;

    /// <summary>Reserve a carried mouse event before any page buttons see it.</summary>
    public static void Begin()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && (_pending != null || Active))
        {
            Cancel();
            e.Use();
            return;
        }
        // A fast move/release can arrive without a separate drag pass. Promote before the target's
        // buttons see that release, using the same screen-space threshold as ordinary drags.
        if ((e.rawType == EventType.MouseDrag || e.rawType == EventType.MouseUp && e.button == 0)
            && _payload == null && _pending != null
            && (GUIUtility.GUIToScreenPoint(e.mousePosition) - _downAt).magnitude > Threshold)
        {
            _payload = _pending;
            _draw = _pendingDraw;
            _pending = null;
            _pendingDraw = null;
            GUIUtility.hotControl = 0;
        }
        // Drop targets use rawType. Ordinary buttons must never see a carried drag or release, even
        // when they are drawn before the source/target (the Hamlet roster, for example).
        if (Active && (e.rawType == EventType.MouseDrag || e.rawType == EventType.MouseUp)) e.Use();
    }

    public static void Cancel()
    {
        if (Active) GUIUtility.hotControl = 0;
        _pending = _payload = null;
        _pendingDraw = _draw = null;
    }

    /// <summary>Something that can be picked up: <paramref name="drawIcon"/> draws it in a rect (used while carried).</summary>
    public static void Source(Rect r, object payload, Action<Rect> drawIcon)
    {
        if (payload == null || !GUI.enabled) return;
        var e = Event.current;
        if (e.rawType == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) && _payload == null)
        {
            _pending = payload;
            _pendingDraw = drawIcon;
            _downAt = GUIUtility.GUIToScreenPoint(e.mousePosition);
            _size = r.size;
            _grab = e.mousePosition - r.position;
        }
    }

    /// <summary>Did the carried thing (of type T) land on this rect this frame? Hovering reports through <paramref name="hover"/>.</summary>
    public static bool Drop<T>(Rect r, out T payload) where T : class
    {
        payload = null;
        var e = Event.current;
        if (!GUI.enabled || !(_payload is T carried) || !r.Contains(e.mousePosition)) return false;
        if (e.rawType != EventType.MouseUp) return false;
        payload = carried;
        _payload = null;
        _draw = null;
        GUIUtility.hotControl = 0;
        _droppedFrame = Time.frameCount;
        e.Use();
        return true;
    }

    /// <summary>Is the carried thing (of type T) over this rect (for highlighting a valid target)?</summary>
    public static bool Hovering<T>(Rect r) where T : class => _payload is T && r.Contains(Event.current.mousePosition);

    /// <summary>A drop landed somewhere this frame (lets a click handler ignore the same release).</summary>
    public static bool JustDropped => _droppedFrame == Time.frameCount;

    /// <summary>Call last in OnGUI: draws the carried icon under the mouse, cancels drops that hit nothing.</summary>
    public static void Overlay()
    {
        var e = Event.current;
        if (e.rawType == EventType.MouseUp)
        {
            Cancel();
            return;
        }
        if (_payload == null || e.type != EventType.Repaint) return;
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, 0.85f);
        _draw?.Invoke(new Rect(e.mousePosition - _grab, _size));
        GUI.color = old;
    }
}
