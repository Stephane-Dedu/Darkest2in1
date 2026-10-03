using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>What happened when a hero touched a curio.</summary>
public sealed class CurioReport
{
    public string CurioId, HeroId, ItemUsed;
    public string OutcomeType;
    public string Text;
    public List<LootDrop> Loot = new();
    public List<string> Effects = new();
    public string QuirkGained, Purged;
    public bool Scouted;
}

/// <summary>DD1 curio resolution: pick an outcome by weight (or the item interaction), then apply it.</summary>
public sealed class CurioResolver
{
    /// <summary>DD1 heroes have ~20-40 HP; effects with absolute numbers become fractions of a typical 30.</summary>
    public const float TypicalDd1Hp = 30f;

    private readonly CurioLibrary _curios;
    private readonly EffectLibrary _effects;
    private readonly LootTables _loot;

    public CurioResolver(CurioLibrary curios, EffectLibrary effects, LootTables loot)
    {
        _curios = curios;
        _effects = effects;
        _loot = loot;
    }

    public CurioDef Def(string curioId) => _curios.Get(curioId);

    /// <summary>Items that do something special on this curio (e.g. a skeleton key on a locked strongbox).</summary>
    public IEnumerable<string> UsefulItems(string curioId) => _curios.Get(curioId)?.Items.Select(i => i.Item) ?? Enumerable.Empty<string>();

    public CurioReport Resolve(string curioId, string heroId, string itemId, ExpeditionState state, IParty party, Rng rng)
    {
        var report = new CurioReport { CurioId = curioId, HeroId = heroId };
        var def = _curios.Get(curioId);
        if (def == null)
        {
            report.OutcomeType = "Nothing";
            report.Text = "There is nothing of interest here.";
            return report;
        }

        CurioOutcome outcome;
        var interaction = itemId == null ? null : def.Items.FirstOrDefault(i => i.Item == itemId);
        if (interaction != null && state.Pack.TryUse(itemId))
        {
            report.ItemUsed = itemId;
            outcome = interaction.Outcome;
        }
        else
        {
            outcome = PickWeighted(def.Outcomes, o => o.Weight, rng);
        }

        report.OutcomeType = outcome?.Type ?? "Nothing";
        report.Text = outcome?.Text;
        if (outcome == null) return report;

        int difficulty = state.Quest?.Difficulty ?? 1;
        string zone = Core.Dungeon.ZoneBase.Of(state.Quest?.Dungeon) ?? "";
        switch (outcome.Type)
        {
            case "Loot":
                // Every listed table is drawn; the weight column is the number of draws.
                foreach (var r in outcome.Results)
                    report.Loot.AddRange(_loot.Roll(r.Name, Math.Max(1, (int)r.Weight), difficulty, zone, rng));
                break;
            case "Effect":
                var effect = PickWeighted(outcome.Results, r => r.Weight, rng);
                if (effect != null) ApplyEffect(effect.Name, heroId, party, rng, report);
                break;
            case "Quirk":
            case "Disease":
                var quirk = PickWeighted(outcome.Results, r => r.Weight, rng);
                if (quirk != null) report.QuirkGained = party.AddDd1Quirk(heroId, quirk.Name);
                break;
            case "Purge":
                report.Purged = party.PurgeNegative(heroId);
                break;
            case "Scouting":
                foreach (var room in state.Map.Rooms) room.Scouted = true;
                foreach (var tile in state.Map.AllTiles) tile.Scouted = true;
                report.Scouted = true;
                break;
        }
        return report;
    }

    /// <summary>Apply a DD1 effect out of combat: stress, stress relief, healing, and damage over time.</summary>
    public void ApplyEffect(string effectName, string heroId, IParty party, Rng rng, CurioReport report = null)
    {
        var e = _effects.Get(effectName);
        report?.Effects.Add(effectName);
        if (e == null) return;

        float stress = e.Float("stress");
        if (stress > 0) AddDd1Stress(party, heroId, stress, rng, effectName);
        float relief = e.Float("healstress");
        if (relief > 0) AddDd1Stress(party, heroId, -relief, rng, effectName);

        float heal = e.Float("heal");
        if (heal > 0) party.Heal(heroId, heal / TypicalDd1Hp);

        // Out of combat a DoT simply lands: damage per turn times its duration.
        float dot = e.Float("dotPoison") + e.Float("dotBleed");
        if (dot > 0) party.Damage(heroId, dot * Math.Max(1, e.Int("duration", fallback: 1)) / TypicalDd1Hp, effectName);
    }

    private static void AddDd1Stress(IParty party, string heroId, float dd1, Rng rng, string cause)
    {
        float points = Math.Abs(dd1) / Expedition.CrawlRules.Dd1StressPerDd2Point;
        int whole = (int)Math.Floor(points);
        if (rng.Chance(points - whole)) whole++;
        if (whole > 0) party.AddStress(heroId, Math.Sign(dd1) * whole, cause);
    }

    private static T PickWeighted<T>(IList<T> items, Func<T, float> weight, Rng rng) where T : class
    {
        float total = items.Sum(weight);
        if (total <= 0) return items.FirstOrDefault();
        double roll = rng.NextDouble() * total;
        foreach (var item in items)
        {
            roll -= weight(item);
            if (roll < 0) return item;
        }
        return items.LastOrDefault();
    }
}
