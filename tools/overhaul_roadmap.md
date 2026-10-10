# Future systems and content

Owner ideas, 2026-10-09. Keep each track, refine it independently, and implement a bounded
version once its rules and acceptance cases are clear. Most tracks are proposals; O3 and O6
have owner-defined rules in the linked design, and the item mapping is research tooling.
Gameplay implementation remains pending. The main quest-flow backlog in
[quest_parity_map.md](quest_parity_map.md) remains active.

Latest owner follow-up selects O3 Faded Memory for implementation next, then O6 Haunting Memories.
Resume after the recorded quota pause. The linked feature design contains the agreed rules and
remaining choices; other overhaul tracks remain deferred.

## Tracks

| ID | Owner's direction | State | First useful implementation |
|---|---|---|---|
| O1 | DD2 supplies, coherent generated curios and shopkeeper stock | Item definitions mapped; gameplay design open | One item bought in town, carried into a DD2 zone, spent on one new curio, saved and restored correctly |
| O2 | DD2 inn items become camping items, with a limit of 1 | Proposal; scope of the limit is open | One item with explicit targeting, duration and a persisted camp-use budget |
| O3 | Faded Memory: one curio per boss quest, hand interaction, DD1 boss/zone and hero sprites with unchanged skills; one random Legendary plus one random Ancestor DD1 item on victory | Owner-defined; reward-pool mapping and encounter policies open | One complete enter/fight/two-reward/return encounter with one boss and a supported party |
| O4 | The Mountain takes the final Darkest Dungeon region's role, with predetermined layouts, void enemies and a confession boss for each quest | Proposal | One authored quest with its own fixed route, corridor/room encounters and confession boss |
| O5 | Bring back stun in a more DD1-like form | Balance question | Audit existing stun/daze skills, requirements and resistances before changing access |
| O6 | Haunting Memories event: a random area quest becomes a DD1 dungeon with current expedition mechanics and DD1 trinket loot; later replaces manual region switching | Owner-defined; scheduling and selection rules open | One persisted event quest with destination, UI and loot policy |

O1 supplies the identity and inventory work needed by O2. O3 also depends on the DD1 trinket
adapter and a distinct encounter-return checkpoint. O4 can reuse authored plot-map support.
O5 stays independent, so a content update does not silently rebalance native combat.
The older resolve-level, tiered skill-upgrade and central-button trinket-panel ideas remain in PARITY.md.

## O1: items, shopkeeper and new curios

Use [dd2_item_mapping.md](dd2_item_mapping.md) and its private per-item catalogue before assigning
interactions. It covers every installed Item record, including equipment, currencies and quest objects.
World props and scene interactions are a separate future asset survey.

Preserve the DD1 curio pattern: inspect, choose a hero, use a relevant supply or investigate,
resolve one outcome, collect loot, resume travel. A DD2 item gets an explicit new curio rule;
its combat skill must not accidentally execute against an exploration actor.

The first candidate set is medical/cleansing supplies, because their purposes are legible.
Suggested pairings below are new design, not native DD2 behaviour:

| Native item | Proposed curio use | Visual cue and likely region | Outstanding rule |
|---|---|---|---|
| bandages | Stabilize a wounded survivor before collecting a reward | Field dressing station in the Tangle | Reward, consumption and whether the survivor can be helped without it |
| antivenom | Neutralize a poisoned sample or seal | Diseased storehouse in the Foetor | Success outcome and untreated risk |
| medicinal_herbs | Treat contaminated remains or supplies | Apothecary shelf or abandoned camp | Keep distinct from antivenom; avoid a universal best supply |
| holy_water | Cleanse a profaned shrine | Cult intrusion in any region | Regional effects, reward and cursed alternative |
| laudanum | Settle a delirious witness | Locked refuge or roadside vigil | Stress tradeoff and repeat-use limits |
| smoke_bomb | Distract guards while searching a cache | Guarded Sprawl salvage | Encounter avoidance, scouting and reward balance |

