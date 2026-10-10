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

Round178 adds `Dd1HeroArt` and a presentation-only `Dd1MonsterView.PrepareHeroes` entry. It loads
skeletons/atlases from the campaign hero's anim directory and page images from outfit A; campaign
Flagellant works without borrowing the multiplayer copy. All 13 namesake classes produce nonempty
combat/defend/skill poses in installed-data tests. Missing classes reject rather than borrow another
hero. Shared DD2 skill names, including mastery/path suffixes, select their corresponding DD1 pose.
Unmatched new skills currently retain the combat pose; explicit visual equivalents remain to define.

Players bind by exact GUID and never pass through enemy name/skill/AI changes. Their sprite faces
right and uses their measured model height. Native player effects remain visible. Render visibility
is remembered per renderer and restored on Clear, including parts loaded after binding. Hero sizes
do not participate in the existing enemy scale median. No normal gameplay caller enables this yet.

Private native estate2 check shows Vestal, Flagellant, Jester and Leper as DD1 sprites (screenshot01),
then restores their DD2 models (screenshot04). Class/name/max-HP/equipped-skill snapshots are identical
immediately before/after preparation and clearing. The attempted attack click did not establish a
skill-pose acceptance result. Live attack/defend/death timing, remaining classes and actual memory
exit routing remain unverified. Twenty-four new cases bring the suite to 992 tests.

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

## Single summon contract: round177

`Dd1SingleSummon` supports the Necromancer's explicit single performer effect, once on hit or miss,
with weighted candidates and its summon-loot flag. It rejects additional fields, multiple attempts,
conditional flags, malformed weights and missing actor coverage. It does not claim to cover summons
with limits, specified ranks, captures or custom initiative rules.

Private native `EffectApplyInternal@1404bc4f0` builds its candidate pool, rolls cumulatively at
`1404bee20`, then checks the selected class against usable ranks at `1404bf13e`; the attempt counter
advances at `1404bf0e0` even when the class cannot fit. The tested helper therefore spends a roll on
a full formation and never rerolls an oversized selection. Unity's SummonMonstersEffect instead
removes oversized candidates and retries, so it is not the authority for that edge case.

DD2's native SummonController/EventSummonQueueActor supplies front placement and actor creation.
Unplaced queue entries can persist unless removeAfterProcess is true. Team.OnQueueSummon replaces
eligible actors before processing, so a bridge must still establish DD1 corpse eligibility, skill
completion timing, dynamic sprite binding, no-loot handling and turn-order behavior. The helper
accepts usable rank capacity from that future bridge; it does not infer corpse rules.

Thirty tests check all three installed tiers and every Necromancer skill, selection boundaries,
capacity failure/RNG consumption, fresh pools and fail-closed unsupported effects. No runtime caller
or playable memory boss is added in this round. Native summons remain unverified.

## Independent enemy definitions: round179

`Dd1EnemyKit` reads exact-tier numbers, complete attacks and life-link metadata. The first runtime
factory accepts only apprentice Necromancer/common/militia skeletons, rejects missing mechanics
and unsupported dodge, and generates independent class/stats/skills. Protection uses DD2's direct
damage-received stat; DD2 retains its accuracy engine. Private ResourceActor/ResourceSkill clones
carry the generated IDs and complete starting skills. Only the exact CreateActor(string) overload
routes registered IDs through those clones; shared actors/resources and generic getters stay intact.

Native estate2 probe PID18912 shows the three DD1 enemies, Necromancer105HP/speed8 and all three
attacks, skeleton8/10HP and their complete attacks. Original Bishop resource/class snapshots match
before and after actor creation. Screenshot02 shows the boss and skeleton sprites, proper boss name,
Six Feet Under banner and105/105HP. Summoning is intentionally not connected yet. Random controllers
are loaded; the skeleton brains' marked-target preference still needs coverage. Their installed
life_link.base_class=necromancer, corpse and death rules must be connected before enabling the fight.

Eleven cases bring the suite to1003 passing tests. The temporary probe and estate2 main/.bak were
restored and hash-checked after exit; normal Release was rebuilt/deployed with the game stopped.
Private evidence is under rea-workbench/evidence/round179. No normal caller exposes the factory yet.

## Native summon bridge: round180

Prepared enemy skills now use DD2's once-per-result finalization, independent of target count or hit
results. Candidate coverage is checked before RNG; the SUMMON channel rolls from the full installed
pool, including on a full formation. A successful attempt queues one front actor, removeAfterProcess,
without a deferred spawn. The factory rejects loot-bearing summons rather than ignoring that flag.
Fresh classes have no inherited native loot. Exact created GUIDs add sprites without clearing the
existing party/enemy presentation. No normal encounter registers/activates these enemies yet.

