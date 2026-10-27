using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Core.Expedition;

/// <summary>What happened when a hero touched a curio.</summary>
public sealed class CurioReport
{
    /// <summary>The part of <see cref="Loot"/> the pack had no room for (still on DD1's loot scroll).</summary>
    public List<LootDrop> LeftBehind = new();
    public string CurioId, HeroId, ItemUsed;
    public string OutcomeType;
    public string Text;
    public List<LootDrop> Loot = new();
    public List<string> Effects = new();
    public string QuirkGained, Purged;
    public bool Scouted;

    /// <summary>JSON restores shared loot objects as copies. Rebind waiting drops to their display entries without
    /// changing counts or producing rewards; identical entries are matched only once.</summary>
    internal void RestoreLootLinks()
    {
        var matched = new HashSet<LootDrop>();
        for (int i = 0; i < LeftBehind.Count; i++)
        {
            var waiting = LeftBehind[i];
            if (Loot.Contains(waiting)) { matched.Add(waiting); continue; }
            var display = Loot.FirstOrDefault(d => !matched.Contains(d)
                && d.Type == waiting.Type && d.Id == waiting.Id && d.Amount == waiting.Amount);
            if (display == null) continue;
            LeftBehind[i] = display;
            matched.Add(display);
        }
    }
}

/// <summary>DD1 curio resolution: pick an outcome by weight (or the item interaction), then apply it.</summary>
public sealed class CurioResolver
{
    /// <summary>DD1 heroes have ~20-40 HP; effects with absolute numbers become fractions of a typical 30.</summary>
    public const float TypicalDd1Hp = 30f;

    private readonly CurioLibrary _curios;
    private readonly EffectLibrary _effects;
    private readonly LootTables _loot;
    private readonly Dictionary<string, LootTables> _contentLoot;

    public CurioResolver(CurioLibrary curios, EffectLibrary effects, LootTables loot)
    {
        _curios = curios;
        _effects = effects;
        _loot = loot;
    }

    /// <summary>Explicit content views, validated before an interaction can consume supplies or RNG.</summary>
    public CurioResolver(CurioLibrary curios, EffectLibrary effects, LootTables loot,
                         IReadOnlyDictionary<string, LootTables> contentLoot) : this(curios, effects, loot)
    {
        if (contentLoot == null) throw new ArgumentNullException(nameof(contentLoot));
        _contentLoot = new Dictionary<string, LootTables>(StringComparer.Ordinal);
        foreach (string id in curios.Curios.Keys)
        {
            string source = curios.ContentSourceOf(id);
            if (source == null) throw new InvalidOperationException($"Curio {id} has no content ownership metadata");
            if (source == "base" || _contentLoot.ContainsKey(source)) continue;
            if (!contentLoot.TryGetValue(source, out var scoped) || scoped == null)
                throw new InvalidOperationException($"Curio loot content is unavailable: {source}");
            _contentLoot.Add(source, scoped);
        }
    }

    private LootTables LootFor(string curioId)
    {
        if (_contentLoot == null) return _loot;
        string source = _curios.ContentSourceOf(curioId);
        if (source == "base") return _loot;
        if (source != null && _contentLoot.TryGetValue(source, out var scoped)) return scoped;
        throw new InvalidOperationException($"Curio {curioId} has no validated loot content");
    }

    public CurioDef Def(string curioId) => _curios.Get(curioId);
    public string SpriteOf(string curioId) => _curios.SpriteOf(curioId);

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

        var loot = LootFor(curioId);
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
                    report.Loot.AddRange(loot.Roll(r.Name, Math.Max(1, (int)r.Weight), difficulty, zone, rng));
                break;
            case "Effect":
                var effect = PickWeighted(outcome.Results, r => r.Weight, rng);
                if (effect != null) ApplyEffect(effect.Name, heroId, party, rng, report, state);
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
                foreach (var room in state.Map.QuestRooms) room.Scouted = true;
                foreach (var tile in state.Map.AllTiles)
                {
                    tile.Scouted = true;
                    if (tile.SecretRoomId >= 0 && tile.SecretDoorAlwaysAccessible)
                        state.Map.Room(tile.SecretRoomId).Scouted = true;
                }
                report.Scouted = true;
                break;
        }
        return report;
    }

    /// <summary>Apply a DD1 effect out of combat: stress, stress relief, healing, and damage over time.</summary>
    public void ApplyEffect(string effectName, string heroId, IParty party, Rng rng, CurioReport report = null, ExpeditionState state = null)
    {
        var e = _effects.Get(effectName);
        report?.Effects.Add(effectName);
        if (e == null) return;
        if ((party as IResolveParty)?.Allows(heroId,"effect:"+effectName)==false) return;

        float stress = e.Float("stress");
        if (stress > 0) AddDd1Stress(party, heroId, stress, rng, effectName);
        float relief = e.Float("healstress");
        if (relief > 0) AddDd1Stress(party, heroId, -relief, rng, effectName);

        float heal = e.Float("heal");
        if (heal > 0) party.Heal(heroId, heal / TypicalDd1Hp);

        // Keep the treatment window. Effects tick on later movement, never on resolution or resume.
        foreach (var (field, kind) in new[] { ("dotPoison", "blight"), ("dotBleed", "bleed") })
        {
            float dot = e.Float(field);
            if (dot <= 0 || state == null) continue;
            float resistance = (party as ITravelResistance)?.Resistance(heroId, kind) ?? 0f;
            if (!rng.Chance(Math.Max(0f, e.Float("chance", fallback: 1f) - resistance))) continue;
            state.TravelEffects.Add(new TravelEffect { HeroId = heroId, Kind = kind,
                DamageFraction = dot / TypicalDd1Hp, TurnsLeft = Math.Max(1, e.Int("duration", fallback: 1)) });
            report?.Effects.Add(kind == "bleed" ? "Bleeding. Use a bandage before travelling." : "Blighted. Use antivenom before travelling.");
        }
    }

    private static void AddDd1Stress(IParty party, string heroId, float dd1, Rng rng, string cause)
    {
        if (dd1 != 0) party.AddStress(heroId,ResolveStress.Units(dd1),cause);
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
