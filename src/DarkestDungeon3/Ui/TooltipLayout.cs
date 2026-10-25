using System;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Keep native-sized tooltip text on screen, including large-font and long-effect descriptions.</summary>
internal readonly struct TooltipLayout
{
    internal const float Padding = 20, Margin = 8, DefaultWidth = 460, MaxWidth = 640, ScrollHintHeight = 26;
    internal readonly Rect Frame, Viewport;
    internal readonly float MaxScroll;

    internal TooltipLayout(Vector2 pointer, float width, float contentHeight, float screenWidth, float screenHeight)
    {
        float height = Math.Min(contentHeight + Padding * 2, screenHeight - Margin * 2);
        float x = Math.Max(Margin, Math.Min(pointer.x + 20, screenWidth - width - Margin));
        float y = Math.Max(Margin, Math.Min(pointer.y + 20, screenHeight - height - Margin));
        Frame = new Rect(x, y, width, height);
        bool scroll = contentHeight + Padding * 2 > height;
        Viewport = new Rect(x + Padding, y + Padding, width - Padding * 2,
            Math.Max(1, height - Padding * 2 - (scroll ? ScrollHintHeight : 0)));
        MaxScroll = Math.Max(0, contentHeight - Viewport.height);
    }

    internal TooltipLayout(Rect anchor, float width, float contentHeight, float screenWidth, float screenHeight)
        : this(new Vector2(anchor.xMax, anchor.y - 20), width, contentHeight, screenWidth, screenHeight)
    {
        // Keep the hovered slot visible. Its edges, rather than the moving mouse,
        // determine placement for the whole hover and while reading a long tip.
        float right = anchor.xMax + 20;
        float x = right + width + Margin <= screenWidth ? right : anchor.x - width - 20;
        x = Math.Max(Margin, Math.Min(x, screenWidth - width - Margin));
        Frame = new Rect(x, Frame.y, Frame.width, Frame.height);
        Viewport = new Rect(x + Padding, Viewport.y, Viewport.width, Viewport.height);
    }

    internal static float Width(Func<float, float> contentHeight, float screenHeight)
    {
        float width = DefaultWidth;
        while (width < MaxWidth && contentHeight(width - Padding * 2) + Padding * 2 > screenHeight - Margin * 2)
            width = Math.Min(MaxWidth, width + 60);
        return width;
    }

    internal float Scroll(float current, float wheel, float bodySize)
        => Math.Max(0, Math.Min(MaxScroll, current + wheel * bodySize * 2));
}
