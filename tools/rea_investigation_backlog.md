# REA investigation choices

Owner request, 2026-10-06: use REA for difficult DD1-to-DD2 systems, provide a list,
then let the owner set priorities. These IDs are stable discussion references.
They are investigation candidates, not claims that native DD1 rules were recovered.
For setup, skill loading and command examples, use [the REA usage guide](rea_usage.md).

Update 2026-10-08: the owner prioritized a complete base-game quest lifecycle map and its implementation in DD2.
Use [the quest parity map](quest_parity_map.md). R1-R10 remain reference IDs; work no longer waits for their ranking.

## Trial result

REA 4.0.1 is installed at `C:/Users/Piral/rea-workbench` with npm lifecycle scripts
disabled. Its direct CLI works without changing agent configuration.

The installed DD2 `IronCrown.dll` was inspected with
`inspect-managed-artifact` and `inspect-managed-members`. REA observed Unity Mono,
AnyCPU/CIL, 4,721 type definitions and 33,158 method definitions. Metadata coverage
reported complete, with no issues. Selected CIL bodies are present for
`StressManager.ProcessOverstressedActor`, `RunBhv.StartRun` and
`ActorDataEffectDescription.GetDescription`. This confirms usable static access,
not runtime behavior, latency or safe hook timing.

The installed assembly and the existing compilation reference have the same
SHA-256, `dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6`.
The source assembly hash was unchanged after analysis. Build-local tokens must
remain tied to that digest and MVID `c8627dd8-3822-4311-9403-8920d40a2892`.

Private evidence is outside Git:

- `C:/Users/Piral/rea-workbench/evidence/dd2-managed-artifact.json`, Evidence
  `ev_57d1a0fc097bb445b6b3b78d4052c6d1a110251c47095307b1c27438abb3de93`.
- `C:/Users/Piral/rea-workbench/evidence/dd2-managed-members.json`, Evidence
  `ev_790fdee0e6fe7a5824d9eb3d20e6c8f1a0733ff9d59b34b8961d6e0c0787440d`.
- `C:/Users/Piral/rea-workbench/evidence/dd2-selected-member-summary.json` and
  the local summary helper preserve selected entry points without rerunning REA.
- `C:/Users/Piral/rea-workbench/evidence/dd1-native-readiness.json` records the
  failed native provider admission. No DD1 function analysis occurred.

DD1 native analysis is not ready on this host. Selecting the installed
`_windows/win64/Darkest.exe` with `inspect --provider ghidra` returned
`provider_unavailable/not_configured`. Ghidra and a full JDK are absent. The current
official Windows guide also explicitly states that published npm 4.0.1 lacks the
Windows native bundle. Adding MCP registration would not supply that bundle.
Native work needs a compatible verified bundle, Ghidra 12.1.4 and full x64 JDK 21,
or a separately validated supported analysis host. Do not bypass provider controls.

