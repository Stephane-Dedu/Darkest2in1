# Curio rework: design catalog v01

## Regional presentation, round348

The owner now prioritises plausible regional surroundings over literal DD1
furniture. Read [regional_context.md](regional_context.md). The cabinet uses
an existing locked coffer in the four DD2 regions, with matching street-cache,
field-store, farm-coffer and dock-cargo names. This is one shared interim body.
Its canonical ID, outcomes, supplies, RNG and used state remain unchanged.
Classic destinations and Faded Memory keep their existing defaults.

Schema 3 adds explicit per-ID `Regions` bindings with `ArtId` and `DisplayName`.
Schema 2 remains supported. The loader preloads and caches the selected art;
prompt and result titles use the same regional name. Bindings add neither
encounters nor coverage identities. `region_art_overrides` in the private
selection file builds these bindings from reviewed art pairs.

Runtime348 and its private pack are installed while DD2 remains closed. The
isolated Release build, 1,316 Core tests, 290 UI tests and seven artifact tests
pass. Four-region closed/used composites were reviewed offline. Native QA is
pending because the owner explicitly requires the game remain closed until
new permission. Do not run the new optional `-dd3-curio-regions` preview yet.
Accepted painting coverage stays 34 IDs / 26 pairs. The four round347 drafts
remain unaccepted and uninstalled. Latest checkpoint:
`local-art/ui-overhaul/curio-regional-coherence-v01/CONTINUE.md`.

## Current coverage, round346

34 exact identities use26 installed pairs. Eldritch Altar retains DD1's narrow
spiral sculpture, fluted column and blood-stained used state with DD2 stone
paint. All68 unsaved native state/input checks pass. Runtime343 is unchanged;
69 designs plus the retained Faded Memory exception remain. Normal saved
campaign/actor/controller/performance QA is pending. Latest checkpoint:
`local-art/ui-overhaul/curio-native-quality-v06/CONTINUE.md`.

## Current coverage, round345

33 exact identities now use 25 installed pairs. The bas-relief keeps its round
octopus carving and split used state with DD2 Shroud stone paint. Fixed rubble
landmarks preserve scale and ground contact. All 66 native state/input checks
pass; runtime343 is unchanged. There are70 pending designs and the retained
Faded Memory exception. Normal saved campaign/actor/controller/performance QA
remains separate. Latest checkpoint:
`local-art/ui-overhaul/curio-native-quality-v05/CONTINUE.md`.

## Current coverage, round344

32 exact identities now use 24 installed pairs. The Altar of Light, Fish Idol
and Ship's Figurehead keep their DD1 silhouettes with the existing DD2 stone
and timber palette. Fallen sword/head states stay distinct. All 64 native
state/input checks pass; runtime 343 is unchanged. There are 71 pending designs
and the retained Faded Memory exception. The bas-relief has references and a
prompt only. Latest checkpoint:
`local-art/ui-overhaul/curio-native-quality-v04/CONTINUE.md`.

## Native art and cache follow-up, round343

The installed pack now covers 29 identities with 21 pairs: 28 base-runtime
identities and one Court egg design. The egg artwork does not add DLC encounters.
Holy Fountain keeps its original identical body states; Pristine Fountain loses
its growth after use. Court eggs retain native shape/paint references with a
repaired root cutout and opened shells. All 58 native state/input checks pass.
Nearby paintings now reserve memory in distance order, so large old neighbours
cannot prevent the current tile from loading. The regression fails on the old
loader and passes with the fix; all 289 UI and five artifact tests pass. Source
Core logic is unchanged from round 342's 1,307 passing tests. Fitting/catalog
guards now enforce the same four-million-pixel decoder bound as the runtime.
Latest checkpoint: `local-art/ui-overhaul/curio-native-quality-v03/CONTINUE.md`.
There are 74 pending designs and the retained Faded Memory exception.

## Runtime integration, round342

The exact-ID renderer accepts an optional private `curios/manifest.json` under
`NativeRoomSceneryFolder`. `build_pack.py` produces schema 2 packs from reviewed
state pairs and explicitly audited original-sprite aliases. Display alpha stays
unchanged; only picking uses a conservatively pooled closed-state alpha mask.
Both states share dimensions, scale and a 16px ground anchor. Missing, damaged
or pending pairs retain the correct original curio art. No generic family or
region-wide substitution is used. Pending curios never display blue map icons.
Faded Memory retains its owner-requested DD1 spectral presentation.

