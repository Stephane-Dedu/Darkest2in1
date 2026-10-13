# DD2 heroes and campaign Paths

The Hamlet recruits all 15 hero classes in the installed DD2 expedition ruleset. DLC classes appear only when
their owned ResourceActor is available. Bounty Hunter is a permanent recruit, following the owner's campaign
direction. Existing weekly offers stay intact; newly available classes enter the next normal coach refresh.

## Playing

Open the Guild, select a roster hero and choose **Choose Path**. Selecting a Path previews its effects and skill
changes without spending gold. **Follow** confirms the choice. Specialising and retraining require Resolve 2,
an available hero, an open Guild and 1,000 gold before the existing Guild discount. Retraining happens in the
Hamlet. Resolve, equipment ranks, learned skills, mastery and the selected loadout carry over.

The default is Wanderer, except Bounty Hunter's native Professional. Default choices serialize as a null PathId,
so older saves retain their class defaults. Invalid or unavailable saved Paths use the class default in combat
and show a warning; loading a campaign does not rewrite the saved choice.

| Class | Specialisations |
|---|---|
| Abomination | Unchained, Fiend, Moribund |
| Crusader | Aggressor, Templar, Banneret |
| Duelist | Instructrice, Antagoniste, Intrepide |
| Flagellant | Maniac, Exanimate, Scourge |
| Grave Robber | Deadeye, Nightsworn, Venomdrop |
| Hellion | Ravager, Berserker, Carcass |
| Highwayman | Rogue, Sharpshot, Yellowhand |
| Jester | Virtuoso, Soloist, Intermezzo |
| Leper | Tempest, Poet, Monarch |
| Man-at-Arms | Sergeant, Bulwark, Vanguard |
| Occultist | Ritualist, Warlock, Aspirant |
| Plague Doctor | Surgeon, Alchemist, Physician |
| Runaway | Arsonist, Survivor, Orphan |
| Vestal | Confessor, Chaplain, Seraph |
| Bounty Hunter | Professional only |

This is 42 specialisations across 14 classes, plus 15 class defaults. DD1-only Shieldbreaker keeps her existing
Guild skills and has no invented DD2 Paths. Hero forms, reserve/story Paths and Kingdom overrides are not recruits.

Abomination chooses four human and four beast skills. Transform and Revert occupy the remaining native slot in
each form and stay equipped automatically. The Guild labels each form and enforces its capacity independently.
Bounty Hunter's skills are already fully trained in DD2 and do not offer nonexistent mastery upgrades.

## Native contracts

HeroPathCatalog reads the installed hero/path CSVs, including expedition and owned DLC definitions, while excluding
Kingdom. The bridge independently gates recruitment and Path choices through ActorResources.Has. CSV presence
alone does not grant DLC ownership. Dd2Tables loads DLC hero stats and skills for cold Hamlet previews too.

Learned, mastered and equipped progress always stores canonical base skill IDs. When building a native actor,
HeroSkills selects the native class default, unlocks learned and starting skills, applies mastery, then sets the
chosen ActorDataPath and equips the canonical loadout. Native SetActorPath installs effects and replacements.
Applying the Path before mastery would make base-skill lookups miss replaced skills. GetCombatBaseSkillId is the
oracle for preserving identity; GetCombatReplacedSkill takes the replacing ID.

The Guild and hero sheet resolve active skill icons and descriptions through the same replacement catalog. Before
a native run exists, a worker reads native localization, stats, targets, buffs and effects. Unsupported conditional
details explicitly say additional effects apply in combat. When native libraries exist, DD2's own description
formatter supplies full text. Cold previews are therefore useful comparisons, but some rare conditional details
are abbreviated. Combat always uses the actual native effects rather than this formatter.

