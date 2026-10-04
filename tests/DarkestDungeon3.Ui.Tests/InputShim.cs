// An event-only shim for running the actual Drag.cs outside Unity. It deliberately does not simulate
// native GUI buttons, GUI.matrix, rendering or resource loading; those still need an in-game check.
namespace UnityEngine;

public enum EventType { MouseDown, MouseDrag, MouseUp, MouseMove, Repaint, Layout, Used, KeyDown }
public enum KeyCode { None, Escape }
public sealed class Event
{
    public static Event current;
    public EventType type, rawType;
    public int button;
    public KeyCode keyCode;
    public Vector2 mousePosition;
    public void Use() => type = EventType.Used;
}
public readonly struct Vector2(float x, float y)
{
    public readonly float x = x, y = y;
    public float magnitude => MathF.Sqrt(x * x + y * y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
}
public readonly struct Rect(float x, float y, float width, float height)
{
    public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y) { }
    public Vector2 position => new(x, y);
    public Vector2 size => new(width, height);
    public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
}
public readonly struct Color(float r, float g, float b, float a) { }
public static class GUI { public static Color color; public static bool enabled = true; }
public static class GUIUtility
{
    public static int hotControl;
    public static Vector2 clipOffset;
    public static Vector2 GUIToScreenPoint(Vector2 point) => point + clipOffset;
}
public static class Time { public static int frameCount; }
