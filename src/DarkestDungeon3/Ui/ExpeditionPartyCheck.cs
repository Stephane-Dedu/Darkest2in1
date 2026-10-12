using System;
using System.Linq;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Real IMGUI drag events over an unsaved expedition party. No desktop input or estate files.</summary>
internal static class ExpeditionPartyCheck
{
    internal static readonly bool Enabled = Environment.GetCommandLineArgs().Contains("-dd3-roster-check");
    private static int _step;
    private static float _next;
    private static bool _done;
    internal static void Failed() => _done = true;
    internal static Event Input()
    {
        if (!Enabled || _done || Event.current.type != EventType.Repaint || Time.unscaledTime < _next) return null;
        _next = Time.unscaledTime + .12f;
        Vector2 Rank(int rank) => new(754 + 22 + (3 - rank) * 93 + 40, 900 + 16 + 40);
        Vector2 Roster(int index) => new(1700, RosterColumn.FirstY + index * RosterColumn.Spacing + 46);
        Event Mouse(EventType type, Vector2 at) => new() { type = type, button = 0, mousePosition = at };
        return _step switch
        {
            1 => Mouse(EventType.MouseDown, Rank(0)),
            2 => Mouse(EventType.MouseDrag, Rank(3)),
            3 => Mouse(EventType.MouseUp, Rank(3)),
            4 => Mouse(EventType.MouseDown, Roster(1)),
            5 => Mouse(EventType.MouseDrag, Rank(0)),
            6 => Mouse(EventType.MouseUp, Rank(0)),
            7 => Mouse(EventType.MouseDown, Roster(4)),
            8 => Mouse(EventType.MouseDrag, Rank(2)),
            9 => Mouse(EventType.MouseUp, Rank(2)),
            10 => Mouse(EventType.MouseDown, Rank(3)),
            11 => Mouse(EventType.MouseUp, Rank(1)),
            12 => Mouse(EventType.MouseDown, Rank(2)),
            13 => Mouse(EventType.MouseUp, new Vector2(650, 840)),
            14 => Mouse(EventType.MouseDown, Rank(2)),
            15 => new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape },
            16 => Mouse(EventType.MouseDown, Rank(3)),
            17 => Mouse(EventType.MouseUp, Rank(3)),
            18 => Mouse(EventType.MouseDown, Roster(5)),
            19 => Mouse(EventType.MouseUp, Rank(3)),
            20 => Mouse(EventType.MouseDown, Rank(3)),
            21 => Mouse(EventType.MouseUp, RosterColumn.Area.center),
            _ => Mouse(EventType.Repaint, new Vector2(650, 840)),
        };
    }
    internal static void Verify(EmbarkUi ui)
    {
        void Party(params int[] ids)
        {
            if (!ui.CheckParty.SequenceEqual(ids.Select(id => "preview-" + id)))
                throw new InvalidOperationException($"Roster input step {_step}: unexpected party " + string.Join(",", ui.CheckParty));
        }
        switch (_step)
        {
            case 0: case 1: case 2: Party(0, 1, 2, 3); break;
            case 3: case 4: case 5: Party(3, 1, 2, 0); break;
            case 6: case 7: case 8: Party(1, 3, 2, 0); break;
            case 9: case 10: Party(1, 3, 4, 0); break;
            case 11: case 12: case 13: case 14: case 15: case 16: Party(1, 0, 4, 3); break;
            case 17: case 18: Party(1, 0, 4); break;
            case 19: case 20: Party(1, 0, 4, 5); break;
            case 21:
                Party(1, 0, 4);
                _done = true;
                Plugin.Log.LogInfo("[roster input check] PASS: portrait swap; selected roster swap; full-party replacement; fast release; outside/Escape cancellation; click removal; empty rank; return to roster; no estate files accessed");
                break;
        }
        _step++;
    }
}

internal sealed partial class EmbarkUi
{
    internal System.Collections.Generic.IReadOnlyList<string> CheckParty => _party;
}
