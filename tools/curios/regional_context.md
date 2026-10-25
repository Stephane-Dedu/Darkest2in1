# Curios must belong to their surroundings

Owner direction, 2026-10-25: region and setting take priority over copying the
DD1 object's literal silhouette. A freestanding display cabinet in an outdoor
DD2 area breaks immersion even when its painting is detailed. This supersedes
the earlier blanket preference for faithful DD1 silhouettes.

Keep the existing curio ID, encounter placement, outcomes, RNG, supplies, used
flag and save data. Adapt its visible object and presentation name to the
region. Keep item affordances readable: a key opens a lock, a shovel can force
a container, herbs treat contamination. Do not change encounter probabilities
or remove curios to hide an art problem. DD1 destinations and Faded Memory keep
their intended classic presentation.

| Region | Suitable context and materials | Locked container direction |
|---|---|---|
| Sprawl | Burned streets, fallen shop fittings, watch stores, soot, scorched oak and iron | Weathered street cache or damaged shop strongbox |
| Tangle | Abandoned military stores, encampments, overgrown field equipment, damp oak and canvas | Locked field stores or military supply coffer |
| Foetor | Farm stores, butcher equipment, grain, spoiled provisions, stained timber and dull iron | Locked farm/tool coffer |
| Shroud | Wreckage, docks, fishing equipment, salt, wet timber, ropes and barnacles | Sealed dock cargo or ship's strongbox |

Neither a room tile nor a battle implies an indoor location. Use the actual
scenery/context when available. A coffer can sit indoors or outdoors; a tall
glass cabinet, confessional or standing bookshelf needs a convincing structure
or a different outdoor interpretation.

First repair the cabinet with an existing weathered closed/used coffer and
region-specific naming, preserving its underlying interaction. Then replace
that shared interim painting with distinct native regional fittings. Do not
claim four new paintings when they share one existing body.

Audit priorities: cabinet, bookshelf, confessional, display armor and other
interior furniture. Then check fountains, torture devices and loose books for
regional placement/material cues. Ordinary sacks, crates, remains, stumps and
field equipment can work outdoors when their condition and ground contact fit.
The Old Tree, Moonshine Barrel, Knife Rack and native Dinner Cart draft batch
from round347 is offline reviewed, but native QA ended early. It remains
unaccepted and uninstalled; it is not a reason to continue literal replicas.

Acceptance must include actual region selection, both used states, matching
presentation names, correct masks/grounding and stable supplies/outcomes. A
registry entry, offline sheet or correct isolated texture is insufficient.

Round348 installs only the interim cabinet binding and the regional lookup
support. Schema 3 selects art and names by canonical curio ID plus region;
schema 2 and classic defaults remain supported. The four DD2 labels are Locked
street cache, Locked field stores, Locked farm coffer and Sealed dock cargo.
All four use the previously accepted locked-strongbox painting. Isolated
software tests and offline composites pass; in-game acceptance is pending.
The owner currently requires DD2 remain closed until explicit newer permission.

## Context audit, round349

Check the installed pack before preparing new art. The installed bookshelf
already uses the complete native ruined Sprawl bookcase, with its searched
state from round342. The proposed archive remake duplicated it and was parked.
Native source studies and DD1 reference sheets are not the installed registry.