Sources: [Windows guide](https://raw.githubusercontent.com/morluto/rea/main/docs/windows-ghidra-p0.md),
[managed analysis](https://github.com/morluto/rea/blob/main/docs/managed-code-analysis.md),
[installation](https://github.com/morluto/rea/blob/main/docs/installation.md).

## Update 2026-10-06: DD1 native analysis works through the code map

The owner asked for a full DD1 map first. Ghidra 12.1.4 now analyses `Darkest.exe` directly, without REA; see
[tools/dd1re/README.md](dd1re/README.md). The map is private, under `D:\dd1-decomp`. First answers, in our words:

- **R1, answered except two details:** `Roster::System::OnRaidFinish` ages every quirk by one at each quest end
  ("turn" means a finished quest). It then locks eligible negative quirks (age >= 2, roll < 0.25) until 3 are locked.
  Still to confirm: the candidate filter and the quest-results condition that gates the block. PARITY has the full rule.
  The same function holds the quest-end quirk and disease work (R2, R4); the disease keys load into globals
  `disease_after_quest_*` / `disease_max_chance`, which the full map lists.
- **R3, resolved:** DD1 applies no dismissal stress. The exe never references `dismissed_hero_stress_penalties`,
  and the dismissal event listeners are achievements, building slots, a counter and the UI. The mod already matches.
- **Found on the way:** the surprise roll is shaped differently from the mod's, and the exe never reads `meals_table`.
  Both are in PARITY.

## Choices

Difficulty estimates describe implementation after the rule is understood;
native investigation time remains uncertain. The suggested order is advisory.

| ID | System | What we need to establish | REA value / route | Implementation scope |
| --- | --- | --- | --- | --- |
| R1 | Negative quirk locking | Mostly answered by the code map (see the update above); left: candidate filter, gating condition, age reset on replacement | Code map | High, per-quirk persistence and return processing |
| R2 | Post-quest disease | Exact stress/resistance formula, resolve eligibility, retreat treatment and disease selection | High, DD1 native; currently blocked | Medium/high, return roll and DD2 disease/resistance mapping |
| R3 | Hero dismissal stress | Resolved: DD1 never reads the key, so there's no dismissal stress; the mod matches | Done | None |
| R4 | Post-quest quirk gains | Positive/negative roll order, stress interpolation, success/failure behavior, weighted choices and incompatibilities | High, DD1 native | High, native candidate pool and once-only gains |
| R5 | "Never Again" veterans | Per-hero completion flag, strict/permissive entry rules and veteran party XP stacking | Medium/high, DD1 native for hidden state; visible option values already available | Medium/high, save migration, embark restrictions and XP |
| R6 | From Beyond resurrection | Which original hero fields survive, reset or are reconstructed | High, DD1 native | Medium/high, preservation policy; event already implemented |
| R7 | Afflictions and virtues around DD2 combat | Where to adapt DD1 resolve outcomes into DD2 overstress, effects and presentation without duplicate processing | Medium, DD2 managed plus DD1 native for unresolved rules | High, combat-adjacent behavior and persistence; owner design choice |
| R8 | Remaining trinket descriptions | Advanced trigger targets, conditions and recursive effects missing in cold Hamlet tooltips | Medium, DD2 managed exact-build validation; readable decompilation already exists | Medium, one supported description gap per round |
| R9 | Combat startup and first-load delays | Readiness/event order versus art decoding, upload or repeated scans; identify the measured bottleneck | Medium for DD2 call-path inspection; timing still requires a permitted runtime trace | Medium/high, measured changes rather than speculative optimization |
| R10 | Stagecoach, minimap and loot modal interactions | Verify native event delivery, scaled windows, overlapping controls and movement behind loot | Low for DD1 binary analysis; actual Unity input/render checks are more useful | Medium, existing fixes need native checks and remaining modal gaps need reproduction |

R3 is the smallest native pilot. R1, R2 and R4 likely share a return-processing
entry path and are worth investigating together, while implementing each rule
in its own tested round. R5 and R6 share persistent hero-state questions.
The owner can prioritize any ID, including the immediate UX/performance items.

## Evidence and boundaries by system

DD1 paths below are relative to
`C:/Program Files (x86)/Steam/steamapps/common/DarkestDungeon`.
Unity port paths are relative to
`C:/Users/Piral/csharpdd/Darkest-Dungeon-Unity/Assets/Scripts`.
Mod paths are relative to `C:/Users/Piral/DarkestDungeon3/mod`.

### R1: negative quirk locking

`shared/rules.json` defines `quirk_negative_locked_after_turn_count=2`,
`quirk_chance_to_lock_negative=.25` and `quirks_max_locked_negative=3`.
Unity `Character/QuirkInfo.cs` stores `Longetivity`, but the inspected script tree
did not supply its advancement/auto-lock rule. Core `Campaign/HeroRecord.cs`
stores IDs and locks without per-quirk age. Trace reads of the native rule keys,
then age mutation, eligibility, roll and lock insertion. Do not interpret “turn”
as a week, quest or combat round without executable evidence.

### R2: post-quest disease

`shared/rules.json` gives minimum resolve 2, minimum chance .05, maximum .32 and
resistance weight .33. `shared/quirk/quirk_library.json` supplies disease choices.
Unity `Character/Hero.cs` can add a disease; inspected callers do not establish
the return formula. Core `Campaign/Homecoming.cs` copies native actor conditions
and awards XP but has no independent DD1 disease roll. Recover arithmetic and
ordering before adding any formula. DD2 condition IDs/resistance must be mapped
explicitly; static inspection alone cannot validate live application.

### R3: dismissal stress

`shared/rules.json` has `dismissed_hero_stress_penalties` with upper levels
4/12/1000000 and penalties 5/10/20. Unity `Campaign/Campaign.cs:DismissHero`
only removes the hero and returns its roster ID. Core
`Campaign/Town/Hamlet.cs:Dismiss` also preserves achievements and returns trinkets,
but applies no stress. Trace the rule-table consumer and recipient filter.
Confirm how 5/100 stress maps into the mod's DD2 stress representation rather
than silently rounding it away.

### R4: ordinary post-quest quirks

`shared/rules.json` contains all eight `negStress*/posStress*Success/Failure`
endpoints; the quirk library contains weights and incompatibilities. Core
`Campaign/Homecoming.cs` reports gains/losses by comparing actor snapshots;
`Runtime/Driver.cs:FinishExpedition` passes those outcomes directly. Neither
establishes a DD1 post-quest roll. Existing `QuirkLimits` helps enforce caps but
does not define the candidate pool, interpolation, RNG order or locking order.
The earlier combined “quest and curio gains” parity entry was too broad.

### R5: Never Again

`campaign/roster/roster.settings.json` explicitly supplies strict refusal and
permissive entry with minimum stress 80 and `never_again_affliction`.
`shared/buffs/base.buffs.json` supplies that penalty and
`completed_darkest_dungeon_quest_party_resolve_xp` at +.5. Core currently records
estate plot completion, not a per-hero veteran flag; `Campaign/Embark.cs` checks
availability/resolve, not veteran refusal. Native analysis is most useful for
stamping, qualifying quests, stacked mentors and resurrection interactions.
Legacy saves cannot recover unknown historic participants by assumption. Which
mode/entry policy to use remains an owner's design choice.

### R6: From Beyond

`campaign/town_events/base.town_events.events.json:dead_recruit` specifies week 15,
three dead heroes and three offers, not the restoration policy. Core
`Campaign/Town/Hamlet.cs` already offers graveyard HeroRecord objects and recruits
one by clearing dead/from-graveyard/cause fields. Round 149 preserved recorded
fallen quirks. Unity `Campaign/Town/DeathRecord.cs` contains memorial metadata;
`Character/Hero.cs` reconstructs from it and rerolls quirks. That port behavior
does not prove DD1's original preservation rules. Trace native resurrection
construction and field resets before changing the implemented event.

### R7-R10: DD2 adaptation and runtime checks

The REA member inventory confirms CIL for `StressManager.ProcessOverstressedActor`
and existing DD2 decompilation shows weighted class overstress selection followed
by pending-overstress assignment. This supplies a candidate integration seam for
R7, not proof that replacing it is safe or the owner wants a full DD1 combat model.
Native DD2 combat remains the standing design.

For R8, the last recorded audit is 182 complete, 8 partial and 1 blank of 191.
Core `Dd2Data/TrinketDescriptions.cs` is the cold formatter; DD2
`ActorDataEffectDescription` and `ConditionDescription` define the native route.
REA can tie their CIL/signatures to the shipped assembly and compare updates.
It does not remove the existing hidden-condition rule or the need for a correct
fixture before revisiting the reverted round-75 attempt.

For R9, `RunBhv.StartRun` and `GameTypeMgr.OnGameTypeStarted` are observable managed
entry points. Existing art workers and host-readiness guards are implemented.
Static call graphs cannot measure Unity loading, GPU uploads or actual delays.

For R10, rounds 153/154 fixed headless stagecoach release fallback and map labels;
155-158 improved loot reachability, capacity and required-item protection.
They remain unverified in native Unity. Movement/input behind modal loot is a
separate documented candidate. Do not describe these tested fixes as native
symptoms conclusively resolved, or as missing DD1 rules requiring REA.

No game was launched, protected estate read, abandoned project accessed or native
art changed. Raw binary/CIL evidence remains outside Git. Next implementation
round is 159 after the owner supplies priorities.