Workers read/hash the private PNGs; Unity uploads at most one image per frame.
Nearby tiles and adjoining rooms are preloaded, with an eight-pair/24-million
pixel cache and explicit GPU cleanup. Initial warmup is bounded to two seconds
after the native host is ready, and begins alongside departure. Curio IDs,
outcomes, RNG, supplies, used state and save format remain authoritative.

Round 342 has 26 exact base-runtime IDs represented by 18 fitted pairs,
including eight explicit aliases. Five additional native-body used states and
two DD1-identity/DD2-material remakes complete this batch. The remaining 77
designs and the retained Faded Memory exception are tracked separately. DLC and
legacy designs do not create encounters. The old 70-entry prototype registry
and its primitive renderer are unfinished foreign work and are not this schema.

The opt-in `-dd3-curio-check` fixture exercises the real draw, alpha click and
supply-drop paths in DD2's main menu using an unsaved Core stand-in party. It
requires DebugKeys, refuses an already selected estate and suppresses persistence
only for its own transient session. It does not start the native expedition
host or native actors. `-dd3-curio-shots=ABSOLUTE_FOLDER` captures both states
at 1920x1080; `review_native_captures.py` indexes and crops these real captures.
All 26 IDs passed; 52 captures and all 18 canonical pairs were inspected. Saved
reload, ordinary controller input, actor overlap, a normal departure and long
session frame time still require campaign QA.

Latest checkpoint: `local-art/ui-overhaul/curio-native-quality-v02/CONTINUE.md`.
Heavy sources, exact built-in ImageGen prompts, originals and evidence stay on
D:. No extracted/generated game art or per-pixel masks belong in Git.

## Production art, round341

The owner moved the active art task to all curios on2026-10-24. Hamlet333 and
expedition339/dossier340 are parked. The private checkpoint is
`local-art/ui-overhaul/curio-native-quality-v01/CONTINUE.md`; heavy art is on D:.
This remains an offline production pass. No curio pack or runtime was installed.

The104-identity inventory currently has11 fitted closed/used candidates, four
more complete native bodies awaiting used states,88 pending identities and the
retained Faded Memory exception. A registry entry or source assembly is not
finished art. The existing70-entry native registry/renderer/runtime files are
separate unfinished work and must not be deployed as a complete pack.

Eight distinctive objects use DD1 identity/state references repainted against
actual DD2 props: iron maiden, armor, urn, oyster, coral, sarcophagus,
confessional and display cabinet. Sack, crate and books retain native DD2
geometry/UV/paint for closed states and use generated edits for used states.
Urn and coral deliberately retain one static body painting across both states;
the coral's large halo is absent. Other pairs preserve opening/collapse cues.
These are recognizable adaptations, not pixel-identical copies of DD1.

New tools read private sources and write private outputs outside Git:

- `extract_reference_states.py` exports196 direct atlas state references,
  restoring clockwise-packed rotation and trim offsets. Complex NPC/partial
  states remain reported gaps. They are not full Spine animation renders.
- `render_native_sources.py` renders seven complete native bodies with original
  mesh/UV/material transforms, tint and per-material alpha cutoff. It records
  every texture dependency. Lighting is an unlit preview approximation.
- `fit_state_pair.py` registers shared RGBA canvases with a16px foot padding.
  Use fixed body landmarks or horizontal spans, never a moving lid/top.
- `preview_state_pairs.py` applies the actual1920x720 crawl width/height/padding
  formula to offline sheets. Those sheets are not native game captures.
- `catalog_quality.py` verifies selected PNG hashes, alpha, anchors, dimensions
  and native dependencies against the full inventory, then makes a private
  coverage report. It never upgrades offline evidence to native acceptance.

Four artifact tests cover rotated packing, transparent state registration,
invalid scale spans and overwrite protection. No shared dirty C# build was run.
Before integration, review alpha-aware closed-state picking, bounds of open
doors, exact identity coverage beyond four regions and bounded loading. Isolate
the curio seam from concurrent combat/hero changes. An incomplete full native
switch would hide the remaining objects; do not install one.

This is an art proposal for every curio identity found in the installed DD1 data, plus the mod's Faded Memory.
It does not install new art or change curio behavior. The private visual catalog is
`local-art/ui-overhaul/curio-design-v01/index.html`, beside this repository's parent directory.

## Coverage