| Curio | Current finding | Next action |
|---|---|---|
| Cabinet | Outdoor coffer interim installed348, with four region names | Distinct regional fittings; check original result text for indoor wording |
| Bookshelf | Installed native splintered Sprawl frame already fits street debris | Keep pair and scale; contextual name can change separately |
| Confession booth | Intact double-bay timber furniture and curtain still read as an indoor booth | Develop a small wayside prayer/votive place; preserve holy-water and reflection affordances |
| Suit of armor | Armored sentinel on a weathered plinth can plausibly stand beside ruined city gates | Review guard-post surroundings and native armor paint before replacing the body |
| Alchemy table | Laboratory identity needs an outdoor explanation | Adapt field-chemist supplies; keep herb/torch interactions readable |
| Loose books and scrolls | Existing native piles suit fallen Sprawl archives; other regions need a reason for records | Field journals or spoiled farm ledgers where appropriate; no blanket bookshelf swap |
| Iron maiden and torture devices | Plausible in occupied Sprawl streets, weaker in ordinary countryside | Preserve distinct dangerous affordances and review actual backdrop |
| Fountains and tombs | Outdoor civic or graveyard masonry can fit | Check backdrop, scale and materials before declaring them indoor-only |
| Dinner cart | The parked347 whole native butcher cart fits Foetor well | Keep its provenance and wait for native QA; do not regenerate it |
| DLC and legacy designs | Design scope does not establish a live encounter location | Keep inactive designs separate; do not invent spawns |

The native Altar of Hope prayer study keeps its geometry, UVs and paint. Its
floating emblem and broad offering bowl are not yet a convincing finished
confessional substitute. It needs a searched-state design and comparison with
other prayer/roadside assets. No installation349 and no game launch. Contextual
result text also needs review: a matching heading alone cannot fix a result
that still describes a cabinet or booth.

## Round350 follow-up

Cabinet result wording now follows the region through explicit `ResultTexts`
bindings. The result renderer performs an exact lookup without changing the
original report or outcome. The wording-only pack and runtime350 are installed
offline; all existing painting files are unchanged. Classic text and unknown
messages pass through. Native QA is still pending under the closed-game limit.

The native Hope altar's hovering emblem is intentional. Front-facing studies
confirmed it; do not paint a support into that model. A better contextual body
candidate is the complete `story_node_city_statue(Clone)` renderer from the
native Sprawl dungeon exterior. It reads as a defaced memorial surrounded by
books, with reflection and holy-water cleansing affordances.

The memorial candidate retains every native triangle and UV. Its searched
state moves a complete open-book assembly, including its separate page pieces.
A first geometry edit left pages behind and was rejected; a generated edit
drifted from the native paint and was also rejected. The selected native pair
is fitted and reviewed offline in a corridor and room. It remains uninstalled,
with contrast, hover, masks and actual interaction still awaiting native QA.
Do not count it as accepted new coverage or install it during this no-launch
period. The latest candidate is `pack-memorial-CANDIDATE-v02` on D:.

## Round351: native field chemistry source

The Plague Doctor story bundles contain a complete native medical trolley with
glass vessels, test tubes, stained cloth and timber/iron fittings. It is a
plausible abandoned apothecary trolley in the Sprawl. The sink and restraint
apparatus from those bundles are references, not alchemy replacements. These
native props are painted two-triangle cards; preserve their UVs and paint.
They lie in Unity's Y/Z plane and need yaw90 for the corridor-facing study.
Yaw0/180 studies were empty and rejected. Native source/alpha checks pass.

The 300px body-height trial fits the street better than340px in offline room
and corridor composites. A generated used-state edit altered the body/wheels
and was rejected. No valid pair, fitting, pack or installation exists351.
Checkpoint: `local-art/ui-overhaul/curio-regional-coherence-v04/CONTINUE.md`.
The alchemy identity lacks an accepted default pair: do not install this as a
global default merely to satisfy the current regional builder. Region-only
presentation with original classic fallback remains a future tested seam.

Owner still forbids game launch/input. An existing external DD2 process was
observed351; this agent did not launch, control, close or install into it.
That observation grants no permission. Runtime350/wording pack/config/map
remain exact,52 accepted PNGs unchanged. Native art/UI QA is still pending.

## Round355: registered native body and regional-only fallback

The unresolved trolley now has an offline fitted pair. Its large specimen jar
becomes empty and open. A built-in generated edit supplies only that jar's
surface; six planar UV pieces keep the native frame, wheels, cloth, rack,
foreground jar and other supplies on their original texture. Both states use
the same subdivision and camera. Pixel checks prove the lower body identical
between states; source geometry/paint files remain untouched. It is a native
body with a generated searched surface, not a wholly native searched asset.

