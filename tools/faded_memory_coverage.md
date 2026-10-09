# Faded Memory implementation coverage

Audit 2026-10-10, round176. This records implementation gaps; it does not enable the curio.
The feature rules remain in [faded_memory_design.md](faded_memory_design.md).

## Existing adapters

`Driver.StartFight` deliberately excludes bosses from DD1 encounter translation. `data/zones.json`
currently substitutes DD2 configurations/tables for DD1 bosses. `Dd1Bestiary.Translate` also falls
back if any required actor lacks a mapping and can truncate actors whose DD2 sizes exceed four ranks.
Neither path establishes a faithful memory boss encounter.

`Dd1MonsterView` and `Dd1SkillData` can supply DD1 enemy sprites and translated ordinary attacks.
`Dd1SkillToDd2` supports basic damage, healing, movement and token/DOT effects, but omits several
boss mechanics. Its `Pair` method can also omit DD1 skills when the borrowed actor has fewer skills
of that kind. A boss adapter must account for every required skill and encounter component explicitly.

`Dd1Backdrop.Compose` currently reads the expedition's original region. A memory needs its own
presentation destination while retaining the original quest, map, objectives and native player actors.
Ordinary `Crawl.ResolveBattle` marks the current expedition fight resolved; it must not resolve the
main quest boss or award normal quest progression when completing a separate memory.

## Regional bosses

Installed `dungeons/<zone>/*.mash.darkest`, `monsters/<family>/<family>_<tier>/*.info.darkest`,
their animation/art files and `effects/base.effects.darkest` supply the first regional audit.
The table lists native dependencies to investigate, not completed DD2 implementations.

| DD2 region / DD1 scenery | DD1 boss family | Required encounter work beyond ordinary attacks |
|---|---|---|
| Sprawl / Ruins | Necromancer | Summons with native weights, capacity/ranks, advancement and no summon loot |
| Sprawl / Ruins | Prophet | Pews, rank warnings, delayed rubble targeting and extra targets |
| Foetor / Warrens | Swine Prince | Wilbur, mark targeting and retaliation/AI decisions |
| Foetor / Warrens | Flesh | Four changing components with shared health and component skills |
| Tangle / Weald | Hag | Empty/full cauldron, captured hero, release and two actions per round |
| Tangle / Weald | Brigand Pounder | Fuseman-driven cannon action, misfire and reinforcement summons |
| Shroud / Cove | Siren | Hero control/return, aquatic summons and two actions per round |
| Shroud / Cove | Drowned Crew | Anchor summons, linked companion buffs, restraint and healing |

The apprentice Necromancer is the first adapter candidate: one body, ordinary attacks plus a
summon effect. This is an implementation order, not a settled boss-selection/randomization policy.
No memory boss is yet declared ready. Existing Flesh component mappings alone do not implement Flesh.
Mountain/final and other enabled destinations still need explicit matching memory encounters before
the promised one-curio-on-every-boss-quest feature can be enabled.

Native evidence: private `EffectTarget::GetTypeFromId@1404e6c00` reads summon count, candidates,
weights/ranks/limits, initiative and summon-loot flags, plus capture/control fields. Installed
`NecroSummon 1` requests one weighted skeleton and disables summon loot; the current translator
does not handle those keys. This proves a missing data contract, not a working runtime summon.
Unity's `MonsterBrain` and raid saved-location code are additional references; native DD1 takes
precedence for mechanics/order. Continue through callers/consumers when implementing each mechanic.

## Hero presentation

The installed DD2 CSVs contain 15 hero-tagged class IDs, including Bounty Hunter. Runtime recruitment
eligibility is a separate library rule. Thirteen have same-name DD1 combat skeletons/atlases, defend
poses and attack poses: Abomination, Bounty Hunter, Crusader, Flagellant, Grave Robber, Hellion,
Highwayman, Jester, Leper, Man-at-Arms, Occultist, Plague Doctor and Vestal.

Flagellant's campaign art is under `dlc/580100_crimson_court/features/flagellant/heroes/flagellant`.
Exclude Butcher's Circus copies when finding campaign art. Duelist and Runaway have no namesake
DD1 art here; their visual counterparts/generated sprites remain to be defined. Do not inherit a
camping-cost fallback as a visual mapping. This audit checks files, not rendered poses, transformations
or DD2 skill-animation matching. Player skills/stats/loadouts must remain unchanged.

## Rewards

Installed base trinket entries contain 24 `very_rare`, 9 `ancestral` and 5 `ancestral_shambler` items.
These are base-file counts, not an agreed DLC-inclusive eligible pool. The owner selected `very_rare`
for Legendary. Ancestor inclusion, unique ownership and other open policies remain in the design.
`Dd2Catalog` currently maps DD1 rarity requests to DD2 trinkets, so it cannot supply the promised DD1
rewards. A DD1 identity/effect/equip/art adapter is required; a DD2 trinket with a DD1 label is insufficient.

## Saved return position: round176

`ExpeditionState.FadedMemory` is optional and absent from older saves. Its initial record retains
identity, destination boss/difficulty, per-hero sprite IDs and a `MemoryReturnPosition` checkpoint.
The checkpoint captures room/hall heading, previous entrance and secret-room return coordinates.
It validates source quest/region/seed and destination coordinates before restoring any field.
It changes only location, retaining current party condition/deaths, skills/equipment, inventory,
quest progress and RNG. Entry gates, fight recovery, victory/reward state and live routing are next
slices; this low-level position helper alone does not make a memory encounter playable.

Twenty-one tests cover real Crawl room/hall/reverse/secret travel, save/reload, retained combat
consequences and loadouts, secret exit after return, repeat restoration, rejected foreign/invalid
destinations and legacy saves. Native memory transitions remain unverified because none is exposed.

Private audit output and input hashes are at
`C:/Users/Piral/rea-workbench/evidence/round176/memory-coverage.json`. It includes 80 boss-tagged
records across 27 families, including companions/props; that is not 80 distinct boss encounters.
Raw catalogue and analysis evidence stay outside Git.