| Scope | Entries | Meaning |
|---|---:|---|
| Base campaign | 60 | Ordinary, tutorial, secret-room and event curios in the base library |
| Quest objects | 10 | Additional gather/activation identities in the base quest definitions |
| Installed DLC definitions | 27 | Crimson Court and Color of Madness design coverage; runtime support is separate |
| Legacy art | 6 | Existing art folders without a matching live library/quest definition; no encounter invented |
| Owner exception | 1 | Faded Memory keeps its explicitly requested DD1 appearance |
| **Total** | **104** | Each identity has an original proposal and used-state description |

`author_designs.py` is the editable source; `designs.json` is its generated, asset-free manifest.
`catalog.py` independently inventories the installed data and rejects missing, extra or duplicate design IDs.
The private inventory records original sprite aliases, supply IDs and quest goals. Shared sprite aliases do
not merge behavior: Thanks Chest, tutorials and quest objects retain their separate identities.
Quest regions come from the actual goal lists; Animalistic Shrine belongs to Warrens/Foetor.

## Shared visual direction

Keep DD1's immediate object recognition and DD2's painted surfaces. At corridor scale, a chest must first read
as a chest, a tomb as a tomb, and an oyster as a shell. Use one dominant silhouette, one secondary detail and
small ground contact. Avoid surrounding every prop with crates, rocks and candles. Preserve a dark cavity,
a broad mid-value body and a few pale edges so the prop reads against both bright and dark scenery.

| Region family | Materials and shapes | Avoid |
|---|---|---|
| Sprawl / Ruins | Chipped devotional stone, scorched books, crooked timber, iron locks, dull brass | Full buildings resized into desktop props |
| Foetor / Warrens | Swollen casks, rough crate boards, butcher frames, stained cloth, dark meat | Replacing human remains with food props |
| Tangle / Weald | Bent roots, split bark, worn canvas, moss in joints, bound branch frames | Growth hiding the searchable opening |
| Shroud / Cove | Wet dark timber, salt-white crust, fish forms, carved marine stone, cool worn edges | The same circular fish shrine for every marine curio |
| Crimson Court | Courtier eggs, thin gilded trim, wine cloth, insect invasion of court furniture | Reusing the nurse as a finished Court NPC |
| Farmstead | Crooked farm furniture, restrained cyan/violet crystal fractures | Permanent bloom washing out native painted detail |
| Mountain / occult | Broken black stone, pale engraved edges, controlled red tendrils | Roadside Study buildings standing in for small altars |

Use regional material variations on the same ordinary chest/pack/crate families. Keep substantial identity
differences for coffins, oysters, statues, torture devices and people; those need new geometry. Fit props to the
current DD1 placement and party footline before adding depth. A coherent corridor matters more than the number
of decorative pieces.

## Interaction contract

Build separate `closed`, `active` and `open` presentations to match the current `CrawlUi.DrawProp` contract.
Lids, doors, flaps, shells and objective seals must separate cleanly for state changes. Idle motion belongs to
cloth, water or flame; solid masonry stays still. Hover can use a narrow warm edge and the native action glyph,
without a large glow or revealing a random outcome. Use a generous interaction mask around the principal body,
not around every dangling rope or transparent web thread.

The catalog proposes visual resolutions, not new outcome rules. Existing curio effects, supply consumption,
loot, journal entries, RNG, actor selection, quest progress and saved location state remain authoritative.
Different effects may need different art variants; never infer the effect from an animation or roll again for art.
Used props keep their anchor and footprint, making return travel readable. Keyed Court chests have distinct
plate shapes as well as ribbon colors. Faded Memory's spectral DD1 appearance and encounter rules are preserved;
its catalog sources describe only an optional secondary frame.

## Source audit and current limits

There are 39 source-reference groups with rendered previews and exact object/material provenance in the private
`audit.json`. Most textures are 2048 pixels; a few small props use 1024. Resolution alone does not prove a useful
model. The source audit checks geometry, static-batch face ranges, texture existence and dimensions, then renders
the chosen objects for visual inspection. It rejected or replaced these misleading candidates:

- `pickup_chest` is a two-triangle road icon. The design uses the actual 3D camp coffer.
- The old inn-scene table/knife references expose combined furniture geometry. Direct `inn_table.fbx` export
  provides separate table and dagger meshes.
- Guardian's object named `altar` is large shrine architecture. A compact Chirurgeon slab supplies altar-base
  proportions; the cultist/Shambler body, sigil and torch socket remain bespoke work.
- Hero Shrine is a chapel, not a statue. Statues use stone-material references and need sculpted bodies.
- A hanging-cage export used only black backing. A textured chain supplies metal reference; the iron maiden
  still needs a human-shaped shell, doors, hinges and spikes.
