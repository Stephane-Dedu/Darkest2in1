using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>Persistent resolve condition. Stress retains the legacy ten-DD1-points-per-unit save scale.</summary>
public sealed class ResolveState
{
    public string TraitId;
    public bool Virtuous;
    public Dictionary<string, int> History = new();
    public int ToxicLevel;
    public Dictionary<string, int> ToxicBuffs = new();
    [JsonIgnore] public bool IsToxic => TraitId == "toxic";
    public ResolveState Copy() => new()
    {
        TraitId = TraitId, Virtuous = Virtuous, ToxicLevel = Math.Max(0,Math.Min(3,ToxicLevel)),
        History = new Dictionary<string,int>(History ?? new()), ToxicBuffs = new Dictionary<string,int>(ToxicBuffs ?? new())
    };
    public void Clear() { TraitId = null; Virtuous = false; ToxicLevel = 0; (ToxicBuffs ??= new()).Clear(); }
}

public sealed class ResolveTrait
{
    public string Id, CurioTag;
    public bool Virtuous, Generated;
    public float GenerationModifier, CurioChance;
    public bool KeepLoot;
    public List<string> BuffIds = new();
    public List<ResolveAct> Acts = new();
    public Dictionary<string,ResolveAct> Reactions = new();
    public List<string> Classes = new();
}

public sealed class ResolveAct
{
    public string Id, Effect;
    public float Amount, Chance;
    public int RaidLimit;
    public List<string> Classes = new();
}

public enum ResolveEvent { None, Affliction, Virtue, Toxic, Cured, LostVirtue, HeartAttack, HeartAttackDeath }

public readonly struct ResolveResult
{
    public readonly float Stress;
    public readonly ResolveEvent Event;
    public ResolveResult(float stress, ResolveEvent ev) { Stress = stress; Event = ev; }
}

/// <summary>DD1's resolve thresholds over the compatible fractional native stress scale.</summary>
public sealed class ResolveStress
{
    public const float Break = 10, Maximum = 20, Dd1PerUnit = 10;
    public float AfflictionChance = .75f, MinAffliction = .05f, MaxAffliction = .99f, VirtueStress = 4.5f;
    public int AfflictionWeight = 4, VirtueWeight = 4;
    public int[] HistoryIncrement = {0,2,3,4};
    public float[] DifficultyModifiers = {0,.05f,.10f,.15f,.20f,.25f,.33f};
    public IReadOnlyList<ResolveTrait> Traits => _traits;
    private readonly List<ResolveTrait> _traits = new();
    public ResolveTrait Get(string id) => _traits.FirstOrDefault(t => t.Id == id);
    public static float Dd1(float units) => units*Dd1PerUnit;
    public static float Units(float dd1) => dd1/Dd1PerUnit;
    // Fractional native units accumulate float dust (one hundred .1f additions can stay just below10).
    public static bool Reached(float units,float threshold) => units>=threshold-.0001f;

    public static ResolveStress Load(Dd1Install install, JObject rules) => Parse(
        JObject.Parse(File.ReadAllText(install.PathOf("shared","trait","trait_library.json"))),rules);

    public static ResolveStress Parse(JObject document, JObject rules)
    {
        var result = new ResolveStress();
        if (rules != null)
        {
            result.AfflictionChance = (float?)rules["affliction_base_onset_chance"] ?? result.AfflictionChance;
            result.MinAffliction = (float?)rules["affliction_min_chance"] ?? result.MinAffliction;
            result.MaxAffliction = (float?)rules["affliction_max_chance"] ?? result.MaxAffliction;
            result.VirtueStress = Units((float?)rules["stress_affliction_resisted"] ?? 45);
            result.AfflictionWeight = (int?)rules["base_affliction_history_WF"] ?? 4;
            result.VirtueWeight = (int?)rules["base_virtue_history_WF"] ?? 4;
            result.HistoryIncrement = rules["affliction_history_increment"]?.Select(t => (int)t).ToArray() ?? result.HistoryIncrement;
            result.DifficultyModifiers = rules["effectiveDifficultyAfflictionOnsetModifiers"]?.Select(t => (float)t).ToArray() ?? result.DifficultyModifiers;
        }
        foreach (var t in document["traits"] ?? new JArray())
        {
            var trait = new ResolveTrait { Id = (string)t["id"], Virtuous = (string)t["overstress_type"] == "virtue",
                Generated = (bool?)t["is_generated"] ?? true, GenerationModifier = (float?)t["generate_chance_modifier"] ?? 0,
                CurioTag = (string)t["curio_tag"], CurioChance = (float?)t["curio_tag_chance"] ?? 0,
                KeepLoot = (bool?)t["keep_loot"] ?? false,
                BuffIds = t["buff_ids"]?.Select(b => (string)b).ToList() ?? new(),
                Classes = t["valid_hero_class_ids"]?.Select(c => (string)c).ToList() ?? new() };
            foreach (var a in t["combat_start_turn_act_outs"] ?? new JArray()) trait.Acts.Add(Act(a));
            foreach (var a in t["reaction_act_outs"] ?? new JArray()) { var act=Act(a); trait.Reactions[act.Id]=act; }
            if (trait.Id != null) result._traits.Add(trait);
        }
        return result;
    }