Each accepted pairing needs a record with region, curio ID, item source/ID, target, consumption,
success/failure/untreated outcomes, RNG weights, loot, tooltip, art and save version. Generated
art should visibly explain the interaction and match the region's existing room/corridor art.

Shop stock needs an explicit eligible-item list, quantities, unlocks, restock timing and campaign
gold prices. Native DD2 costs use its currencies and modifiers; copying those numbers into DD1
gold is not a balance rule. Start with a small stock list rather than exposing all 602 item IDs.

Acceptance: buy/sell/cancel, insufficient money, full pack, drag-use and cancel, wrong item,
double click, pending loot, quest-item protection, reload before/after use, combat transfer and
return, independent DD1 supplies and clear tooltips. Consumption and rewards happen once.

## O2: camping items

Keep DD1 camping's meal and respite phases. Add an item action at a deliberate phase so camp
skills, items, ambushes and the existing modal guards remain coherent. DD2 inn items can affect
one hero, a pair or the whole party, and may have adverse effects or duration rules.

The owner's "limit to 1" is retained as an open design choice:

- one item for the whole camp;
- one application per hero per camp;
- one copy/use of each item or category per camp.

My starting recommendation is one item for the whole camp, with native target counts retained.
This keeps the new action small while we measure its value beside camping skills. It is not
implemented or treated as a settled rule. A four-target item must not consume four uses by
accident. Food-tag limits and adverse relationship effects need individual review.

Persist the chosen budget and expiry state. Define whether effects end at the next battle,
next camp or quest return. Native inn-arrival/exit expiry cannot be copied blindly when this
mod has no inn between rooms. Resetting on load must never grant another use.

Acceptance: target eligibility, multi-target effects, rejection without consuming an item,
one successful budget charge, effects across battle handoff, reload, camp exit and ambush.

## O3 and O6: Faded Memory and Haunting Memories

The owner's detailed rules, proposed original prose, presentation contract, reward evidence,
acceptance cases and open decisions are in [faded_memory_design.md](faded_memory_design.md).
Use that specification for either feature. It replaces the earlier single-reward O3 sketch.
Keep the manual DD1/DD2 switch until the later special-feature transition is complete.

## O4: Mountain campaign finale

Use the Mountain as the primary final-region identity while retaining the DD1 quest structure.
Each quest has its own predetermined rooms, halls, encounters and confession boss. Keep its
progression independent from the four ordinary region pairs. The exact confession ordering,
unlock rules and number of quests remain to be specified.

"Void monsters" is the owner's theme. Audit native cultist/cosmic encounters and their arena
requirements before choosing a roster; it is not assumed to be a native data category.
Map battles in both corridors and rooms, an authored route and any quest-specific mechanics.
Extend local Mountain art with coherent generated traversal scenes and arrival transitions.

Existing `data/zones.json` already routes the final ancestor-heart encounter to a Mountain
body-boss table. Audit `ZoneEncounters`, DD1 plot maps and quest progression before replacing
that mapping. One mapping is not the proposed confession-quest campaign.

Acceptance: a fixed and connected route, correct boss/arena selection, corridor and room fights,
quest-specific completion, recovery after reload, defeat/retreat rules, unique reward/progression
and no loss of existing campaign progress.

## O5: stun

DD2 already defines stun and daze. Installed token data assigns skip-turn consumption to stun
and delay-turn consumption to daze; both participate in its stun-resistance data. Therefore
the question is how often skills can apply them and under what conditions.

Before changing balance, inventory hero/enemy skills, combo or other prerequisites, cooldowns,
mastery differences, boss resistances, removal and chained-turn interactions. Compare the
DD1 resistance/recovery rules with this native behaviour. Keep any experiment isolated until
ordinary fights and bosses remain functional. Automatic stun on every former DD1 stun skill
is not an agreed design.

## Moving a track into implementation

For one bounded slice, record its rules, native evidence, art coverage, persistence boundaries
and acceptance cases here or in a linked design. Then implement it through the normal parity
loop. Existing authorization covers local investigation and prototypes; owner preferences
still determine unresolved feature choices. This document records ideas, not a blanket claim
that they are implemented or verified in the game.