The exploration HUD uses native GetCombatBaseSkillId instead of trimming `_u`, so Path replacements retain
their skill icons. It asks the native equipped-skill query to filter by current mode, keeping beast-only skills
out of Abomination's human banner and vice versa. Tooltips describe the actual equipped variant. Portrait cache
keys include the mode. Where DD2 supplies a distinct combat portrait but no colour override, the HUD uses that
native combat portrait instead of silently retaining the human image.

Monarch's two Oath buff IDs survive DD2's actor recreation between battles. ExpeditionHeroState captures their
counts, restores them idempotently from native buff definitions and bounds them to the native 66-stack cap.
Fight checkpoints deep-copy this state, so an interrupted battle replays from the pre-fight Oath. Returning to
the Hamlet discards it; HeroRecord never stores expedition accumulation. The audit followed applied infinite
buffs through hero skills, effects, passives and token edges. Other reachable candidates have native combat-end
cleanup. This is a narrow whitelist, not a general mid-battle serialization system.

## Verification and maintenance

Round234 uses installed IronCrown SHA256
`dd97e30f2f37e2a287633ab322b8fd18bcfe9e240903713a5c2ae00c894692e6`,
MVID `c8627dd8-3822-4311-9403-8920d40a2892`. Matching private REA managed-artifact/member evidence was reused.
The decompilation and exported game data remain outside Git.

HeroPathTests checks roster completeness against the installed selectable hero catalog, all native choices,
ordinary/mastered replacement IDs, DLC stats, ownership policy, training refusal/transactions, persistence,
two-form loadouts and Oath checkpoint rollback. RecruitClassesTests exercises normal weekly offers and permanent
recruitment for Abomination, Duelist, Crusader and Bounty Hunter. Final suites pass 1,133 Core and 188 UI cases.

With debug keys enabled, `-dd3-paths-check` creates its own transient campaign at the main menu. It opens a Guild
preview, verifies no mutation before confirmation, then waits for a real **Follow Alchemist** click. It embarks a
party preferring Abomination, Duelist, Crusader and Bounty Hunter, and checks every native Path with ordinary and
mastered skills. It also checks mode-specific HUD capacities and canonical skill mapping. All 114 actor cases
and bounded/idempotent Oath restoration pass. The fixture refuses an existing
save and never selects an estate slot. At 1600x900 the confirmation button center is client 905,688.
`-dd3-paths-actors-check` repeats actor/HUD assertions using rule-based confirmation, without OS input. This
does not count as GUI click acceptance; the full flag's separate real-click run supplies that evidence.
`-dd3-paths-visual-check` adds human -> beast -> human model/HUD checks through the native mode-change event,
then ordinary homecoming/model release and application quit. Each stage holds 15 seconds for a GPU capture.
This exercises mode presentation, not a Transform/Revert skill's combat calculation or animation timeline.

Private evidence is in `local-art/hero-paths-v01`: logs, Guild captures, native party capture, build/test outputs and
installation verification. Native creation, skill/mastery/Path mapping and Guild confirmation are verified.
Full combat playthroughs for every skill/Path, balance, controller navigation and arbitrary text/window sizes
remain separate acceptance work.

The October 13 follow-up in `local-art/hero-paths-v02` verifies both native form models, five equipped skills per
form, restoration to human and ordinary homecoming/model release. It exposed the colour-portrait fallback above.
The combat-portrait correction builds successfully; its final native visual check is pending while the separate
Hamlet task owns the shared game session. Frozen sources pass 1,134 Core and 188 UI tests, including concurrent
Hamlet changes that stay outside the hero commit.

The shutdown investigation reproduces IMainInstaller's missing callback and GameSpeedMgr's late audio access
with Doorstop disabled for one owned cold-start process. BepInEx/plugin startup is absent from that process's
Player log, and its BepInEx log is unchanged. The process was closed through its ordinary window-close request.
Identical installer stacks also predate Paths in October 11 fixtures. These are existing native shutdown errors;
the hero changes do not suppress them. Positional-audio errors after a road session remain a separate runtime
comparison. The owner's October 12 launch restriction expired before these October 13 checks.