Private native estate2 probe PID18404 starts with one boss; real attacks grow the enemy formation
1->2->3->4, with new skeletons at position0 and DD1 hero sprites retained. The following attack spends
its attempt with0 free ranks and creates no fifth actor. Screenshot03 shows all four enemies during
the all-target stress attack. No exceptions found. All1003 tests and Release pass; estate2 main/.bak
and probe sources are restored/hash-matched, normal Release rebuilt/deployed with the game stopped.

Corpse replacement is deliberately still unimplemented here. Installed corpse_A.can_be_summon_rank
and Unity AvailableSummonSpace confirm that corpses can supply ranks, unlike the current free-rank
check. Zero native add-to-turn-order uses next-round timing; Unity's absent summon initiative field
also selects no initiative roll, but exact native DD1 timing remains to confirm. Actual miss, retreat,
reload, linked deaths and corpse behavior need acceptance before enabling the complete encounter.

## Linked skeleton death: round181

Private DD1 MonsterClass loader stores life_link.base_class at0xeb8. Callback1405f69a0 matches the
configured base-class identity against its notification and sets the dependent's0x1504 flag; readers
include monster/captor lifecycle code. Unity additionally checks whether the formation retains the
linked base class. DD2's native m_DeathChainIds matches same-team death events and emits CHAIN kills.
The scoped single-Necromancer encounter now links skeletons to that prepared same-tier actor ID,
registering the anchor before native class validation. Chain loot and chain corpse are absent.
Multiple anchors and other bosses are outside this adapter's supported contract.

Two cases bring the suite to1005 passing tests. Native probe1/PID5664 receives three CHAIN death
events (two initial skeletons and one summon) and removes those views. Direct Kill left boss HP at105
and did not establish fight completion; do not count screenshot01 as a successful lethal kill.
Probe2/PID28292 used lethal ApplyHealthDamage with placeholder source ID probe181. AchievementsMgr
requires an actual skill ID for that event and throws before the remaining death listeners run.
This is a probe setup failure, not evidence of successful victory. Native full lethal kill/return
remains pending after the two-attempt timebox. Use a real equipped skill ID in the next death-flow
acceptance test. Nonliving performers are now also rejected by the summon bridge.

Both games stopped by exact PID; estate2 main/.bak and probe sources restored/hash-matched, normal
Release rebuilt/deployed. Private attempts/logs/screenshots stay in rea-workbench/evidence/round181.

## Independent corpses and replacement: round182

Installed corpse_A defines7HP, zero turns/skills, three-round lifetime and can_be_summon_rank=True.
Native MonsterClass stores that flag at0xf91; EffectApplyInternal's capacity branch excludes eligible
actor sizes, matching Unity AvailableSummonSpace. Dd1EnemyKit retains corpse/death metadata; the
scoped factory creates private corpse class/stats/resource and links skeleton death_class to it.
DD2 m_IsSummonReplacable and the existing bounded front queue consume that rank. Corpse class-change
resets native round count; m_DeathRound=3 supplies the lifetime. Existing crit/DOT veto is retained.

Native probes PID29684 and28876 both show skeleton death changing the same GUID into the private
corpse,7/7HP and replaceable=True, then a real boss attack replacing it with a fresh front skeleton.
The dead-pose log is present and the board returns to four living enemies. Two cases bring the suite
to882 Core +125 UI =1007 passing tests. An unreplaced corpse's exact expiry remains native-unverified.

The first probe also reduces boss HP below zero using a real source skill ID and observes all three
linked skeletons become nonliving/remove. That direct damage call bypasses standard skill presentation,
so the boss remains onscreen. DD2 CombatPresentationBhv handles standard SKILL deaths through skill
results; a direct call is not a complete lethal attack. The second harness tries native skill/target
selection but never finds a valid equipped damaging attack at boss rank4. Full victory/return remains
pending after two attempts; do not count either as completed Faded Memory. Log per-skill validity and
use a reachable boss rank for the next native acceptance test. No exceptions were found in either log.

Both exact PIDs stopped/waited; estate2 main/.bak and both probe sources restored/hash-matched.
Normal Release Rebuild/deploy completed with DD2 stopped. Private evidence stays outside Git under
rea-workbench/evidence/round182; the initial hidden boot PID30128 was stopped before any scene test.

## DD1 trinket identities: round183

Dd1Trinkets reads the installed base entries at runtime, retaining all seven native entry fields:
id, rarity, buffs, hero_class_requirements, price, limit and origin_dungeon. Definitions and lists
are read-only; duplicate IDs, incomplete fields and unknown entry fields reject the catalogue rather
than silently substituting items. Exact rarity queries remain separate and deterministically ordered.
This does not select reward weights, ownership filters or DLC inclusion.

