# Expedition stress and resolve

Round255, 2026-10-16. Built and checked offline; native gameplay acceptance is pending.
The owner explicitly requested keeping DD2 closed. Do not launch it until the owner lifts that hold.

## Rules

| Condition | Result |
|---|---|
| No condition, reaches100 | DD1 resolve test; the crossing hit caps at100 |
| Affliction | One of the seven generated DD1 afflictions; remains until stress reaches0 |
| Virtue | One of the five DD1 virtues; stress becomes45, as in the installed DD1 rules |
| Flagellant reaches100 | Native DD2 Toxic self-effects; stress remains100 |
| Afflicted/Toxic reaches200 above0 HP | Heart attack takes the hero to Death's Door |
| Afflicted/Toxic reaches200 at Death's Door | Immediate death, without a deathblow-resistance roll |
| Virtuous reaches200 | Lose virtue, stress becomes0; no heart attack |
| Return to Hamlet | Cap stress at100, clear virtue, retain affliction/Toxic until cured at0 |

The resolve roll reads DD1's base75% affliction chance, effective-difficulty modifiers,
installed5%/99% clamps, generated/class filters and trait-history weights. Native DD2
`overstress_chance_modifier/resolute` bonuses improve virtue chance. Afflictions are
Fearful, Paranoid, Selfish, Masochistic, Abusive, Hopeless (`depressed`) and Irrational;
virtues are Stalwart, Courageous, Focused, Powerful and Vigorous. Flagellant always gets
Toxic instead of rolling either pool.

The native DD1 heart-attack survivor consumer relieves `30 * (1 + modifier)` stress.
The bridge currently supplies the base zero modifier, leaving170 stress. The exact
native actor field's modifier semantics have not been mapped to DD2. Do not replace
this with the Unity port's150 or a remembered wiki value without further evidence.

## Compatible storage and ownership

Existing saves use ten DD1 stress per native unit: legacy7 still means70. The scale is
unchanged; persistent records now accept fractional units. This preserves small travel,
camp and curio amounts instead of probabilistically rounding them to whole DD2 pips.
Stress damage rounds at a whole DD1 point after modifiers. Native attacks keep their
original strength: a native1-stress attack deals10 displayed stress.

`ResolveStress` owns rules and parsed trait data in Core. `Dd1Stress` binds only the
actors built from expedition hero records. Those actors have a200-stress cap and do
not enter DD2's normal meltdown/resolute queue. Unbound actors keep their native rules.
Initialization restores raw condition before enabling reactions, avoiding load-time
damage, heal refusals or a rerolled break.

`ResolveState` is copied through actor captures, expedition snapshots, save JSON and
homecoming outcomes. It includes trait/history, virtue state and Toxic effect counts.
Condition changes persist immediately when a real selected save path exists. No estate
was opened, read or written during this offline implementation or verification.

An interrupted native battle still restarts HP, inventory spending and travel treatment
from its checkpoint. Current resolve condition/history and stress survive that restart;
heart-attack deaths stay dead. Legacy checkpoints without resolve state retain their
former rewind behavior. Ordinary mid-fight DD2 deaths retain that existing behavior.

## Toxic

The initial break applies DD2's actual Toxic effect definitions, excluding only
`toxic_stress`. Native path conditions still choose the HP adjustment, blight and
cooldown effects. The bridge keeps virulent-blight/manager tokens and native Toxic
DBR/Scourge buff counts through combat-end cleanup and actor recreation.

Restoration reinstates ongoing tokens/buffs only. It never replays onset HP changes or
cooldown resets. Toxic remains active below100 and between fights until stress reaches0;
returning to town does not silently remove it. Reaching100 again while already Toxic
does not trigger a second onset;200 still triggers the DD1 heart-attack branch.

## Trait effects and presentation

DD1 trait buffs use the existing DD1-to-DD2 stat adapter: damage, HP, speed, crit and
resistances map to native stats. DD2 has no DD1 accuracy/dodge stats, so their existing
crit/damage-reduction adaptations remain. Combat math and animations otherwise stay DD2's.

The bridge implements weighted turn-start acts, pass/random-command selection,
self/friendly attacks, move/mark, stress/heal and ally/party buffs. It also implements
hit/miss comments, health-heal and retreat refusals, supply refusals, camping meal/skill
refusals and exact named-effect blocking. A virtue's `block_effect` applies specifically
to `BarkStressHeal`, not to every stress heal. Random-command acts with no valid skill
target fall back to passing instead of locking player input.

Campaign HUD, roster, results and native status tooltips display0–200. Ten pips gain a
second layer above100. Native floating stress numbers are rescaled at presentation;
the underlying event amount remains compatible for other listeners.

DD1 curio compulsion/loot theft, generic combat buff/item refusal and player-directed
move refusal are not yet connected. Their trait metadata is loaded, but they must not
be reported as verified trait parity. Native act-out animation/bark timing also remains
an adaptation using announcements rather than DD1's original presentation.

## Evidence and acceptance

Private evidence is in `local-art/stress-system-v01`, outside Git:

- Installed DD1 rule/trait data and DD2 assembly SHA256 inputs.
- Native DD1 stress/resolve consumers: `14047bfb0`, `14049eef0`, `1404a4050`,
  `14047ef50`, `140480bf0`, `14047c480` and `14047c6d0`. No decompiled code is shipped.
- Frozen425-file source, stress-only overlay and fresh four-artifact rollback.
- Release build,1,242 Core tests and221 UI tests passing.
- Metadata validation of18 native method targets and6 reflected fields.
- Four installed DLL/PDB hashes; unchanged config,13 other plugin files and190 private
  scenery/model files matching the round254 baseline.

The installed build preserves round254's Hamlet loading/cache work, current corridor
and room art, cadence and installed hero models. Shared corridor and Shieldbreaker
work in progress is excluded, including unrelated hunks in `CrawlUi`.

After the owner permits launching DD2, use an unsaved fixture first. Check each100-point
outcome, a second100-to200 climb, both heart-attack HP branches, virtue loss, cure at0,
Toxic retaliation/Scourge across two fights and reload, initialization without onset
replay, native turn input and updated tooltips/pips/pop numbers. Then check town return
and the exact saved expedition using only an authorized test estate. All game-facing
claims remain `[?]` until those scenarios are observed.
