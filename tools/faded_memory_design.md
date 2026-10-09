# Faded Memory and Haunting Memories

Owner-defined feature rules, 2026-10-09. This supersedes the earlier O3 "old memory" sketch in
[overhaul_roadmap.md](overhaul_roadmap.md). The rules below are recorded for implementation;
neither feature is enabled by this document.

Owner follow-up, 2026-10-09: "Start" selects these features for implementation. Faded Memory is
the next feature track after the quota reset, followed by Haunting Memories. Begin with the
boss/zone and sprite coverage audit and a saved encounter contract, then one complete playable
encounter. Existing quest-flow defects remain recorded. Do not expose a curio that cannot finish
its fight, award both rewards and return safely. Unresolved pool/policy details below remain open.

## Faded Memory: agreed experience

| Part | Owner's rule |
|---|---|
| Curio | A newly generated curio with a DD1 appearance, named **Faded Memory** |
| Placement | One guaranteed Faded Memory on every boss quest |
| Interaction | Use the ordinary hand interaction, with prose inviting the player to confront the past |
| Destination | A fight against a DD1 boss in its corresponding DD1 zone environment |
| Heroes | Switch hero visuals to DD1 sprites only; retain their current skills, stats and combat behaviour |
| Victory | Drop one random Legendary DD1 item and one random Ancestor DD1 item, such as Ancestor's Pistol |

This is a guaranteed placement, not a chance roll. Place the curio on a reachable part of the
boss quest and persist its identity. Reloading or revisiting the area must not create another
curio or reroll its encounter. Availability does not force the player to interact.
The hand action is the specified entry; no supply requirement or entry fee has been added.

The guarantee applies to every boss quest. Mountain/final quests will need an explicit matching
memory boss/zone when that track is implemented; do not silently exclude them. A Haunting
Memories quest also gets the curio if it is a boss quest, not merely because the event is active.

The boss encounter uses the corresponding DD1 regional scenery. Existing regional pairs provide
the starting association: Sprawl/Ruins, Foetor/Warrens, Tangle/Weald and Shroud/Cove. A selected
boss must agree with its own DD1 zone. Boss selection, difficulty and the remaining region
mappings still need a concrete table. This curio's destination is a boss fight; a full replacement
quest is the separate Haunting Memories feature below.

## Proposed original UI prose

These lines are new draft copy, not extracted game text:

- Name: **Faded Memory**
- Description: "A remnant of another age. Its horrors have not forgotten you."
- Hand action: **"Confront the past."**
- Action hint: "Enter the memory and face the horror within."
- Arrival: "What was buried has endured."
- Victory: "The vision recedes. Its spoils remain."

Keep the normal DD1-style curio interaction layout and hand icon. Make the transition into a
fight clear before the click. Generate the curio as a readable DD1-style environmental prop,
with an idle silhouette and interaction state that fit existing prop framing. Art production
and sprite coverage are later tasks; no generated asset is claimed here.

## Presentation and combat contract

Preserve the current DD2 actors and combat engine. Temporarily replace the hero presentation
with DD1 sprites, including the poses needed to communicate the current skills. A visual
counterpart is not a class conversion: do not swap skill lists, mastery, paths, equipment,
stats or status effects to DD1 values. Heroes without a direct DD1 counterpart need an
explicit visual mapping. Keep imported/generated art outside Git.

The boss is a DD1 boss, not an ordinary DD2 boss with its name changed. Its encounter needs
an adapter for DD2 combat and an audit of the mechanics it depends on. The owner's unchanged
skills instruction applies to the player party. It does not define the boss's translated kit.

Keep the current room/corridor position, heading, quest progress and return destination in a
saved entry checkpoint. On return, restore regional/hero presentation while preserving the
fight's HP, stress, deaths, consumed items and earned loot. This proposed return mechanism
continues the original memory-curio design; retreat and defeat policy remain open below.

## Two distinct victory rewards