    private static ResolveAct Act(JToken a) => new()
    {
        Id = (string)a["id"], Chance = (float?)a["chance"] ?? 0, Amount = (float?)a["data"]?["number_value"] ?? 0,
        Effect = (string)a["data"]?["string_value"] ?? (string)a["data"]?["effect"],
        RaidLimit = (int?)a["raid_limit"] ?? 0,
        Classes = a["valid_hero_class_ids"]?.Select(c => (string)c).ToList() ?? new()
    };

    public float Chance(int resolveLevel, int difficulty, float virtueBonus)
    {
        int delta = Math.Max(0,Math.Min(DifficultyModifiers.Length-1,difficulty-resolveLevel));
        return Math.Max(MinAffliction,Math.Min(MaxAffliction,AfflictionChance-virtueBonus+DifficultyModifiers[delta]));
    }

    public ResolveResult Check(ResolveState state, float stress, bool atDeathsDoor, string classId,
        int resolveLevel, int difficulty, float virtueBonus, Rng rng, float heartReliefBonus = 0)
    {
        if (state == null || float.IsNaN(stress) || float.IsInfinity(stress)) throw new ArgumentException("Invalid resolve state");
        stress = Math.Max(0,Math.Min(Maximum,stress));
        if (stress <= .00001f && state.TraitId != null && !state.Virtuous)
        { state.Clear(); return new ResolveResult(0,ResolveEvent.Cured); }
        if (state.TraitId == null && Reached(stress,Break))
        {
            state.History ??= new();
            // The hit that first crosses 100 is capped there, including oversized stress events.
            if (classId == "flagellant")
            { state.TraitId="toxic"; state.ToxicLevel=1; return new ResolveResult(Break,ResolveEvent.Toxic); }
            bool virtue = !rng.Chance(Chance(resolveLevel,difficulty,virtueBonus));
            var candidates = _traits.Where(t => t.Generated && t.Virtuous == virtue && (t.Classes.Count==0 || t.Classes.Contains(classId))).ToList();
            if (candidates.Count == 0) throw new InvalidOperationException("DD1 resolve trait pool is empty");
            float Weight(ResolveTrait t)
            {
                int count = state.History.TryGetValue(t.Id,out int n) ? Math.Max(0,n) : 0;
                return Math.Max(0,(virtue ? VirtueWeight : AfflictionWeight+(HistoryIncrement.Length>0 ? HistoryIncrement[Math.Min(count,HistoryIncrement.Length-1)] : 0))+t.GenerationModifier);
            }
            var picked = Weighted(candidates,Weight,rng) ?? throw new InvalidOperationException("DD1 resolve trait weights are empty");
            state.TraitId=picked.Id; state.Virtuous=virtue;
            state.History[picked.Id]=state.History.TryGetValue(picked.Id,out int old) ? old+1 : 1;
            return new ResolveResult(virtue ? VirtueStress : Break,virtue ? ResolveEvent.Virtue : ResolveEvent.Affliction);
        }
        if (Reached(stress,Maximum))
        {
            if (state.Virtuous) { state.Clear(); return new ResolveResult(0,ResolveEvent.LostVirtue); }
            // Native QueueHeartAttack's survivor branch relieves 30*(1+modifier) DD1 stress.
            return new ResolveResult(Math.Max(0,Maximum-Units(30*(1+heartReliefBonus))),atDeathsDoor ? ResolveEvent.HeartAttackDeath : ResolveEvent.HeartAttack);
        }
        return new ResolveResult(stress,ResolveEvent.None);
    }

    public ResolveAct PickAct(ResolveState state, string classId, Rng rng) => Weighted(
        Get(state?.TraitId)?.Acts.Where(a => a.Classes.Count==0 || a.Classes.Contains(classId)).ToList() ?? new(),a=>a.Chance,rng);

    private static T Weighted<T>(IReadOnlyList<T> entries, Func<T,float> weight, Rng rng) where T:class
    {
        float total=entries.Sum(weight); if (total<=0) return null;
        double roll=rng.NextDouble()*total;
        foreach (var entry in entries) { roll-=weight(entry); if (roll<0) return entry; }
        return entries.LastOrDefault();
    }

    public static ResolveState Home(ResolveState source, float stress)
    {
        var result=source?.Copy();
        if (result?.Virtuous==true || stress<=0) result?.Clear();
        return result;
    }
}