The 300px body trial is registered in both states, with 16px foot padding.
Sprawl corridor and room composites are reviewed offline. Contrast against
native actors, masks, hover and actual herb/torch interaction still need QA.
Do not promote it on the strength of the offline comparison alone.

The regional-only seam is now implemented and tested in source. Explicit
`regional_only` selection and schema3 entries without default `ArtId` retain
the original fallback outside their listed DD2 regions. The alchemy candidate
binds only `dd2_city`, with name Abandoned apothecary trolley. Core and the real
loader tests cover both states, return to classic/other regions and unchanged
RNG, canonical ID and used flag. No default painting was added to alchemy.

Runtime355 and pack-apothecary-CANDIDATE-v01 are UNINSTALLED/UNACCEPTED. Current
runtime354 and its Hamlet assets remain exact; the installed 34-ID/26-pair pack
and all52 accepted PNGs stay unchanged. Never restore runtime350 from the
historical paragraphs above. Owner's no-launch/input restriction remains.
Checkpoint: `local-art/ui-overhaul/curio-regional-coherence-v05/CONTINUE.md`.

## Round356: distinct Tangle field coffer

The installed four-region cabinet still shares an ornate generated strongbox.
For Tangle field stores, the complete native camp coffer provides a plainer oak
and iron silhouette. Its native source matches the actual installed geometry,
UVs and paint. All lid islands move together; the lower box stays fixed. The
open lid's originally flat-black underside reuses native lid planks, with a
shared damp-wood grade. No generated surface is needed for this fitting.

Closed/open corridor and room composites are reviewed at the actual proposed
320px canvas width, with a common horizontal body span and foot anchor. The
opened lid naturally changes occlusion around the upper rim. Lower body pixels
below row700 in the1024px source renders are identical; all178body vertices
remain fixed. Do not claim the whole rendered body is pixel-identical at its
rim, or that every original UV is retained: six underside triangles adapt.

The candidate replaces only cabinet art for `dd2_forest`; Locked field stores
and its existing result wording retain. Classic cabinet and other regions keep
their installed paintings. Candidate identity coverage stays34;27paintings is
an artifact count. Pack/native input acceptance remains pending under the
owner's no-launch restriction. No runtime or art installation356.
Checkpoint: `local-art/ui-overhaul/curio-regional-coherence-v06/CONTINUE.md`.


## Round357: recovered cache source is not a farm fitting

Round357 recovered a complete native swine-cache body after the cached supply-depot export omitted three renderers. Adding farm bundles did NOT resolve cab-532a166fba3ea6051de280e3ff4c28ee; adding biome_cave assets/models did. The fresh actual-game export now retains swine_cache 1, swine_cache_front and swine_cache_plank, 4525 triangles with original UV/paint/transforms. The remaining missing fire renderers are outside this three-part body. All three source renderers are inactive in the generic prefab; no active encounter/default state or Foetor spawn is inferred. Source-study-v01 renders both facings; body-0 was inspected. It reads as a military trophy/loot pile with weapons, skulls and scattered supplies, not an ordinary farm coffer. It is source/reference only, not a selected cabinet replacement. No used state, fitting, pack or acceptance357. Do not regenerate this discovery or promote it based only on the filename. Next inspect actual corridor scale and choose a suitable existing identity/context, or seek a better cabinet body. Complete356Tangle and355apothecary candidates remain separate/uninstalled. Current354/Hamlet/config/accepted34IDs26pairs52PNGs/67installed targets/44foreign files remain exact. No C# build/tests/deploy/launch/input/host/estate/save/restore357. Protected estates1/3 and abandoned Documents checkout remain forbidden. No subagents. Heavy outputs D:, ordinary usage first and7-8%reserve. Latest checkpoint local-art/ui-overhaul/curio-regional-coherence-v07/CONTINUE.md.