The installed file has490 entries, with24 very_rare,9 ancestral and5 ancestral_shambler. Ancestor's
Pistol retains ranged accuracy, speed and stress buff references plus limit1; Legendary Bracer is
very_rare with limit0; Sacred Scroll retains its Vestal restriction. Ten cases bring the suite to
892 Core +125 UI =1017 passing tests, with Release deployed while DD2 stopped.

At round183 this reader had no gameplay caller. A complete effect adapter must retain and evaluate buff
conditions; the existing Dd1Buffs loader only captures scalar stat/duration fields and is insufficient
for conditional trinket effects. DD2 has no direct accuracy stat, so these references must not be
advertised as already applied. Identity/effect/equipment/art, two retained draws and atomic grant
integration remain required before the curio can be exposed. No native data files enter Git.

## First playable encounter: round184

Crypts/Sprawl currently supports apprentice Necromancer only. Separate entrance-room placement retains
ordinary/required props; Ctrl+F6 places the same curio in an unblocked estate2 room for testing.
Click the mirror, select a living hero, and choose Confront the past. Hand entry persists fighting
state, return position, exact hero sprite mapping and both reward draws before native combat starts.
Saved fighting state reuses the existing native fight checkpoint; return restores location only.
Victory retains two concrete DD1 trinkets in the pack/report without completing the ordinary boss quest.
Retreat spends the memory without rewards; wipe uses the ordinary expedition-loss path.

Defaults chosen within the owner's autonomous implementation scope: uniform base very_rare24 and
ancestral9 pools, no ancestral_shambler/DLC; prefer unowned limited items, allow a duplicate only when
that pool is exhausted to preserve two rewards. Duelist uses Grave Robber art and Runaway Houndmaster
art; player class/stats/skills remain DD2. These policies can be refined independently.

Trinket buff rule metadata is retained. Native equipment receives supported scalar/ranged/melee/rank/
Death's Door effects and stress modifiers. Tooltips label explicit DD2 adaptations (accuracy to crit,
dodge to damage reduction, virtue to Resolute, Eldritch to Cultist), unsupported rules remain inactive.
Trap/scouting/surprise bonuses are integrated in Core. XP and unsupported legacy mode rules remain
pending; native conditional equipment/icon coverage is not yet accepted. Real DD1 inventory art is
loaded locally. Generated mirror art is private in the save cache; no game-derived art enters Git.

All906 Core +125 UI tests pass, Release rebuilt/deployed. Native estate2 observes the hand encounter,
DD1 hero sprites, Necromancer and summons. F10 debug victory returns two actual items and leaves
QuestComplete false; this is not actual hero-skill lethal presentation acceptance. Owner reported
oversized heroes/boss and the wrong room; round185 addresses those before regional expansion.
Estate2 main/.bak and temporary source probe restored/hash checked; no protected slot accessed.

## Boss scenery, proportions and real skill return: round185

Native DD1 FUN_1407544f0 prefers zone.final_room_wall.plotId.png then zone.final_room_wall.png;
FUN_1407530e0 loads the room variations. Unity DungeonGenerator.LoadRoomEnvironment assigns an
ordinary variation to boss rooms; RaidRoom loads that texture. Installed crypts has no Necromancer
final-room file. The mod explicitly chooses crypts.room_wall.library.png when dedicated final art
is absent, and always composes a room independently of the source room/hall/entrance. ZoneArt also
accepts explicit boss/tier/family art before generic final art for future coverage. No claim that
the chosen library is a separate original Necromancer asset. Four tests verify selection and real files.

Heroes use0.68 of native model-bound calibration; only memory Necromancer uses0.85 of monster scale.
Minions/corpses/ordinary fights retain existing sizes. First native boss300px was too small for the
owner; revised check gives417px, summon242px, heroes approximately250px, with camera zoom retained.
These are presentation sizes, independent of DD1 rank footprint.

Two native attempts. The first was interrupted by owner F10 and excluded. The second disables F10
only in its private probe, arranges Leper/Flagellant/Jester/Vestal ranks, lowers boss HP to1 as a
fixture, then uses native skill selection and a delayed target event. Chop misses; Judgement kills.
Native death presentation removes Necromancer, linked militia dies/removes, victory exits combat,
and the controller returns both DD1 trinkets with ordinary QuestComplete false. No direct damage,
Kill or death event substitutes for that skill result. Full-health balance is still untested.

All910 Core +125 UI =1035 tests and normal Release Rebuild pass. Exact PIDs stopped, estate2 main/
.bak and DebugKeys restored/hash checked; normal DLLs rebuilt/deployed with stopped game. Private
evidence in round185 and round185b. Native restart, retreat, unreplaced corpse expiry, conditional
equipment and additional bosses/tiers remain required before claiming global feature completion.
