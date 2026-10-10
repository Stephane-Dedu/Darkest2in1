using System;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>
/// Geometry of the DD2-style destination screen (virtual 1920x1080): the Mountain banner, the region cards between
/// the quest details (x 30..430) and the roster (x 1538..), the quest medallions paged inside each card, and the
/// crop that fills a card with DD2's region painting.
/// </summary>
internal static class DestinationLayout
{
    public const float Left = 452, Right = 1520, Top = 160, CardWidth = 254, Gap = 18;
    public const float Header = 46, Painting = 280, Medal = 64, MedalStep = 70, Arrow = 18;
    public const int PerPage = 3;
    public const float CardHeight = Header + Painting + 10 + Medal + 40;

    public static readonly Rect Mountain = new((Left + Right) / 2 - 210, 86, 420, 62);

    /// <summary>Card <paramref name="index"/> of <paramref name="count"/>, the row centred between the side panels.</summary>
    public static Rect Card(int index, int count)
    {
        float row = count * CardWidth + Math.Max(0, count - 1) * Gap;
        return new Rect((Left + Right - row) / 2 + index * (CardWidth + Gap), Top, CardWidth, CardHeight);
    }

    public static Rect PaintingOf(Rect card) => new(card.x + 4, card.y + Header, card.width - 8, Painting);

    /// <summary>Medallion <paramref name="slot"/> of the <paramref name="shown"/> on a page, centred under the painting.</summary>
    public static Rect Medallion(Rect card, int slot, int shown)
    {
        float row = shown * MedalStep - (MedalStep - Medal);
        return new Rect(card.x + (card.width - row) / 2 + slot * MedalStep, card.y + Header + Painting + 10, Medal, Medal);
    }

    public static Rect PreviousPage(Rect card) => new(card.x + 3, card.y + Header + Painting + 30, Arrow, 24);
    public static Rect NextPage(Rect card) => new(card.xMax - 3 - Arrow, card.y + Header + Painting + 30, Arrow, 24);
    public static Rect QuestLine(Rect card) => new(card.x + 4, card.y + Header + Painting + 10 + Medal + 4, card.width - 8, 30);

    public static int Pages(int quests) => Math.Max(1, (quests + PerPage - 1) / PerPage);

    /// <summary>The quests on <paramref name="page"/> (clamped to the pages there are).</summary>
    public static (int First, int Count) Page(int quests, int page)
    {
        page = Math.Max(0, Math.Min(page, Pages(quests) - 1));
        int first = page * PerPage;
        return (first, Math.Max(0, Math.Min(PerPage, quests - first)));
    }

    /// <summary>
    /// The part of a sprite (<paramref name="source"/>, texture pixels, origin bottom-left) that covers a
    /// <paramref name="width"/>x<paramref name="height"/> target without stretching: the sides are cut evenly, and
    /// <paramref name="fromTop"/> (0 = keep the top, 1 = keep the bottom) chooses what a cut height keeps.
    /// </summary>
    public static Rect Cover(Rect source, float width, float height, float fromTop = 0.5f)
    {
        if (source.width <= 0 || source.height <= 0 || width <= 0 || height <= 0) return source;
        float aspect = width / height;
        if (source.width / source.height > aspect)
        {
            float cut = source.width - source.height * aspect;
            return new Rect(source.x + cut / 2, source.y, source.width - cut, source.height);
        }
        float spare = source.height - source.width / aspect;
        return new Rect(source.x, source.y + spare * (1 - fromTop), source.width, source.height - spare);
    }
}