## Round358: native Shroud dock cargo

The installed coast bundle contains a complete barnacled timber chest with
native ironwork, shell geometry and 2048 paint. It is a better Shroud cabinet
fitting than the shared ornate strongbox. The native cargo crate and common inn
crates were also inspected. The common pickup chest is a two-triangle icon,
so it is not a complete articulated body.

The new dock-cache pair retains all 282 native faces and UVs. Nine rim vertices
split at the hinge boundary; all 169 lid faces and their attached shells move
together. The 113 body faces stay fixed. Two new underside faces reuse original
plank paint where the closed native shell has no interior. No generated art.
Closed pixels match the untouched native reference exactly. Open and closed
pixels below source row650 are identical; upper-rim visibility changes naturally.

The separate 34-ID/27-painting candidate changes only `dd2_coast` cabinet art.
The Sealed dock cargo name, all regional result messages, other region bindings,
classic defaults and accepted 52 PNGs retain. Actual installed Core354 parses
the full candidate offline; 12 artifact tests pass. Corridor and room composites
were reviewed at 320px canvas width. These are offline observations, not native
shader, actor, hover or supply acceptance. No installation or game input358.

Checkpoint: `local-art/ui-overhaul/curio-regional-coherence-v08/CONTINUE.md`.
Keep this candidate separate from completed356 Tangle and355 apothecary drafts.
Next investigate distinct Sprawl/Foetor cache bodies without inventing spawns
or treating recolours as new paintings. The357 military pile remains reference
only. Owner permission is still required before launching or driving DD2.

## Round359: a Foetor feeding trough for the dining-table identity

The complete native `meat_trough` provides a more plausible Foetor food curio
than an indoor dining setup. Both native meshes, their UVs, transforms and
1024 paint match the installed source. The 689-vertex/466-face timber body
stays fixed. Its separate1285-vertex/1473-face meat assembly is hidden only
when searched. Small draped remains belong to the original timber assembly
and remain deliberately. No generated art or repaint.

The opposite-facing trial projected hanging food below the fitting allowance
and was rejected. Final source-facing native-trough-pair-v02 grounds both
states without altering geometry or weakening the fitting guard. Closed pixels
match the untouched native source study exactly; pixels below source row810
match between states. Front timber is naturally occluded while food is present.
Do not describe all body image pixels as identical.

The360px fit was preferred over300px in installed farm corridor and quiet-room
background composites. Only `makeshift_dining_table/dd2_farm` gains this art
and the name Fouled feeding trough. One exact result changes dinner-table
wording to feeding scraps; other messages retain. The canonical identity,
outcomes, medicinal-herb supply handling, diseases and spawn rules retain.
Classic and other DD2 regions use the original presentation through the355
regional-only fallback. Frozen Core355 parses the full35-ID/27-painting pack;
12 artifact tests pass. No C# build or installation359. Current354 cannot parse
the omitted-default entry. Accepted installed coverage remains34IDs/26pairs.

The separate pack-trough-CANDIDATE-v01 is UNINSTALLED/UNACCEPTED. Source hashes,
pixel checks, regional/classic/FadedMemory checks and offline evidence are in
local-art/ui-overhaul/curio-regional-coherence-v09/CONTINUE.md, with heavy art D:.
Native actors, shader contrast, hover, herb input and return travel await
explicit newer launch permission. Keep all prior candidates separate.

The academic-cache source was inspected in four facings. It is a complete
multi-container depot with seals, weapons and loose supplies, not a selected
single street coffer. No chest was isolated or state pair produced. The farm
barn_box_03 is a building; road_meat_boxes is a mixed produce/meat source study.
Neither is a locked-cabinet candidate. Continue other regional curios from
actual provenance rather than forcing unsuitable source names into cabinets.