A successful boss kill awards **two items total**: one random draw from the agreed Legendary
DD1 pool and one random draw from the agreed Ancestor DD1 pool. This replaces the old draft's
single-trinket reward. The count is fixed; neither item is an optional chance bonus.
Roll the rewards once, retain them through a full inventory and save/reload, and prevent a
second grant on repeated encounter-completion callbacks. Retreat must not count as a kill.

The intended item system is DD1 trinkets, consistent with the owner's earlier request and
Ancestor's Pistol example. Resolve the pool names against native data before implementing:

- Installed `trinkets/base.rarities.trinkets.json` has `very_rare`, `ancestral` and
  `ancestral_shambler`; there is no rarity ID named `legendary`.
- `trinkets/base.entries.trinkets.json` places `ancestors_pistol` in `ancestral`.
- The owner's **Legendary** reward label is retained. A proposed mapping is `very_rare`, but
  that is not yet an agreed pool. An item name containing "legendary" does not define a tier.
- Whether the Ancestor draw also includes `ancestral_shambler` needs an explicit pool decision.
  Do not merge special pools or duplicate-limited trinkets by inference.

Other open reward rules: already-owned/unique-item handling, DLC inclusion, class restrictions
and random weighting. These do not change the agreed two-reward contract.

## Haunting Memories: agreed experience

A special event named **Haunting Memories** makes a random quest in an area a DD1-dungeon
quest. It uses the same expedition mechanics as our DD2-region quests and DD1 trinket loot.
This is a whole-quest regional experience, distinct from touching a Faded Memory curio.

The event changes the chosen quest's destination/presentation and trinket loot policy; it
does not introduce a second exploration or combat ruleset. The owner has not specified
automatic DD1 hero-sprite conversion for this full quest, so keep that requirement confined
to the Faded Memory fight until extended explicitly.

Proposed event copy:

> Old roads return, and with them, horrors long consigned to dust. A forgotten dungeon awaits.

Show the affected quest at the area's existing map position, with its quest directly beneath
the area name. Label the DD1 destination and event so the player can see the change before
embarking. This UI placement follows the existing quest-board direction.

Persist which offer was selected, its DD1 destination and its loot policy. Reloading the town
must not keep rolling a different quest. Ordinary money/supply/quest-object loot is not
specified for replacement; the owner's rule concerns DD1 trinket loot. It also does not give
ordinary event quests the curio's guaranteed Legendary-plus-Ancestor reward.

Still to define: event trigger/frequency/duration, eligible areas and quest types, when the
selection occurs, difficulty/progression credit, DD1 trinket tables and expiry behaviour.

## Later retirement of the region switch

The owner intends to remove the DD1/DD2 area swap button later and use DD1 destinations through
these special features. Keep the switch operational during development. Remove it only as
part of the completed special-feature transition, with existing DD1 progress, offered quests
and in-progress expeditions preserved. The current region pairs remain useful for matching
destinations even after their manual UI switch is retired.

## Implementation slices and acceptance

1. Define boss/zone/hero-visual mappings and exact reward pools. Audit DD1 trinket effect support.
2. Add guaranteed, reachable placement to all boss-quest generation paths, with stable saved
   identity and no duplication after reload. Verify this does not displace required quest props.
3. Implement one complete memory encounter, including hand interaction, boss adapter, DD1
   scenery, temporary hero sprites, both reward draws and return. Broaden its coverage afterward.
4. Add Haunting Memories selection, persistence, quest-board presentation and DD1 trinket policy.
5. Retire the manual region switch after equivalent special access and save migration are ready.

Tests must cover overlapping inputs, pending reports, repeated victory callbacks, full pack,
reload before/during/after the encounter, unchanged player skills/stats from the visual swap,
retained combat consequences, ordinary versus boss event quests and independent loot policies.
Native verification must inspect the curio art, hero sprites/skill animations, zone match,
quest-board labels, transitions and reward presentation.

Remaining encounter policy: optional dismissal, retreat, defeat/wipe, replay after a failed
attempt and visual counterparts for unsupported heroes. Do not claim these are settled by
the placement or sprite-only rules above.