- Moss, banners, tendrils and the nurse are explicitly material/shape/pose references. They require new meshes
  or adapted sections. Their existing node placement and identity are not a finished curio.

Twelve native prop assemblies demonstrate scale and grouping: chest, bones, books, food cart, fountain, tent,
fish carcass, bookshelf, sacrificial slab, egg cluster, cargo crates and an occult altar base. They are source
studies, not completed sculptures. The catalog labels them accordingly. Offline renders use native base color
and tint through an emission preview shader; native lighting, cutout sorting, VFX, animation and picking remain
unverified. No game launch or save access is part of this design pass.

## Build order

1. **Ordinary searchable containers:** coffer, strongbox, chest, crate, sack and travel pack. Produce convincing
   interiors and closed/open variants once, then add tutorial, keyed, trapped, objective and regional fittings.
2. **Books, bones and abandoned camps:** native book piles/shelves, skeletons, tent and fire ring. These provide
   recognizable shapes with modest adaptation work.
3. **Altars and fountains:** a compact devotional base, sacrificial slab and basin family, with dedicated occult,
   cleansing and quest variants. Keep the Shambler socket bespoke.
4. **Distinct bodies:** tombs, urn, iron maiden, empty armor, effigy, fish idol, oyster, coral and figurehead. Sculpt
   each silhouette rather than stretching unrelated props.
5. **DLC sets and people:** Court furniture/eggs, Farmstead crystal props, Blood Shrews and Miller's Wife. New NPC
   bodies/rigs are a separate production task. Legacy-only designs wait for an actual encounter assignment.

Before production, review the complete catalog by region and the assembly sheet together. The first acceptance
batch should cover a locked chest, books, bones, fountain and tent in actual corridors at normal gameplay zoom,
with hover, supply drop, use, reload and return travel. Do not mark runtime acceptance from these offline images.

## Proposed implementation seam

Keep the existing curio rules and location IDs. Resolve presentation by exact curio ID, with an optional regional
variant and explicit shared-family references. Put new art behind one reversible feature flag with DD1 art as the
fallback for missing/unready assets. Do not infer identities from sprite aliases or introduce save migrations.
Keep loading/caching and disposal separate from gameplay resolution; a presentation lookup must not consume RNG.
Choose real 3D props or pre-rendered layered states after checking corridor perspective/occlusion. This catalog
does not make that runtime choice or add a feature flag yet.

## Reproduce locally

Use the local asset-pipeline Python (numpy, Pillow, UnityPy) and Blender. All exported models, textures, fonts,
images and generated catalogs stay outside Git. Only this authored manifest, tools and notes are versioned.

```powershell
$curioPython = '..\universal-modder\.venv\Scripts\python.exe'
$curioKit = '..\local-art\dd2-native-kit'
$curioOut = '..\local-art\ui-overhaul\curio-design-v01'
$curioGame = 'C:\Users\Piral\darkestwithdlc\game'
$curioBlender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'

# Extra direct models absent from the initial kit. This writes only to the private output.
& $curioPython "$curioKit/tools/export_prefab.py" --game $curioGame "$curioOut/extra" `
  biome_common_assets_all.bundle,biome_common_models_platform_high_assets_all.bundle `
  chirurgeon_node_altar.fbx guardian_node_altar.fbx oasis_node_02_no_moss.fbx `
  the_inn_banner_01.fbx courtier_node_moss.fbx
& $curioPython "$curioKit/tools/export_prefab.py" --game $curioGame "$curioOut/extra" `
  biome_inn_assets_all.bundle inn_table.fbx

& $curioPython tools/curios/author_designs.py
& $curioPython tools/curios/catalog.py --game $curioGame --kit $curioKit --out $curioOut
& $curioBlender -b --python tools/curios/render_parts.py -- --kit $curioKit --out $curioOut
& $curioBlender -b --python tools/curios/render_studies.py -- --kit $curioKit --out $curioOut
# Rebuild HTML so the newly rendered studies appear.
& $curioPython tools/curios/catalog.py --game $curioGame --kit $curioKit --out $curioOut
& $curioPython tools/curios/review.py --out $curioOut
```

`render_parts.py --refresh key1 key2` refreshes selected images after source changes. `review.py` checks all 104
cards, local image targets, native chest/table/knife geometry and nonempty transparent renders. It creates
`source-sheet.jpg`, `assembly-studies.jpg` and `artifact-check.json` for the private review.
