using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>
/// Which of an exported boss's own animations plays for each state of the DD2 donor that runs its fight
/// (<c>&lt;model&gt;.moveset.json</c>, written beside the private export by ds3-extract's make_moveset.py).
/// DD2 names its states <c>&lt;donor&gt;_&lt;move&gt;_&lt;part&gt;</c>: a move's antic (wind-up), action (strike), recover, and an
/// idle held between antic and action; impact (being hit) has action and recover; idle_A/idle_B are the stance.
/// Each move names one export clip with two split times (wind-up end, strike end), played at the clip's own speed
/// from the moment the donor enters the part and then held, so the strike lands when DD2's action starts.
/// The phase-change move (e.g. the knight's Death's Door) plays its own clip, then the next phase's moves and weapon.
/// </summary>
public sealed class ExportedMoveset
{
    public sealed class Move
    {
        public string Clip;
        public float WindupEnd, StrikeEnd, Duration;
    }

    public sealed class Phase
    {
        public string Weapon, IdleClip;
        public float IdleDuration;
        public Dictionary<string, Move> Moves { get; } = new();
    }

    private static readonly string[] Parts = { "antic", "action", "recover", "impact", "idle" };

    public List<Phase> Phases { get; } = new();
    public string ChangeOn, ChangeClip;
    public float ChangeDuration;
    public int ChangeNext;
    public int Current { get; private set; }
    private bool _changing;

    public static ExportedMoveset Parse(string json)
    {
        var o = JObject.Parse(json);
        var set = new ExportedMoveset();
        foreach (var p in (JArray)o["phases"])
        {
            var phase = new Phase { Weapon = (string)p["weapon"], IdleClip = (string)p["idle"]["clip"], IdleDuration = (float)p["idle"]["duration"] };
            foreach (var m in ((JObject)p["moves"]).Properties())
            {
                var split = (JArray)m.Value["split"];
                phase.Moves[m.Name] = new Move { Clip = (string)m.Value["clip"], WindupEnd = (float)split[0], StrikeEnd = (float)split[1], Duration = (float)m.Value["duration"] };
            }
            set.Phases.Add(phase);
        }
        if (o["phase_change"] is JObject change)
        {
            set.ChangeOn = (string)change["on"];
            set.ChangeClip = (string)change["clip"];
            set.ChangeDuration = (float)change["duration"];
            set.ChangeNext = (int)change["next"];
        }
        if (set.Phases.Count == 0) throw new FormatException("A moveset needs at least one phase");
        return set;
    }

    /// <summary>A donor state's move and part: "lost_battalion_knight_riposte_action_v2_downpunch" -> (riposte, action).</summary>
    public static (string Move, string Part) Split(string stateClip, string donor)
    {
        string name = stateClip ?? "";
        if (donor != null && name.StartsWith(donor + "_", StringComparison.Ordinal)) name = name.Substring(donor.Length + 1);
        if (name.StartsWith("idle", StringComparison.Ordinal)) return ("idle", "idle");
        var words = name.Split('_');
        // Some native hero clips keep "action" in the motion's name and append
        // "recover" afterwards. That final phase must take precedence.
        int recovery=Array.LastIndexOf(words,"recover");
        if (recovery>0) return (string.Join("_",words.Take(recovery)),"recover");
        for (int i = 1; i < words.Length; i++)
            if (Parts.Contains(words[i])) return (string.Join("_", words.Take(i)), words[i]);
        return (name, "");
    }

    /// <summary>The clip, time (seconds) and weapon for the donor's state, <paramref name="inState"/> seconds after it
    /// began; <paramref name="clock"/> runs the looping stance.</summary>
    public (string Clip, float Time, string Weapon) Resolve(string stateClip, string donor, float inState, float clock)
    {
        var (move, part) = Split(stateClip, donor);
        if (ChangeOn != null && move == ChangeOn && part != "idle")
        {
            _changing = true;
            return (ChangeClip, Math.Min(inState, ChangeDuration), Phases[Current].Weapon);
        }
        if (_changing)
        {
            _changing = false;
            if (ChangeNext >= 0 && ChangeNext < Phases.Count) Current = ChangeNext;
        }
        var phase = Phases[Current];
        if (move == "idle" || move == ChangeOn || !phase.Moves.TryGetValue(move, out var m))
            return (phase.IdleClip, phase.IdleDuration > 0 ? clock % phase.IdleDuration : 0, phase.Weapon);
        float t = part switch
        {
            "antic" => Math.Min(inState, m.WindupEnd),
            "idle" => m.WindupEnd,                                     // held at the top of the wind-up
            "recover" => Math.Min(m.StrikeEnd + inState, m.Duration),
            _ => Math.Min(m.WindupEnd + inState, m.StrikeEnd),         // action, impact
        };
        return (m.Clip, t, phase.Weapon);
    }

    /// <summary>Back to the first phase (a new fight).</summary>
    public void Reset()
    {
        Current = 0;
        _changing = false;
    }

    /// <summary>Every weapon form the phases show (to build them all and switch between them).</summary>
    public IEnumerable<string> Weapons => Phases.Select(p => p.Weapon).Where(w => !string.IsNullOrEmpty(w)).Distinct();
}
